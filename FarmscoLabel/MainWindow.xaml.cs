using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using FarmscoLabel.Models;
using FarmscoLabel.Services;
using Microsoft.Win32; // OpenFileDialog

namespace FarmscoLabel
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;                       // 프로그램 설정
        private readonly ObservableCollection<DeliveryRow> _rows = new(); // 업로드된 원본 데이터
        private ICollectionView _view = null!;                         // 화면에 보이는(필터된) 목록
        private readonly ObservableCollection<LabelItem> _detailLabels = new(); // 상세: 박스별 라벨

        public MainWindow()
        {
            InitializeComponent();

            // 저장된 설정 불러오기 (없으면 기본값)
            _settings = AppSettings.Load();

            // 마스터 표: 원본 데이터를 '필터 가능한 뷰'로 감싸서 연결
            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = FilterPredicate; // 어떤 행을 보여줄지 결정하는 규칙
            GridMaster.ItemsSource = _view;

            // 상세 표 연결
            GridDetail.ItemsSource = _detailLabels;

            // 프린터 목록 채우기
            LoadPrinters();
        }

        // 설치된 프린터를 콤보박스에 채운다.
        private void LoadPrinters()
        {
            CmbPrinter.Items.Clear();
            foreach (var name in LabelPrinter.GetInstalledPrinters())
                CmbPrinter.Items.Add(name);

            // 설정에 저장된 프린터가 있으면 선택
            if (!string.IsNullOrWhiteSpace(_settings.PrinterName) &&
                CmbPrinter.Items.Contains(_settings.PrinterName))
                CmbPrinter.SelectedItem = _settings.PrinterName;
            else if (CmbPrinter.Items.Count > 0)
                CmbPrinter.SelectedIndex = 0;
        }

        // ── 엑셀 업로드 ──
        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "엑셀 파일 선택",
                Filter = "엑셀 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*"
            };

            // 최근에 불러온 폴더가 있으면 그 폴더를 열어준다 (폴더가 아직 존재할 때만)
            if (!string.IsNullOrWhiteSpace(_settings.LastImportFolder) &&
                Directory.Exists(_settings.LastImportFolder))
            {
                dlg.InitialDirectory = _settings.LastImportFolder;
            }

            if (dlg.ShowDialog() != true) return;

            string path = dlg.FileName;

            // 이번에 고른 파일의 폴더를 '최근 폴더'로 기억한다
            try
            {
                string? folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    _settings.LastImportFolder = folder;
                    _settings.Save();
                }
            }
            catch { /* 폴더 기억 실패는 무시 */ }

            // 로딩 오버레이 표시 + 진행바 초기화
            LoadingBar.Value = 0;
            TxtLoading.Text = "엑셀을 여는 중입니다...";
            LoadingOverlay.Visibility = Visibility.Visible;
            BtnUpload.IsEnabled = false;

            // 백그라운드에서 읽은 진행률을 UI로 전달하는 통로
            var progress = new Progress<int>(percent =>
            {
                LoadingBar.Value = percent;
                TxtLoading.Text = $"엑셀을 불러오는 중입니다... {percent}%";
            });

            try
            {
                // 무거운 읽기 작업은 백그라운드 스레드에서 실행 (화면 멈춤 방지)
                var imported = await Task.Run(() => ExcelImporter.Import(path, progress));

                _rows.Clear();
                foreach (var r in imported)
                    _rows.Add(r);

                _view.Refresh();
                _detailLabels.Clear();
                TxtDetailInfo.Text = "행을 선택하면 박스별 라벨 목록이 여기에 표시됩니다.";

                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("엑셀을 읽는 중 문제가 발생했어요.\n\n" + ex.Message,
                    "업로드 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnUpload.IsEnabled = true;
            }
        }

        // ── 필터: 체크된 보관유형만 보여주는 규칙 ──
        private bool FilterPredicate(object obj)
        {
            if (obj is not DeliveryRow row) return false;

            return row.Category switch
            {
                StorageCategory.상온 => ChkRoom.IsChecked == true,
                StorageCategory.냉장 => ChkCold.IsChecked == true,
                StorageCategory.냉동 => ChkFreeze.IsChecked == true,
                _ => true // '기타'는 항상 표시
            };
        }

        // 필터 체크박스가 바뀌면 목록 새로고침
        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            _view?.Refresh();
            UpdateStatus();
        }

        // ── 행 선택 시 상세(박스별 라벨) 표시 ──
        private void GridMaster_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _detailLabels.Clear();

            if (GridMaster.SelectedItem is not DeliveryRow row)
            {
                TxtDetailInfo.Text = "행을 선택하면 박스별 라벨 목록이 여기에 표시됩니다.";
                return;
            }

            // 선택한 행 1건을 박스별 라벨로 펼침
            var labels = NumberingEngine.Expand(row, _settings.ShippingSource);
            foreach (var l in labels)
                _detailLabels.Add(l);

            // 박스(꽉 찬) / 낱개(잔량) 장수 계산
            int boxCnt = labels.Count(l => l.IsFullBox);
            int looseCnt = labels.Count(l => !l.IsFullBox);

            TxtDetailInfo.Text =
                $"[{row.DeliveryPlace}] {row.ItemName}  ·  박스 {boxCnt}장 / 낱개 {looseCnt}장";
        }

        // ── 선택한 행들만 출력 ──
        private void BtnPrintSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = GridMaster.SelectedItems.Cast<DeliveryRow>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("먼저 표에서 출력할 행을 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PrintRows(selected);
        }

        // ── 현재 필터로 보이는 전체 출력 ──
        private void BtnPrintAll_Click(object sender, RoutedEventArgs e)
        {
            var all = _view.Cast<DeliveryRow>().ToList();
            if (all.Count == 0)
            {
                MessageBox.Show("출력할 데이터가 없습니다.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PrintRows(all);
        }

        // 실제 인쇄 처리 (공통)
        private async void PrintRows(List<DeliveryRow> rows)
        {
            // 출고 구분 필터 확인 (둘 다 꺼져 있으면 출력할 게 없음)
            bool includeBox = ChkBox.IsChecked == true;
            bool includeLoose = ChkLoose.IsChecked == true;
            if (!includeBox && !includeLoose)
            {
                MessageBox.Show("‘박스출고’ 또는 ‘낱개출고’ 중 하나 이상을 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 라벨로 펼친 뒤 출고 구분 필터 적용
            var labels = NumberingEngine.ExpandMany(rows, _settings.ShippingSource)
                .Where(l => l.IsFullBox ? includeBox : includeLoose)
                // 박스를 먼저, 낱개를 나중에 출력 (OrderBy는 안정 정렬 → 원래 순서 유지)
                .OrderByDescending(l => l.IsFullBox)
                .ToList();

            if (labels.Count == 0)
            {
                MessageBox.Show("인쇄할 라벨이 없습니다. (수량 또는 출고 구분 필터를 확인하세요)", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 프린터 선택 확인
            string printer = CmbPrinter.SelectedItem as string ?? "";
            if (string.IsNullOrWhiteSpace(printer))
            {
                MessageBox.Show("프린터를 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 프린터 선택 대화상자 표시 (프로그램에서 고른 프린터를 기본 선택으로 띄운다)
            using var dlg = new System.Windows.Forms.PrintDialog
            {
                AllowSomePages = false,
                AllowSelection = false,
                AllowPrintToFile = false,
                UseEXDialog = true,
                PrinterSettings = new System.Drawing.Printing.PrinterSettings
                {
                    PrinterName = printer
                }
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            // 대화상자에서 최종 선택된 프린터를 사용하고, 화면 목록에도 반영
            printer = dlg.PrinterSettings.PrinterName;
            if (CmbPrinter.Items.Contains(printer))
                CmbPrinter.SelectedItem = printer;

            // 출력 진행 오버레이 표시 + 진행바 초기화
            int total = labels.Count;
            LoadingBar.Value = 0;
            TxtLoading.Text = $"출력 준비 중입니다... (0 / {total}장)";
            LoadingOverlay.Visibility = Visibility.Visible;
            SetPrintButtonsEnabled(false);

            // 백그라운드에서 인쇄한 장수를 UI로 전달하는 통로
            var progress = new Progress<int>(done =>
            {
                LoadingBar.Value = total == 0 ? 0 : done * 100.0 / total;
                TxtLoading.Text = $"출력 중입니다... ({done} / {total}장)";
            });

            try
            {
                var printerService = new LabelPrinter(_settings);
                // 인쇄는 화면을 멈추지 않도록 백그라운드 스레드에서 실행
                await Task.Run(() => printerService.Print(labels, printer, progress));

                // 선택한 프린터를 설정에 저장
                _settings.PrinterName = printer;
                _settings.Save();

                TxtStatus.Text = $"출력 완료: 라벨 {total}장 ({printer})";
            }
            catch (Exception ex)
            {
                MessageBox.Show("인쇄 중 문제가 발생했어요.\n\n" + ex.Message,
                    "인쇄 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                SetPrintButtonsEnabled(true);
            }
        }

        // 인쇄 중 출력 버튼들을 잠갔다 풀었다 한다.
        private void SetPrintButtonsEnabled(bool enabled)
        {
            BtnPrintSelected.IsEnabled = enabled;
            BtnPrintAll.IsEnabled = enabled;
            BtnUpload.IsEnabled = enabled;
        }

        // ── 설정 창 열기 ──
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settings) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _settings.Save();
                // 출고지 등이 바뀌었을 수 있으니 상세를 다시 계산
                GridMaster_SelectionChanged(GridMaster, null!);
                TxtStatus.Text = "설정을 저장했습니다.";
            }
        }

        // 상태표시줄 갱신 (개수 안내)
        private void UpdateStatus()
        {
            int total = _rows.Count;
            var visible = _view.Cast<DeliveryRow>().ToList();
            int shown = visible.Count;

            // 보이는 행들을 라벨로 펼쳐 박스/낱개 개수를 센다
            var labels = NumberingEngine.ExpandMany(visible, _settings.ShippingSource);
            int boxCnt = labels.Count(l => l.IsFullBox);
            int looseCnt = labels.Count(l => !l.IsFullBox);

            // 현재 출고 구분 필터로 실제 출력될 장수 계산
            bool includeBox = ChkBox?.IsChecked == true;
            bool includeLoose = ChkLoose?.IsChecked == true;
            int printCnt = (includeBox ? boxCnt : 0) + (includeLoose ? looseCnt : 0);

            TxtStatus.Text =
                $"전체 {total}건 · 필터 표시 {shown}건 · 박스 {boxCnt}장 / 낱개 {looseCnt}장 · 출력 예정 {printCnt}장";
        }
    }
}

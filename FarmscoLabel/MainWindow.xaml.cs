using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "엑셀 파일 선택",
                Filter = "엑셀 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var imported = ExcelImporter.Import(dlg.FileName);

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

            TxtDetailInfo.Text =
                $"[{row.DeliveryPlace}] {row.ItemName}  ·  총수량 {row.Quantity} / 입수 {row.BoxUnitQty}  →  박스 {row.BoxCount}장";
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
        private void PrintRows(List<DeliveryRow> rows)
        {
            // 라벨로 펼치기
            var labels = NumberingEngine.ExpandMany(rows, _settings.ShippingSource);
            if (labels.Count == 0)
            {
                MessageBox.Show("인쇄할 라벨이 없습니다. (수량을 확인하세요)", "안내",
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

            // 인쇄 전 확인 (실수 방지)
            var confirm = MessageBox.Show(
                $"'{printer}' 프린터로 라벨 {labels.Count}장을 출력할까요?",
                "출력 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var printerService = new LabelPrinter(_settings);
                printerService.Print(labels, printer);

                // 선택한 프린터를 설정에 저장
                _settings.PrinterName = printer;
                _settings.Save();

                TxtStatus.Text = $"출력 완료: 라벨 {labels.Count}장 ({printer})";
            }
            catch (Exception ex)
            {
                MessageBox.Show("인쇄 중 문제가 발생했어요.\n\n" + ex.Message,
                    "인쇄 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            int shown = _view.Cast<DeliveryRow>().Count();
            int boxSum = _view.Cast<DeliveryRow>().Sum(r => r.BoxCount);
            TxtStatus.Text = $"전체 {total}건 · 필터 표시 {shown}건 · 출력 예정 라벨 {boxSum}장";
        }
    }
}

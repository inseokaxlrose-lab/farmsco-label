using System.Globalization;
using System.Windows;
using FarmscoLabel.Models;

namespace FarmscoLabel
{
    // 설정 값을 편집하는 작은 창.
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            // 현재 설정값을 화면에 채워 넣기
            TxtSource.Text = _settings.ShippingSource;
            TxtWidth.Text = _settings.LabelWidthMm.ToString(CultureInfo.InvariantCulture);
            TxtHeight.Text = _settings.LabelHeightMm.ToString(CultureInfo.InvariantCulture);
            TxtOffsetX.Text = _settings.OffsetXMm.ToString(CultureInfo.InvariantCulture);
            TxtOffsetY.Text = _settings.OffsetYMm.ToString(CultureInfo.InvariantCulture);
            TxtFont.Text = _settings.FontFamily;
            ChkRotate.IsChecked = _settings.Rotate90;
            ChkShowBoxUnitQty.IsChecked = _settings.ShowBoxUnitQty;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 입력값을 설정에 반영 (숫자는 안전하게 변환)
            _settings.ShippingSource = TxtSource.Text.Trim();
            _settings.LabelWidthMm = ParseDouble(TxtWidth.Text, _settings.LabelWidthMm);
            _settings.LabelHeightMm = ParseDouble(TxtHeight.Text, _settings.LabelHeightMm);
            _settings.OffsetXMm = ParseDouble(TxtOffsetX.Text, _settings.OffsetXMm);
            _settings.OffsetYMm = ParseDouble(TxtOffsetY.Text, _settings.OffsetYMm);
            _settings.FontFamily = string.IsNullOrWhiteSpace(TxtFont.Text) ? "맑은 고딕" : TxtFont.Text.Trim();
            _settings.Rotate90 = ChkRotate.IsChecked == true;
            _settings.ShowBoxUnitQty = ChkShowBoxUnitQty.IsChecked == true;

            DialogResult = true; // 창을 닫으면서 '저장됨'을 알림
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // 글자를 실수(double)로 바꾸되, 실패하면 기존값 유지
        private static double ParseDouble(string text, double fallback)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                return v;
            if (double.TryParse(text, out v)) // 현지 형식도 시도
                return v;
            return fallback;
        }
    }
}

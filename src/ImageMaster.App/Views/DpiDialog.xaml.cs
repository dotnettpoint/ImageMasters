using System.Windows;
using System.Windows.Controls;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>DPI change dialog with common presets plus a custom option.</summary>
public partial class DpiDialog : Window
{
    private static readonly double[] PresetDpis = { 72, 96, 150, 300 };
    private bool _isUpdatingFromCode;

    public DpiChangeRequest? Result { get; private set; }

    public DpiDialog(double currentDpiX, double currentDpiY)
    {
        InitializeComponent();

        _isUpdatingFromCode = true;
        DpiXBox.Text = currentDpiX.ToString("0.##");
        DpiYBox.Text = currentDpiY.ToString("0.##");
        var presetIndex = Array.IndexOf(PresetDpis, currentDpiX);
        PresetCombo.SelectedIndex = presetIndex >= 0 ? presetIndex : PresetDpis.Length; // "Custom"
        _isUpdatingFromCode = false;
    }

    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFromCode || PresetCombo.SelectedIndex < 0 || PresetCombo.SelectedIndex >= PresetDpis.Length)
            return;

        var dpi = PresetDpis[PresetCombo.SelectedIndex];
        _isUpdatingFromCode = true;
        DpiXBox.Text = dpi.ToString();
        DpiYBox.Text = dpi.ToString();
        _isUpdatingFromCode = false;
    }

    private void OnDpiTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFromCode) return;

        if (LinkXyCheckBox.IsChecked == true && sender == DpiXBox && double.TryParse(DpiXBox.Text, out var x))
        {
            _isUpdatingFromCode = true;
            DpiYBox.Text = x.ToString("0.##");
            _isUpdatingFromCode = false;
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(DpiXBox.Text, out var dpiX) || !double.TryParse(DpiYBox.Text, out var dpiY))
        {
            ValidationText.Text = "Enter valid numbers for DPI.";
            return;
        }

        var request = new DpiChangeRequest { DpiX = dpiX, DpiY = dpiY };
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            ValidationText.Text = string.Join(" ", errors);
            return;
        }

        Result = request;
        DialogResult = true;
    }
}

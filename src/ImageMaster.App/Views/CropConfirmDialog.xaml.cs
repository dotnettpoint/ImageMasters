using System.Windows;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>Asks whether a confirmed crop selection should be extracted as a new file or replace the working image.</summary>
public partial class CropConfirmDialog : Window
{
    public CropMode? Result { get; private set; }

    public CropConfirmDialog()
    {
        InitializeComponent();
    }

    private void OnExtractAsNewClick(object sender, RoutedEventArgs e)
    {
        Result = CropMode.ExtractAsNew;
        DialogResult = true;
    }

    private void OnReplaceExistingClick(object sender, RoutedEventArgs e)
    {
        Result = CropMode.ReplaceExisting;
        DialogResult = true;
    }
}

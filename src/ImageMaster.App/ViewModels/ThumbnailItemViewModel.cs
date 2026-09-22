using System.Windows.Media.Imaging;

namespace ImageMaster.App.ViewModels;

/// <summary>A single entry in the folder thumbnail strip.</summary>
public sealed class ThumbnailItemViewModel : ViewModelBase
{
    public string FilePath { get; }
    public string FileName { get; }

    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ThumbnailItemViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileName(filePath);
    }
}

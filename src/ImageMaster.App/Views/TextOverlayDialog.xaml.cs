using System.Windows;
using System.Windows.Media;
using ImageMaster.Core.Models;

namespace ImageMaster.App.Views;

/// <summary>
/// Editor for the document's text overlay layers. Content/style fields are
/// edited here; position can also be set here and further fine-tuned by
/// dragging the layer directly on the main canvas.
/// </summary>
public partial class TextOverlayDialog : Window
{
    private readonly List<TextOverlayLayer> _layers;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private bool _isUpdatingFromCode;

    public IReadOnlyList<TextOverlayLayer>? Result { get; private set; }

    public TextOverlayDialog(IReadOnlyList<TextOverlayLayer> existingLayers, int imageWidth, int imageHeight)
    {
        InitializeComponent();
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;

        // Work on clones so cancelling the dialog leaves the document untouched.
        _layers = existingLayers.Select(CloneLayer).ToList();

        foreach (var family in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s))
            FontFamilyCombo.Items.Add(family);

        RefreshList();
    }

    private static TextOverlayLayer CloneLayer(TextOverlayLayer source) => new()
    {
        Id = source.Id,
        Text = source.Text,
        FontFamily = source.FontFamily,
        FontSizePoints = source.FontSizePoints,
        IsBold = source.IsBold,
        IsItalic = source.IsItalic,
        OpacityPercent = source.OpacityPercent,
        ArgbColor = source.ArgbColor,
        X = source.X,
        Y = source.Y
    };

    private void RefreshList()
    {
        _isUpdatingFromCode = true;
        var selectedId = (LayersListBox.SelectedItem as TextOverlayLayer)?.Id;
        LayersListBox.ItemsSource = null;
        LayersListBox.ItemsSource = _layers;
        LayersListBox.SelectedItem = _layers.FirstOrDefault(l => l.Id == selectedId);
        _isUpdatingFromCode = false;

        LoadSelectedIntoEditor();
    }

    private void OnAddLayerClick(object sender, RoutedEventArgs e)
    {
        var layer = new TextOverlayLayer
        {
            Text = "New Text",
            X = _imageWidth / 4.0,
            Y = _imageHeight / 4.0
        };
        _layers.Add(layer);
        RefreshList();
        LayersListBox.SelectedItem = layer;
    }

    private void OnRemoveLayerClick(object sender, RoutedEventArgs e)
    {
        if (LayersListBox.SelectedItem is TextOverlayLayer layer)
        {
            _layers.Remove(layer);
            RefreshList();
        }
    }

    private void OnLayerSelected(object sender, RoutedEventArgs e) => LoadSelectedIntoEditor();

    private void LoadSelectedIntoEditor()
    {
        var layer = LayersListBox.SelectedItem as TextOverlayLayer;
        EditorPanel.IsEnabled = layer is not null;
        if (layer is null) return;

        _isUpdatingFromCode = true;
        TextBox.Text = layer.Text;
        FontFamilyCombo.SelectedItem = layer.FontFamily;
        FontSizeBox.Text = layer.FontSizePoints.ToString("0.#");
        BoldCheckBox.IsChecked = layer.IsBold;
        ItalicCheckBox.IsChecked = layer.IsItalic;
        OpacitySlider.Value = layer.OpacityPercent;
        XBox.Text = layer.X.ToString("0.#");
        YBox.Text = layer.Y.ToString("0.#");
        ColorSwatch.Background = new SolidColorBrush(ArgbToColor(layer.ArgbColor));
        _isUpdatingFromCode = false;
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => OnFieldChanged(sender, e);

    private void OnFieldChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFromCode) return;
        if (LayersListBox.SelectedItem is not TextOverlayLayer layer) return;

        layer.Text = TextBox.Text;
        if (FontFamilyCombo.SelectedItem is string family) layer.FontFamily = family;
        if (double.TryParse(FontSizeBox.Text, out var size)) layer.FontSizePoints = size;
        layer.IsBold = BoldCheckBox.IsChecked == true;
        layer.IsItalic = ItalicCheckBox.IsChecked == true;
        layer.OpacityPercent = (int)OpacitySlider.Value;
        if (double.TryParse(XBox.Text, out var x)) layer.X = x;
        if (double.TryParse(YBox.Text, out var y)) layer.Y = y;

        // Keep the list display (bound to layer.Text) in sync without losing selection.
        var index = LayersListBox.Items.IndexOf(layer);
        if (index >= 0) LayersListBox.Items.Refresh();
    }

    private void OnChooseColorClick(object sender, RoutedEventArgs e)
    {
        if (LayersListBox.SelectedItem is not TextOverlayLayer layer) return;

        var dialog = new ColorPickerDialog(layer.ArgbColor) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            layer.ArgbColor = dialog.SelectedArgb;
            ColorSwatch.Background = new SolidColorBrush(ArgbToColor(layer.ArgbColor));
        }
    }

    private static Color ArgbToColor(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var allErrors = _layers.SelectMany(l => l.Validate()).ToList();
        if (allErrors.Count > 0)
        {
            ValidationText.Text = string.Join(" ", allErrors.Distinct());
            return;
        }

        Result = _layers;
        DialogResult = true;
    }
}

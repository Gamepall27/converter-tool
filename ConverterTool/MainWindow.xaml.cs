using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ConverterTool.Models;
using Microsoft.Win32;

namespace ConverterTool;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<ConversionResult> Conversions { get; } = new();
    public ObservableCollection<string> SupportedFormats { get; } = new(["png", "jpg", "bmp", "gif", "tiff"]);

    private string _selectedFormat = "png";
    private ConversionResult? _selectedConversion;
    private string _base64Output = string.Empty;
    private string _statusMessage = "Bereit";
    private double _busyIndicator;
    private bool _createBase64 = true;
    private bool _preserveOriginal = true;
    private string _base64SourceFile = "Keine Datei gewählt";
    private string _base64Input = string.Empty;
    private string _decodeStatus = string.Empty;
    private string _base64Format = "png";
    private string _decodeFormat = "png";
    private ImageSource? _previewSource;
    private string? _base64SourcePath;

    public string OutputFolder { get; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        OutputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Converted");
        Directory.CreateDirectory(OutputFolder);
    }

    public string SelectedFormat
    {
        get => _selectedFormat;
        set => SetField(ref _selectedFormat, value);
    }

    public bool CreateBase64
    {
        get => _createBase64;
        set => SetField(ref _createBase64, value);
    }

    public bool PreserveOriginal
    {
        get => _preserveOriginal;
        set => SetField(ref _preserveOriginal, value);
    }

    public ConversionResult? SelectedConversion
    {
        get => _selectedConversion;
        set
        {
            if (SetField(ref _selectedConversion, value) && value?.Preview is not null)
            {
                PreviewSource = value.Preview;
                Base64Output = value.Base64Data;
            }
        }
    }

    public string Base64Output
    {
        get => _base64Output;
        set => SetField(ref _base64Output, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public double BusyIndicator
    {
        get => _busyIndicator;
        set => SetField(ref _busyIndicator, value);
    }

    public string Base64SourceFile
    {
        get => _base64SourceFile;
        set => SetField(ref _base64SourceFile, value);
    }

    public string Base64Input
    {
        get => _base64Input;
        set => SetField(ref _base64Input, value);
    }

    public string DecodeStatus
    {
        get => _decodeStatus;
        set => SetField(ref _decodeStatus, value);
    }

    public string Base64Format
    {
        get => _base64Format;
        set => SetField(ref _base64Format, value);
    }

    public string DecodeFormat
    {
        get => _decodeFormat;
        set => SetField(ref _decodeFormat, value);
    }

    public ImageSource? PreviewSource
    {
        get => _previewSource;
        set => SetField(ref _previewSource, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        AnimateDropArea(DropZone, 1);
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        await ConvertFilesAsync(files);
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e) => AnimateDropArea(DropZone, 1.03);

    private void DropZone_DragLeave(object sender, DragEventArgs e) => AnimateDropArea(DropZone, 1);

    private void DropZone_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => AnimateDropArea(DropZone, 1.02);

    private void DropZone_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => AnimateDropArea(DropZone, 1);

    private async void ManualConvert_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            await ConvertFilesAsync(dialog.FileNames);
        }
    }

    private async Task ConvertFilesAsync(IEnumerable<string> files)
    {
        BusyIndicator = 1;
        StatusMessage = "Konvertiere...";

        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            await ConvertSingleAsync(file);
        }

        BusyIndicator = 0;
        StatusMessage = "Bereit";
    }

    private async Task ConvertSingleAsync(string file)
    {
        var item = new ConversionResult
        {
            FileName = Path.GetFileName(file),
            FromFormat = Path.GetExtension(file).Trim('.').ToUpperInvariant(),
            ToFormat = SelectedFormat.ToUpperInvariant(),
            OriginalPath = file,
            Status = "Konvertiere..."
        };

        Conversions.Insert(0, item);
        SelectedConversion = item;

        try
        {
            var frame = await LoadFrameAsync(file);
            var encoder = CreateEncoder(SelectedFormat);
            var targetPath = BuildTargetPath(file, SelectedFormat);

            await using (var targetStream = File.Create(targetPath))
            {
                encoder.Frames.Add(frame);
                encoder.Save(targetStream);
            }

            item.TargetPath = targetPath;
            item.Preview = await CreatePreviewAsync(frame);

            if (CreateBase64)
            {
                item.Base64Data = await EncodeBase64Async(frame, SelectedFormat);
                Base64Output = item.Base64Data;
                Base64Input = item.Base64Data;
            }

            if (!PreserveOriginal)
            {
                TryDeleteFile(file);
            }

            PreviewSource = item.Preview;
            item.Status = "Fertig";
        }
        catch (Exception ex)
        {
            item.Status = $"Fehler: {ex.Message}";
            StatusMessage = item.Status;
        }
    }

    private static async Task<BitmapFrame> LoadFrameAsync(string file)
    {
        await using var stream = File.OpenRead(file);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapEncoder CreateEncoder(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "png" => new PngBitmapEncoder(),
            "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
            "bmp" => new BmpBitmapEncoder(),
            "gif" => new GifBitmapEncoder(),
            "tif" or "tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }

    private string BuildTargetPath(string file, string format)
    {
        var safeName = Path.GetFileNameWithoutExtension(file);
        var fileName = $"{safeName}_{format}.{format}";
        return Path.Combine(OutputFolder, fileName);
    }

    private static async Task<BitmapImage> CreatePreviewAsync(BitmapSource source)
    {
        return await Task.Run(() =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(ms.ToArray());
            image.EndInit();
            image.Freeze();
            return image;
        });
    }

    private static async Task<string> EncodeBase64Async(BitmapSource source, string format)
    {
        return await Task.Run(() =>
        {
            var encoder = CreateEncoder(format);
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        });
    }

    private void AnimateDropArea(FrameworkElement element, double scale)
    {
        if (element.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1, 1);
            element.RenderTransform = transform;
        }

        var animation = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static void TryDeleteFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private async void CopyBase64_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Base64Output))
        {
            StatusMessage = "Kein Base64 Inhalt";
            return;
        }

        Clipboard.SetText(Base64Output);
        StatusMessage = "Base64 kopiert";
        await Task.Delay(800);
        StatusMessage = "Bereit";
    }

    private void SaveBase64_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Base64Output))
        {
            StatusMessage = "Kein Base64 Inhalt";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Textdatei|*.txt|Alle Dateien|*.*",
            FileName = "image_base64.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, Base64Output);
            StatusMessage = $"Gespeichert: {dialog.FileName}";
        }
    }

    private void Base64DropZone_DragEnter(object sender, DragEventArgs e) => AnimateDropArea(Base64DropZone, 1.03);

    private void Base64DropZone_DragLeave(object sender, DragEventArgs e) => AnimateDropArea(Base64DropZone, 1);

    private async void Base64DropZone_Drop(object sender, DragEventArgs e)
    {
        AnimateDropArea(Base64DropZone, 1);
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var file = files[0];
        await PrepareBase64Async(file);
    }

    private async void PickBase64File_Click(object sender, RoutedEventArgs e)
    {
        await PickAndPrepareBase64Async();
    }

    private async Task PickAndPrepareBase64Async()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            await PrepareBase64Async(dialog.FileName);
        }
        else
        {
            StatusMessage = "Keine Datei gewählt";
        }
    }

    private async Task PrepareBase64Async(string file)
    {
        if (!File.Exists(file))
        {
            return;
        }

        _base64SourcePath = file;
        Base64SourceFile = Path.GetFileName(file);
        var frame = await LoadFrameAsync(file);
        PreviewSource = await CreatePreviewAsync(frame);
        Base64Output = await EncodeBase64Async(frame, Base64Format);
        Base64Input = Base64Output;
        StatusMessage = "Base64 erzeugt";
    }

    private async void GenerateBase64_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_base64SourcePath) && File.Exists(_base64SourcePath))
        {
            await PrepareBase64Async(_base64SourcePath);
            return;
        }

        await PickAndPrepareBase64Async();
    }

    private async void DecodeBase64_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Base64Input))
        {
            DecodeStatus = "Keine Base64 Daten";
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(Base64Input.Trim());
            using var ms = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            var encoder = CreateEncoder(DecodeFormat);
            encoder.Frames.Add(frame);

            var target = Path.Combine(OutputFolder, $"base64_{DateTime.Now:yyyyMMdd_HHmmss}.{DecodeFormat}");
            await using (var fs = File.Create(target))
            {
                encoder.Save(fs);
            }

            PreviewSource = await CreatePreviewAsync(frame);
            DecodeStatus = $"Gespeichert: {target}";
        }
        catch (Exception ex)
        {
            DecodeStatus = $"Fehler: {ex.Message}";
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

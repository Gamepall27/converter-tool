using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ConverterTool.Models;
using ConverterTool.Services;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace ConverterTool;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ImageConversionService _conversionService = new();
    private string _outputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "DIALConversions");
    private string _selectedFormat = "PNG";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ConvertedItem> ConversionHistory { get; } = new();

    public List<string> TargetFormats { get; }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (_outputDirectory != value)
            {
                _outputDirectory = value;
                OnPropertyChanged(nameof(OutputDirectory));
            }
        }
    }

    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (_selectedFormat != value)
            {
                _selectedFormat = value;
                OnPropertyChanged(nameof(SelectedFormat));
            }
        }
    }

    public MainWindow()
    {
        TargetFormats = _conversionService.SupportedFormats
            .OrderBy(f => f)
            .Append("Base64")
            .ToList();

        InitializeComponent();
        DataContext = this;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        await ProcessFilesAsync(files);
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        Mouse.SetCursor(Cursors.Arrow);
    }

    private async void OpenFilePicker_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Bilddateien|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|Alle Dateien|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await ProcessFilesAsync(dialog.FileNames);
        }
    }

    private async Task ProcessFilesAsync(IEnumerable<string> files)
    {
        foreach (var file in files.Where(File.Exists))
        {
            try
            {
                await ProcessSingleFileAsync(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Fehler bei {Path.GetFileName(file)}: {ex.Message}",
                    "Konvertierung fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private async Task ProcessSingleFileAsync(string file)
    {
        var target = string.IsNullOrWhiteSpace(SelectedFormat) ? "PNG" : SelectedFormat;
        string outputPath;
        string targetLabel;

        if (string.Equals(target, "Base64", StringComparison.OrdinalIgnoreCase))
        {
            (outputPath, _) = await _conversionService.ToBase64Async(file, OutputDirectory);
            targetLabel = "Base64";
        }
        else
        {
            outputPath = await _conversionService.ConvertAsync(file, target, OutputDirectory);
            targetLabel = target.ToUpperInvariant();
        }

        var item = new ConvertedItem
        {
            OriginalName = Path.GetFileName(file),
            OriginalFormat = Path.GetExtension(file).Trim('.').ToUpperInvariant(),
            TargetFormat = targetLabel,
            OutputPath = outputPath,
            ConvertedAt = DateTime.Now,
            Preview = LoadPreview(targetLabel == "Base64" ? file : outputPath)
        };

        Application.Current.Dispatcher.Invoke(() => ConversionHistory.Insert(0, item));
    }

    private static BitmapImage? LoadPreview(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 120;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Ausgabeordner wählen",
            SelectedPath = OutputDirectory
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            OutputDirectory = dialog.SelectedPath;
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

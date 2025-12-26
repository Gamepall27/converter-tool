using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ConverterTool.Models;

public class ConversionResult : INotifyPropertyChanged
{
    private string _status = string.Empty;
    private ImageSource? _preview;
    private string _targetPath = string.Empty;
    private string _base64Data = string.Empty;

    public string FileName { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string FromFormat { get; init; } = string.Empty;
    public string ToFormat { get; init; } = string.Empty;

    public string TargetPath
    {
        get => _targetPath;
        set => SetField(ref _targetPath, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public ImageSource? Preview
    {
        get => _preview;
        set => SetField(ref _preview, value);
    }

    public string Base64Data
    {
        get => _base64Data;
        set => SetField(ref _base64Data, value);
    }

    public string ConversionLabel => string.IsNullOrWhiteSpace(ToFormat)
        ? FromFormat
        : $"{FromFormat} → {ToFormat}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}

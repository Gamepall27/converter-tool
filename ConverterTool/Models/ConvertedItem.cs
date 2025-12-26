using System;
using System.Windows.Media.Imaging;

namespace ConverterTool.Models;

public class ConvertedItem
{
    public string OriginalName { get; init; } = string.Empty;
    public string OriginalFormat { get; init; } = string.Empty;
    public string TargetFormat { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public DateTime ConvertedAt { get; init; }
    public BitmapImage? Preview { get; init; }
    public bool IsBase64 => string.Equals(TargetFormat, "Base64", StringComparison.OrdinalIgnoreCase);
}

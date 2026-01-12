using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ConverterTool.Services;

public class ImageConversionService
{
    private readonly Dictionary<string, ImageFormat> _formatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PNG"] = ImageFormat.Png,
        ["JPG"] = ImageFormat.Jpeg,
        ["JPEG"] = ImageFormat.Jpeg,
        ["BMP"] = ImageFormat.Bmp,
        ["GIF"] = ImageFormat.Gif,
        ["TIFF"] = ImageFormat.Tiff,
        ["ICO"] = ImageFormat.Icon
    };

    public IReadOnlyCollection<string> SupportedFormats => _formatMap.Keys.ToList();

    public async Task<string> ConvertAsync(string inputPath, string targetFormat, string outputDirectory)
    {
        if (!_formatMap.TryGetValue(targetFormat, out var format))
        {
            throw new NotSupportedException($"Unsupported format: {targetFormat}");
        }

        return await Task.Run(() =>
        {
            Directory.CreateDirectory(outputDirectory);
            using var image = Image.FromFile(inputPath);

            var outputPath = Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(inputPath)}.{targetFormat.ToLowerInvariant()}");

            image.Save(outputPath, format);
            return outputPath;
        });
    }

    public async Task<(string outputPath, string base64)> ToBase64Async(string inputPath, string outputDirectory)
    {
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(outputDirectory);

            using var image = Image.FromFile(inputPath);
            using var memoryStream = new MemoryStream();
            image.Save(memoryStream, ImageFormat.Png);
            var base64 = Convert.ToBase64String(memoryStream.ToArray());

            var outputPath = Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(inputPath)}.b64.txt");

            File.WriteAllText(outputPath, base64);
            return (outputPath, base64);
        });
    }
}

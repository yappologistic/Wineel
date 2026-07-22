namespace Wineel;

public readonly record struct PixelSample(byte R, byte G, byte B, byte A = 255);

public static class DominantColorExtractor
{
    public static RgbColor Extract(IEnumerable<PixelSample> pixels, RgbColor fallback)
    {
        double totalWeight = 0;
        double red = 0;
        double green = 0;
        double blue = 0;

        foreach (var pixel in pixels)
        {
            if (pixel.A < 48) continue;
            var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) / 255d;
            var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) / 255d;
            var luminance = (0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B) / 255d;
            var saturation = max <= 0 ? 0 : (max - min) / max;
            if (luminance < 0.08 || luminance > 0.94 || saturation < 0.12) continue;
            var midWeight = 1 - Math.Abs(luminance - 0.55);
            var weight = pixel.A / 255d * (0.25 + saturation * 1.75) * midWeight;
            red += pixel.R * weight;
            green += pixel.G * weight;
            blue += pixel.B * weight;
            totalWeight += weight;
        }

        return totalWeight <= 0.001
            ? fallback
            : new RgbColor((byte)(red / totalWeight), (byte)(green / totalWeight), (byte)(blue / totalWeight));
    }
}

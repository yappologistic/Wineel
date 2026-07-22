using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wineel;

public sealed record ResolvedIcon(ImageSource Image, RgbColor Accent);

public sealed class IconResolver
{
    private readonly Dictionary<string, ResolvedIcon> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly ImageSource _fallback;

    public IconResolver(ImageSource fallback) => _fallback = fallback;

    public ResolvedIcon Resolve(SwitcherItem item, string? executablePath)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(item.IconCacheKey, out var existing)) return existing;
        }

        var packaged = item.Identity.StartsWith("package:", StringComparison.OrdinalIgnoreCase);
        var image = packaged
            ? TryWindowIcon(item.TargetWindow) ?? TryHighResolutionExecutableIcon(executablePath) ?? TryShellExecutableIcon(executablePath)
            : TryHighResolutionExecutableIcon(executablePath) ?? TryWindowIcon(item.TargetWindow) ?? TryShellExecutableIcon(executablePath);
        image ??= _fallback;
        if (image.CanFreeze) image.Freeze();
        var resolved = new ResolvedIcon(image, ExtractAccent(image));
        lock (_gate) _cache[item.IconCacheKey] = resolved;
        return resolved;
    }

    private static ImageSource? TryWindowIcon(nint hwnd)
    {
        nint source = 0;
        if (Native.SendMessageTimeout(hwnd, Native.WmGetIcon, Native.IconBig, 0, Native.SmtoAbortifhung, 75, out var iconResult) != 0)
            source = (nint)iconResult;
        if (source == 0) source = Native.GetClassLongPtr(hwnd, Native.GclpHicon);
        if (source == 0 && Native.SendMessageTimeout(hwnd, Native.WmGetIcon, Native.IconSmall2, 0, Native.SmtoAbortifhung, 75, out iconResult) != 0)
            source = (nint)iconResult;
        if (source == 0) source = Native.GetClassLongPtr(hwnd, Native.GclpHiconsm);
        if (source == 0) return null;
        var copy = Native.CopyIcon(source);
        return copy == 0 ? null : FromOwnedIcon(copy);
    }

    private static ImageSource? TryHighResolutionExecutableIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var icons = new nint[1];
        var iconIds = new uint[1];
        var extracted = Native.PrivateExtractIcons(path, 0, 256, 256, icons, iconIds, 1, 0);
        return extracted > 0 && icons[0] != 0 ? FromOwnedIcon(icons[0], 256) : null;
    }

    private static ImageSource? TryShellExecutableIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var resolvedPath = path;
        var info = new Native.ShFileInfo();
        var result = Native.SHGetFileInfo(resolvedPath, 0, ref info, (uint)Marshal.SizeOf<Native.ShFileInfo>(), Native.ShgfiIcon | Native.ShgfiLargeicon);
        return result == 0 || info.Icon == 0 ? null : FromOwnedIcon(info.Icon);
    }

    private static ImageSource? FromOwnedIcon(nint icon, int requestedSize = 128)
    {
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(requestedSize, requestedSize));
            source.Freeze();
            return source;
        }
        finally { _ = Native.DestroyIcon(icon); }
    }

    private static RgbColor ExtractAccent(ImageSource image)
    {
        if (image is not BitmapSource source) return RgbColor.AccentFallback;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = Math.Min(32, converted.PixelWidth);
        var height = Math.Min(32, converted.PixelHeight);
        var scaled = new TransformedBitmap(converted, new ScaleTransform(width / (double)converted.PixelWidth, height / (double)converted.PixelHeight));
        var stride = scaled.PixelWidth * 4;
        var bytes = new byte[stride * scaled.PixelHeight];
        scaled.CopyPixels(bytes, stride, 0);
        var pixels = new List<PixelSample>(scaled.PixelWidth * scaled.PixelHeight);
        for (var i = 0; i + 3 < bytes.Length; i += 4) pixels.Add(new PixelSample(bytes[i + 2], bytes[i + 1], bytes[i], bytes[i + 3]));
        return DominantColorExtractor.Extract(pixels, RgbColor.AccentFallback);
    }
}

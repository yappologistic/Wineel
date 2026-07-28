using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Wineel.Tests;

public sealed class RenderedUiTests
{
    [Fact]
    public void SettingsShellRendersAtStandardAndHighDpiWithAccessiblePrimaryControls()
    {
        RunSta(() =>
        {
            EnsureApplicationResources();
            var window = new SettingsWindow();
            window.Left = -20000;
            window.Top = -20000;
            window.LoadSettings(new AppSettings { OnboardingCompleted = true });
            window.Show();
            var standard = Render(window, 1120, 760, 96);
            var highDpi = Render(window, 1120, 760, 192);

            Assert.Equal(1120, standard.PixelWidth);
            Assert.Equal(760, standard.PixelHeight);
            Assert.Equal(2240, highDpi.PixelWidth);
            Assert.Equal(1520, highDpi.PixelHeight);
            Assert.True(ContainsVisiblePixels(standard));

            var replacement = Assert.IsType<CheckBox>(window.FindName("ReplaceAltTab"));
            var shortcut = Assert.IsType<TextBox>(window.FindName("FallbackShortcut"));
            Assert.Equal("Replace Windows Alt+Tab", AutomationProperties.GetName(replacement));
            Assert.Equal("Fallback shortcut", AutomationProperties.GetName(shortcut));

            var icon = Assert.IsAssignableFrom<BitmapSource>(window.Icon);
            Assert.True(icon.PixelWidth > 0);
            Assert.True(icon.PixelHeight > 0);
            var brandedImages = Descendants<Image>(window).ToArray();
            Assert.Equal(2, brandedImages.Length);
            Assert.All(brandedImages, image =>
            {
                var source = Assert.IsAssignableFrom<BitmapSource>(image.Source);
                Assert.True(source.PixelWidth > 0);
                Assert.True(source.PixelHeight > 0);
            });
            window.Hide();
        });
    }

    [Fact]
    public void SettingsNavigationAndSaveFeedbackCoverHappyAndUnhappyPaths()
    {
        RunSta(() =>
        {
            EnsureApplicationResources();
            var window = new SettingsWindow();
            window.Left = -20000;
            window.Top = -20000;
            window.LoadSettings(new AppSettings { OnboardingCompleted = true });
            window.Show();
            window.Measure(new Size(1120, 760));
            window.Arrange(new Rect(0, 0, 1120, 760));

            var appearanceNav = Assert.IsType<RadioButton>(window.FindName("AppearanceNav"));
            appearanceNav.IsChecked = true;
            Assert.Equal(Visibility.Visible, Assert.IsType<ScrollViewer>(window.FindName("AppearancePage")).Visibility);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<ScrollViewer>(window.FindName("GeneralPage")).Visibility);

            var shortcut = Assert.IsType<TextBox>(window.FindName("FallbackShortcut"));
            var validation = Assert.IsType<TextBlock>(window.FindName("ShortcutValidationText"));
            shortcut.Text = "Ctrl+F12";
            Assert.Contains("supported key", validation.Text, StringComparison.OrdinalIgnoreCase);

            shortcut.Text = "Ctrl+Alt+W";
            window.SettingsChanged += settings => SettingsApplyResult.Failure(settings with { FallbackShortcut = "Ctrl+Alt+Space" }, "Shortcut is already in use");
            shortcut.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            var saveStatus = Assert.IsType<TextBlock>(window.FindName("SaveStatusText"));
            Assert.Equal("Shortcut is already in use", saveStatus.Text);
            Assert.Equal("Ctrl+Alt+Space", shortcut.Text);
            window.Hide();
        });
    }

    private static RenderTargetBitmap Render(FrameworkElement element, int width, int height, double dpi)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            (int)Math.Round(width * dpi / 96),
            (int)Math.Round(height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    private static bool ContainsVisiblePixels(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha != 0);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void EnsureApplicationResources()
    {
        if (Application.Current is not null) return;
        var app = new App();
        app.InitializeComponent();
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

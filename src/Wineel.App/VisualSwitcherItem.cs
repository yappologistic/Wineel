using System.Windows.Media;

namespace Wineel;

public sealed record VisualSwitcherItem(SwitcherItem Item, ImageSource Icon, RgbColor Accent, bool IsPinned = false);

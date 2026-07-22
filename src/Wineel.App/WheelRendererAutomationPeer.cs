using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace Wineel;

internal sealed class WheelRendererAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
    private readonly WheelRenderer _owner;

    public WheelRendererAutomationPeer(WheelRenderer owner) : base(owner) => _owner = owner;

    protected override string GetClassNameCore() => "WineelWheel";
    protected override string GetNameCore() => string.IsNullOrWhiteSpace(_owner.AutomationStatus) ? "Wineel application switcher" : $"Wineel application switcher, {_owner.AutomationStatus}";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    protected override List<AutomationPeer>? GetChildrenCore() => _owner.AutomationItems
        .Select((item, index) => (AutomationPeer)new WheelItemAutomationPeer(_owner, item, index))
        .ToList();

    public IRawElementProviderSimple[] GetSelection()
    {
        if (_owner.AutomationSelectedIndex < 0 || _owner.AutomationSelectedIndex >= _owner.AutomationItems.Count) return Array.Empty<IRawElementProviderSimple>();
        return new[] { ProviderFromPeer(new WheelItemAutomationPeer(_owner, _owner.AutomationItems[_owner.AutomationSelectedIndex], _owner.AutomationSelectedIndex)) };
    }

    public bool CanSelectMultiple => false;
    public bool IsSelectionRequired => false;

    public void RaiseSelectionChanged()
    {
        RaiseAutomationEvent(AutomationEvents.SelectionPatternOnInvalidated);
        RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent,
            GetName(), "Wineel.Selection");
    }
}

internal sealed class WheelItemAutomationPeer : AutomationPeer, IInvokeProvider, ISelectionItemProvider
{
    private readonly WheelRenderer _owner;
    private readonly VisualSwitcherItem _item;
    private readonly int _index;

    public WheelItemAutomationPeer(WheelRenderer owner, VisualSwitcherItem item, int index)
    {
        _owner = owner;
        _item = item;
        _index = index;
    }

    protected override string GetClassNameCore() => "WineelWheelItem";
    protected override string GetNameCore() => $"{_item.Item.DisplayName}, item {_index + 1} of {_owner.AutomationItems.Count}{(_item.Item.WindowCount > 1 ? $", {_item.Item.WindowCount} windows" : string.Empty)}{(_item.IsPinned ? ", pinned" : string.Empty)}";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    protected override string GetAcceleratorKeyCore() => string.Empty;
    protected override string GetAccessKeyCore() => string.Empty;
    protected override string GetAutomationIdCore() => $"Wineel.Item.{_index}";
    protected override Rect GetBoundingRectangleCore() => _owner.AutomationItemBounds(_index);
    protected override Point GetClickablePointCore()
    {
        var bounds = GetBoundingRectangleCore();
        return bounds.IsEmpty ? new(double.NaN, double.NaN) : new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    protected override string GetHelpTextCore() => _item.Item.WindowCount > 1
        ? "Press Space to choose a window."
        : "Press Enter to activate.";
    protected override string GetItemStatusCore() => IsSelected ? "Selected" : string.Empty;
    protected override string GetItemTypeCore() => "application";
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
    protected override bool HasKeyboardFocusCore() => IsSelected;
    protected override bool IsEnabledCore() => true;
    protected override bool IsKeyboardFocusableCore() => true;
    protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;
    protected override bool IsPasswordCore() => false;
    protected override bool IsRequiredForFormCore() => false;
    protected override void SetFocusCore() => Select();
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem ? this : null;
    public void Invoke() => _owner.Dispatcher.BeginInvoke(() => _owner.AutomationInvoke(_index));
    public void Select() => _owner.Dispatcher.BeginInvoke(() => _owner.AutomationSelect(_index));
    public void AddToSelection() => Select();
    public void RemoveFromSelection() { }
    public bool IsSelected => _owner.AutomationSelectedIndex == _index;
    public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(new WheelRendererAutomationPeer(_owner));
}

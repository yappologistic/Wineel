# Compatibility matrix

Wineel targets x64 Windows 10 version 2004 (build 19041) and later. Automated coverage validates geometry and scale conversions; hardware rows remain release smoke tests because virtual CI does not reproduce real mixed-DPI topology changes.

## Automated matrix

| Area | Coverage |
|---|---|
| DPI scales | 100%, 125%, 150%, 175%, 200% |
| Coordinate space | Positive and negative virtual-screen origins |
| Work areas | Bottom, top, and side taskbar-style bounds |
| Wheel bounds | Normal and smaller-than-wheel work areas |
| Item counts | Empty, short, and more than 12 applications |

## Release smoke matrix

| Scenario | Expected result |
|---|---|
| Single 100% monitor | Wheel is centered inside the work area and remains fully visible. |
| Single 150% monitor | Icons and labels are crisp; hit targets align with rendered icons. |
| 100% primary + 150% secondary | Wheel opens on the pointer monitor and stays centered after crossing monitors. |
| 150% primary + 100% left monitor | Negative-origin placement is correct and does not jump to the primary display. |
| 175% or 200% monitor | Wheel size is consistent in logical units; no clipped status or labels. |
| Taskbar on top or side | Wheel is centered within the usable work area, not the full monitor bounds. |
| Display connected/disconnected while open | Active wheel closes safely; the next session uses the new topology. |
| Windows high contrast | The wheel uses system foreground/background colors and remains readable. |
| Windows reduced motion | Open and selection motion is suppressed or shortened. |

Record the Windows build, GPU/driver, scale values, monitor arrangement, and result when completing a release candidate. Do not commit screenshots or recordings; `.gitignore` deliberately excludes them.

# CodexBar for Windows Taskbar Dock Design

## Purpose

Add an always-visible minimal usage surface for CodexBar on Windows 11. The feature should let users see provider usage at a glance without opening the notification-area popover.

The selected direction is a **taskbar-adjacent dock**: a compact translucent strip positioned just above the Windows taskbar near the notification area. It should feel like it belongs with the taskbar, but it will not attempt unsupported Explorer/taskbar injection.

## Windows Platform Constraint

Windows 11 does not provide a supported modern API for placing arbitrary live app controls directly inside the taskbar. Supported shell surfaces include taskbar buttons, progress indicators, overlay icons, thumbnail toolbar buttons, jump lists, notification-area icons, and the Windows Widgets Board.

Windows Widgets are official, but they appear inside the Widgets Board rather than staying persistently visible on the desktop. For CodexBar's always-visible goal, the first implementation should use a topmost WPF window positioned relative to the taskbar work area.

## User Experience

When enabled, the dock appears near the bottom-right edge of the primary work area, just above the taskbar. It uses the same translucent white visual language as the popover, with enough blur/opacity that desktop text does not become clearly readable through it.

The dock shows one compact tile per enabled provider:

- Provider name.
- Primary usage percent.
- Small horizontal usage bar.
- Optional subtle stale/error indication.

For the first implementation, the primary usage value is the provider's first usage window, matching the current tray/docked overview behavior. If a provider has no usage windows, the tile shows `--` and a muted state.

Clicking the dock opens the full CodexBar popover. Right-clicking the dock opens a small context menu with:

- Refresh.
- Settings.
- Hide Taskbar Dock.

The dock should not take focus during normal refresh updates.

## Layout

The initial dock layout is a single horizontal strip:

- Default width: about `320px`.
- Height: content-driven, roughly one taskbar-adjacent row.
- Four equal-width provider tiles when all current providers are enabled.
- Fewer tiles when fewer providers are enabled.
- Provider text must ellipsize rather than resizing the dock.

The dock should be visually compact enough to stay above the taskbar without feeling like a second popover. It should not include cost rows, reset copy, settings icons, or explanatory text in the MVP.

## Positioning

Initial placement:

- Align to the right side of `SystemParameters.WorkArea`.
- Offset from the right edge by about `16px`.
- Offset above the bottom work-area edge by about `12px`.

The app should recalculate dock position when:

- Usage data changes and the dock size changes.
- Settings are applied.
- Display/work-area settings change.

The first version will use the primary work area only. Multi-monitor selection, manual dragging, and per-monitor persistence are deferred.

## Settings

Replace or rename the current `Dock overview near taskbar` option with `Show taskbar dock`.

The setting should continue to live in `AppSettings` and persist through the existing JSON settings store. Existing users with `DockOverviewNearTaskbar` enabled should keep the dock enabled after the rename/migration. If the property is not renamed internally during MVP implementation, the UI label should still use the new user-facing name.

## Architecture

Reuse the existing Windows app structure:

- `UsageSnapshot` remains the source of provider data.
- `RefreshScheduler` remains responsible for refreshing providers.
- `App` continues to update tray, popover, and dock surfaces from the shared snapshot store.
- `DockedOverviewWindow` can evolve into the taskbar dock window, or be replaced by a new `TaskbarDockWindow` if that keeps the implementation clearer.
- `DockedOverviewViewModel` can evolve into a tile-focused view model, or be replaced by `TaskbarDockViewModel`.

The implementation should prefer a focused new dock view model if the existing overview model becomes awkward. The dock should not duplicate provider API calls or maintain its own refresh loop.

## Error Handling

The dock should remain stable when providers fail:

- Missing provider data: show `--`.
- Stale snapshot: use muted text or a subtle warning accent.
- Refresh failure: keep the last good snapshot through the existing scheduler behavior.
- No enabled providers: hide the dock, or show a compact empty state only if needed for discoverability.

The full popover remains the place for detailed error copy. The dock should avoid long messages.

## Testing

Add or update tests for:

- Dock/tile view model formatting for normal data, empty windows, disabled providers, and stale snapshots.
- Settings label or settings persistence behavior around the taskbar dock option.
- Position calculation near the bottom-right work area.
- Existing tray and popover behavior should remain unchanged.

Manual verification should include:

- Primary monitor at standard scaling.
- Ultra-wide monitor placement near the tray edge.
- Taskbar auto-hide if available.
- Popover opens from clicking the dock.
- Dock hides and reappears when the setting is toggled.

## Deferred Work

Not included in the first implementation:

- Explorer/taskbar injection.
- Windows Widgets Board provider.
- Drag-to-position.
- Per-monitor docking.
- Multiple layout modes.
- Opacity slider.
- Fullscreen app suppression.
- Code signing or installer changes.

These can be added after the dock proves useful and stable.

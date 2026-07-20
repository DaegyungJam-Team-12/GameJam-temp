# UI-02 Launcher / Icebreaking HUD QA

- Date: 2026-07-20
- Unity: 6000.3.19f1
- Branch: `feature/ui-02-launcher-hud`

## Automated validation

`Icebreaker.UI.Editor.Ui02PrefabBuilder.Validate` completed successfully in Unity batch mode.

- Launcher Canvas reference resolution: 800×72
- Launcher regions: 112 / 200 / 80 / 96 / 208 / 48 px
- Icebreaking Canvas reference resolution: 960×540
- Icebreaking regions: funds 220×40, timer 160×40, settings 40×40
- All action hit areas are transparent parent regions with smaller non-raycast visual children.
- Action hit areas do not overlap.
- Presenter data, theme, text, image, and button references are assigned.
- No missing scripts were found in either Prefab.
- State text contract passed: `12.4K`, `00:42`, `37/120`, and zero-clamped countdown.
- C# compilation completed without errors.

## Prefabs

- `Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab`
- `Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab`

Each Prefab includes a preview-only `Ui02HudSampleSource`. Integration code can replace it through the presenter's `Bind(IGameStateSource)` method. The Prefabs intentionally do not create their own `EventSystem`, so an integration Scene can provide one shared input module without duplicates.

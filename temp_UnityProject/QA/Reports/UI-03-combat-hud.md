# UI-03 Countdown / Combat HUD QA

- Date: 2026-07-21
- Unity: 6000.3.19f1
- Branch: `feature/ui-03-combat-hud`

## Automated validation

`Icebreaker.UI.Editor.Ui02PrefabBuilder.BuildCombatHud` completed successfully in Unity batch mode.

- C# compilation completed without errors.
- `UI_IcebreakingHud.prefab` uses the 960×540 reference resolution.
- Combat regions match the locked layout: funds 220×40, timer 160×40, settings 40×40.
- The centered countdown region is 200×240 at X 380 / Y 150.
- Countdown states render only `3`, `2`, and `1`; `0` and additional messages are not rendered.
- Playing state renders only `정비 자금 12.4K`, `01:00`/`00:42`, and the settings action.
- Destination, maintenance, route, fold, and right-side status views are absent from the combat HUD.
- Negative time is formatted as `00:00`.
- One settings button click raises exactly one `SettingsRequested` event.
- Presenter data, theme, countdown, text, and button references are assigned.
- No missing scripts were found in the Prefab.
- Full EditMode regression suite passed: 46/46 tests, 0 failures, 0 skipped.

## Preview procedure

Open `Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab` and use the `Ui02HudSampleSource` component context menu:

1. `UI-03/Show Countdown 3`
2. `UI-03/Show Countdown 2`
3. `UI-03/Show Countdown 1`
4. `UI-03/Show Combat 60 Seconds`

`UI-02/Advance One Second` also advances a countdown and changes `1` to a 60-second Playing state. Integration can replace the sample source through `IcebreakingHudPresenter.Bind(IGameStateSource)`.

## Scope boundary

This task only presents `GameState` and raises the settings request. The game loop owns phase/timer changes, and later UI tasks own the settings modal and pause behavior.

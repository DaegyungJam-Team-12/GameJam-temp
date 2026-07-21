# UI-04 Reward / Settlement QA

- Date: 2026-07-21
- Unity: 6000.3.19f1
- Branch: `feature/ui-04-reward-settlement`

## Automated validation

`Icebreaker.UI.Editor.Ui04PrefabBuilder.Build` completed successfully in Unity batch mode.

- C# compilation completed without errors.
- `UI_RewardSettlement.prefab` uses the 960×540 reference resolution.
- Reward positions are clamped to X 28–932 / Y 64–389.
- The first reward renders `정비 자금 +80`; subsequent rewards render `+80`.
- Three rewards with the same chain ID inside 0.1 seconds combine into one `+240` popup at their center.
- Critical and chain events produce distinct `치명타!` and `연쇄 xN` feedback.
- Context-menu reward, critical, and chain samples remain visible for 2.5 seconds for visual inspection.
- UI-04 uses the shared dynamic Noto Sans KR TMP font with the global fallback configured.
- Settlement text displays the supplied `SettlementSummary` values without recomputing or granting funds.
- The settlement cannot continue before 1.2 seconds.
- Button, screen click, Enter, and Space can continue after 1.2 seconds.
- No-input settlement continues once at four seconds.
- Repeated manual/automatic continuation attempts raise `ContinueRequested` exactly once.
- The destination settlement displays the `목적지 도달` badge and port name.
- `InputLockChanged` and optional bound input targets remain locked for the full settlement view.
- No missing scripts or required serialized references were found in the Prefab.
- Full EditMode regression suite passed: 63/63 tests, 0 failures, 0 skipped.

The only remaining compiler warning comes from the pre-existing UI-02 builder's obsolete TMP wrapping property.

The sample source also resolves the live scene presenter when its context-menu actions are invoked, so reward, critical, and chain previews always attach to the visible Game-view canvas.

## Preview procedure

Open `Assets/01.Scenes/jeonghwan.unity`, enter Play Mode, select the `UI_RewardSettlement` scene instance, and use the `Ui04RewardSettlementSampleSource` context menu:

1. `UI-04/Reset Sample`
2. `UI-04/Show Edge Reward`
3. `UI-04/Show Critical`
4. `UI-04/Show Three-Reward Chain`
5. `UI-04/Show Settlement`
6. `UI-04/Show Destination Settlement`

The integration layer can replace all preview sources through
`RewardSettlementPresenter.Bind(ICombatEventSource, IProgressionEventSource, IGameStateSource)`.
Gameplay input components can be registered through `SetInputTargets(...)`; their previous enabled state is restored when settlement closes.

## Scope boundary

The Presenter only visualizes approved events and `SettlementSummary`. It never calculates rewards, changes `GameState`, or grants funds. The game loop owns the transition after `ContinueRequested`, and destination arrival presentation remains in UI-05.

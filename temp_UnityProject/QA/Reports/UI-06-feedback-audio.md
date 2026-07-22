# UI-06 Feedback / Audio QA

## Scope

- Branch: `feature/ui-05-management-views` (UI-05 and UI-06 combined PR)
- Baseline: `origin/dev` at `973b173`
- Unity: `6000.3.19f1`
- Prefab: `Assets/03.Prefabs/30.UI/Feedback/UI_FeedbackAudio.prefab`

## Automated validation

`Icebreaker.UI.Editor.Ui06PrefabBuilder.Build` completed successfully in a clean temporary Unity project.

- The Canvas uses the 960×540 reference resolution.
- The support device contains exactly 12 serialized charge segments.
- Waiting without a `SupportChargeChangedEvent` does not increase charge.
- Valid charge events render idle, charging, ready, and firing states.
- Critical, chain, crystal-ice, and crack-ice feedback use distinct labels and colors.
- Support firing enables the trail and muzzle flash for 0.22 seconds.
- Destruction audio is limited to eight simultaneous voices; additional destruction uses one rush layer.
- Both AudioSources are 2D, non-looping, and disabled on awake.
- First-run master volume defaults to zero and later slider changes are stored in `PlayerPrefs`.
- Duplicate `SettlementReady` events for one stage play the settlement confirmation cue once.
- No required serialized reference or script is missing from the Prefab.

Result: **PASS**

## EditMode regression

- Total: 122
- Passed: 122
- Failed: 0
- Skipped: 0

Result: **PASS**

The only compiler warnings are the pre-existing obsolete TMP wrapping properties in the UI-02 and UI-05 builders.

## Preview procedure

Place `UI_FeedbackAudio.prefab` in the UI sandbox, enter Play Mode, and use the `Ui06FeedbackSampleSource` context menu:

1. `UI-06/Reset Sample`
2. `UI-06/Add Valid Charge`
3. `UI-06/Complete Charge`
4. `UI-06/Fire Support Shot`
5. `UI-06/Show Critical`
6. `UI-06/Show Crystal Ice`
7. `UI-06/Show Crack Ice`
8. `UI-06/Show Five Chain`
9. `UI-06/Show Twenty Destroy Burst`
10. `UI-06/Show Settlement Twice`

Audio begins muted on a profile without a saved volume. Raise the master-volume slider in the UI-05 settings modal to hear the procedural fallback cues.

## Integration notes

- Bind the live combat and progression sources through `Ui06FeedbackAudioPresenter.Bind(...)`.
- Register live UI buttons through `SetUiButtons(...)` to enable shared button audio.
- The UI-05 master-volume slider now applies and persists the master volume directly.
- Short 2D procedural tones are used as license-free fallback clips until final sound assets are assigned.
- Final Scene placement and live source wiring remain part of `INT-03`; UI-06 does not edit the integration Scene.

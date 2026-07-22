# UI-05 Management Views QA

## Scope

- Branch: `feature/ui-05-management-views`
- Baseline: `origin/dev` at `973b173`
- Unity: `6000.3.19f1`
- Prefab: `Assets/03.Prefabs/30.UI/Management/UI_ManagementViews.prefab`

## Automated validation

`ICEBREAKER/UI/Rebuild UI-05 Management Views` builds the prefab and runs the following checks:

- 960×540 Canvas reference resolution.
- Exactly 13 fixed maintenance nodes with the C01–C04, D01–D03, S01–S03, and H01–H03 IDs.
- Owned, available, and locked sample states are all rendered.
- The management screen contains no `ScrollRect`.
- The route view contains no `Selectable`, so it cannot expose route selection.
- The settings modal contains master volume, screen shake, and quit controls.
- A purchasable node emits one purchase request without mutating the supplied ViewData.
- Repeated settings open/close calls emit one pause-boundary event per actual state change.
- A duplicate arrival request for the same destination is rejected and completes once.
- The completed route state displays `운항 완료` and disables stage start.

Result: **PASS**

## EditMode regression

- Command: Unity Test Runner, `EditMode`
- Total: 122
- Passed: 122
- Failed: 0
- Skipped: 0

Result: **PASS**

## Inspector / Play Mode checks

1. Place `UI_ManagementViews.prefab` in the UI sandbox or open it in Prefab Mode.
2. Use the `Ui05ManagementSampleSource` context menu:
   - `UI-05/Reset Sample`
   - `UI-05/Show Maintenance`
   - `UI-05/Show Route Status`
   - `UI-05/Show Ready State`
   - `UI-05/Show Completed State`
   - `UI-05/Open Settings`
   - the three `UI-05/Show ... Arrival` commands
3. Confirm all 13 maintenance nodes remain visible together at 960×540.
4. Confirm state is distinguishable by both label and panel treatment, not color alone.
5. Confirm selecting a node updates only the detail panel and a purchase click only raises `PurchaseRequested`.
6. Confirm the route panel has current destination, progress, cargo, completed destinations, and upcoming destinations but no route input.
7. Confirm settings visibility raises `SettingsVisibilityChanged`; timer pausing remains owned by the integration layer.
8. Confirm each arrival overlay lasts 1.5 seconds and raises `ArrivalPresentationCompleted` once.

## Integration notes

- The presenter consumes already-calculated `MaintenanceNodeViewData`; it does not calculate cost, effects, prerequisites, or affordability.
- Purchase, stage start, collapse, settings, quit, and arrival completion are output events only.
- Core purchase/save, window switching, pause application, route transition, and final scene wiring remain integration responsibilities.

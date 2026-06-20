# AGENTS.md — duck_test (Unity 6000.4.1f1)

Agent working notes. Repo root is `duck_test/` (NOT `Assets/`). 2D conveyor/inspector game.
Companion to `.cursor/rules/duck-game.mdc` — this file is the deep reference + task recipes.

---

## 0. READ FIRST — build/test reality (saves the most time)

- **Build Settings contains ONLY `Assets/Scenes/SampleScene.unity`.** That file is what ships.
- **Play mode runs the in-memory scene**, which can differ from the saved `.unity` file on disk.
  Symptom seen before: menu works in Play, build shows "only background" → the scene was edited
  in-editor but never saved, so the on-disk scene lacked the UI objects.
- When a bug is "works in editor, broken in build" → first verify the **on-disk** scene actually
  contains the objects (grep the `.unity` for the script GUID), and that it's the scene in Build Settings.
- I **cannot save the user's in-editor scene** for them. If on-disk scene is stale, tell them to
  `Ctrl+S` (and confirm correct scene is in Build Settings), then re-read the file to verify.
- `Assets/_Recovery/*.unity` are crash backups; `0 (2).unity` once held the full game+menu scene.
- `FindAnyObjectByType<T>()` and `FindObjectsByType<T>()` (no-arg) **skip inactive GameObjects**.
  To find components on inactive objects use `FindObjectsByType<T>(FindObjectsInactive.Include)`.
  The `FindObjectsSortMode` overload is **deprecated** in this Unity version — don't use it.

---

## 1. Core game loop

Conveyor (`PipelineController`) shifts `PipeObject`s left one slot every `MoveInterval` seconds.
The object reaching **slot 0** is handed to `InspectorController`, which **pauses the tick**, waits
`BaseInspectionDuration`, then **auto-decides Approve/Reject** (player never chooses the verdict).

- Player input = **throwing** objects (`ThrowSystem`, weight = slots flown) to merge/shove/bounce them
  before they reach the inspector.
- Spawning is an **explicit queue** from `LevelConfig.SpawnQueue` (NOT random), advanced on each tick.
- **Duckiness 0–3** lives on `PipeObjectData.BaseDuckiness`. For `Fake` archetype it's computed:
  `+1 Paint modifier, +1 Kit modifier` (max 2; needs both → still only 2, never 3 — fakes can't be perfect).
- **Anger 0–AngerMax(6)**; tiers at `AngerThresholdTier1(3)` / `Tier2(6)`. At tier ≥ 2 the inspector
  rejects anything below 3/3. Filling the bar → `OnRageFilled` → lose (if `LoseOnRageBarFull`).
- **Win:** all goals complete (`WinOnGoalsComplete`) OR pipeline drained (`WinOnPipelineDrained`).
  **Lose:** rage bar full.

### Data flow (one inspection)
```
PipelineController.TickPipeline()
  → shifts slots, slot0 occupant removed
  → InspectorController.BeginInspection(obj, onComplete)   // sets _pipeline.IsPaused = true
      → wait BaseInspectionDuration
      → ResolveOutcome(duckiness, anger.Tier) → Approve/Reject
      → anger.AddAnger(delta)  // delta from GetAngerDelta()
      → LevelManager.ReportInspection(obj, outcome, duckiness)   // ← goals counted HERE only
      → Destroy(obj); IsPaused = false; onComplete()
  → CompleteTick(): spawner.SpawnNext(); MoveOcupasToNewSlot(); TryNotifyPipelineDrained()
```

> **Goals are credited ONLY in `LevelManager.ReportInspection` / `ReportMergeGoals`**, i.e. at the
> moment the inspector checks the object — not when a merge/approve happens in the pipeline.

---

## 2. System map (MonoBehaviours, in `SampleScene`)

| Object | Components | Role |
|--------|-----------|------|
| `PipelineRoot` | `PipelineController` | conveyor tick, slots list, reach math, homerun points |
| (child) `PipeInteractionResolver` | `PipelineInteractionResolver` | merge/shove/bounce/move resolution by archetype + weight |
| `PipeSpawner` | `PipelineSpawner` | dequeues `SpawnQueue`, instantiates `PipeObject` |
| `ThrowSystem` | `ThrowSystem` | click→launch, flight coroutine (time/arc per slot) |
| `Slot0..Slot5` | `PipeSlot` | occupant holder; `Index` set by controller |
| `LevelSystems` | `GameStateManager`, `LevelManager`, `InspectorController`, `LevelSession`, `LevelResultsController`, `LevelMenuController`, `LevelPauseController` | level orchestration |
| (UI objects) | `UIMainMenu`, `UIPauseMenu`, `UILevelResultsScreen` (+`UIDocument`s) | UI Toolkit facades |

`LevelSession` is the **orchestrator**: wires everything in `Awake`, owns level start/end/pause,
subscribes to `OnAllGoalsComplete` / `OnRageFilled` / `OnPipelineDrained`, writes `PlayerProgress`.

---

## 3. UI architecture — Facade pattern (consistent across all UI)

Each UI area has the same 3-layer shape (look at one before adding another):

```
ILevelXxxActions      // what the UI can ask the game to do (Pause, Next, NewGame…)
ILevelXxxFacade       // what the controller tells the UI (Show…, Hide, SetRage…)
LevelXxxFacadeBase : MonoBehaviour, IFacade   // abstract; forwards button clicks → actions
  └─ UIXxx : LevelXxxFacadeBase               // concrete; binds UI Toolkit elements via Q<T>(name)
LevelXxxController : MonoBehaviour, IActions   // game-side logic; finds facade via FindAnyObjectByType
```

| Area | Controller | Facade base | Concrete | UXML |
|------|-----------|-------------|----------|------|
| Main menu | `LevelMenuController` | `LevelMenuFacadeBase` | `UIMainMenu` | `MainMenu.uxml`, `LoadLevel.uxml` |
| Pause/HUD | `LevelPauseController` | `LevelPauseFacadeBase` | `UIPauseMenu` | `GameHUD.uxml`, `PauseMenu.uxml` |
| Results | `LevelResultsController` | `LevelResultsFacadeBase` | `UILevelResultsScreen` | `ResultsScreen.uxml` |

UI Toolkit gotchas (learned the hard way):
- `rootVisualElement` / `Q<T>(name)` is only reliably populated **after** the UIDocument's panel
  builds (end of frame). Binding in `Awake` may return null → callbacks never register → "button dead,
  slider still drags" (drag is native and needs no callback). Bind in `Start`/on-show, or delay one frame.
- A script on the **same GameObject** as a `UIDocument` that does `gameObject.SetActive(false)` in
  `Awake` disables **itself**. Prefer hiding via `root.style.display = DisplayStyle.None` and keep the
  GO active (UIDocument needs an active GO to render).
- All menu `UIDocument`s share `Assets/UI Toolkit/MainPanelSettings.asset`. `ScaleMode =
  ScaleWithScreenSize`, ref res `1080x1920`, `Match = 0.5` (set for phone/auto-rotate adaptivity).
- App orientation is **auto-rotate (all 4)** → menus must center on screen, not anchor top-left.

---

## 4. Data assets (ScriptableObjects)

| Asset type | Menu | Files | Key fields |
|------------|------|-------|-----------|
| `LevelConfig` | Level/Level Config | `Assets/Data/Levels/Level_01.asset`, `Level_02.asset` | `MoveInterval`, `SpawnQueue[]`, `Goals[]`, `BaseInspectionDuration`, `Anger*`, `EndRules` |
| `LevelDatabase` | Level/Level Database | `Assets/Data/Levels/Campaign.asset` | `Levels[]` (campaign order = index) |
| `PipeObjectData` | Pipe/Pipe Object Data | `Assets/Prefabs/pipeline/objects/*Data.asset` | `Archetype`, `BaseDuckiness`, `Weight`, `FollowSpeed`, `ModifierType`, visual prefabs |

`LevelGoalData` is `[Serializable]` (inline in `LevelConfig.Goals`, not a separate asset).
Goal types: `ApproveObject`, `ApproveArchetype`, `MergePair`, `MergeToResult`.

### New level workflow
Duplicate `Level_01.asset` → edit `SpawnQueue` + `Goals` → add to `Campaign.asset.Levels`.
Commit the new `.asset` **with its `.meta`**.

---

## 5. Tunable parameters — where they live

Designer knobs (serialized):
- **Per-level:** `LevelConfig` (move interval, inspection duration, anger thresholds/max, end rules).
- **Per-object:** `PipeObjectData` (`Weight` = throw distance in slots, `FollowSpeed`,
  `FlightSpeedMultiplier`, `FlightArcMultiplier`, `BaseDuckiness`).
- **Scene SerializeFields:** `ThrowSystem` (`_flightTimePerSlot 0.14`, `_minFlightTime 0.25`,
  `_arcPerSlot 0.2`), `PipelineSpawner` (`_spawnOffset 2`), `PipelineController` (`_homerunBeyondSpacing 1`).

Hardcoded balance (C# literals, NOT serialized — change with care):
- `InspectorController.GetAngerDelta`: approve duck=2 → +1; reject duck=1 → +2; reject duck≤0 → +3.
- `InspectorController.ResolveOutcome`: approve if duckiness ≥ 2; tier≥2 forces 3/3.
- `PipelineInteractionResolver`: bounce destroy delay `0.35f`; weight rules (less→Shove, more→Bounce,
  equal+empty-behind→Move, equal+blocked→Shove).
- `PipeObject.FallbackFollowSpeed = 8f` (used when no `PipeObjectData`).

> History: a `CoreSettings`/`AllParamsController` system to expose all of these in an in-game menu was
> built then **reverted by the user**. Don't reintroduce unless asked.

---

## 6. Script GUID map (for editing `.unity` / `.asset` / `.prefab` YAML)

```
PipelineController              76ed4f3210a85cb99b8c0132ef7546da
PipelineInteractionResolver     682ef439cf4432e4699973ebcab40378
PipelineSpawner                 00bf82e20e01b564187e5bbe350c03a1
ThrowSystem                     f6cd5201b5c128145949635d0cb9908c
PipeObject                      83417d659fa2b0ceeb2dac45f0867391
PipeSlot                        c4d0e25397ba186f30b7ea69d148f25c
PipeObjectData                  29a7b1e6d80cf453194cad0352efb678
GameStateManager                c74398b5601da2ef08dbaef3476159c2
LevelManager                    ef0123456789abcd123456789abcdef0
InspectorController             f0123456789abcde23456789abcdef01
LevelSession                    def0123456789abc0123456789abcdef
LevelConfig                     bcde1234abcd5678ef9012345678abcd
LevelDatabase                   cdef2345bcde6789f0123456789abcde
LevelGoalData                   bb22cc33dd44ee55ff66778899001122
LevelGoalRuntime                dd44ee55ff6677889900112233445566
GoalType                        aa11bb22cc33dd44ee55ff6677889900
LevelEndRules                   cc33dd44ee55ff667788990011223344
LevelMenuController             a1b2c3d4e5f6478899aabbccddeeff01
LevelPauseController            b1c2d3e4f5a6478899aabbccddeeff02
LevelResultsController          0a1b2c3d4e5f67890123456789abcdef
PlayerProgress                  88990011223344556677889900112233
UIMainMenu                      a7b033d75ff32774c8468d91c77d79db
UIPauseMenu                     c1d2e3f4a5b6478899aabbccddeeff03
UILevelResultsScreen            d1e2f3a4b5c6478899aabbccddeeff04
MainPanelSettings (asset)       62a93875637bc944c83614a1b589aa56
```

---

## 7. Conventions & gotchas

- Match existing C# style: `[SerializeField] private`, no over-abstraction, file-scoped helper
  enums/structs at the end of the owning file (e.g. `InspectorAngerState` lives in `InspectorController.cs`,
  `LevelResultsSnapshot`/`LevelGoalResultLine` in `GoalType.cs`).
- **Prefer extending an existing imported `.cs`** over creating new orphan files (Unity must import +
  generate a `.meta`; new files also need adding to `Assembly-CSharp.csproj` for IDE intellisense).
- Every new/changed `.cs`/`.asset`/`.uxml` must be committed **with its `.meta`**. GUID = 32 lowercase hex.
- After adding a `.cs`, verify it appears in the generated `Assembly-CSharp.csproj`.
- Never commit `Library/`, `UserSettings/`, `Temp/`.
- `PlayerProgress` is a static facade over `IProgressStore` (`PlayerPrefsProgressStore` by default;
  keys `progress_highest_index`, `progress_completed_<levelId>`). `_ignoreSaveForTesting` on
  `LevelSession` bypasses saving + unlocks all levels.
- `PipeObject.MergeInputA/B` are set during `PipeObject.MergeWith`/`TryApplyModifier`; merge goals read
  them off the inspected object in `LevelManager.ReportMergeGoals`.
- Inspector verbose logs are prefixed `[Inspector]`, `[RageBar]`, `[Results]`, `[Spawner]` — grep these
  when debugging runtime behavior from user-pasted logs.

---

## 8. Task recipes (prompts → where to act)

- **"Add a new level"** → duplicate `Level_01.asset`, edit `SpawnQueue`+`Goals`, append to
  `Campaign.asset.Levels`. No code. Commit assets + metas.
- **"Add a new goal type"** → add enum case in `GoalType`, fields in `LevelGoalData`, match logic in
  `LevelGoalRuntime.MatchesXxx` + `GetDisplayDescription`, credit it in `LevelManager.ReportInspection`
  or `ReportMergeGoals`.
- **"Add a new pipe object / archetype interaction"** → `PipeObjectData` asset + visual prefab; for
  interactions edit `PipelineInteractionResolver` pair tables (`IsMergePair`/`IsShovePair`/
  `IsWeightBasedPair`) and `PipeObjectState` duckiness logic.
- **"Add a UI screen/button"** → follow the facade trio (§3). Add element to the `.uxml` with a `name`,
  bind via `root.Q<T>("name")` in the concrete `UIXxx`, route action through the interface. Bind on
  show, not in `Awake`. Center layout; touch-sized buttons.
- **"Change balance/feel"** → first decide: per-level (`LevelConfig`), per-object (`PipeObjectData`),
  global feel (`ThrowSystem` SerializeFields), or hardcoded (§5). Edit the narrowest scope.
- **"Tweak win/lose"** → `LevelConfig.EndRules` (flags) + `LevelSession.HandleAllGoalsComplete/
  HandlePipelineDrained/HandleRageFilled`.
- **"Build/phone bug"** → run §0 checklist (on-disk scene vs Play, Build Settings scene, inactive-object
  finds, UIDocument timing).

---

## 9. Verification before declaring done
- `ReadLints` on every edited `.cs`.
- For scene/asset YAML edits: re-read the file and confirm the block landed (right GUID, right fields).
- If the change must show in a build: confirm it's in the **on-disk** `SampleScene.unity`, and that
  scene is the one in Build Settings.

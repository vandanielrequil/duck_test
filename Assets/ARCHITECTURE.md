# duck_test — архитектура (handoff)

Документ для продолжения работы в новом чате. Подключай `@Assets/ARCHITECTURE.md` + `@AGENTS.md` + `@.cursor/rules/duck-game.mdc`.

Источник правды — **код**, не этот файл. Ниже зафиксировано состояние на момент сессии (механика бросков, Kit, Duck↔Paint, рейтинг, UI, баги).

Unity **6000.4.1f1**. Корень репо: `duck_test/` (не только `Assets/`).

---

## 0. Что это за игра

2D puzzle на конвейере. Объекты едут влево к инспектору. Игрок кликает по объектам и **бросает** их на `Weight` слотов, чтобы смержить / столкнуть / проскользить до инспекции.

Игрок **не выбирает** Approve/Reject. Инспектор решает сам по **duckiness 0–3**.

Цели засчитываются **только в момент инспекции**, не в момент merge/броска в пайплайне.

---

## 1. Сцена, билд, Unity-реальность

- **Build Settings содержит ТОЛЬКО** `Assets/Scenes/SampleScene.unity`. Это то, что шипится.
- Play mode крутит **in-memory scene**, она может отличаться от `.unity` на диске. Симптом: в Play меню есть, в билде «только фон» → сцена не сохранена (`Ctrl+S`).
- Агент **не может** сохранить in-editor scene за пользователя.
- `Assets/_Recovery/*.unity` — crash backups; `0 (2).unity` когда-то держал полный game+menu.
- `FindAnyObjectByType<T>()` / `FindObjectsByType<T>()` **без флага пропускают inactive**. Для UI на выключенных GO: `FindObjectsByType<T>(FindObjectsInactive.Include)`.
- `FindObjectsSortMode` overload в этой версии Unity **deprecated** — не использовать.

---

## 2. Карта систем (SampleScene)

| Object | Components | Роль |
|--------|------------|------|
| `PipelineRoot` | `PipelineController` | тик конвейера, слоты, homerun points, pause |
| (child) | `PipelineInteractionResolver` | merge/shove/bounce/move/KitKnock/slide |
| `PipeSpawner` | `PipelineSpawner` | dequeue `SpawnQueue`, instantiate |
| `ThrowSystem` | `ThrowSystem` | клик → полёт / Kit jump / slide / KitKnock |
| `Slot0..SlotN` | `PipeSlot` | occupant; `Index` выставляет controller |
| `LevelSystems` | `GameStateManager`, `LevelManager`, `InspectorController`, `LevelSession`, `LevelResultsController`, `LevelMenuController`, `LevelPauseController` | оркестрация |
| UI GOs | `UIMainMenu`, `UIPauseMenu`, `UILevelResultsScreen` + `UIDocument`s | UI Toolkit facades |

`LevelSession` — оркестратор: `Awake` вайрит всё, start/end/pause, подписки `OnAllGoalsComplete` / `OnRageFilled` / `OnPipelineDrained`, пишет `PlayerProgress`.

### Скрипты

```
Assets/Scripts/
  Level/
    GoalType.cs                 enums + LevelResultsSnapshot / LevelGoalResultLine
    LevelGoalData.cs            inline в LevelConfig.Goals
    LevelGoalRuntime.cs         matching + display
    LevelEndRules.cs
    LevelConfig.cs              + SpawnEntry
    LevelDatabase.cs
    LevelManager.cs             goals, actions, rage, additional, snapshot
    LevelSession.cs             lifecycle
    InspectorController.cs      + InspectorAngerState, IPipelineControl
    LevelMenuController.cs
    LevelPauseController.cs
    LevelResultsController.cs
    Progress/PlayerProgress.cs
  Pipe/
    PipelineController.cs
    PipelineSpawner.cs
    PipelineInteractionResolver.cs   + InteractionResult
    PipeObject.cs                    + PipeObjectState
    PipeObjectData.cs                enums archetype/modifier/tags
    PipeSlot.cs
    ThrowSystem.cs
    GameStateManager.cs
  UI/
    UIMainMenu.cs
    UIPauseMenu.cs
    UILevelResultsScreen.cs
```

Хелпер-типы **не выносить** в отдельные `.cs`: `InspectorAngerState` живёт в `InspectorController.cs`, snapshot — в `GoalType.cs`, `PipeObjectState` — в `PipeObject.cs`.

---

## 3. Слоты и тик пайплайна

```
index 0              = eject / off-screen (ClearOccupant каждый shift)
_inspectionSlotIndex = 1 (default) — отсюда забирают в инспекцию
last                 = tail / spawn
```

`PipelineController._inspectionSlotIndex` serialized, default **1**. Slot 0 специально пустой (выезд за экран).

### Один тик (`TickPipeline`)

```
если _stopped / IsPaused / inspector.IsBusy / state != Playing → return
toInspect = slots[inspectIndex].OccupiedObject  (снимаем со слота)
ShiftSlotsTowardInspector(inspectIndex)
  → copy [i+1] → [i] для i >= inspectIndex
  → clear tail
  → clear все слоты i < inspectIndex   ← slot 0 всегда чистится без Destroy
если toInspect != null:
  Inspector.BeginInspection(toInspect, CompleteTickAfterInspection)
  return   // CompleteTick ждёт конца инспекции
иначе CompleteTick()
```

`CompleteTick`: `spawner.SpawnNext()` → `MoveOcupasToNewSlot()` → `TryNotifyPipelineDrained()`.

`OnPipelineDrained` если: очередь кончилась + все слоты пусты + инспектор не busy.

### Pause

`IsPaused` ставит ThrowSystem (полёт, Kit jump), Inspector (во время wait), LevelSession (pause menu, `Time.timeScale = 0`).

Тик также гейтится `inspector.IsBusy` (`_inspectionRoutine != null`). Если выйти в меню mid-inspection без `CancelInspection()` — пайплайн мёртв навсегда.

---

## 4. Объекты

### `PipeObjectData` (ScriptableObject)

```
Archetype: Duck | Fake | Modifier
ModifierType: None | Paint | Kit     // только для Modifier
Weight >= 1                          // дистанция броска в слотах
BaseDuckiness 0..3                   // Duck default 3 via OnValidate
FollowSpeed, FlightSpeedMultiplier, FlightArcMultiplier
ClickCooldown                        // Kit = 1s
CanBeMerged, CanBeShoved
visual prefabs: Display + Fake / FakePaint / FakeKit / FakePaintKit
```

Ассеты: `Assets/Prefabs/pipeline/objects/*Data.asset` (`DuckData`, `PaintData`, `KitData`, fake variants…).

### Duckiness runtime (`PipeObjectState.Duckiness`)

- не Fake → `ObjectData.BaseDuckiness`
- Fake → `+1` за Paint, `+1` за Kit, **max 2**, никогда 3 (фейк не может быть идеальным)

Merge history: `MergeInputA/B/C` пишутся в `TryAddModifier` при Fake+Modifier. Первый merge → A+B, второй modifier → C.

### Клик

`ThrowSystem.Update`:
1. `IsPlayableState` (`Playing`) и не `IsBusy` (идёт полёт)
2. pointer press → ray `OverlapPointAll` → `PipeObject`
3. если `IsClickOnCooldown` → ignore (не считает action)
4. `LevelManager.RegisterAction()` + `obj.RegisterClick()`
5. Kit → `TryJumpInPlace` (пауза пайплайна, прыжок на месте, cooldown)
6. иначе → `TryLaunchByWeight`

---

## 5. Бросок (`ThrowSystem`)

Направление:
- **Duck → -1** (влево, к инспектору)
- всё остальное → **+1** (вправо, к хвосту)

Дистанция = `Weight`. Если `GetSlotAtOffset` = null (за край) — бросок не стартует (`TryLaunchByWeight` false), но action уже посчитан.

Полёт: `FlyToSlotRoutine` — снимает со слота, `BeginFlight`, дуга `FlyArcSegment`. Пайплайн на паузе.

После приземления, **до** `ResolveInteraction`:
- Duck↔Paint slide continuation (см. §6)
- если `currentTarget.Index <= ejectSlot.Index` → **Destroy сразу** (иначе следующий tick `ClearOccupant` slot 0 оставит объект висеть без слота)
- иначе `ResolveInteraction`

Bounce не снимает pause сам в ThrowSystem (destroy delay в Resolver снимает).

Feel knobs (scene SerializeField): `_flightTimePerSlot 0.14`, `_minFlightTime 0.25`, `_arcPerSlot 0.2`.

---

## 6. Взаимодействия (`PipelineInteractionResolver`)

`DetermineInteraction` priority:

1. **Merge** если `CanMerge` = Fake↔Modifier (Paint/Kit на Fake). Результат остаётся Fake в слоте target, modifier Destroy. Visual через `GetVisualPrefab()`.
2. **KitKnock** если target is Kit (см. §7)
3. **WeightBased** пары: Fake↔Duck, Duck↔Fake, Fake↔Fake
4. **ShovePair** (тоже через weight logic): Duck↔Modifier, Duck↔Duck, Modifier↔Modifier, Fake↔Modifier, Fake↔Duck
5. **Duck↔Paint** (любая сторона) → `ResolveWeightBased` (не Bounce!)
6. default → **Bounce**

Пустой слот → **Move**.

### Weight rules (`ResolveWeightBased`)

```
movingWeight < targetWeight → Shove   // target на +1 (к хвосту), moving занимает слот
movingWeight > targetWeight → Bounce  // flying уничтожается ~0.35s
equal + empty behind target → Move    // target сдвигается на +1, moving занимает
equal + blocked             → Shove
```

«Behind» = `target.Index + 1` (хвост, вправо). Bounce: flying улетает по касательной, Destroy, `_pipeline.IsPaused = false`.

Homerun: `ResolveHomerun` — Destroy, left/right beyond pipeline (`_homerunBeyondSpacing`).

### Duck ↔ Paint slide (симметрично)

Реализовано в **ThrowSystem loop**, не отдельным enum.

`TryGetDuckPaintSlideContinuation`:
- blocker на landing slot — Duck+Paint пара
- hit #0 в цепочке этого броска → повторный полёт на `Weight` летящего
- hit #1+ → 1 слот
- направление от **летящего**: Duck -1, Paint +1
- `TryGetSlideTargetSlot` **clamp** у края пайплайна (если +2 некуда — летит +1; если 0 — continuation false)
- blocker **не двигается** во время slide
- на финальной клетке (continuation false) → обычный `ResolveInteraction`

Баг который ловили: Paint→Duck падал в default Bounce, потому что Modifier+Duck не было в shove/weight tables и continuation false у края. Фикс: `IsDuckPaintPair` в DetermineInteraction + clamp.

---

## 7. Kit

- **Клик:** не летит. `JumpInPlaceRoutine` (дуга вверх, `_minFlightTime`). Пайплайн paused. `ClickCooldown` на `KitData.asset` = 1s.
- **Удар по Kit (KitKnock):**
  ```
  kitFlyDistance = clamp(3 - strikerWeight, 1, 2)  // w=2 → 1 слот, w=1 → 2 слота
  direction = sign(kitIndex - strikerFromIndex)    // вдоль вектора удара
  ```
  Atomic swap: Kit отвязать от слота вручную (`CurrentSlot=null`, `OccupiedObject=null`), striker занимает слот Kit.
  Затем **параллельно**:
  - Kit летит на `kitTarget` на `kitDistance`
  - striker rebound **1 слот** в противоположную сторону исходного полёта
  Потом `ResolveInteraction` для обоих landings.

  Если `TryResolveKitKnock` false (Kit у края, некуда лететь) → **Destroy Kit**, striker в его слот. Иначе Kit зависает без слота.

---

## 8. Инспектор

`BeginInspection` → pause pipeline → wait `BaseInspectionDuration` → `ResolveOutcome` → anger → `ReportInspection` → eject.

### Verdict (`ResolveOutcome`)

```
anger.Tier >= 2 && duckiness < 3 → Reject
duckiness >= 2 → Approve
else Reject
```

Игрок не выбирает.

### Anger delta (`GetAngerDelta`) — hardcoded

```
Approve duck=2 → +1
Approve else   → 0
Reject duck=1  → +2
Reject else    → +3   // duck ≤ 0
```

Anger 0..`AngerMax`(6). Tiers: T1 @ `AngerThresholdTier1`(3), T2 @ `AngerThresholdTier2`(6).

**Lose:** `Current >= Max` → `OnRageFilled` → `LevelSession.HandleRageFilled` если `EndRules.LoseOnRageBarFull`.

Это **не** «3 rage modes со сбросом шкалы». Старые доки/rules врут: сейчас одно заполнение бара = fail. `RageModeCount` нет.

`RegisterRage(angerDelta)` копится отдельно для рейтинга (сумма дельт за уровень, не текущий бар).

### Eject

После решения объект **не Destroy сразу** на слоте инспекции:
1. `BeginEject` — в `_managedObjects`, снять со слота, `MoveTo(GetEjectSlot())` (= slot 0)
2. **pipeline unpause сразу**, `onComplete()` → `CompleteTick` параллельно с анимацией
3. `DestroyAfterEject` потом Destroy и выкинуть из `_managedObjects`

### `CancelInspection`

`StopAllCoroutines`, `_inspectionRoutine = null`, Destroy все `_managedObjects`. Вызывается из `BindLevel()` и `LevelSession.ReturnToMenu()`. Без этого повторный старт уровня блокируется `IsBusy`.

---

## 9. Спавн и QueueStartSlotIndex

Очередь **явная** `LevelConfig.SpawnQueue[]` (`SpawnEntry { PipeObjectData Object }`), не RNG. `SpawnRandom()` = alias на `SpawnNext()`.

Спавн **всегда в хвост**. `QueueStartSlotIndex`:
- `-1` (default): пустой старт, по одному с хвоста каждый tick
- `>= 0`: `PrefillQueueStart()` заполняет `[start .. tail]` из очереди **без** slide-in. Голова очереди сидит в `start` (ближе к инспектору). Дальше обычный тик: сдвиг влево, досыпка в хвост.

`LevelSession.BeginLevel`: `BindQueue(queue, QueueStartSlotIndex)` → `ResetForLevel` → `PrefillQueueStart()`.

---

## 10. Цели

`LevelGoalData` — `[Serializable]` inline в `LevelConfig.Goals`, не отдельный asset.

| GoalType | Когда | Matching |
|----------|--------|----------|
| `ApproveObject` | Approve + duckiness ≥ `MinDuckiness` (default 3) + Data == `ApproveTarget` | inspection |
| `ApproveArchetype` | Approve + duckiness ≥ Min + archetype/tags | inspection |
| `MergePair` | на inspected object есть merge history | inspection (`ReportMergeGoals`) |
| `MergeToResult` | `obj.State.ObjectData == MergeResult` | inspection |

**Все цели** идут через `LevelManager.ReportInspection` / `ReportMergeGoals`. Merge в пайплайне только пишет `MergeInputA/B/C`.

### MergePair matching

- `MergeInputC == null`: A/B **или** B/A (swap). Не мультисет. Объект с тремя входами **не** матчит 2-input цель (нужны ровно A и B).
- `MergeInputC != null`: все 6 перестановок A,B,C. Если у объекта нет C → false.

История: пытались мультисет «2-input цель матчит 3-input объект» — **сломало** Merge Pair, откатили к permutation/swap. Не возвращать мультисет без запроса.

### Additional goals

`LevelGoalData.IsAdditional`:
- трекаются в HUD/results (`[bonus]`)
- **не** входят в `AllGoalsComplete` (win только по non-additional; если required целей нет вообще — `AllGoalsComplete` = false)
- влияют на рейтинг через `MinAdditionalForRating3/2`

---

## 11. Win / Lose / Rating

`LevelEndRules` (на LevelConfig, default все true):
- `WinOnGoalsComplete`
- `WinOnPipelineDrained`
- `LoseOnRageBarFull`

`LevelEndReason`: `GoalsComplete` | `PipelineDrained` | `RageBarFull`.
`LevelEndOutcome`: `Success` | `Fail`.

### Рейтинг 0–3

Счётчики в `LevelManager`, сброс в `Load()`:

| Метрика | Откуда |
|---------|--------|
| `actionsUsed` | каждый успешный click-path в ThrowSystem (`RegisterAction`) |
| `rageAccumulated` | сумма anger delta (`RegisterRage`) |
| `additionalCompleted` | сколько additional целей дошли до Complete |

Пороги **per level** в `LevelConfig`:

```
MaxActionsForRating3 / 2     // inclusive; пример 2 и 4
MaxRageForRating3 / 2        // defaults 0 и 2
MinAdditionalForRating3 / 2  // defaults 0 и 0 (нет требования)
```

```
rating = min(actionsRating, rageRating, additionalRating)
Fail → rating = 0 всегда
```

`OnValidate` следит чтобы порог 2 ≥ порог 3 (actions/rage) и additional2 ≤ additional3.

UI: `ResultsScreen.uxml` — Actions, Rage, Rating `X/3`, цели. HUD: goal progress через `OnGoalProgress`.

---

## 12. UI — Facade pattern

```
ILevelXxxActions        // UI → game (Pause, Next, NewGame…)
ILevelXxxFacade         // game → UI (Show, Hide, SetRage…)
LevelXxxFacadeBase : MonoBehaviour, IFacade
  └─ UIXxx              // Q<T>(name) bind
LevelXxxController : MonoBehaviour, IActions
  finds facade via FindAnyObjectByType
```

| Area | Controller | Facade base | Concrete | UXML |
|------|------------|-------------|----------|------|
| Main menu | `LevelMenuController` | `LevelMenuFacadeBase` | `UIMainMenu` | `MainMenu.uxml`, `LoadLevel.uxml` |
| Pause/HUD | `LevelPauseController` | `LevelPauseFacadeBase` | `UIPauseMenu` | `GameHUD.uxml`, `PauseMenu.uxml` |
| Results | `LevelResultsController` | `LevelResultsFacadeBase` | `UILevelResultsScreen` | `ResultsScreen.uxml` |

### UI Toolkit gotchas (дорого выучено)

- `rootVisualElement` / `Q<T>` надёжно **после** build панели (конец кадра). Bind в `Awake` → null → мёртвые кнопки, слайдер при этом живой (native drag). Bind в `Start` / on-show / `schedule.ExecuteLater(0)`.
- `SetActive(false)` на GO с `UIDocument` **выключает сам скрипт**, если он на том же GO. Прятать через `root.style.display = DisplayStyle.None`, GO оставить active.
- Все меню шарят `Assets/UI Toolkit/MainPanelSettings.asset`: `ScaleWithScreenSize`, ref `1080x1920`, `Match = 0.5` (телефон + auto-rotate).
- Ориентация **все 4** → layout центрировать, не top-left.
- LoadLevel: кнопки `Level_1`…`Level_10`. `UIMainMenu` prefix `Level_`, offset 1 → `Level_3` = campaign index 2. Locked: `(Locked)`.
- Pause Restart: в UXML имя `Retry`, fallback `FindButton(..., "Retry")`.

### Повторный выбор уровня

Симптом: выбрать уровень → меню → выбрать снова — клик мёртвый. New Game → pause → меню → Load Level — ок (document ещё не deactivatили).

Причина: `SetActive(false)` на LoadLevel UIDocument пересобирает visual tree, старые button refs orphan. Бинд был one-shot.

Фикс: каждый `ShowLoadLevelDocument()` → `UseLoadLevelDocument()` + `RefreshLevelButtons()` + `ScheduleLoadLevelRebind()` (`ExecuteLater(0)`). Плюс `CancelInspection` (см. §8).

---

## 13. Прогресс и состояния

`PipeGameState`: `MainMenu`, `Playing`, `Inspecting`, `GameOver`, `Paused`, `LevelWon`, `LevelLost`, `CampaignComplete`.

`PlayerProgress` — static facade над `IProgressStore` (`PlayerPrefsProgressStore`):
- `progress_highest_index`
- `progress_completed_<levelId>`

`LevelSession._ignoreSaveForTesting` — не писать сейв + unlock all. `_autoStartLevelForDebug` + `_debugStartLevelIndex`.

New Game: `PlayerProgress.ResetCampaign`. Win: `CompleteLevel(levelId, nextIndex)` если не ignore save.

---

## 14. Данные уровней

| Asset | Menu | Path | Поля |
|-------|------|------|------|
| `LevelConfig` | Level/Level Config | `Assets/Data/Levels/Level_*.asset` | MoveInterval, SpawnQueue, QueueStartSlotIndex, Goals, BaseInspectionDuration, Anger*, EndRules, rating thresholds |
| `LevelDatabase` | Level/Level Database | `Assets/Data/Levels/Campaign.asset` | `Levels[]` = campaign order |
| `PipeObjectData` | Pipe/Pipe Object Data | `Assets/Prefabs/pipeline/objects/*Data.asset` | см. §4 |

Новый уровень: duplicate `Level_01.asset` → SpawnQueue + Goals + QueueStart + rating → append в `Campaign.asset.Levels`. Commit **asset + .meta**.

---

## 15. Где живут параметры

**Per-level (`LevelConfig`):** interval, inspection duration, anger thresholds/max, end rules, queue start, rating.

**Per-object (`PipeObjectData`):** Weight, FollowSpeed, Flight*Multiplier, BaseDuckiness, ClickCooldown.

**Scene SerializeFields:** ThrowSystem flight feel, Spawner `_spawnOffset 2`, Pipeline `_homerunBeyondSpacing 1`, `_inspectionSlotIndex 1`.

**Hardcoded (менять осторожно):**
- `GetAngerDelta` / `ResolveOutcome`
- bounce delay `0.35f`
- weight shove/bounce/move
- Kit knock `clamp(3 - weight, 1, 2)`, rebound 1 slot
- Duck-Paint slide: first hit = Weight, later = 1
- `PipeObject.FallbackFollowSpeed = 8f`

**НЕ возвращать** `AllParamsController` / `CoreSettings` UI — собрали, пользователь откатил.

---

## 16. Data flow инспекции

```
TickPipeline
  → shift, occupant inspect-слота снят
  → Inspector.BeginInspection            // IsPaused = true, IsBusy = true
      → wait BaseInspectionDuration
      → ResolveOutcome
      → anger.AddAnger + RegisterRage
      → LevelManager.ReportInspection    // ← ЕДИНСТВЕННОЕ место кредита целей
          ReportMergeGoals (MergePair / MergeToResult, в т.ч. additional)
          если Approve → Approve* goals
      → BeginEject (slot 0), IsPaused = false, onComplete
  → CompleteTick: SpawnNext, MoveOcupas, TryNotifyPipelineDrained
```

---

## 17. Script GUID map (YAML scene/prefab/asset)

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
MainPanelSettings               62a93875637bc944c83614a1b589aa56
```

---

## 18. Конвенции / Git

- Стиль: `[SerializeField] private`, без оверабстракции, file-scoped helpers в конце owning file.
- Предпочитать **расширять существующий `.cs`**, не плодить orphan files (Unity import + `.meta` + `Assembly-CSharp.csproj`).
- Каждый новый/изменённый `.cs` / `.asset` / `.uxml` / `.prefab` коммитить **с `.meta`**. GUID = ровно 32 lowercase hex. Короткий GUID → CS0246, скрипт не импортируется.
- После нового `.cs` — проверить что он в generated `Assembly-CSharp.csproj`.
- Не коммитить `Library/`, `UserSettings/`, `Temp/`, `*.csproj`, `*.sln`.
- Логи: `[Inspector]`, `[RageBar]`, `[Results]`, `[Spawner]`.

---

## 19. Баги из треда и фиксы (не повторять)

| Симптом | Причина | Фикс |
|---------|---------|------|
| Второй выбор уровня мёртвый | UIDocument rebuild + one-shot bind; `IsBusy` после mid-inspect exit | rebind LoadLevel каждый show + `ExecuteLater(0)`; `CancelInspection` на menu/BindLevel |
| Play ок, билд пустой | on-disk scene ≠ Play; не тот scene в Build Settings | Ctrl+S, grep GUID в SampleScene, только она в build |
| UI кнопки мертвы, слайдер жив | bind в Awake до panel | bind Start/on-show |
| Pause Restart мёртв | UXML `Retry` vs код `Restart` | fallback имя |
| После инспекции объект замирает на slot 1, пайплайн стоит | Destroy сразу / pause до конца eject | eject на slot 0, unpause сразу |
| Paint→Duck bounce | нет пары в tables, slide false у края | `IsDuckPaintPair` + clamp slide |
| Merge Pair сломался | мультисет required.Count==actual.Count | откат: 2-input swap, 3-input permutations |
| Paint бьёт Kit, сам «теряет пайплайн» | striker не занял слот Kit | atomic swap `SetOccupant(striker)` |
| Kit jump spam, очередь уезжает | нет cooldown, jump не паузит | `ClickCooldown` + `IsPaused` в jump |
| Kit у последнего слота зависает | knock fail → не Destroy | Destroy Kit, striker в слот |
| Утки зависают за экраном | посадка в slot 0, tick `ClearOccupant` без Destroy | Destroy если `target.Index <= ejectSlot.Index` |
| `FindAnyObjectByType` не находит UI | inactive GO | `FindObjectsInactive.Include` |
| скрипт на UIDocument `SetActive(false)` в Awake | самовыпиливается | display:None, GO active |

---

## 20. Что явно откатили / не делать

- `AllParamsController` + in-game CoreSettings — **reverted**. Баланс через assets/SerializeField/hardcoded.
- MergePair как мультисет (2-input цель на 3-input объект) — **reverted**, ломает цели.
- Не считать цели в момент merge в пайплайне.

Устаревшее в `.cursor/rules/duck-game.mdc` / старом этом файле:
- «lose after 3 rage modes» — сейчас один full bar = fail
- «slot 0 → inspector» — сейчас inspect = index 1, slot 0 = eject
- TODO `InitialOccupants` — сделано как `QueueStartSlotIndex`
- TODO results Canvas — сделано UI Toolkit `UILevelResultsScreen`

---

## 21. Рецепты задач

- **Новый уровень** → duplicate asset, SpawnQueue+Goals+QueueStart+rating, append Campaign. Без кода.
- **Новый GoalType** → enum + поля `LevelGoalData` + `MatchesXxx` + credit в `ReportInspection`/`ReportMergeGoals`.
- **Новый объект/взаимодействие** → Data asset + visual prefab; пары в Resolver; duckiness в `PipeObjectState`; полёт/особые кейсы в ThrowSystem.
- **Новый UI** → facade trio, named element в UXML, bind on show, center, touch-size.
- **Баланс** → самый узкий слой: LevelConfig vs PipeObjectData vs ThrowSystem fields vs hardcoded §15.
- **Win/lose** → `EndRules` + handlers в `LevelSession`.
- **Build/phone** → §1 checklist.

---

## 22. Верификация перед «готово»

- `ReadLints` на каждый изменённый `.cs`
- YAML правки: перечитать файл, GUID/поля на месте
- Если должно быть в билде: on-disk `SampleScene.unity` + она в Build Settings
- Play smoke: меню → уровень → меню → тот же уровень снова
- Duck/Paint у края пайплайна (не bounce на первом контакте)
- Fail → rating 0/3; success → actions/rage/additional
- Additional complete сам по себе не вин
- Kit у хвоста уничтожается, не висит
- Бросок в eject-зону уничтожает объект

---

## Быстрый старт в новом чате

```
@Assets/ARCHITECTURE.md
@AGENTS.md
@.cursor/rules/duck-game.mdc

[задача]
```

Смотри код, не этот файл, если они разойдутся. Сопутствующие: `PROMPTS.md` (рецепты багов той же сессии), `AGENTS.md` (GUID map + recipes).

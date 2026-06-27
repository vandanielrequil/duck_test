# PROMPTS.md — знания из диалога (duck_test)

Промпты и рецепты для агента. Дополняет `AGENTS.md` (общая архитектура) и `.cursor/rules/duck-game.mdc`.
Repo root: `duck_test/`, Unity **6000.4.1f1**.

---

## Как пользоваться

- Сначала `AGENTS.md` §0–§2 (сцена, цикл, карта систем).
- Этот файл — **конкретные фичи и баги**, сделанные/обсуждённые в сессии.
- При правках — минимальный diff, `.meta` к каждому новому/изменённому файлу, без `Library/`/`Temp/`.

---

## 1. Меню: повторный выбор уровня не работает

### Симптом
Выбрать уровень → выйти в меню → выбрать снова — второй раз не срабатывает (клик «в никуда»).
**New Game → пауза → меню → выбор уровня** — работает (Load Level document ещё не показывали).

### Причина
`LoadLevel.uxml` скрывается через `SetActive(false)`. При повторном показе UI Toolkit **пересобирает visual tree** — старые кнопки «осиротевшие», а `UIMainMenu.ShowLoadLevelDocument()` биндил кнопки **один раз** (`if (_levelButtonBindings.Count == 0)`).

### Правильный паттерн (уже в коде)
В `UIMainMenu.ShowLoadLevelDocument()`:
1. Каждый показ: `UseLoadLevelDocument()` + `RefreshLevelButtons()`.
2. Отложенный ребайнд: `ScheduleLoadLevelRebind()` → `root.schedule.Execute(...).ExecuteLater(0)` (как `UIPauseMenu.ScheduleGameHudRebind()`).

### Промпт
> «Второй раз не работает выбор уровня после возврата в меню» → проверить rebind LoadLevel UIDocument на каждый show, не one-shot guard.

### Связанный фикс (инспектор)
`InspectorController.CancelInspection()` + вызов из `BindLevel()` и `LevelSession.ReturnToMenu()` — отмена корутины инспекции и `_managedObjects`, иначе `_inspector.IsBusy` блокирует `PipelineController.TickPipeline()` навсегда.

---

## 2. Точка начала очереди (QueueStartSlotIndex)

### Не путать со «слотом спавна»
Спавн **всегда в хвост** (последний слот). `QueueStartSlotIndex` — **где начинается голова очереди** при старте уровня.

### Поведение
- `-1` (default): пустой пайплайн, объекты по одному с хвоста.
- `>= 0`: при старте `PipelineSpawner.PrefillQueueStart()` заполняет слоты `[start .. tail]` из очереди; объект `[0]` очереди — в слоте `start` (ближе к инспектору). Дальше — обычный тик: сдвиг влево, досыпка в хвост.

### Файлы
- `LevelConfig.QueueStartSlotIndex`
- `PipelineSpawner.PrefillQueueStart()`, `SpawnIntoSlot(slot, slideIn)`
- `LevelSession.BeginLevel()` после `ResetForLevel` → `PrefillQueueStart()`

### Индексация слотов
`0` = сторона инспектора/выезда, **last** = хвост спавна. Инспекция по умолчанию в `_inspectionSlotIndex = 1`, выезд в slot 0.

### Промпт
> «Объекты должны появляться не с последней клетки, а очередь начинается с середины» → `QueueStartSlotIndex`, prefill до хвоста, спавн по-прежнему только в tail.

---

## 3. Направление броска

### Правило
- **Duck** (`PipeArchetype.Duck`) летит **влево** (к инспектору, меньший индекс слота): `GetThrowDirection = -1`.
- **Всё остальное** — **вправо** (к хвосту): `+1`.

### Файл
`ThrowSystem.cs`: `slotsToFly = GetWeight(obj) * GetThrowDirection(obj)`, в корутину — `Mathf.Abs(slotsToFly)` для длительности/дуги.

### Промпт
> «Утка должна лететь влево, остальные вправо» → только Duck получает `-1` в `GetThrowDirection`.

---

## 4. Duck ↔ Paint slide (скольжение)

### Правило (симметрично)
Пара **Duck + Paint** (`ModifierType.Paint`), в **обе стороны**:
1. **Первое** попадание в цепочке одного броска → повторный полёт на **`Weight` летящего** (обычно 2) с анимацией дуги.
2. **Второе и дальше** попадания на Paint/Duck в той же цепочке → **+1 клетка**.
3. На **финальной** клетке — обычное `ResolveInteraction` (move/shove/bounce/merge).
4. Paint/Duck на месте **не сдвигаются** во время slide — летящий «проскальзывает», blocker остаётся в слоте.

### Где реализовано
- `PipelineInteractionResolver.TryGetDuckPaintSlideContinuation()` — проверка пары, расчёт дистанции.
- `TryGetSlideTargetSlot()` — от `landingSlot.Index`, **clamp у края пайплайна** (если +2 некуда — летит на +1; если некуда — slide не стартует).
- `ThrowSystem.FlyToSlotRoutine()` — цикл сегментов + `FlyArcSegment`.
- Fallback: `IsDuckPaintPair` в `DetermineInteraction` → `ResolveWeightBased`, **не Bounce** (Paint→Duck раньше не было в `IsShovePair`).

### Направление slide
От **летящего** объекта: Duck `-1`, Paint `+1`.

### Промпт
> «Duck на Paint / Paint на Duck — повтор полёта, потом +1» → continuation в ThrowSystem, не менять только Resolver без цикла полёта.

### Типичный баг
Paint влетает в Duck → bounce: continuation вернул false (край пайплайна) + Modifier+Duck не было в парах → default Bounce. Фикс: clamp slide + `IsDuckPaintPair` в DetermineInteraction.

---

## 5. Инспекция и выезд (eject)

### Слоты
- `_inspectionSlotIndex` (default 1) — откуда забирают на инспекцию.
- `GetEjectSlot()` — slot 0 (предпоследняя видимая точка; последнюю можно увести за экран).

### Поведение после инспекции
Объект **не уничтожается сразу** на слоте инспекции:
1. `BeginEject` — снять со слота, `MoveTo(ejectSlot)`, корутина `DestroyAfterEject`.
2. **Пайплайн не ждёт** eject: `_pipeline.IsPaused = false` сразу после начала eject, `onComplete()` → `CompleteTick()` (спавн + сдвиг параллельно).

### Промпт
> «После инспекции объект должен уехать на slot 0, а конвейер не стоять» → decouple eject animation from pipeline pause.

---

## 6. Система оценки уровня (3/3)

### Счётчики
| Метрика | Где копится | Когда сброс |
|--------|-------------|-------------|
| **Actions** | `LevelManager.RegisterAction()` | `Load()` |
| **Rage** (сумма delta за уровень) | `RegisterRage()` из `InspectorController` при `angerDelta > 0` | `Load()` |
| **Additional goals** | `_additionalCompleted` при завершении goal с `IsAdditional` | `Load()` |

Actions: каждый клик по `PipeObject` в `ThrowSystem.Update` (даже если бросок не удался).

### Критерии в `LevelConfig` (per level)
**Actions (max inclusive):**
- `MaxActionsForRating3`, `MaxActionsForRating2`

**Rage (max inclusive, defaults 0 / 2):**
- `MaxRageForRating3 = 0`, `MaxRageForRating2 = 2`

**Additional (min completed, defaults 0 / 0):**
- `MinAdditionalForRating3`, `MinAdditionalForRating2`

### Формула
```csharp
rating = Min(
    actionsRating,
    rageRating,
    additionalRating
);
```
При **провале уровня** (`LevelEndOutcome.Fail`) → **всегда 0/3**, независимо от метрик.

### UI результатов
`ResultsScreen.uxml`: `ActionsUsed`, `RageAccumulated`, `Rating`, `TaskProgress`.
`LevelResultsController.FormatRating` → `"X/3"`.

### Промпт
> «Добавить в оценку rage / additional goals» → поля в LevelConfig, RegisterRage / additional counter, min/max логика в ComputeRating, не трогать win condition для additional.

---

## 7. Additional goals

### Поле
`LevelGoalData.IsAdditional` — чекбокс на каждой цели в `LevelConfig.Goals[]`.

### Поведение
- **Трекаются** в UI и snapshot (`AdditionalCompleted`).
- **Не влияют** на `AllGoalsComplete` / победу по целям (win только по non-additional).
- **Влияют на рейтинг** через `MinAdditionalForRating3/2`.

### UI
`LevelResultsController.BuildGoalProgressText`:
- Обычные: `[x]` / `[ ]`
- Additional: префикс **`[bonus]`** (в HUD и на results screen).

### Промпт
> «Бонусная цель — показывать в UI, не блокировать уровень, но учитывать в 3/3» → `IsAdditional` + исключить из `AllGoalsComplete` + MinAdditional в рейтинге.

---

## 8. Load Level UI

### Файл
`Assets/UI/LoadLevel.uxml` — кнопки `Level_1` … `Level_10`.

### Биндинг
`UIMainMenu`: prefix `Level_`, offset `_levelNumberToIndexOffset = 1` → `Level_3` = index 2.
Заблокированные уровни: `RefreshLevelButtons()` → `(Locked)`.

### Промпт
> «Добавить N кнопок уровней в Load Level» → UXML `Level_N`, код менять не нужно если naming convention соблюдён.

---

## 9. Pause / HUD мелочи

- **Restart в PauseMenu**: в UXML кнопка `Retry`, в `UIPauseMenu` fallback `FindButton(..., "Retry")`.
- **RageBar**: бинд в `Start`/on-show или `ScheduleGameHudRebind`, не полагаться на `Awake` UIDocument.
- **Goal progress в HUD**: `UIPauseMenu` подписка на `LevelManager.OnGoalProgress`.

---

## 10. Player progress

`PlayerProgress` (PlayerPrefs), `LevelSession._ignoreSaveForTesting` — разблокировка всех уровней в тесте.
New Game: `PlayerProgress.ResetCampaign`.

---

## 11. Откат AllParamsController

Пользователь **откатил** централизованный `AllParamsController` / CoreSettings UI. **Не возвращать** без явного запроса. Баланс: `LevelConfig`, `PipeObjectData`, SerializeField на компонентах, hardcoded в Resolver/Inspector — см. `AGENTS.md` §5.

---

## 12. Быстрые промпты-копипаста

```
Повторный выбор уровня не работает — rebind LoadLevel UIDocument на каждый show + ExecuteLater(0).
```

```
Очередь начинается с N-го слота, спавн всё равно с хвоста — QueueStartSlotIndex + PrefillQueueStart.
```

```
Утка влево, остальное вправо — ThrowSystem GetThrowDirection, Duck = -1.
```

```
Duck/Paint slide: 1-й hit = Weight клеток, 2+ = 1 клетка, симметрично, clamp у края, финал — ResolveInteraction.
```

```
Оценка: min(actions, rage, additional); fail = 0/3; пороги в LevelConfig asset.
```

```
Additional goal: IsAdditional, [bonus] в UI, не в AllGoalsComplete, MinAdditionalForRating в конфиге.
```

```
Инспектор завис — CancelInspection при ReturnToMenu/BindLevel, IsBusy блокирует TickPipeline.
```

```
Eject на slot 0 параллельно с тиком пайплайна — не держать IsPaused до DestroyAfterEject.
```

---

## 13. Ключевые файлы (этот диалог)

| Тема | Файлы |
|------|--------|
| Меню / Load Level | `UIMainMenu.cs`, `LoadLevel.uxml`, `LevelMenuController.cs` |
| Очередь / спавн | `LevelConfig.cs`, `PipelineSpawner.cs`, `LevelSession.cs` |
| Бросок | `ThrowSystem.cs` |
| Duck/Paint | `PipelineInteractionResolver.cs`, `ThrowSystem.cs`, `PaintData.asset`, `DuckData.asset` |
| Инспекция / eject | `InspectorController.cs`, `PipelineController.cs` |
| Оценка / цели | `LevelConfig.cs`, `LevelManager.cs`, `LevelGoalData.cs`, `GoalType.cs`, `UILevelResultsScreen.cs`, `ResultsScreen.uxml`, `LevelResultsController.cs` |
| HUD | `UIPauseMenu.cs`, `GameHUD.uxml` |

---

## 14. Что проверять после правок

1. `ReadLints` на изменённые `.cs`.
2. Play: меню → уровень → меню → тот же уровень снова.
3. Play: Duck/Paint slide у края пайплайна (не bounce на первом контакте).
4. Win/Fail: рейтинг 0/3 на fail; rage/actions/additional на success screen.
5. Additional goal complete не завершает уровень сам по себе, если required goals не выполнены.

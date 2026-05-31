# duck_test — архитектура и контекст проекта

Документ для продолжения работы в новом чате Cursor. Подключай через `@docs/ARCHITECTURE.md`.

## Проект и Git

- **Корень Unity-проекта:** `duck_test/` (не только `Assets/`).
- **Версия Unity:** `6000.4.1f1` (`ProjectSettings/ProjectVersion.txt`).
- **В Git:** `Assets/`, `ProjectSettings/`, `Packages/manifest.json`, все `.meta`.
- **Не в Git:** `Library/`, `Temp/`, `UserSettings/`, `*.csproj`, `*.sln` (регенерируются).
- **PlayerPrefs** (`PlayerProgress`) — локально на машине, не синхронизируется через Git.

---

## Геймплей: пайплайн

Конвейер из слотов (`PipeSlot`). Объекты (`PipeObject`) движутся к слоту 0 (инспектор).

| Компонент | Роль |
|-----------|------|
| `PipelineController` | Тик сдвига слотов, блокируется инспектором |
| `PipelineSpawner` | Спавн из очереди уровня (`SpawnEntry[]`) |
| `PipelineInteractionResolver` | Merge / shove / bounce при бросках |
| `ThrowSystem` | Клик по объекту пайплайна: бросок назад на `Weight` слотов |
| `PipeObjectData` | ScriptableObject: archetype, tags, weight, **BaseDuckiness**, visual sprite/prefab |

**Archetypes:** `Duck`, `Fake`, `Modifier`.

---

## Геймплей: утиность и инспектор

Игрок **не выбирает** Approve/Reject. Инспектор решает по **утиности** (0–3).

| Утиность | Источник |
|----------|----------|
| 3/3 | `Duck`, идеальный merge |
| 2/3, 1/3 | промежуточные merge-результаты (`BaseDuckiness` на `PipeObjectData`) |
| 0/3 | `Fake`, `Modifier`, сырой объект |

### Реакции инспектора (tier 0–1, гнев 0–2)

| Утиность | Исход | Шкала гнева |
|----------|--------|-------------|
| 3/3 | Approve | — |
| 2/3 | Approve | +1 |
| 1/3 | Reject | +2 |
| 0/3 | Reject | +3 |

**Reject** сам по себе не влияет на цели уровня (пока).

### Шкала гнева (0–6)

- **Tier 0** (0–2): базовая таблица выше.
- **Tier 1** (3–5): ускорение инспекции (`BaseInspectionDuration / multiplier`).
- **Tier 2** (6): ещё ускорение + **Approve только при 3/3**; остальное → Reject.
- При достижении **6** — **rage mode**, счётчик `RageModeCount++`, гнев сбрасывается в 0.

Класс `InspectorAngerState` — в конце `InspectorController.cs`.

### Блокировка пайплайна

Пока `InspectorController.IsBusy` — `PipelineController` не тикает. Объект со слота 0 уходит в инспекцию (~`BaseInspectionDuration` сек), после решения уничтожается, затем спавн + сдвиг.

---

## Система уровней

### Data (настраивается в Inspector, без кода)

| Asset / класс | Назначение |
|---------------|------------|
| `LevelDatabase` | Массив `LevelConfig` (кампания) |
| `LevelConfig` | Очередь спавна, цели, параметры инспектора, правила конца |
| `LevelGoalData` | Одна цель: тип, target, amount |
| `SpawnEntry` | `{ PipeObjectData Object }` — элемент очереди |

**Ассеты:** `Assets/Data/Levels/Campaign.asset`, `Level_01.asset`, `Level_02.asset`.

### GoalType

| Тип | Когда засчитывается |
|-----|---------------------|
| `ApproveObject` | Approve 3/3 + `PipeObjectData` совпал |
| `ApproveArchetype` | Approve 3/3 + archetype/tags |
| `MergePair` | Инспектор проверил объект, сложенный из `MergeInputA` + `MergeInputB` |
| `MergeToResult` | Инспектор проверил объект с `Data == MergeResult` |

**Все цели** засчитывает только инспектор (`InspectorController` → `LevelManager.ReportInspection`). Merge в пайплайне лишь создаёт объект; входы merge хранятся на `PipeObject.MergeInputA/B`.

### Завершение уровня

| Условие | Outcome |
|---------|---------|
| Все цели выполнены | Win (`GoalsComplete`) |
| Очередь пуста + пайплайн пуст + инспектор не busy | Win (`PipelineDrained`) |
| 3 rage mode | Fail (`RageModeLimit`) → повтор того же уровня |

После win — `LevelResultsScreen` (или 2 сек задержка + лог), затем следующий уровень.  
Прогресс: `PlayerProgress` → `PlayerPrefs` (при win, если не `_ignoreSaveForTesting`).

### Runtime (сцена)

GameObject **`LevelSystems`**:

- `LevelSession` — оркестратор
- `LevelManager` — прогресс целей
- `InspectorController` — инспектор + anger
- `GameStateManager` — `Playing`, `Inspecting`, `LevelWon`, `LevelLost`, `CampaignComplete`

`LevelSession._ignoreSaveForTesting = true` по умолчанию для отладки.

---

## Структура скриптов

```
Assets/Scripts/
  Level/
    GoalType.cs              — enums + LevelResultsSnapshot
    LevelGoalData.cs
    LevelEndRules.cs
    LevelConfig.cs
    LevelDatabase.cs
    LevelGoalRuntime.cs
    LevelManager.cs
    LevelSession.cs
    InspectorController.cs   — + InspectorAngerState, IPipelineControl
    LevelResultsScreen.cs
    Progress/PlayerProgress.cs
  Pipe/
    PipelineController.cs    — IPipelineControl, inspector gate on slot 0
    PipelineSpawner.cs         — BindQueue, SpawnNext
    PipelineInteractionResolver.cs
    PipeObject.cs              — Duckiness, MergeWith returns merged
    PipeObjectData.cs          — BaseDuckiness
    GameStateManager.cs
    ThrowSystem.cs
```

`PipeInspectorController` — obsolete wrapper над `InspectorController`.

---

## PipeObjectData и спавн

- У каждого spawnable объекта: **`PipeObjectData`** с gameplay-полями и visual sprite/prefab.
- `PipelineSpawner` инстанцирует общий `PipeObject` shell prefab и вызывает `Initialize(data)`.
- Merge фейка с `Paint` / `Kit` хранится в `PipeObjectState` и меняет visual-вариант фейка.

Доп. data-ассеты: `FakeDuckData`, `DuckificatorData`; обновлены `DuckData`, `BeakData`, `CrocData`.

---

## Workflow: новый уровень

1. Duplicate `Level_01.asset` → `Level_NN.asset`.
2. Заполнить **Spawn Queue** (массив `PipeObjectData`).
3. Добавить **Goals** в массив.
4. Добавить ссылку в `Campaign.asset` → Levels.
5. Commit **asset + .meta** в Git.

---

## Подводные камни (уже ловили)

1. **`.meta` GUID must be 32 hex chars.** Короткий GUID → Unity не импортирует скript → CS0246.  
   Не плодить отдельные `.cs` без проверки, что они в `Assembly-CSharp.csproj`.
2. **`InspectorAngerState` и `LevelResultsSnapshot`** перенесены в `InspectorController.cs` и `GoalType.cs` — не выносить обратно в отдельные файлы без reimport.
3. **Сцены и prefab-ссылки** — только через `.meta` GUID; без meta на другом ПК всё сломается.
4. **Один `.unity` файл** — не мёржить параллельно двум людям.

---

## Что ещё не сделано / TODO

- UI заставки результатов (`LevelResultsScreen` — опционально на Canvas).
- Визуал шкалы гнева (6 сегментов).
- Анимация инспектора (сейчас `WaitForSeconds`).
- `InitialOccupants` — стартовое заполнение слотов.
- Hub / LoadScene между уровнями (сейчас `BeginLevel` в той же сцене).
- Reject-цели, lose на wrong approve.

---

## Быстрый старт в новом чате

```
@docs/ARCHITECTURE.md
@.cursor/rules/duck-game.mdc

[твоя задача]
```

# Внешние сведения после freeze attempt1

Историческая заметка до нового прямого поручения пользователя.
После проверки addendum SHA44E346…590109C1 и seed-векторов M01 реализован;
текущий контракт — [GALAXY-GENERATION](../GALAXY-GENERATION.md),
результат review — [handoff](galaxy-generation-attempt1-handoff.md).
Описанная ниже прежняя остановка M01 gate более не является текущим статусом.
Hyperlane сведения по-прежнему не реализованы этим модулем.

13 октября 2026. Источник: сообщение исследовательского партнёра
`01a1359a-86d4-78f3-a4d2-5dda015220f6`, полученное после freeze.
Это внешние сведения для следующего контракта, **не изменение frozen реализации**,
не самостоятельное закрытие author'ом M01 и не одобрение интеграции.
Запрошены durable addendum path/hash и контрольные векторы нового seed boundary;
на момент записи они для этого нового checkpoint ещё не получены.
Ранее проверенные M02/M04 provenance остаются в
[индексе](galaxy-generation-evidence-index.md).

## M01: заявленное партнёром закрытие specified-seed boundary

Партнёр указывает native диапазон `0x14098D35F–0x14098D39A`.
Для явного `specifiedSeed p` в диапазоне `0..2^31−1`:

- counter = Mix32(0x5EA6BA9F − p × 0x4AD685B3), все операции uint32 wrap;
- seedBase = 0;
- Mix32 — арифметическая цепочка M02 после начального вычитания,
  без финального очищения sign bit;
- физический порядок локальной пары `{counter, seedBase}` отличается от имён
  параметров нашего `NativeRandomState(SeedBase, Counter)`;
- следующий draw использует seedBase − counter × 0x4AD685B3, затем counter++.

Auto-seed UI зависит от GlobalNextRandom. Предложенный будущий API обязан требовать
явный p; автоматический seed не входит в заявленное закрытие.
До отдельной синхронизации/review существующий M01 gate остаётся в коде.

## Hyperlane settings и частичные операторы

По сообщению партнёра:

- lobby `num_hyperlanes` — int64 fixed с масштабом 100000, settings+0x90;
- допустимый интервал — от scenario+0x138 до base+span;
- default находится в scenario+0x148;
- первый target удаления в `0x140885000`:
  R = trunc((N × 15000) / h) при h > 0, где N — число candidate edges;
- затем `0x140884C30` удаляет до R рёбер;
- второй threshold при h <= 100000 равен 4, иначе round-nearest(4 × h / 100000);
- max alternate-path distance равен 20.

Полный topology semantics партнёр ещё проверяет. Не квалифицированы здесь
tie-breaking округления, intermediate overflow, candidate graph construction,
порядок выбора/проверки рёбер и поведение connectivity/pruning.
Эти числа и формулы не дают основания заменить M13–M15 своим graph algorithm.
Никакие topology или settings operators из этого сообщения не реализованы.

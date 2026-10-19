# Процедурные туманности: первый срез доработки генерации

Прямой запрос пользователя 16 октября 2026: создать ветку от master и начать
доработку генерации. База `5e8f5af`, ветка `feature/galaxy-generation-content`.
Это контракт первого законченного этапа; полное покрытие галактики не заявляется.
Срез реализован и проверен; [результаты и ограничения](reports/galaxy-nebulae-validation-2026-10-16.md).

## Причина и границы

В базе весь ненулевой `GalaxySpecialContent` отвергается, туманностей в generated
snapshot нет. Восстановленный алгоритм позволяет независимо реализовать их
геометрию и source-derived имена до расширения interpreter специальных систем.
Источник: [конспект реверса](reports/galaxy-generation-rules-2026-10-16.md),
поздний addendum §18.5, SHA `2AD39F747DEE73C7128DC1D9FCA2D77A64CBDD70295DF4E048ED179142D4C5D3`.

Срез меняет только Core galaxy, harness metadata и целевые проверки. Session,
сетевой протокол, renderer, UI, ассеты и пользовательские процессы не меняются.
Нулевой count остаётся default существующих callers. Визуализация и игровые
эффекты туманности (сенсоры/движение и т.п.) не входят в этот этап.

## Входы и алгоритм

- Разрешить в `PrototypeOrdinaryV1` явно заданное `SpecialContent.Nebulae` от 0
  до `num_nebulas` выбранного scenario включительно. Отрицательные/превышенные
  значения отклоняются в resolve. Остальные special families остаются запрещены.
  Верхняя граница — объявленная граница этого API, не утверждение о native clamp.
- Радиус и minimum center separation читать из `nebula_size`, `nebula_min_dist`
  выбранного scenario; значения должны быть scalar, уникальными, неотрицательными,
  radius строго положителен при count>0, fixed precision≤5. Invalid source — отказ
  без публикации частичного мира и без молчаливых defaults.
- После получения окончательных координат, до выбора homes/initializers,
  обходить системы в generation order. На каждый candidate потратить local draw,
  выбрать индекс modulo числа систем. Центры сравнивать с ранее принятыми:
  distance² < minimum² означает reject, равенство разрешено.
- Budget на очередную туманность — 10000 отказов. После этих отказов потратить
  draw и проверить геометрию 10001-го кандидата, затем закончить всю фазу до
  принятия. Частичный результат геометрически допустим и имеет явный diagnostic.
- Присоединять ещё свободные системы при distance² < radius²; равенство исключено.
  Перекрытия не меняют уже назначенную принадлежность. Стабильные IDs следуют
  порядку создания, ссылки используют реальные system IDs.
- На принятую туманность отдельный names draw выбирает ключ по modulo pool count;
  удалить выбранный entry swap-with-last. Читать ordered `nebula_names` из
  `common/random_names/base/00_random_names.txt`; файл включить в fingerprint и
  проверку стабильности. Не сохранять оригинальные bytes/fixtures в Git.
  Пустой pool даёт пустое имя и явный diagnostic, не выдуманный ключ. В нашем
  supplied-seed профиле пустой pool не расходует names draw; native расход RNG
  именно на этой ошибочной ветви не заявляется доказанным.
- Для собственного детерминизма использовать `SeedBundle.Content` для локального
  потока размещения до других content draws и `SeedBundle.Names` для имён.
  Это явная политика supplied-seed, не имитация скрытого native global RNG.
  При count=0 не расходовать draws и не менять существующий content stream.

## Выход и совместимость

Generated snapshot содержит immutable туманности с center, radius, name key,
system membership, diagnostics и RNG ledger; metadata и world fingerprint
учитывают эти данные. Добавление поля документировать версией metadata.
Harness сообщает поддержанный count вместо общего обещания special support.
При недоступном/невалидном names source активная генерация отклоняется;
count=0 должен оставаться доступен synthetic catalogs без этого источника.
Source appearance/disappearance/change после импорта считается изменением snapshot.

## Приёмка

Независимые synthetic проверки: строгие/включённые границы, overlap ownership,
одна система, count=0 без draws, exhausted budget и partial result, выбор имён
без повторов и exhausted pool, immutability и некорректные входы/overflow.
Проверки genuine 4.5.2: source count/radius/separation, resolve gates, повтор
seed даёт одинаковые туманности и fingerprint, membership без dangling IDs,
новые данные входят в metadata, zero-count регрессия планет/стартов/топологии.
Build Core/harness и GalaxyGenerationChecks с доступным Triangle worker;
independent actual-diff review перед коммитом кода.

Владение этой веткой: текущий root — контракт/интеграция/проверки; назначенный
`nebula_implementation` — Core galaxy, harness и galaxy tests; независимый
reviewer только читает. Главные checkout, master и другие чаты не изменяются.

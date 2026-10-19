# Граница подключения generated world → M3

Это изолированная инструкция для будущей интеграции. Session/M3 здесь не изменяется.

1. Лобби получает source-derived catalog options; выбранный prototype preset отдельно
   показывает исключённые special content families. Resolve возвращает seed и
   fingerprints. Зафиксировать их в session start transaction; не разрешать клиенту
   подменять resolved request после проверки.
2. Host вызывает generation один раз до публикации session world. При failure
   staged entities отбрасываются; fixture16 не является fallback.
3. Системы/гиперлинии передаются с stable IDs. Galaxy fixed X/Y преобразуются в
   consumer X/Z с explicit scale. Body parent offsets суммируются рекурсивно;
   radius должен покрывать все тела плюс необходимый navigation exit margin.
   M3 EXIT_RADIUS140 — ограничение consumer, не vanilla radius formula.
4. Presentation mapping class→visualId/scale задаётся host. Неизвестный visual
   диагностируется отдельно; планетный класс не заменяется pc_continental ради
   существующей картинки. Moon может отображаться как M3 planet body, сохраняя
   moon class/parent/provenance в semantic snapshot.
5. Home allocation выбирает реально созданное body (в том числе nested moon).
   StartupOwnerContext получает именно его class/size. Старый
   M3StartupCatalog.Select(template,seed) повторно выбирает initializer и size:
   использовать его для generated world нельзя. Требуется lookup/generation plan
   по exact generated class/size/context через существующий StartupBuildingImporter.
6. Existing startup plan применяется existing planet/economy owner. Generated
   body является initial carrier; FinalPlanetSize после effects обновляет его
   consumer state. Сохранить initial и final sizes отдельно, как уже делает M3.
7. Factory staged SpaceWorld должна принимать готовые массивы через существующую
   validation, а не изменять private fields reflection. Сейчас публичной factory
   для arbitrary world нет; её добавление — точная consumer integration задача.
8. Colony/country/species/economy, actual pop/workforce и navigation fleets создаёт
   consumer transaction. Нынешний M3 требует ShipCount3: это prototype actor
   policy, не vanilla science/constructor startup. Generated profile обязан
   раскрыть такое отклонение и не сертифицировать полный game_start.9.
9. Каждый deferred content request получает explicit disposition. После successful
   consumer validation публикуется весь мир и одно session-start event. До этого
   статус producer ReadyForBootstrap, не Playable.

Ожидаемые будущие consumer changes ограничены startup factory, точным выбором
startup plan и session start route. Генератор не импортирует Session и не меняет
существующие экономические правила. Read-only startup-importer smoke проверяет
class/size seam и source anchors, но не заменяет launch/manual gameplay acceptance.

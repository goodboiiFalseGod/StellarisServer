# Локальный профиль базовых planet assets v8

## Контракт

Профиль `m3-static-452-v8` расширяет локальный M3 импортёр исключительно изображениями и их текстовыми источниками для ручной проверки меню планеты. `schemaVersion=1`, `importerVersion=7`, `materialProfileVersion=4`; набор визуальных моделей и материал-профиль остаются прежними. Старые `m3-static-452-v7` / importer 6 несовместимы и должны завершаться понятной ошибкой версии, а не загружаться как новый профиль.

Сервер C# и браузер строят одинаковый упорядоченный allowlist из 177 уникальных relative paths. Канонический UTF-8 digest — SHA-256 текста `profileId`, `gameVersion`, schema/importer/material versions, затем ordinal-sorted строк `path<TAB>byteLength<TAB>lowercaseSha256`, каждая с LF. Точный digest зависит от установленной локальной копии 4.5.2; его получают из одного реального импорта, не зашивают в исходники и сверяют между C# и JS.

Импорт допускает только существующие файлы из allowlist, версию игры `v4.5.2`, валидные DDS и текстовые определения. Недостающий/недоступный файл, malformed source/GFX, неоднозначная sprite-запись, неожиданная геометрия изображения, неизвестный путь, неверный digest/версия или превышение бюджетов означает отказ всего импорта без fallback и частичной публикации. Путь установки читается; оригинальные и производные байты остаются вне Git. Каждый файл ограничен 64 MiB, metadata — 1 MiB, совокупный decoded профиль — 512 MiB, планетная группа изображений — 8 MiB. Предварительные проверки DDS выполняются до декодирования.

## Источники и выбранный inventory

Источник — установленная Stellaris 4.5.2 (`E:/SteamLibrary/steamapps/common/Stellaris`), только чтение. Job definitions и startup buildings проверяются по оригинальным `common/pop_jobs/*.txt`, `common/buildings/*.txt` и GFX-регистрациям ниже. Точные стартап-ветви ordinary/hive/machine и ограничения их civic/origin-контекста сведены в [reports/startup-building-branches-452.md](reports/startup-building-branches-452.md); отчёт служит указателем на оригинал, не заменяет чтение источников.

Базовые jobs, которые добавляются в профиль:

| ID в определении | Definition file | Отображаемый файл |
|---|---|---|
| `technician`, `miner`, `farmer` | `common/pop_jobs/03_worker_jobs.txt` | `job_technician.dds`, `job_miner.dds`, `job_farmer.dds` |
| `physicist`, `biologist`, `engineer`, `bureaucrat`, `foundry`, `artisan`, `enforcer` | `common/pop_jobs/02_specialist_jobs.txt` | соответствующие `job_<icon>.dds` |
| `politician` | `common/pop_jobs/01_ruler_jobs.txt` | `job_politician.dds` |
| `replicator`, `coordinator`, `patrol_drone`, `spawning_drone`, `synapse_drone`, `logistics_drone` | `common/pop_jobs/04_gestalt_jobs.txt` | соответствующие `job_<icon>.dds`; для `logistics_drone` source default icon `low_tech_bureaucrat`, значит `job_low_tech_bureaucrat.dds` |

`job_farmer.dds` и `job_agri_drone.dds` уже входят в профиль и сохраняются с явными local adapter keys, поскольку исходные GFX-регистрации для них не найдены. `job_logistics_drone.dds` в источнике отсутствует и не подменяется выдуманным файлом. Этот список нужен для иконок; профиль не импортирует job outputs/upkeep или правила экономики.

Недостающие иконки выбранных стартовых зданий:

| Источник / ветвь | Definition ID | Локальная текстура |
|---|---|---|
| `common/buildings/07_amenity_buildings.txt`, ordinary pacifist | `building_luxury_residence` | `gfx/interface/icons/buildings/building_luxury_residence.dds` |
| `common/buildings/08_unity_buildings.txt`, ordinary spiritualist | `building_temple` | `gfx/interface/icons/buildings/building_temple.dds` |
| `common/buildings/00_capital_buildings.txt`, hive | `building_hive_capital` | `gfx/interface/icons/buildings/building_hive_capital.dds` |
| `common/buildings/01_pop_assembly_buildings.txt`, hive | `building_spawning_pool` | `gfx/interface/icons/buildings/building_spawning_pool.dds` |
| `common/buildings/01_pop_assembly_buildings.txt`, hive progenitor branch | `building_offspring_nest` | `gfx/interface/icons/buildings/building_offspring_nest.dds` |
| `common/buildings/07_amenity_buildings.txt`, hive | `building_hive_warren` | `gfx/interface/icons/buildings/building_hive_warren.dds` |
| `common/buildings/00_capital_buildings.txt`, machine | `building_machine_capital` | `gfx/interface/icons/buildings/building_machine_capital.dds` |
| `common/buildings/01_pop_assembly_buildings.txt`, machine | `building_machine_assembly_plant` | definition icon `building_robot_assembly_plant` → `gfx/interface/icons/buildings/building_robot_assembly_plant.dds` |
| `common/buildings/07_amenity_buildings.txt`, machine | `building_drone_storage` | `gfx/interface/icons/buildings/building_drone_storage.dds` |

Обычные capital/factory и шесть остальных обычных ethic-start buildings уже имеют mappings в профиле: `building_capital`, `building_factory_1`, `building_precinct_house`, `building_holo_theatres`, `building_research_lab_1`, `building_stronghold`, `building_commercial_zone`, `building_medical_1`. Другие текущие maps, districts, zones, deposits, planet/model/UI изображения сохраняются.

## GFX и локальный adapter

Там, где оригинал регистрирует job sprite, профиль сверяет саму запись `spriteType`, уникальный `textureFile`, один кадр и точный путь:

- `interface/planet_view.gfx`: `GFX_job_miner`, `GFX_job_foundry`, `GFX_job_bureaucrat`, `GFX_job_politician`, `GFX_job_enforcer`;
- `interface/icons.gfx`: `GFX_job_physicist`, `GFX_job_biologist`, `GFX_job_engineer`.

Для jobs без такой GFX-регистрации используется явный browser adapter на существующую локальную текстуру и source ID/icon, указанные выше. Это локальная связь для данного клиента, а не заявление, что оригинальная игра регистрирует такой `GFX_*` sprite. Building images также используют существующую локальную ID→DDS схему; профиль не выдаёт её за GFX-регистрацию. Если определение задаёт явный `icon`, mapping обязан следовать ему: machine starter `building_machine_assembly_plant` использует текстуру `building_robot_assembly_plant.dds`, не texture, вычисленную по definition ID.

Новые 16 DDS jobs имеют размер 30×30; 15 используют `BGRA8` и 1 (`job_miner.dds`) — `DXT5`; один mip, одна face. Девять новых building DDS имеют 78×78 `BGRA8`, один mip, одна face. Добавка decoded RGBA составляет 276,624 bytes (16×3,600 + 9×24,336); она включается в 8 MiB planet budget. Длины исходных файлов и hashes всегда читаются у локального пользователя и входят в manifest digest.

## Поддержка и явные исключения

Покрытие ограничено базовыми ordinary jobs, выбранными прямыми basic authority/capital jobs выше, базовыми ordinary ethic-building icons и startup building sets ordinary/hive/machine. Условные job swaps расширенных civic/origin/planet ветвей не расширяются автоматически. Civic-only/replacement buildings (например, `building_communal_housing_large`, у которого definition задаёт `icon = building_paradise_dome`; civic death-cult, reanimated-armies, storm-devotion, servitor и другие контекстные ветви) пока исключены. Nomadic, wilderness, специальные origins/planet classes, mobile/ark jobs и остальные scripted/dynamic jobs тоже остаются вне этого asset slice. Неизвестная или отсутствующая картинка не получает alias.

Проверка должна сверить allowlist/count/digest в C# и JS, успешно прочитать установленный 4.5.2 profile, отклонить отсутствующие/повреждённые/неподдерживаемые inputs, подтвердить размеры/codecs/decoded totals, проверить immutable definitions и безопасные локальные буферы/worker import. Сервер не получает оригинальные texture bytes; его manifest содержит версии и per-file length/hash, не отдельную таблицу building/job mapping.

## Delta закрытия ресурсов и jobs — профиль v9

Этот раздел дополняет исторический checkpoint v8; исходный inventory и его измерения выше сохранены. Профиль меняется синхронно до `m3-static-452-v9`, `schemaVersion=1`, `importerVersion=8`, `materialProfileVersion=4`. Импорт v8/importer7 несовместим: C# и JS должны явно отказать на старой версии. Остальные визуальные модели, source-safe local import, per-file 64 MiB, metadata 1 MiB, общий decoded budget 512 MiB и digest protocol не меняются.

### Проверенные source bindings

Добавляются четыре playable ordinary job image keys. В job definitions проверяются настоящие ID и `category`; отдельная параллельная ветка `merchant` в список не входит.

| Image key | Source job / stratum | Проверка иконки | Local DDS |
|---|---|---|---|
| `job.clerk` | `common/pop_jobs/03_worker_jobs.txt`, `clerk`, `worker` | `common/game_concepts/01_job_concepts.txt`, `concept_clerk`, alias `concept_clerks`, exact icon path | `gfx/interface/icons/jobs/job_clerk.dds` |
| `job.entertainer` | `common/pop_jobs/02_specialist_jobs.txt`, `entertainer`, `specialist` | `concept_entertainer`, exact icon path | `gfx/interface/icons/jobs/job_entertainer.dds` |
| `job.healthcare` | `common/pop_jobs/02_specialist_jobs.txt`, `healthcare`, `specialist` | `concept_healthcare`, exact icon path | `gfx/interface/icons/jobs/job_healthcare.dds` |
| `job.soldier` | `common/pop_jobs/02_specialist_jobs.txt`, `soldier`, `specialist` | `concept_soldier`, alias `concept_soldiers`, exact icon path | `gfx/interface/icons/jobs/job_soldier.dds` |

`merchant` остаётся отдельной явной omission: в оригинале `common/pop_jobs/01_ruler_jobs.txt` его `possible = { always = no }`; это metadata non-active job, а не добавленный ordinary playable job. `concept_merchant` в проверенном job-concept file отсутствует. Поэтому для него не добавляется ни выведенный по шаблону путь, ни alias. Все четыре included DDS — 30×30 `BGRA8`, один mip/face; по 3,600 decoded bytes.

Для ресурсов добавляются стабильные ключи `resource.<id>`. Source identity проверяется в `common/strategic_resources/00_strategic_resources.txt`, затем через конкретную `spriteType` из `interface/resources.gfx` до её точного texture path:

| Image key | GFX sprite | Local DDS | Размер / decoded RGBA |
|---|---|---|---|
| `resource.alloys` | `GFX_resource_alloys` | `gfx/interface/icons/resources/alloys.dds` | 20×20 / 1,600 B |
| `resource.consumer_goods` | `GFX_resource_consumer_goods` | `gfx/interface/icons/resources/consumer_goods.dds` | 19×19 / 1,444 B |
| `resource.unity` | `GFX_resource_unity` | `gfx/interface/icons/resources/unity.dds` | 18×18 / 1,296 B |
| `resource.physics_research` | `GFX_resource_physics` | `gfx/interface/icons/resources/physics_research.dds` | 18×18 / 1,296 B |
| `resource.society_research` | `GFX_resource_society` | `gfx/interface/icons/resources/society_research.dds` | 18×18 / 1,296 B |
| `resource.engineering_research` | `GFX_resource_engineering` | `gfx/interface/icons/resources/engineering_research.dds` | 18×18 / 1,296 B |
| `resource.trade` | `GFX_resource_trade` | `gfx/interface/icons/resources/trade.dds` | 20×20 / 1,600 B |

Для унифицированного вызова `appendPlanetImage` существующие `energy`, `minerals`, `food` также доступны как `resource.energy`, `resource.minerals`, `resource.food`; это aliases на прежние profile files и verified `GFX_resource_energy/minerals/food`, без новых texture paths и без второго decode. Их существующие `M3AssetManifestBuilder` иконки остаются на местах. Новые seven resource DDS — `BGRA8`, один mip/face; четыре новых job textures имеют тоже один mip/face. Новая decoded source closure даёт +24,228 B относительно предыдущего full profile (14,400 B jobs + 9,828 B новые resources); три базовых aliases не увеличивают полный decode, потому что используют уже загруженные изображения.

Новые required source texts — `common/game_concepts/01_job_concepts.txt` и `common/strategic_resources/00_strategic_resources.txt`; `interface/resources.gfx` уже была в M3 allowlist. Реальная добавка к M3 allowlist — 13 уникальных profile paths: 2 source texts, 4 job DDS, 7 resource DDS; base energy/minerals/food DDS и resources.gfx уже существовали в профиле. После C#/JS parity общий allowlist — 190 paths, `PLANET_ASSET_PATHS` — 132, image keys — 95 (92 distinct image source paths). Read-only import установленной копии дал raw aggregate 130,810,753 bytes и digest `0bbe8bf2c976214141006490a86f60c25146f1d73041a90701a523a927b11236`. Planet image descriptors занимают 1,511,060 decoded bytes, 1,461,908 unique; общий M3 decoded profile — 339,008,872 bytes. Планетный budget остаётся 8 MiB, общий — 512 MiB. Отсутствующие concept/resource/GFX bindings, невалидный или повторный source block, изменённые stratum либо sprite path/identity, недоступный файл, устаревшая версия или превышение budget отклоняют весь локальный импорт; подстановка пути по имени файла запрещена. Новые production client bindings в этом content-asset профиле не реализуются.

# GENERATED-WORLD-VISUALS-001 — evidence

14.10.2026, attempt1, author generated-world-visuals. Base `25d3cb6`, branch
`feature/generated-world-visuals`. Unity не используется. Изменения не закоммичены;
Независимое ревью repairs и actual browser GPU QA выполнены ниже. Модель/effort не менялись;
usage unavailable. [Контракт](../GENERATED-WORLD-VISUALS.md).

## Source-backed кандидат

C# source graph resolver проверил 31 ordinary/home class, native binary material
metadata, exact/01 entities, gfx, textures и supported attachments. Source variant
policy и approximations раскрыты в контракте; unknown не заменяется generic body.
Manifest — 479 unique paths; C#/JS deep equality проверяет также system lighting map.

```text
requirements fingerprint: 72fa3c0a586525526074c36616220dc2930cfc1b83507e07e587f6eff9b92cb4
manifest digest: 06ac10c9436a1758cc404d81cf6ef544f381a5536559bc10902cdfceddb5947d
```

Core checks: 53 PASS, Release build 0 warnings/errors. Это доказательство source
graph/API, не actual gameplay integration. Старый v10 wire проверен отдельно.

JS targeted review repairs: 8 PASS (material branch + 7 real integration cases).
Проверены canonical C#/JS parity, 31 actual native
templates/geometry arrays, binary/triple source stars, moon class/parent origin,
source sc lighting, mismatch/unknown lighting gates, old+staged rollback,
arbitrary server DDS path не открывается, changed hash отклоняется, production
worker full read/decode/recheck и source buffer release. CPU tests подменяют только
GPU staging method; они не объявлены WebGL PASS.

Actual producer export index SHA256:
`B342CADB7C8E2F7C149C35FF574E2CB516E33780015DE0121C0F5F1E481A3B23`.
Проверены каждый world SHA, все class IDs, semantic kinds, parents, source templates
и system lighting keys. 8 cases: seeds1/2 × elliptical/spiral_2/ring/bar,
tiny200/density1/2homes. Всего 18124 тела; union29 классов. Скрытая 16-body fixture
не используется. Per-case native templates действительно проходят Three scene
construction; это пока CPU wiring, не осмотр GPU изображений.

## Память

SourceMip1024V1 отвергнут до publication: для 30 classes decoded385.640MiB +
converted161.063MiB =546.703MiB. Лимит не повышался. RC-координатор согласовал
SourceMip512V1 для native 2D world textures, noise cube1024 сохранён.

Полный 31-class generated profile:

| Учёт | Bytes | MiB, примерно |
|---|---:|---:|
| Decoded payload/geometry conservative | 253107608 | 241.382 |
| Converted normal/spec arrays | 87097984 | 83.063 |
| Combined decoded+converted | 340205592 | 324.445 |
| GPU RGBA/mips/native buffers reservation после material repair | 299701832 | 285.818 |
| Raw encoded sources, production worker | 357702633 | 341.132 |
| Live source+decoded staging peak prediction | 442527613 | 422.027 |

Production worker проверяет peak до allocations, удаляет decoded DDS source
buffers из staging Map и повторно читает источник для stability check. Уже
удерживаемый полный профиль приводит к early encoded/combined budget rejection
перед чтением нового набора; старый profile/view сохранён. Это live buffer/storage
reservation, не измерение RSS/driver VRAM. Actual GPU staging проверен ниже.

В восьми actual worlds retained decoded payload составляет 237457572..243749028B,
converted77660800..81855104B, GPU reservation277359116..286552332B. Полный 31-class
профиль остаётся консервативным upper bound по поддержанному pool.

## Проверки и остаток

Focused legacy renderer/profile/manifest/worker/reader: 100/100 PASS с real source.
Asset checks: 10 наборов, 0 ошибок, 1 SKIP (создание symlink недоступно;
Windows junction guards проверены). Release server после targeted repairs: 0/0.
`git diff --check` PASS. Material repair отделяет Emissive normal.B mask от
Gas/Molten frozen flow/spec. Memory repair проверяет source+4×mesh ДО parse/read;
boundary-case подтверждает 0 открытых source paths и 0 mesh Float32 allocations.
Existing old profile/view preservation checks также PASS. T-star intermediate
review finding снят: native T использует PlanetEmissive, не lava-star branch.
Лишний T switch mapping не добавлен; source-specific regression сохранён.
Логи и технические metadata хранятся локально в ignored
`artifacts/generated-world-visuals/`; оригинальные assets/derived pixels/screenshots
в Git не помещаются. Read-only node_modules junction использует существующие
зависимости, install не выполнялся. Установка, stable/server5192 и UI preview5196
не менялись. Собственный static QA использовал только порт5199 и код/metadata/vendor.

## Actual browser GPU QA — 14 октября, fresh owner

Independent `visual_repairs_review` принял targeted repairs freeze8af8001d,
затем File snapshots repair freeze210eec6e. Первый настоящий browser import
выявил ложный distant-stars DDS alias: basename lookup считал существующим иной
relative path. Worker теперь связывает File только с точным manifest path;
DirectoryHandle, unique-basename, closure и двойная hash-проверка сохранены.
Real snapshots regression: 479 источников, 1 PASS, 0 SKIP.

IAB/WebGL2 получил оригиналы только через локальный FileChooser. Production worker
проверил 479 sources, release/recheck; настоящий `stageGeneratedGpu` выполнил
`initTexture` и offscreen render всех native templates до publication: 11 программ,
187 draw calls, 269417 triangles, 129 geometries, 112 textures; shader/context
errors отсутствовали. Все 31 класса затем отрисованы отдельно, `glError=0`,
native sourceClassId/vertex counts сохранены. Осмотр крупного contact sheet
подтвердил поверхности/attachments, включая neutron/pulsar; native visual parity
не заявляется. Non-emissive поверхности выглядят тёмными при раскрытой source-light
calibration approximation; эстетическая пользовательская приёмка остаётся впереди.

Все восемь actual worlds: SHA-256 проверен, 1600 систем/18124 тела прошли binding
validation; по одной настоящей системе каждого мира опубликовано и отрисовано,
включая triple-star cases, `glError=0`. Это не отрисовка всех систем каждого мира.
Повторный full-profile staging отказал по CPU old+staged >512MiB, сохранив identity
старых profile/resources/view. Driver VRAM и RSS неизвестны; приведённые bytes —
buffer/storage reservation и peak prediction, не фактическая VRAM.

Legacy fresh context: 373 File snapshots, старый manifest/importer8/material4,
четыре reference объекта, 24 draw calls, `glError=0`; console warn/error пусты.
Первоначальный QA dispose→new renderer на том же canvas после generated дал
`1282`; fresh-context legacy прошёл. Последующая проверка production clear-login
и pagehide/pageshow показала тот же lifecycle. Generated→dispose→generated
на exact same canvas/context воспроизвёл отказ GPU staging. Диагностические
checkpoints локализовали ошибку: before/after dispose0, after constructor1282,
after stage/draw0 (только в диагностике с чтением ошибки constructor).
Следовательно ошибка не вызвана later stage и не исключается как особенность стенда.

Узкий repair: `M3WorldRenderer.dispose()` вызывает штатный Three `resetState()`
до освобождения ресурсов. Это возвращает pixel-store/GL bindings к defaults;
production не читает/не очищает error flag для обхода проверки. Three `dispose()`
сам этого reset не выполняет, новый WebGLRenderer переиспользует контекст canvas.
Renderer regressions: 16 PASS. Diagnostic actual browser same-canvas/context
generated reimport после repair: все checkpoints0, source class/native draw0.
Strict actual browser повтор без промежуточного `getError()` перед stage также
PASS: sameCanvas/sameGl=true, after-stage0, continental native draw0; console пустая.
Прежний generated→dispose→legacy same-canvas сценарий после repair тоже glError0.
Repair требует независимого actual-diff review перед коммитом/интеграцией.

Локальные JSON evidence: `artifacts/generated-world-visuals/qa/`
`stage-result.json`, `classes-result.json`, `worlds-result.json`,
`rollback-result.json`, `legacy-result.json` (эксперимент с1282),
`legacy-fresh-result.json`, `console-result.json`. Harness тоже ignored.
Lifecycle evidence: `lifecycle-before-result.json`, `lifecycle-before-checkpoints.json`,
`lifecycle-after-checkpoints.json`, `lifecycle-after-strict.json`;
`lifecycle-after-legacy.json`, `lifecycle-console.json`;
renderer log `../lifecycle-renderer-checks.txt`.
Снимки — только ignored `content-local/references/generated-world-visuals/`.
[Contact sheet 31 classes](../../content-local/references/generated-world-visuals/native31-contact-sheet.png):
SHA256 `A04FCA6BF2C3C58E7FE7DE0E02B63A4FAAE785D46A0A53DAC7BC9A8E7690381F`.

Остаются Session/lobby/UI hooks у соответствующих owners, общий experimental RC QA,
пользовательская ручная приёмка и отдельное разрешение stable/main. Этот срез
подтверждает actual GPU candidate, не готовность всего прототипа.

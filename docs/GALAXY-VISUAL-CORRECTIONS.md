# Исправление отображения галактики VISUAL-01

16 октября 2026, прямое сообщение пользователя и два снимка экрана. База85ee2df,
feature/galaxy-generation-content. Один code writer, independent review, без
браузерных тестов. Этот контракт заменяет визуальную калибровку SIZES-01 там,
где она противоречит перечисленным исправлениям; metadata размеров не меняется.

## Причины и поведение

- Масштаб: разные baseline для stars/planets теряют соотношение source
  entity_scale20/11; отдельная screenshot calibration12/7 дополнительно меняет
  его. Для generated тел нужен единый множитель source model units и единый
  source class baseline pc_continental, включая stars. Сохранить entity/gfx scale
  в template и source class entity_scale в BodyDefinition.Scale ровно по одному
  разу. Существующий size multiplier clamp(size/20,.1,5) остаётся явной local policy,
  не доказанной native формулой. Калибровать общую unit conversion от continental
  surface radius7 при size20, не нормализовать каждый class по своему bbox.
  Stations/fleets/legacy fixture сохраняют свои масштабы. Surface bounds исключают
  FX, belts не раздуваются новой нормализацией; rings следуют тому же body factor.
- Атмосфера: нынешний rim shader максимален на внешней границе mesh и даёт
  резкий полупрозрачный шар. Source pdxmesh.shader:921–968/2703/1675/1711 задаёт
  back-face culling, additive SRC_ALPHA/ONE с RGB-only write, no depth write
  и затухание RGB через pow(NdotL*1.2,4), где
  NdotL=saturate(dot(normal,-toCamera)); на silhouette RGB0. Воспроизвести эту
  bounded outer-shell форму с source tint и depth occlusion, без выдуманной
  непрозрачной оболочки. Native light/HDR conversion остаётся explicit approximation.
  Three BackSide при текущей ориентации decoded mesh — явная локальная facing
  convention, не буквальное совпадение с native rasterizer state.
  Не удалять atmosphere meshes и не выдавать отсутствие свечения за совпадение.
- Фон: вместо одного G-star sky_core выбирать source core<.5/mid<.75/rim для
  actual system worldgfx class. Все10 активных stellar world profiles имеют этот
  grammar и разные HSV shifts. Передавать один optional GalaxyRadius в spatial DTO,
  из resolved source scenario; клиент считает для уже разрешённой системы
  clamp(hypot(galaxyPosition.x,z)/GalaxyRadius,0,1). Не повторять percent/float field
  у каждой системы: выявленный huge wire-budget рост требует убрать дублирование.
  ScenarioRadiusDistanceToCorePercentV1 — собственная policy: native metric
  не доказан. Не вычислять радиус по видимому игроку подмножеству систем.
  Импортировать три локальных sky DDS, проверять ordered triggers/ycocg/HSV;
  неизвестная active profile shape — typed failure. Не-neutral LUT f/m не заменять
  незаметно: объявить HSV-only/static LUT omission в approximation metadata.
  SourceMip512V1 для трёх sky cubemaps ограничивает их суммарную память: budgets
  не повышать, full source paths/hashes и C#/JS manifest должны совпасть.
- Галактическая карта: составить лёгкий локальный glyph из1/2/3 звёзд только по
  разрешённым star bodies, с source class tint. Чёрная дыра имеет тёмный центр и
  кольцевой силуэт, neutron/pulsar — отличимый компактный glyph. Не рисовать всем
  одну сферу system-light color. Новые source map assets не требуются: это явная
  GalaxyStellarGlyphV1, не native icon parity. Общий selection/picking/highlight
  остаётся на system ID; все части glyph выбирают ту же систему. Не раскрывать
  planets/hidden systems, не менять lanes, navigation и generated RNG.

## Приёмка и ограничения

Один focused headless Node процесс: реальные source class meshes→profile→template
и display scale (star/planet/gas/moon/asteroid, rings без двойного масштаба), shader
parameters/numeric falloff/occlusion flags, source background selection .5/.75 и
stellar HSV, glyph1/2/3/BH, общий pick ID и disposal. Проверять actual renderer
integration через headless Three groups/materials, не только отдельные helpers.
Только затронутые manifest/profile assertions; одна настоящая source closure/memory
проверка после правок. Один focused backend projection fixture для source radius,
permitted systems и существующих настроек≥1/nebula/guaranteed; один Server Release.
Тесты не заменяют independent actual-diff review. Повторять лишь затронутое.
Без браузера/WebGL и нового huge stress без причины. После реально выявленного
wire-budget превышения разрешён один целевой повтор huge/max-density projection
с исправленной передачей одного radius, без повторения остальных размеров/сценариев.
Фактический вид новых shaders оценивает
пользователь после передачи обновлённого сервера, результат не объявлять проверенным.
Исходные игровые файлы read-only и вне Git; снимки хранить только локально ignored.

## VISUAL-02 — повторная ручная проверка 16 октября

Базаe84bbd6. Новые пользовательские снимки binary-scale.png и
compact-binary-scale.png в той же ignored папке показывают слишком большие тела
относительно орбит и сближение компонентов кратных систем. Предыдущая приёмка
не подтверждала фактический вид; radius7 при size20 — наша калибровка, не source.
Проверить её вместе с source orbit_distance и вложенными parent coordinates;
не исправлять проблему только увеличением камеры или скрытием пересечений.
Диагностика: source center coordinates не искажены; в сохранённом world snapshot
газовый гигант30 + molten moon12 имеют source center offset13 и текущую оценку
суммы радиусов17.56 (43 пересечения из530 parent-child pairs). Общая калибровка
generated continental size20 меняется с7 на3.5 local units: половинный линейный
масштаб сохраняет соотношения классов и даёт запас для тесных source moon/binary
орбит. Это GeneratedMapUnitSurfaceV1, не native formula. Legacy fixture R7/R12
не меняется; source size/positions/RNG/navigation не меняются. Rings следуют телу,
декоративные belt pieces сохраняют собственный явный радиус. Не вводить адаптивный
масштаб по видимому подмножеству и не передвигать тела ради вида. Проверить actual
source mesh bounds и все пары сохранённого snapshot, отдельно классифицируя
совпадающие source centers и ошибки объёмной оценки; не скрывать остаточные случаи.

Цвет карты должен исходить из уже импортированных HSV конкретной звезды
(sourceAtmosphereColor либо starSurface.atmosphereColor), а не цвета освещения
системы: source B system light почти серый, giant world class не совпадает с
worldgfx tag. HSV value>1 ограничивается1 только для локального map glyph.
Legacy fallback сохраняется. Постоянное бело-жёлтое свечение поверх glyph
устранить: согласованный цвет компонента, слой позади читаемого центра.
Чёрная дыра сохраняет тёмный центр/кольцо, neutron и pulsar различаются формой,
кратные системы — количеством компонентов. Это local map policy, не native icons.
Данные — только разрешённые bodies; picking/disposal остаются общими с системой.
На galaxy overview видимый размер glyph должен сохранять читаемую форму: одни
фиксированные world-unit dots становятся субпиксельными. Допустим локальный minimum
CSS footprint для дочернего glyph по camera depth, без изменения позиции системы,
подписей, navigation и размеров system-view тел; проверить camera projection headless.

Приёмка: один focused headless renderer процесс с actual buildView, source colors,
1/2/3/BH/neutron/pulsar и сохранением legacy; размеры и расстояния проверять
на воспроизводимых source fixtures, не только одинаковости множителей.
Никаких браузерных запусков и broad/huge reruns. Independent final diff review
обязателен; реальный вид снова оценивает пользователь на обновлённом сервере.

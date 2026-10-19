# M3 — результат технической проверки ассетов

12 октября 2026. План зафиксирован до кода коммитом `b84bb1f`. Реализация — отдельный локальный `tools/asset-probe`, не изменение M2. [Запуск и повтор](../tools/asset-probe/README.md).

## Вывод и выбор

Для M3 выбран **Three.js 0.186.1, WebGL2, DOM UI**. Оба кандидата пригодны для статической модели, собственного материала, камеры, picking и instancing. Three предоставляет достаточный графический слой для нашего серверного состояния/ECS и DOM без дополнительной игровой архитектуры. Импортер — независимый модуль, не часть API renderer. Babylon.js 9.30.0 не включать в production-клиент вместе с Three.

Это выбор по достаточности и интеграции, не доказательство превосходства производительности. [Three BufferGeometry](https://threejs.org/docs/pages/BufferGeometry.html), [ShaderMaterial](https://threejs.org/docs/pages/ShaderMaterial.html), [Babylon custom meshes](https://doc.babylonjs.com/features/featuresDeepDive/mesh/creation/custom/) предоставляют нужные расширения. Лицензии установленных пакетов: Three MIT, Babylon Apache-2.0. Нативного reader Paradox у кандидатов не использовано; наши PDX/DDS readers нужны в обоих случаях. Зафиксированные зависимости и lockfile входят только в каталог стенда.

## Среда и результат A01–A07

Windows, Node 24.19.0, Stellaris 4.5.2; Ryzen 7 5700X, 32 ГБ RAM, RTX 4070. Автоматизация выполнена во встроенном Chromium (UA Chrome 155), **не в отдельном Google Chrome профиле пользователя**. WebGL2 через ANGLE/D3D11; S3TC/RGTC/BPTC доступны. Работа на Ryzen 5 3600/16 ГБ и минимальном GPU не проверена.

| Проверка | Фактический результат |
|---|---|
| A01 | Прочитаны 13 реальных mesh/DDS ship/planet/star/Arkship, исследованы gfx/asset и shader-связи. Корвет без skin; остальные три модели содержат skin/skeleton |
| A02 | Реальный File chooser предоставил пять файлов браузеру; upload отсутствует. Неполный повторный набор → видимая ошибка, прежние сцены и picking сохранены. Доступность folder API обнаружена, системный grant/revoke/отмена не проверены |
| A03 | 11/11 тестов PASS, без SKIP: bounded parsing, неверные tokens/types/размеры/NaN/индексы, DDS codec/mips/alpha, Collision, material channels, реальный контент |
| A04 | Three 0.186.1 и Babylon 9.30.0 на одной геометрии/RGBA, одинаковых камерах, DPR=1, antialias=true, preserveDrawingBuffer=false. Оба фактически WebGL2; ресурсы загружаются с loopback, не CDN |
| A05 | Реальный корвет отображён в обоих canvas; клик → fleet:0; reset камер, resize 800×900 и 1280×900, очистка/пересоздание проверены. Orbit/zoom реализованы штатными controls, отдельный жестовый ручной прогон не выполнен. Console warning/error: 0 |
| A06 | Один и 200 экземпляров, 30 warmup + 120 измеряемых кадров на кандидат, по очереди. Результаты ниже |
| A07 | Синтаксис app/adapters/server PASS; независимое ревью выполнено, замечания исправлены и тесты повторены. Общий код M1/M2 не менялся; .NET-регрессии не запускались |

10 циклов загрузка/render/clear: счётчики живых geometry/textures после каждого clear = 0 у обоих; Babylon meshes = 0. Это не доказательство отсутствия всех GPU/driver allocations. GPU allocation failure отдельно не инъецировался; staged resource ownership/rollback проверены ревью.

## Наблюдение нагрузки

Canvas 746×547, DPR=1. 200 корветов = 1 328 800 треугольников; у каждого кандидата одна geometry, три textures, один draw call в измерении.

| Сцена | Three frame p50/p95, мс | Babylon frame p50/p95, мс | Three CPU render p50/p95 | Babylon CPU render p50/p95 |
|---|---|---|---|---|
| 1 корабль | 6,1 / 6,1 | 6,1 / 6,2 | 0,1 / 0,2 | 0,2 / 0,3 |
| 200 экземпляров | 6,1 / 6,2 | 6,1 / 6,1 | 0,1 / 0,2 | 0,5 / 0,8 |

rAF ограничен vsync (~165 Гц); CPU render не GPU timer. Освещение Phong/Standard различается, измерен один короткий прогон, а не партия. Не переносить цифры на всю галактику, браузер пользователя или сервер. Babylon в стенде загружается целым UMD, Three через ESM; размер таких поставок не является честным сравнением production bundles с tree-shaking.

## Форматы и фактически воспроизведённый материал

PDX: magic `@@b@`, `pdxasset` 1.0; дерево глубины, свойства i/f/s, positions/normals/tangents/uv/indices/material. Корвет: 7 156 vertices, 6 644 triangles; Collision скрыта. `_mammalian_01_ships_meshes.gfx` переопределяет embedded PdxMeshTerra на PdxMeshShip. Reader не является универсальным импортом всех chunks/версий.

DDS: DXT1/3/5 декодируются на CPU; загружается mip0, mipmaps строит GPU. Реальные: корвет DXT5 1024²/11 mip; continental diffuse DXT1 2048×1024/12 mip, normals/spec DXT5; Arkship DXT5 2048²/12 mip. DX10/BC5/BC7/cubemap/volume сейчас явно отвергаются, не заменяются пустой текстурой.

По `gfx/FX/standardfuncsgfx.fxh:542–548` normal RRxG: X из G, Y из инвертированной A, Z восстановлен. По `gfx/FX/pdxmesh_ship.fxh:190–207`: specular G²×0,4. Эти преобразования реализованы и протестированы. Diffuse RGB используется; gloss/metalness/emissive/team-color, оригинальный light/shader, particles/locators/анимация не воспроизведены. Различие с оригиналом явно показано в UI, не принято как окончательное качество M3.

## Контрольные SHA-256 образца

Относительно `gfx/models/ships/mammalian_01/`; абсолютных персональных путей нет. Полный манифест 13 бинарных входов печатает тест реального контента; `.gfx` дополнительно хешируется в браузере.

| Файл | SHA-256 |
|---|---|
| mammalian_01_corvette_S1.mesh | 32bcd03bf068936fe31fed32854c4db2ed02f7cc709800306b5512f37b341340 |
| mammalian_01_corvette_diffuse.dds | 96792a19ea8d0f6b5fd1ee40469def7c8582060148cf78d9b0a2b4705428603b |
| mammalian_01_corvette_normal.dds | cb4fb2e5736dbb7e14a19d55aeb07c2618b2c6d02602fb04f31ae5068a78641d |
| mammalian_01_corvette_specular.dds | 304d768d483df02727bb27ccbc0fbc3436316ad530e9708719375afd8162e2d3 |
| _mammalian_01_ships_meshes.gfx | e0c930f540c3e135b7f81ccf3de47f2882351b051afe8b301504d68d419ca63b |

## Следующие обязательные задачи, не реализованные этим экспериментом

1. Контракт V03: версия/DLC/манифест, read-only выбор установки, HTTPS/LAN, отказ/отзыв разрешения, кеш и бюджеты; отдельная приёмка в Google Chrome. Нативный helper не разрешён автоматически.
2. Выбрать визуальный набор M3 и уровень shader parity; planet/star/Arkship пока только разобраны, **не визуально проверены**. Их skin/skeleton требуют обработки, а не молчаливого пропуска.
3. Согласовать серверные пространственные DTO/ID и вертикальный срез V04–V09; не превращать этот стенд в авторитетную симуляцию.

Локальные свидетельства вне Git: `artifacts/asset-probe/renderer-comparison.jpg`, `browser-results.json`. Первое — настоящий браузерный screenshot, второе — структурированные замеры. Исходные ассеты и скриншот не публиковались; установка игры не изменялась.

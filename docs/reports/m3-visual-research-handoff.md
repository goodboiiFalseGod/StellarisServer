# Передача исследования материалов M3

12 октября 2026. Read-only исследование Stellaris 4.5.2; не контракт реализации и не визуальный PASS. Ассеты не копировались в Git. Владелец материалов должен сначала уточнить контракт, зависимости и лимиты.

## Геометрия и масштаб

Через существующий `parsePdxMesh`: `gfx/models/planets/base_star.mesh` surface bbox 8.899×8.909×8.895, radius 4.454671; atmosphere radius 8.590096. Entity-scale 1.3 даёт радиусы 5.791072 / 11.167125. Звезда геометрически не плоская.

`planet_clouded.mesh`: поверхность совпадает с поверхностью звезды; atmosphere radius 5.200054, poles 4.497249, cloud shadow 4.574142, clouds 4.652415. Оба atmosphere: все 1638 triangles outward winding, все 856 normals наружу. Native CULL_BACK ещё требует сопоставления coordinate/projection convention; не менять сторону вслепую.

`gfx/models/ships/starbases/mammalian_01_outpost.mesh`: bbox 16.126×22.509×15.044, radius 15.577618. Chain современного outpost корректен: `common/ship_sizes/00_starbases.txt` → `section_templates/starbase.txt` → `_starbase_entities.asset` → `_starbase_meshes.gfx`. Scale mesh=1, part1 position0, quaternion[0,0,0,-1] identity. Двойное масштабирование не обнаружено; runtime display scale оригинала не восстановлен. `size_multiplier` — не доказательство visual scale.

`mammalian_01_corvette_S1.mesh`: bbox 4.256×3.373×12.648, radius 7.154961. Это согласованный section-only образец M3, не полная современная native assembly: S1 template закомментирован, активен S3, mammalian frame использует molluscoid frame. Не расширять профиль скрыто.

## Материалы и свет

Текущая звезда — MeshBasicMaterial постоянного цвета: объясняет плоское восприятие, не повреждение mesh. `gfx/FX/pdxmesh.shader` PixelPdxMeshStar (838–895) использует animated cube-noise, lava/stone и view-dependent atmosphere; `pdxmesh_samplers.fxh` 69–95 задаёт cube slot7 и 2D slots8/9.

`gfx/worldgfx/star_g_class.txt` указывает `gfx/worldgfx/lava_noise.dds`, `lava_d.dds`, `stone_d.dds`, LavaBright/HotStone/ColdStone HSV/intensity и system_light=g_class_star. Свет проверять по именованному блоку `gfx/lights/star_lights.asset`, не по первому найденному system_light. Native tonemap/bloom/particles не считаются автоматически разрешённой новой реализацией.

DDS header probe: noise 1024² DXT1 cubemap6faces/1mip, decoded RGBA 24 MiB; lava_d 1024² DXT5/11mips, stone_d 1024² DXT1/11mips, каждый 4 MiB base / 5.333332 MiB all. Итого примерно 32 MiB base / 34.667 MiB all, без GPU copies/overhead. Новые зависимости требуют allowlist/hash/profile и пересмотра budget до кода.

Planet shader имеет night/cloud/lighting logic; нынешний Phong со specular green и фиксированной shininess30 — приближение, native также использует alpha gloss/red emissive. RRXG normal adapter соответствует standardfuncsgfx.fxh 542–548; доказанного normal-channel дефекта нет.

## Камера — отложенные reference настройки

`common/defines/00_defines.txt`, NCamera: FOV35, ROTATION_RADIANS_PER_MOUSE_UNIT .004, SYSTEM_SPACE_SCALE_MULT10, system pitch20/80; NGraphics.PLANET_SCALE_SYSTEM {.425,.325,.35,.5,.5,.5,.75}. Native порядок применения scale/zoom пока неизвестен; не вводить случайные коэффициенты станции/планеты. CAM-001 использует .004, но не меняет FOV/pitch/scale. Preview CAM_DIR не доказывает system entry camera.

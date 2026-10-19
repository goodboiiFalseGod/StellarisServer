# Wormholes WORMHOLE-01

Queued after NAV-BOUNDARY-01 and HABITABLE-01; direct instruction October 16, 2026,
generation one playable slice at a time, without browser tests.

## Sources and boundaries

`common/on_actions/00_on_actions.txt:9` включает `game_start.31` в on_game_start;
`events/game_start.txt:1920–1970`, event `game_start.31`: отдельный post-generator
script stage, не native geometry tail (reverse addendum18.5). Читает
num_wormhole_pairs с scale_by5; каждый проход выбирает первую систему без natural
wormhole/starting_system/empire_cluster, затем вторую с теми же исключениями и
distance(source=prev,min_distance>=500). Обе spawn_natural_wormhole создаются лишь
после успешного выбора второй; bypass_type=wormhole, random_pos=yes,
orbit_angle360; link_wormholes=prev. Один endpoint на систему, никаких orphan.
При отсутствии кандидатов попытка может не создать пару; не гарантировать count.

`common/bypass/00_bypasses.txt:76–104`: one_to_one, always_active=yes,
requires_exploration=yes, tech_wormhole_stabilization; icon_frame12. Этот срез
генерирует пары и позволяет найти/выбрать объект. Исследование bypass, технология,
правила перехода и sensor extension требуют отдельного контракта; не выдавать
работающий переход или известное назначение до реализации этих правил.

Сценарии задают num_wormhole_pairs0..5/default1. Сохранить существующий Core int
WormholePairs, UI шаг1; это ограничение M3 до целых значений, не доказательство
полного vanilla slider. M3 число попыток=factor*5, диапазон0..25;
`WormholeAttemptsFactorFiveV1` явно локальная интерпретация настройки до отдельного
доказательства native get_galaxy_setup_value (вопрос направлен reference owner).
Host-only setting,
strict allowed fields, bounds/source range. Другие unsupported families запрещены.

Native default distance metric/conversion и random_system RNG interleaving не
доказаны. Явная WormholeEuclideanMapDistanceV1 использует raw fixed/100000 и
threshold500. В tiny radius200 это может не дать пар; не уменьшать расстояние
или создавать fallback ради демонстрации. Указывать underfill в metadata/report,
не утверждать vanilla parity. Два отдельных deterministic domains для selection
и placement; не менять geometry/content/names/rings RNG. Factor0 без draws.
Source event/точная активная grammar валидируется; drift — typed failure.

Положение endpoint: WormholeOutsideOrbitalEnvelopeV1, proxy max(body center
distance+Size/2, gateway center distance+6), затем40; угол из placement domain.
Это локальная presentation policy, не native random_pos. Immutable stable ids,
внутренняя симметричная pair relation, ledger/policies/fingerprint. Система и
камера включают endpoint. Snapshot optional spatial.wormholes только для
permitted detailed systems; не передавать partnerId/pairId/скрытое назначение.
Карточка «Червоточина»/«Не исследована» без мнимой команды перехода.

## Local rendering

Source natural_wormhole_entity в gfx/models/add_ons/_add_ons_entities.asset
→ wormhole_rings_mesh и attach natural_wormhole_stretched_ring_entity
→ wormhole_stretched_ring_mesh в _add_ons_meshes.gfx. Entity scale12/child1,
gfx scale1, UV metadata; реальные mesh layers above1Shape/above2Shape/above3Shape
используют PdxMeshAlphaAdditiveAnimateUV, gfx override
PdxMeshAlphaAdditiveAnimateUVNoDissolve. Текстуры wormhole_rings.dds,
wormhole_stretch.dds и gfx/models/nospec.dds. StaticWormholeRingsV1 явно показывает
исходные кольца/attachment; particle wormhole_idle_particle, звук и UV animation
не воспроизводятся и не объявляются parity. Никакой активации portal/gateway.
Surface extent6 local units как явная policy, source transforms один раз.
Actual mesh inspection: обе mesh unskinned/skeletonCount0; attachment `root`
находится в locator tree, p=[0,0,0], q=[0,0,0,-1] (identity rotation).
Валидировать этот anchor; не подставлять отсутствующий skeleton transform.

Отдельный bounded ObjectIds natural_wormhole, не pc_*; C#/JS closure и identity,
source hashes и budgets согласованы; прежние gateway/class-only manifests
совместимы. Source worker выявил, что wormhole_rings.dds1024×1024 и
wormhole_stretch.dds2048×1024 имеют mipCount1, поэтому SourceMip512V1 для них
неприменим. Узкая WormholeNativeMip0V1 разрешает mip0 только этим двум точным
paths/dimensions/face1/mipCount1:12MiB RGBA. Отразить её в natural materialProfile
C#/JS и resource policy; прочие class/gateway textures сохраняют SourceMip512V1.
Общие512MiB budgets не увеличивать, actual decode/converted/GPU/peak учитывать.
Новых ресемплеров/производных файлов нет: local runtime использует исходные pixels.
Модель/выбор/label/reconcile/disposal в системе; небольшой маркер на
галактической карте лишь для уже разрешённого endpoint, без линии скрытой пары.
Галактический маркер — явно локальный парный кольцевой glyph; source icon_frame12
не выдаётся за пиксельно воспроизведённую native иконку.
Не расширять snapshot лишними per-system полями. Source event/on_action/bypass
входят в content fingerprint и VerifyStable; отсутствие этих optional sources
блокирует только включённую генерацию червоточин, не legacy/factor0.

## Acceptance

Один focused backend режим: exact source event/bypass,0/1/5/range, исключения,
inclusive distance500, no-orphan/unique/symmetric/deterministic, отсутствие
destination leaks. Pure synthetic positions проверяют успешную ветвь; один tiny
runtime с сохранённым worker допускает честный underfill, без перебора seeds/huge.
Focused Node actual source closure/budgets и actual renderer/picking/card/privacy/
empty legacy. Independent actual-diff review, одна Server Release после freeze,
отдельный коммит/запуск; браузер и широкие suites запрещены.

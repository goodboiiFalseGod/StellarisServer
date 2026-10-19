# Armies in the planet menu — T06

October 15, 2026. Full scope: real army instances, states and permitted actions,
recruitment catalog, dedicated queue, and authoritative commands. Source: 4.5.2;
internal Arkship layout was separately deferred by the human. A missing army domain
does not mean an empty garrison. Do not create demo armies or attach them
to planet construction without an evidenced queue-kind contract.

## Confirmed sources

- `common/armies/01_assault_armies.txt:8–80`: assault_army, raw time=90,
  prerequisite tech_assault_armies; cost branches depend on country_uses_bio_ships
  и is_nomadic: minerals100, alloys50 либо food100×ratio. Upkeep energy1.
  Comment :2 confirms upkeep is monthly before modifiers.
- `common/scripted_variables/08_scripted_variables_biogenesis.txt:12`: ratio1.25.
  This does not authorize ignoring country/species context in the quote.
- Army potential_country excludes Wilderness and conditionally machine; species potential
  accepts shroud-forged or sapient without mechanical/machine-unit. The absence of allow
  in the block does not prove the absence of engine guards. Comment :5 describes an AI filter,
  not complete player-command eligibility.
- `common/technology/00_eng_tech.txt:1803–1810`: tech_assault_armies требует
  tech_planetary_defenses; `00_soc_tech.txt:4–9` sets the latter as start_tech=yes.
- `common/defines/00_defines.txt:2523–2525`: AI MIN10/MAX50 нельзя превращать
  as the game cap. ASSAULT_ARMIES_PER_POP=.001 does not specify rounding, minimum,
  population composition, or treatment of queued units.
- `localisation/english/main_2_l_english.yml:337` описывает species limit на империю;
  :259 — отсутствие подходящего вида на колонии. `main_1_l_english.yml:1555`
  связывает найм с населением планеты. Отказы spawning :1209–1210 не доказывают
  исчерпывающие recruitment guards.
- `common/species_rights/military_service_types/00_military_service.txt`: full :5,
  limited :135, none :194. Допустимость права не равна возможности найма.
- `localisation/english/modifiers_1_l_english.yml:417,475–477`: разные army build
  speed, planet army build speed и planet army build time keys; building formula
  автоматически не применяется.
- `interface/planet_view.gui:3170–3208,7962–8029`: список, recruitment queue,
  embark-all/disband controls. GUI доказывает действия интерфейса, не транзакции.

## Точные зависимости

Unknown: actor/planet/occupation/species-rights guard; единица и daily executor
raw time; aggregation/rounding speed/time; queue N; момент оплаты; cancel/refund;
completion instance/location/autoembark, embark/disband последствия. Историческая
LOC-подпись про 12 мест не принимается за актуальную вместимость.
В текущем runtime army domain/instance producer отсутствует.

Следующий source-only этап может сохранить AST/provenance/условные ветви, но сам
по себе не закрывает T06 и не открывает найм. Для executable этапа нужны отдельные
army identity/owner/species/planet и immutable quote/order/receipt, atomic bank и
дневная фаза по подтверждённой army policy. Unknown блокирует зависимые команды.

Приёмка: exact owner/POV/stale/replay, species eligibility/empire cap с queued units,
pay/refund/storage, active/waiting/completion, instance/location, embark/disband,
отсутствие army effects до completion и отдельная queue-kind проверка. Private
projection выполняется до wire; inaccessible colony/species не раскрываются.

## Отложенная проверка

Своя мирная колония, известные species/pop/rights/modifiers/resources, пустая очередь:
три одинаковые армии; записать pay и daily progress, отменить pending и progressed,
завершить третью и проверить instance/location. Из копий состояния проверить
full/limited/none service и видовой empire-limit при второй планете найма.
Наблюдаемые ответы не предопределены. Запуск/новый save не требуются сейчас.

Source audit: /root/zone_importer (Luna low), reference owner
`01a12f66-8bb2-7ee3-aced-76f5d06d2338`, cursor
`e1a83697-d568-4d8c-ac32-b4b2b525d471:4`; сценарий передан существующему manual owner
`01a12d73-b37b-7080-943c-684cc080b1f6`. Игра не запускалась, установка read-only.

# M16/M20: evidence и координация

13 октября 2026. Продолжение по прямому поручению пользователя координироваться с
research-чатом `01a1359a-86d4-78f3-a4d2-5dda015220f6` и использовать закрытые operators.
Прочитан addendum §12.1–12.2; SHA checkpoint
`1323B90D713D589F531FF9E751C045C6F10BBA0AA6576DBECA9DC1A221AE5ACE`.
Предыдущий прочитанный checkpoint был `2E5C0EFD79FD3BB078C4144E8DA7014C659457F5EC301F3DE4380D66AB13A254`.
Последующие сообщения партнёра уточнили widths, затем они сохранены в durable §12;
текущий hash проверен на одном bytesnapshot. Партнёр продолжает дополнять внешние outputs,
поэтому checkpoint относится к прочитанному содержимому, а не обещанию неизменности пути.
Никакие source bytes/disassembly/scripts не копировались. Original три отчёта не менялись.

## M16

`0x140980782–0x1409807CC`: method/value/cap/empireCount — signed int32.
Для method0 one_every_x_empire count=(E−1+value)/value с signed truncation;
valid E>=0,value>0. Method1 constant даёт value.
При cap!=-1 count=min(count,cap); финальный max(count,1) всегда.
Native unknown enum даёт raw0; наш поддержанный API отвергает неизвестный enum,
а не принимает произвольные enum values за vanilla method.
Native сумма wrap int32, наш API отсекает overflow. Parser absence defaults не доказаны:
projection требует все method/value/max явно. Vanilla пять scenarios их содержат.
Ни одного RNG draw.

Независимые примеры партнёра (method,E,value,cap → count):
0,10,3,−1→4; 0,10,3,2→2; 0,0,3,−1→1;
1,99,7,5→5; 1,99,0,−1→1.

## M20

Evaluator `0x140508F60–0x1405090EF`, caller `0x14097FC10–0x14097FC26`.
spawn/scaled/totalSystems signed int32; fixed factor signed int64 scale100000.
Если scaled>0, первый product native low signed int32; затем rounded base int64.
Sign extension первого int32 product в int64 выполняется **до** bias ±50;
native int32 product+50 wrap не используется. Это уточнение проверено reviewer'ом
против durable §12.2 и отдельно подтверждено партнёром.
Если scaled<=0, base=spawn. Chance=round_half_away(base×factor/100000), int64,
без clamp. API ограничивает первый product int32 и второй product int64 checked.
Отрицательный factor округляется half away from zero.
Evaluator не расходует RNG; caller делает один draw даже при chance<=0 или >=100,
затем сравнивает draw%100<chance. Наш predicate получает уже потраченный uint31 draw.
Default factor100000 относится к отсутствующей setting, не к raw evaluator API;
импорт initializer/settings остаётся за отдельным gate.

Независимые примеры spawn,scaled,N,factor → chance:
17,0,100,100000→17;
0,25,3,150000→2;
0,25,2,50000→1;
5,0,10,−50000→−3;
0,10,4,100000→0.
Они проверяют оба rounding stages и negative-half; сохранены в own checks.

## Дальнейшее сотрудничество

Партнёру переданы текущая coverage и узкие вопросы: durable widths/bias M20,
точный fixed trig порядок и candidate vectors до relaxation.
Следующий срез §16.6 описан в [candidate evidence](galaxy-generation-candidate-evidence.md).
Relaxation kernel/unnamed global, resolved load order и оставшиеся внешние dependencies
не заменяются приближениями. Каждый новый срез проходит собственные проверки и independent review.

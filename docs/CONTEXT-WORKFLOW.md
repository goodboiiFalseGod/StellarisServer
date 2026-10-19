# Context and chat lifecycle

13 October 2026. The human authorized context cleanup after the measured audit. This changes documentation loading and chat handoff, not gameplay scope, model settings or current task ownership.

## Contract

- Mandatory entry: AGENTS, compact DECISIONS and OPEN-QUESTIONS, then only the contract and evidence needed for the current task. Do not preload the documentation tree or whole historical chats.
- Preserve the complete previous decisions/questions as linked detail snapshots in the same directory; retain decision IDs, source attribution and existing linked heading anchors. Accepted rules remain binding. Older planning/checkpoint wording cannot override later authorization.
- Keep the three mandatory project files together below 12,000 characters. Keep detailed rules in topic contracts; update the compact pointer when a binding decision changes. This is a context budget, not a reason to discard requirements.
- Move global Unity detail to a conditional local reference. Other projects receive only common workflow instructions; Unity tasks must load the retained rules before work. Preserve models, effort and plugins. Automation changes require direct authorization; the approved maintenance transition is in CONTEXT-MAINTENANCE.md.
- Start a fresh chat at a completed stage boundary using a short handoff: objective, branch/base/result commit, ownership, dependencies, verified checks and limitations, next atomic action, relevant document links. Do not fork full history.
- Preserve active work, user servers, tabs and ports. Do not archive active chats or assign competing owners. A replacement coordinator starts read-only until ownership is explicitly transferred.

## Acceptance and verification

Verify complete snapshot preservation, linked files/heading anchors, required current authorization and rejection boundaries, mandatory-entry size, and the actual diff. Commit only owned documentation. A clean project build is unnecessary for prose-only changes.

Report measured context reduction separately from latency. Compare a bounded read-only task in old and fresh chats only with matching model/effort, task, tools and output, at a safe idle boundary. Record input/cached tokens and elapsed time; unavailable or unmatched measurements must not become an acceleration claim. This cleanup must not wake old implementation chats solely to benchmark them.

## Принятая организация работы

Пользователь подтвердил применение схемы 13 октября 2026. После безопасной передачи из CONTEXT-MAINTENANCE действуют два постоянных рабочих чата:

- Координатор: приоритеты, разрешения, зависимости, короткое текущее состояние и сообщения пользователю. Принимает результаты с коммитами и ограничениями; не собирает полные логи каждой операции.
- Интегратор: единственный владелец назначений файлов, приёма проверенных изменений и стабильной сборки. На безопасной границе держит два полезных независимых потока: реализацию и другую реализацию либо точечное исследование/review/QA. Две реализации допустимы только при доказанной независимости и в пределах EXECUTION-POLICY; в waiting mode — одна реализация, но параллельные исследование/review/QA сохраняются. Общие файлы и зависимые операции идут последовательно.

Исполнители и независимые ревьюеры получают краткие задачи со свежим контекстом, без полной истории и вложенного делегирования. Небольшую работу интегратор может сделать сам, но его код также проверяет другой агент. Референс, численные исследования и ручная приёмка сохраняются как специализированные источники; их не опрашивают целиком перед каждой задачей.

Решение пользователя 14 октября: один интегратор общей сборки, без параллельных цепочек интеграции; координатор задаёт приоритеты. Смена интегратора — после явной передачи полномочий.

Задание: цель, base commit, свои файлы, контракты/источники, исключения, поведение/ошибки, приёмка и проверки. Передача: цель; ветка, worktree, base/result commit, файлы; незакоммиченные изменения и владелец (либо «нет»); зависимости; что работает; проверки и ограничения; следующий шаг; ссылки. Сверять Git и изменившиеся зависимости, не всю историю. После потери контекста обязательный вход сохраняется.

Цикл: контракт → реализация → независимое ревью → исправления и ревью затронутого → проверки → интеграция → передача. Малое изменение: затронутые проверки; полный набор — на согласованной точке интеграции/выпуска. Повторы/расширение — при изменениях, регрессии или неразрешённом риске; прежний PASS относится к неизменённому проверенному состоянию. Обязательные contract/security/visual gates сохраняются. Логи — локально; дублирующие аудиты, ревью-каскады и опросы без нового события не нужны.

Решение хранится в одном тематическом контракте; компактная памятка содержит инвариант или ссылку. Текущий handoff обновляется на месте. Новый отчёт создаётся для новой самостоятельной проверки/поставки, а не для каждого сообщения. На границе завершённой поставки длинный рабочий чат заменяется краткой передачей; завершённые чаты архивируются после подтверждения приёма.

Единственный heartbeat обращается к координатору. Пока полезная работа идёт, он не будит всех исполнителей повторно и не создаёт дополнительные мониторы. Без изменений/ошибок/нужного решения пользователя — тишина.

Экономию оценивают по принятой задаче: доступные input/cached/output tokens, время до интеграции, исправления после ревью и обнаруженные регрессии. Данные добавляют кратко в существующий handoff; неизвестные значения помечают как недоступные. Цены и ускорение не выводятся из числа символов. Настройки моделей сохраняются, дешёвые модели применяются только по принятой политике задач.

## Измеряемый контроль роста

Прямое поручение пользователя 13 октября: проверить соблюдение правил и закрыть пробелы контроля. Инструмент `tools/context-audit.mjs` перед новым назначением и при heartbeat проверяет обязательные файлы (суммарно 12 000 символов), текущий handoff (6 500) и этот регламент (9 000). Это рабочие бюджеты проекта, а не технические пределы модели. При превышении сокращать повторения/исторические записи с сохранением актуальных требований и ссылок. Не усекать текст вслепую.

Для двух рабочих чатов читать только метрики локальных JSONL: последний input/cached input, размер окна и число compaction. Суммарный расход за всю сессию не равен текущему контексту. Сигнал передачи: последний input ≥60% окна либо ≥3 compaction. Если окно/метрики недоступны, явно вернуть unknown. Скрипт не читает содержимое сообщений в модель и не выводит сырые журналы. JSON-результат не более 4 000 символов; состояние сравнения — только в игнорируемом artifacts/context-control/state.json. Файлы сессий не изменять.

При сигнале передачи завершить текущий атомарный цикл, сохранить один handoff ≤6 500 символов, создать свежий чат соответствующей роли без fork истории, сохранить модель/effort и получить приём точного состояния. Затем передать полномочия, обновить ID в handoff/задании единственного монитора и архивировать прежний чат. Активную реализацию, незавершённые ветки и approval не прерывать и не переносить автоматически. Если модель недоступна, waiting с единственным разрешённым повтором; создание свежего чата не является обходом capacity или разрешений. Перед следующим независимым этапом выполнить отложенную передачу. Порог — сигнал безопасной смены чата, а не потеря разрешённого scope.

Координатор не перечитывает неизменённые документы на каждом heartbeat: проверить хеши/изменения и краткий статус; после потери контекста прочитать обязательный вход заново. Если новый результат/блокер/порог отсутствует, не отправлять сообщений исполнителям и пользователю. Повтор одного и того же pending approval/capacity фиксируется без повторного запуска. Независимое разрешённое чтение допустимо; недоступный QA остаётся pending.

Межчатовый запрос/результат — до 1 500 символов плюс ссылки; tool output по умолчанию до 4 000. `wait_threads` печатать только status, error, короткий результат и cursor. `read_thread` — только для конкретного нового события, максимум один turn, без outputs, фильтровать items до текста agentMessage. Полные JSON, переписки, heartbeat prompt и логи тестов не выводить. Исключение — необходимый участок доказательства с явной причиной; полный лог сохранять локально. Фактический raw ответ инструмента может быть длиннее: ограничивается именно вывод в контекст модели.

Приёмка контроля: корректная граница бюджета, корректный last input вместо cumulative, unknown без нулевой подмены, обработка незавершённой JSONL-строки, отсутствие сырых сообщений в результате, устойчивый повтор без изменений и компактный вывод. Проверить на временных fixtures и реальных двух чатах; независимое ревью фактического кода обязательно. Контроль исполняет монитор и владелец перед назначениями; он не устанавливает жёсткий лимит на уровне самого приложения Codex.

# M3 — отменяемый browser importer

Реализует [локальный импорт](M3-LOCAL-ASSET-IMPORT.md), [manifest/digest](M3-ASSET-MANIFEST.md) и ограниченный static profile reader. Не заменяет сетевую авторизацию. Технический preview m3.html сохраняется как отдельный воспроизводимый стенд.

## Источник и проверка

UI по действию пользователя получает read-only DirectoryHandle корня либо ограниченный test File input 44 файлов; не запрашивает write permission. DirectoryHandle структурно клонируется в отдельный module Worker. Worker читает только PROFILE_PATHS, не сканирует каталог и не делает fetch/upload. При File input UI сопоставляет уникальные basename ровно этому набору; отсутствующий/лишний/дублированный файл — отказ, ничего лишнего не читается. Не поддерживать произвольные пути серверного manifest: множество paths строго равно собственному профилю.

До чтения проверяются schema/profile/game/importer/material versions, 44 уникальные normalized paths/kind/размеры (≤64 MiB)/lowercase SHA256 и canonical profileDigest; four visual IDs/dependencies/materialProfile строго собственному серверному шаблону. Metadata ≤1 MiB на стороне transport. Неверная структура/профиль — несовместимо, не частичный импорт. Хеши/длина каждого прочитанного File точно совпадают с manifest. Затем buildM3Profile с переданным остаточным общим decoded-бюджетом; guards reader не ослабляются.

После декодирования DirectoryHandle повторно разрешает/читает/хеширует все 44 текущих файла: изменение/отзыв доступа отклоняет staged-набор. File input позволяет проверять только выбранные immutable File snapshots, не перечитать текущий путь на диске; этот test fallback явно помечается в результате, для новой сессии требуется новый явный выбор. Ошибка File snapshot чтения тоже отказ. Для directory новой сессии нужна новая полная проверка источника; прежний живой набор допустим лишь для reconnect в ту же sessionId.

## Worker и владение

Один запрос импорта на worker; requestId — локальное поколение. Сообщения progress содержат фазу (`metadata/read/decode/recheck/ready`), checked/total и относительное имя; result — проверенный CPU-профиль Maps/typed arrays, digest, mode, decodedBytes и длительность. Нет абсолютных путей/исходных байтов в сообщениях к игровому серверу. Worker не создаёт GPU/DOM и не сохраняет данные на диск.

Результат передаётся с transfer уникальных ArrayBuffers геометрии/текстур; исходные File/AST/raw buffers не сохраняются в UI. UI создаёт staged GPU-ресурсы, учитывая старую/новую CPU и converted memory по static-adapter, и лишь затем заменяет набор. Сетевая assetReady отправляется после успешной атомарной публикации, не по progress ready worker.

Importer owner прекращает worker после result/error/cancel. AbortSignal/кнопка отмены/pagehide/новый import завершают worker; поздние сообщения старого поколения игнорируются, Promise явно завершается AbortError. Отмена сохраняет старый профиль/GPU и не меняет мир. Worker `error`/`messageerror` отклоняют импорт с видимой диагностикой; исключения не теряются. Отменённый worker не может создать поздний результат. Progress callback не должен скрывать ошибки; callback exception отменяет запрос. Нет постоянного cache, повторного background re-import без действия пользователя или скрытого сетевого чтения.

## Приёмка

Pure metadata/digest: parity с C#, shuffled sorting, duplicate/extra/traversal/version/kind/hash/размеры/visual mapping и несовпадение digest. Import: real 44 snapshots и directory path mocks, hashes/length mismatch, changed reread, permission denial, unsupported PDX/DDS, decoded limit. Lifecycle owner: progress/result/error/messageerror, отмена до старта/при чтении/декодировании, новый запрос, поздний result, освобождение worker ровно один раз. Transfer buffers уникальны, данные/слои сохранены. Реальный module Worker проверить во встроенном браузере, Chrome folder grant/revoke и производительность оставить pending, если недоступны. Численные tests не заменяют осмотр моделей.

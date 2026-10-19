import { GENERATED_VISUAL_ERROR_CODES } from './generated-world-visual-profile.mjs';

const abortError = () => new DOMException('Импорт отменён.', 'AbortError');
const visibleError = value => value instanceof Error ? value : new Error(`Ошибка импорта: ${String(value)}`);

export class M3Importer
{
    constructor({ workerFactory = () => new Worker(new URL('./m3-import-worker.mjs', import.meta.url), { type: 'module' }) } = {})
    {
        this.workerFactory = workerFactory;
        this.active = null;
        this.generation = 0;
        this.disposed = false;
    }

    import(request, { signal, onProgress = () => {} } = {})
    {
        this.cancel();
        if (this.disposed) { return Promise.reject(new Error('Importer освобождён.')); }
        if (signal?.aborted) { return Promise.reject(abortError()); }
        const requestId = ++this.generation;
        return new Promise((resolve, reject) =>
        {
            let worker;
            try { worker = this.workerFactory(); }
            catch (error) { reject(visibleError(error)); return; }
            let settled = false;
            const finish = (error, value) =>
            {
                if (settled) { return; }
                settled = true;
                worker.removeEventListener('message', message);
                worker.removeEventListener('error', failure);
                worker.removeEventListener('messageerror', malformed);
                signal?.removeEventListener('abort', cancel);
                worker.terminate();
                if (this.active?.requestId === requestId) { this.active = null; }
                if (error) { reject(error); } else { resolve(value); }
            };
            const cancel = () => finish(abortError());
            const failure = event => finish(new Error(event.message || 'Ошибка worker импорта.'));
            const malformed = () => finish(new Error('Не удалось прочитать сообщение worker.'));
            const message = event =>
            {
                if (settled || this.active?.requestId !== requestId || event.data?.requestId !== requestId) { return; }
                const data = event.data;
                if (data.type === 'progress')
                {
                    try { onProgress(data.progress); }
                    catch (error) { finish(visibleError(error)); }
                }
                else if (data.type === 'result') { finish(null, data.result); }
                else if (data.type === 'error')
                {
                    const error=new Error(data.message || 'Ошибка импорта.'); const typed=data.generatedVisualError;
                    if(typed&&typeof typed==='object'&&!Array.isArray(typed)&&Object.keys(typed).sort().join(',')==='classId,code'&&GENERATED_VISUAL_ERROR_CODES.includes(typed.code)&&
                        (typed.classId===null||typeof typed.classId==='string'&&/^pc_[a-z0-9_]{1,96}$/.test(typed.classId))) { error.code=typed.code; error.classId=typed.classId; }
                    finish(error);
                }
                else { finish(new Error('Неизвестное сообщение worker.')); }
            };
            this.active = { requestId, cancel };
            worker.addEventListener('message', message);
            worker.addEventListener('error', failure);
            worker.addEventListener('messageerror', malformed);
            signal?.addEventListener('abort', cancel, { once: true });
            try { worker.postMessage({ requestId, request }); }
            catch (error) { finish(visibleError(error)); }
        });
    }

    cancel() { this.active?.cancel(); }
    dispose() { this.disposed = true; this.cancel(); }
}

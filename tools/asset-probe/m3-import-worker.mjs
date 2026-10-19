import { runM3Import, collectTransferBuffers } from './m3-worker-pipeline.mjs';
import { GeneratedVisualError } from './generated-world-visual-profile.mjs';

let started = false;
self.addEventListener('message', async event =>
{
    if (started) { return; }
    started = true;
    const { requestId, request } = event.data ?? {};
    try
    {
        const result = await runM3Import(request, { onProgress: progress => self.postMessage({ type: 'progress', requestId, progress }) });
        self.postMessage({ type: 'result', requestId, result }, collectTransferBuffers(result.profile));
    }
    catch (error)
    {
        self.postMessage({ type: 'error', requestId, message: error?.message ?? 'Ошибка worker импорта.',
            ...(error instanceof GeneratedVisualError?{generatedVisualError:{code:error.code,classId:typeof error.classId==='string'&&/^pc_[a-z0-9_]{1,96}$/.test(error.classId)?error.classId:null}}:{}) });
    }
});

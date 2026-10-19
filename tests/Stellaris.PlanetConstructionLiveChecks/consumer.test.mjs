import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

const { constructionOperations } = await import(pathToFileURL(process.argv[3]).href);

const model = JSON.parse(await readFile(process.argv[2], 'utf8'));
const development = model.development;
const operations = constructionOperations(model);
const pausedOrder = development.queue.find(order => order.orderId === 'paused-precinct');
assert.equal(pausedOrder.status, 'pending');
assert.equal(pausedOrder.gates.length, 0);
assert.ok(operations.some(operation => operation.type === 'cancelConstruction' && operation.orderId === pausedOrder.orderId));
assert.ok(!operations.some(operation => operation.type === 'clearBlocker'));
assert.ok(development.features.every(feature => !development.startupFeatures.some(startup => startup.instanceId === feature.instanceId && startup.isHidden)));
assert.ok(development.features.filter(feature => feature.clearable).every(feature => feature.gates.length > 0 && feature.clearanceSlotId == null));
for (const source of development.startupBuildings)
{
    const matches = development.installed.filter(item => item.slotId === source.slotInstanceId && item.definitionId === source.definitionId);
    assert.equal(matches.length, 1);
    assert.deepEqual(source.compatibleZoneInstanceIds, [development.slots.find(item => item.slotId === source.slotInstanceId).slotId.split('/building-slot/')[0]]);
}
console.log('PASS accepted consumer: paused cancellation, hidden/blocker categories, exact startup building identity');

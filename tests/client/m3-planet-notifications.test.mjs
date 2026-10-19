import test from 'node:test';
import assert from 'node:assert/strict';
import { createPlanetNotifications } from '../../src/Stellaris.Server/wwwroot/m3-planet-notifications.mjs';

const order = (orderId, cancellationReason = 'non-positive-speed', status = 'cancelled') => ({ orderId, slotId: 'slot-' + orderId, definitionId: 'building', cancellationReason, status });
const model = (queue, id = 'colony') => ({ id, name: 'Колония', ownerId: 1, development: { colonyId: id, ownerParticipantId: 1, queue } });

test('Исчезнувший payer даёт отдельную причину без повторного уведомления и без выдуманного refund', () =>
{
    const owner = createPlanetNotifications(), m = model([order('lost-payer', 'payer-missing')]);
    const first = owner.sync('own-context', [m]);
    assert.equal(first.length, 1); assert.equal(first[0].orders[0].cancellationReason, 'payer-missing');
    assert.equal('refundQuote' in first[0].orders[0], false);
    owner.dismiss(owner.generation, m.id);
    assert.deepEqual(owner.sync('own-context', [m]), []);
});

test('Ошибка расчёта строительства группируется, повторно не появляется и не раскрывает детали возврата', () =>
{
    const owner = createPlanetNotifications(), m = model([order('calc-failed', 'calculation-failed')]);
    const first = owner.sync('own-context', [m]);
    assert.equal(first.length, 1);
    assert.equal(first[0].orders[0].cancellationReason, 'calculation-failed');
    assert.equal('refundQuote' in first[0].orders[0], false);
    assert.equal(owner.sync('own-context', [m])[0].orders.length, 1);
    assert.equal(owner.dismiss(owner.generation, m.id), true);
    assert.deepEqual(owner.sync('own-context', [m]), []);
});

test('Автоотмены группируются и не повторяются после dismiss/reconnect текущего POV', () =>
{
    const owner = createPlanetNotifications(), m = model([order('a'), order('b', 'condition-lost'), order('manual', 'player'), order('done', null, 'completed'), order('unknown', 'new-reason')]);
    let groups = owner.sync('session/owner', [m]);
    assert.deepEqual(groups.map(group => group.orders.map(item => item.orderId)), [['a', 'b']]);
    const version = owner.version, epoch = owner.generation;
    assert.equal(owner.sync('session/owner', [m])[0].orders.length, 2); assert.equal(owner.version, version);
    assert.equal(owner.dismiss(epoch, 'colony'), true); assert.deepEqual(owner.sync('session/owner', [m]), []);
    m.development.queue.push(order('c', 'ownership-changed'));
    groups = owner.sync('session/owner', [m]); assert.deepEqual(groups[0].orders.map(item => item.orderId), ['c']);
});

test('POV/session и удаление доступной колонии инвалидируют старые записи', () =>
{
    const owner = createPlanetNotifications(), m = model([order('a')]);
    owner.sync('session/owner', [m]); const epoch = owner.generation;
    assert.equal(owner.get(epoch, 'colony', 'a').slotId, 'slot-a');
    assert.deepEqual(owner.sync('session/owner', []), []); assert.equal(owner.get(epoch, 'colony', 'a'), null);
    owner.sync('session/other', []); owner.sync('session/owner', [m]);
    assert.equal(owner.get(epoch, 'colony', 'a'), null); assert.equal(owner.dismiss(epoch, 'colony'), false);
    assert.equal(owner.get(owner.generation, 'colony', 'a').orderId, 'a');
    assert.deepEqual(owner.sync('session/owner', [{ ...m, ownerId: 2 }]), []);
});

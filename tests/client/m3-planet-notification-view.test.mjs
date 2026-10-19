import test from 'node:test';
import assert from 'node:assert/strict';
import { renderPlanetNotifications } from '../../src/Stellaris.Server/wwwroot/m3-planet-notification-view.mjs';

class Element
{
    constructor(tagName) { this.tagName = tagName.toUpperCase(); this.children = []; this.attributes = {}; this.hidden = false; this.text = ''; }
    get textContent() { return this.text + this.children.map(child => child.textContent).join(''); }
    set textContent(value) { this.children = []; this.text = String(value ?? ''); }
    append(...children) { for (const child of children) { child.parentNode = this; this.children.push(child); } }
    replaceChildren(...children) { this.children = []; this.text = ''; this.append(...children); }
    setAttribute(name, value) { this.attributes[name] = String(value); }
    all() { return this.children.flatMap(child => [child, ...child.all()]); }
}

const makeDocument = () => ({ createElement: tagName => new Element(tagName) });
const byClass = (root, name) => root.all().filter(item => item.className === name);
const buttons = root => root.all().filter(item => item.tagName === 'BUTTON');

test('Сообщает подтверждённое исчезновение исходного payer без перенаправления возврата', () =>
{
    const document = makeDocument(), root = new Element('div');
    renderPlanetNotifications(root, [{ colonyId: 'C1', colonyName: 'Митра', orders: [
        { orderId: 'O1', slotId: 'S1', definitionId: 'building_factory', cancellationReason: 'payer-missing' }
    ] }], { document });
    assert.equal(byClass(root, 'planet-notice-order').length, 1);
    assert.match(root.textContent, /Исходный плательщик больше не существует; возврат невозможен/);
});

test('Показывает отдельную причину ошибки расчёта без излишков или сведений о возврате', () =>
{
    const document = makeDocument(), root = new Element('div');
    renderPlanetNotifications(root, [{ colonyId: 'C1', colonyName: 'Митра', orders: [
        { orderId: 'O1', slotId: 'S1', definitionId: 'building_factory', cancellationReason: 'calculation-failed', excessRefund: 123, refundQuote: { minerals: 50 } }
    ] }], { document });
    assert.equal(byClass(root, 'planet-notice-order').length, 1);
    assert.match(root.textContent, /Ошибка расчёта строительства/);
    assert.equal(root.textContent.includes('123'), false);
    assert.equal(root.textContent.includes('50'), false);
    assert.equal(root.textContent.includes('calculation-failed'), false);
});

test('группирует разрешённые причины отмены и локализует название заказа', () =>
{
    const document = makeDocument(), root = new Element('div');
    const groups = [
        { colonyId: 'C1', colonyName: 'Митра', orders: [
            { orderId: 'O1', slotId: 'S1', definitionId: 'building_factory', localizationKey: 'BUILDING_FACTORY', cancellationReason: 'non-positive-speed' },
            { orderId: 'O2', slotId: 'S2', definitionId: 'district_city', cancellationReason: 'condition-lost' },
            { orderId: 'done', definitionId: 'building_done', cancellationReason: 'completed' },
            { orderId: 'manual', definitionId: 'building_manual', cancellationReason: 'manual' },
            { orderId: 'excess', definitionId: 'building_excess', cancellationReason: 'excess-refund' },
            { orderId: 'unknown', definitionId: 'building_unknown', cancellationReason: 'player-choice' },
        ] },
        { colonyId: 'C2', colonyName: 'Таласс', orders: [{ orderId: 'O3', definitionId: 'building_lab', cancellationReason: 'ownership-changed' }, { orderId: 'O4', definitionId: 'district_mining', cancellationReason: 'destroyed' }] },
        { colonyId: 'C3', colonyName: 'Пустая', orders: [{ orderId: 'O5', cancellationReason: 'completed' }] },
    ];
    renderPlanetNotifications(root, groups, { document, resolveLocalization: (key, fallback) => key === 'BUILDING_FACTORY' ? 'Гражданские фабрики' : fallback });
    const sections = byClass(root, 'planet-notice');
    assert.equal(sections.length, 2); assert.deepEqual(sections.map(section => section.children[0].textContent), ['Митра', 'Таласс']);
    const orderButtons = byClass(root, 'planet-notice-order');
    assert.equal(orderButtons.length, 4);
    assert.deepEqual(orderButtons.map(button => button.children[1].textContent), [
        'Неположительная скорость строительства', 'Условия строительства больше не выполнены', 'Смена владельца колонии', 'Колония уничтожена'
    ]);
    assert.equal(orderButtons[0].children[0].textContent, 'Гражданские фабрики');
    assert.equal(root.hidden, false);
    for (const privateValue of ['O1', 'S1', 'building_factory', 'completed', 'manual', 'excess-refund', 'player-choice']) { assert.equal(root.textContent.includes(privateValue), false); }
});

test('кнопки открытия и dismiss вызывают только точные callbacks один раз', () =>
{
    const document = makeDocument(), root = new Element('div'); const opened = [], dismissed = [];
    renderPlanetNotifications(root, [{ colonyId: 'C9', colonyName: 'Орбита', orders: [{ orderId: 'Q7', definitionId: 'building_x', cancellationReason: 'destroyed' }] }], {
        document, onOpen: (...args) => opened.push(args), onDismiss: id => dismissed.push(id)
    });
    const order = byClass(root, 'planet-notice-order')[0], dismiss = buttons(root).find(button => button.attributes['aria-label'] === 'Закрыть уведомления колонии Орбита');
    assert.ok(dismiss); order.onclick(); dismiss.onclick();
    assert.deepEqual(opened, [['C9', 'Q7']]); assert.deepEqual(dismissed, ['C9']);
    assert.equal(root.hidden, false, 'dismiss обновляется контроллером; renderer не придумывает локальное состояние');
});

test('скрывает пустую группу и root без известных причин отмены', () =>
{
    const document = makeDocument(), root = new Element('div');
    renderPlanetNotifications(root, [{ colonyId: 'C1', colonyName: 'Митра', orders: [] }, { colonyId: 'C2', colonyName: 'Таласс', orders: [{ orderId: 'done', cancellationReason: 'completed' }] }], { document });
    assert.equal(root.children.length, 0); assert.equal(root.hidden, true);
});

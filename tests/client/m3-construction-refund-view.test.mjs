import test from 'node:test';
import assert from 'node:assert/strict';
import { renderConstructionRefund, renderConstructionRefundLosses } from '../../src/Stellaris.Server/wwwroot/m3-construction-refund-view.mjs';

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

const document = { createElement: tagName => new Element(tagName) };
const byClass = (root, className) => root.all().filter(item => item.className === className);
const buttons = root => root.all().filter(item => item.tagName === 'BUTTON');
const options = icons => ({
    document,
    appendResourceIcon(parent, id) { icons.push(id); const icon = document.createElement('img'); parent.append(icon); },
    formatAmount: value => `×${value}`,
    resourceLabel: id => ({ minerals: 'Минералы', energy: 'Энергия' })[id] ?? 'Ресурс',
});

test('подтверждение показывает ресурсные пары и вызывает только выбранное действие', () =>
{
    const root = new Element('div'), icons = [], calls = [];
    renderConstructionRefund(root, { minerals: 12.5, energy: 4, zero: 0, invalid: Infinity, negative: -2 }, {
        ...options(icons), onConfirm: () => calls.push('confirm'), onKeep: () => calls.push('keep')
    });
    assert.equal(root.hidden, false);
    assert.ok(root.textContent.includes('Часть возврата будет потеряна'));
    assert.ok(root.textContent.includes('Отменить заказ')); assert.ok(root.textContent.includes('Сохранить заказ'));
    const pairs = byClass(root, 'planet-refund-resource');
    assert.equal(pairs.length, 2); assert.deepEqual(pairs.map(pair => pair.children[1].textContent), ['×12.5', '×4']);
    assert.deepEqual(icons, ['minerals', 'energy']);
    assert.deepEqual(pairs.map(pair => pair.attributes['aria-label']), ['Минералы: ×12.5', 'Энергия: ×4']);
    assert.equal(root.textContent.includes('minerals'), false, 'ID ресурса не попадает в видимый текст');
    const actions = byClass(root, 'planet-refund-actions')[0];
    actions.children[0].onclick(); assert.deepEqual(calls, ['confirm']);
    actions.children[1].onclick(); assert.deepEqual(calls, ['confirm', 'keep']);
});

test('фактическая потеря имеет отдельный вид без кнопок; пустые и нулевые суммы скрыты', () =>
{
    const root = new Element('div'), icons = [];
    renderConstructionRefundLosses(root, { minerals: 3 }, options(icons));
    assert.equal(root.hidden, false); assert.ok(root.textContent.includes('Потеря возврата'));
    assert.equal(byClass(root, 'planet-refund-resources').length, 1); assert.equal(byClass(root, 'planet-refund-actions').length, 0);
    assert.equal(buttons(root).length, 0); assert.deepEqual(icons, ['minerals']);

    renderConstructionRefundLosses(root, { minerals: 0, energy: -1, invalid: NaN }, options(icons));
    assert.equal(root.hidden, true); assert.equal(root.children.length, 0);
    renderConstructionRefundLosses(root, {}, options(icons));
    assert.equal(root.hidden, true); assert.equal(root.children.length, 0);
});

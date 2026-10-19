import test from 'node:test';
import assert from 'node:assert/strict';
import { appendJobControls } from '../../src/Stellaris.Server/wwwroot/m3-job-controls.mjs';

class Element {
    constructor(tagName)
    {
        this.tagName = tagName;
        this.children = [];
        this.listeners = {};
        this.textContent = '';
        this.value = '';
        this.disabled = false;
    }

    append(...elements)
    {
        this.children.push(...elements);
    }

    addEventListener(type, callback)
    {
        this.listeners[type] = callback;
    }
}

const createDocument = () => ({ createElement: (tagName) => new Element(tagName) });

test('renders received count, number constraints, and a job-specific accessible label', () =>
{
    const parent = new Element('main');
    const model = { count: 2.5, jobId: 'researcher' };

    appendJobControls(parent, { document: createDocument(), jobId: model.jobId, count: model.count, disabled: false, pending: false, onSubmit() {} });

    const [section] = parent.children;
    const [label, button] = section.children;
    const [input] = label.children;
    assert.equal(label.textContent, 'Вакансии (researcher)');
    assert.equal(input.type, 'number');
    assert.equal(input.min, '0');
    assert.equal(input.max, '6');
    assert.equal(input.step, '0.5');
    assert.equal(input.value, '2.5');
    assert.equal(button.textContent, 'Изменить вакансии');
    assert.deepEqual(model, { count: 2.5, jobId: 'researcher' });
});

test('submits the raw input string when enabled', () =>
{
    const parent = new Element('main');
    const submitted = [];
    appendJobControls(parent, { document: createDocument(), jobId: 'miner', count: 1, disabled: false, pending: false, onSubmit: (value) => submitted.push(value) });
    const [section] = parent.children;
    const [label, button] = section.children;
    const [input] = label.children;
    input.value = ' 2.50 ';

    button.listeners.click();

    assert.deepEqual(submitted, [' 2.50 ']);
});

test('disables controls and prevents submission for disabled and pending states', () =>
{
    for (const state of [{ disabled: true, pending: false }, { disabled: false, pending: true }])
    {
        const parent = new Element('main');
        let submissions = 0;
        appendJobControls(parent, { document: createDocument(), jobId: 'farmer', count: 0, ...state, onSubmit: () => submissions++ });
        const [section] = parent.children;
        const [label, button] = section.children;
        const [input] = label.children;

        assert.equal(input.disabled, true);
        assert.equal(button.disabled, true);
        if (state.pending)
        {
            assert.equal(button.textContent, 'Ожидание ответа…');
        }
        button.listeners.click();
        assert.equal(submissions, 0);
    }
});

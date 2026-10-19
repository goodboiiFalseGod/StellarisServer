import test from 'node:test';
import assert from 'node:assert/strict';
import { selectVisibleLabelKeys } from './m3-presentation.mjs';

const row = positions => positions.map((left, index) => ({ key: `system-${index + 1}`, priority: 1, left, top: 355, right: left + 70, bottom: 375 }));

test('QA crowded galaxy row admits readable neighbors only', () =>
{
    const candidates = row([628, 659, 691, 723]);
    assert.deepEqual([...selectVisibleLabelKeys(candidates)], ['system-1', 'system-4']);
});

test('selection is stable across shuffled input and ordinal key ties', () =>
{
    const candidates = [
        { key: 'system-z', priority: 4, left: 100, top: 20, right: 170, bottom: 40 },
        { key: 'system-b', priority: 4, left: 250, top: 20, right: 320, bottom: 40 },
        { key: 'system-a', priority: 4, left: 100, top: 20, right: 170, bottom: 40 },
        { key: 'system-c', priority: 4, left: 320, top: 20, right: 390, bottom: 40 }
    ];
    const expected = ['system-a', 'system-b', 'system-c'];
    assert.deepEqual([...selectVisibleLabelKeys(candidates)], expected);
    assert.deepEqual([...selectVisibleLabelKeys([...candidates].reverse())], expected);
});

test('higher priority identity wins an overlapping rectangle', () =>
{
    const candidates = [
        { key: 'selected', priority: 10, left: 50, top: 50, right: 120, bottom: 70 },
        { key: 'hovered', priority: 100, left: 50, top: 50, right: 120, bottom: 70 },
        { key: 'ordinary', priority: 1, left: 125, top: 50, right: 195, bottom: 70 }
    ];
    assert.deepEqual([...selectVisibleLabelKeys(candidates)], ['hovered', 'ordinary']);
});

test('vertical separation and edge touching do not count as rectangle overlap', () =>
{
    const candidates = [
        { key: 'upper', priority: 2, left: 10, top: 10, right: 80, bottom: 30 },
        { key: 'below', priority: 1, left: 10, top: 30, right: 80, bottom: 50 },
        { key: 'beside', priority: 1, left: 80, top: 10, right: 150, bottom: 30 }
    ];
    assert.deepEqual([...selectVisibleLabelKeys(candidates)], ['upper', 'below', 'beside']);
});

test('zoom separation reveals labels and selection leaves candidate records unchanged', () =>
{
    const crowded = Object.freeze(row([628, 659, 691, 723]).map(candidate => Object.freeze(candidate)));
    const separated = Object.freeze(row([628, 710, 792, 874]).map(candidate => Object.freeze(candidate)));
    const crowdedBefore = structuredClone(crowded);
    const separatedBefore = structuredClone(separated);

    assert.equal(selectVisibleLabelKeys(crowded).size, 2);
    assert.equal(selectVisibleLabelKeys(separated).size, 4);
    assert.deepEqual(crowded, crowdedBefore);
    assert.deepEqual(separated, separatedBefore);
});

test('malformed and duplicate candidate identities fail clearly', () =>
{
    assert.throws(() => selectVisibleLabelKeys(null), /array/);
    assert.throws(() => selectVisibleLabelKeys([{ key: '', priority: 1, left: 0, top: 0, right: 1, bottom: 1 }]), /unique key/);
    assert.throws(() => selectVisibleLabelKeys([{ key: '  ', priority: 1, left: 0, top: 0, right: 1, bottom: 1 }]), /unique key/);
    assert.throws(() => selectVisibleLabelKeys([
        { key: 'same', priority: 1, left: 0, top: 0, right: 1, bottom: 1 },
        { key: 'same', priority: 2, left: 2, top: 0, right: 3, bottom: 1 }
    ]), /duplicate label candidate key/);
    assert.throws(() => selectVisibleLabelKeys([{ key: 'bad-priority', priority: Infinity, left: 0, top: 0, right: 1, bottom: 1 }]), /finite priority/);
    assert.throws(() => selectVisibleLabelKeys([{ key: 'bad-rectangle', priority: 1, left: 0, top: NaN, right: 1, bottom: 1 }]), /finite CSS rectangle/);
    assert.throws(() => selectVisibleLabelKeys([{ key: 'inverted-width', priority: 1, left: 2, top: 0, right: 1, bottom: 1 }]), /valid finite CSS rectangle/);
    assert.throws(() => selectVisibleLabelKeys([{ key: 'inverted-height', priority: 1, left: 0, top: 2, right: 1, bottom: 1 }]), /valid finite CSS rectangle/);
});

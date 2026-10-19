import test from 'node:test';
import assert from 'node:assert/strict';
import { selectSourceBackgroundBand } from './m3-background.mjs';

test('Source background bands preserve strict native trigger boundaries', () =>
{
    assert.equal(selectSourceBackgroundBand(0),'core');
    assert.equal(selectSourceBackgroundBand(.499999),'core');
    assert.equal(selectSourceBackgroundBand(.5),'mid');
    assert.equal(selectSourceBackgroundBand(.749999),'mid');
    assert.equal(selectSourceBackgroundBand(.75),'rim');
    assert.equal(selectSourceBackgroundBand(1),'rim');
    for(const value of [-.01,1.01,NaN,Infinity]) { assert.throws(()=>selectSourceBackgroundBand(value),/distance/); }
});

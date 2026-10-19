import test from 'node:test';
import assert from 'node:assert/strict';
import { simulationRateText } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

test('simulation rate formatter labels measured zero and nonzero throughput with count and window', () =>
{
    assert.equal(simulationRateText({ value: { status: 'measured', automaticDaysPerRealSecond: 0, completedAutomaticDays: 0, windowSeconds: 10 }, stale: false }), 'Автоматически: 0 дн./с · завершено: 0 · окно: 10 с');
    assert.equal(simulationRateText({ value: { status: 'measured', automaticDaysPerRealSecond: 1.25, completedAutomaticDays: 5, windowSeconds: 4 }, stale: false }), 'Автоматически: 1,25 дн./с · завершено: 5 · окно: 4 с');
});

test('simulation rate formatter labels missing, stale, and nonmeasured statuses without a rate', () =>
{
    assert.equal(simulationRateText({ value: null, stale: false }), 'Автоматически: нет данных');
    assert.equal(simulationRateText({ value: { status: 'measured', automaticDaysPerRealSecond: 2, completedAutomaticDays: 8, windowSeconds: 4 }, stale: true }), 'Автоматически: устарело');
    for (const [status, label] of [['warming-up', 'прогрев'], ['paused', 'пауза'], ['not-running', 'не запущено'], ['unavailable', 'недоступно']])
    {
        assert.equal(simulationRateText({ value: { status, automaticDaysPerRealSecond: null, completedAutomaticDays: 0, windowSeconds: 0 }, stale: false }), `Автоматически: ${label}`);
    }
});

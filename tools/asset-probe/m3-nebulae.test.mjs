import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createNebulaGroup } from './m3-nebulae.mjs';
import { validateSnapshot, collectViewObjects } from './m3-world-view.mjs';
import { decimalPrecisionWithin } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

const nebula = () => ({ id: 'N1', nameKey: 'Test_Nebula', center: { x: 12, y: 0, z: -34 }, radius: 60, systemIds: ['S1'] });
const snapshot = () => ({ protocol: 2, sessionId: 'nebula', sequence: 1, participants: [], spatial: {
    systems: [{ id: 'S1', galaxyPosition: { x: 12, y: 0, z: -34 }, radius: 150 }],
    bodies: [], fleets: [], hyperlanes: [], colonies: [], nebulae: [nebula()] } });

test('публичная туманность: геометрия и membership валидируются без скрытых ссылок', () => {
    assert.doesNotThrow(() => validateSnapshot(snapshot()));
    for (const change of [n => { n.center.x = NaN; }, n => { n.radius = 0; }, n => { n.systemIds = ['hidden']; }, n => { n.systemIds = ['S1', 'S1']; }]) {
        const state = snapshot(); change(state.spatial.nebulae[0]); assert.throws(() => validateSnapshot(state));
    }
    const duplicate = snapshot(); duplicate.spatial.nebulae.push(nebula()); assert.throws(() => validateSnapshot(duplicate));
    const overlap = snapshot(); overlap.spatial.nebulae.push({ ...nebula(), id: 'N2' }); assert.throws(() => validateSnapshot(overlap));
    const empty = snapshot(); empty.spatial.nebulae = []; assert.doesNotThrow(() => validateSnapshot(empty));
    assert.deepEqual(collectViewObjects(snapshot(), { kind: 'galaxy' }).map(o => o.kind), ['system']);
});

test('облако использует точные center/radius, не выбирается; owned ресурсы освобождаются', () => {
    const view = createNebulaGroup([nebula()]); const cloud = view.group.children[0];
    assert.deepEqual(cloud.position.toArray(), [12, 0, -34]);
    assert.equal(cloud.geometry.parameters.width, 120); assert.equal(cloud.geometry.parameters.height, 120);
    assert.equal(cloud.material.depthWrite, false); assert.equal(cloud.material.transparent, true);
    const hits = []; cloud.raycast({}, hits); assert.deepEqual(hits, []);
    let disposed = 0; for (const resource of view.owned) { resource.addEventListener('dispose', () => disposed++); resource.dispose(); }
    view.group.clear(); assert.equal(disposed, 2); assert.equal(view.group.children.length, 0);
    assert.equal(createNebulaGroup([]).group.children.length, 0);
});

test('UI отправляет count, подтверждает его ACK и ограничивает новым сценарием', () => {
    const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
    const body = name => client.slice(client.indexOf(`function ${name}(`), client.indexOf('\nfunction ', client.indexOf(`function ${name}(`) + 1));
    const nodes = new Map([['galaxy-scenario', { value: 'tiny' }], ['galaxy-shape', { replaceChildren() {} }], ['galaxy-density', {}], ['galaxy-nebulae', { value: '8' }]]);
    const scenario = { id: 'tiny', shapes: [], maximumNebulae: 2 };
    const run = new Function('state', '$', 'element', `${body('renderGalaxyShapes')}\nrenderGalaxyShapes();`);
    run({ galaxy: { scenarios: [scenario] } }, id => nodes.get(id), () => ({}));
    assert.equal(nodes.get('galaxy-nebulae').value, '2'); assert.equal(nodes.get('galaxy-nebulae').max, '2');
    const confirmed = new Function(`${body('confirmedGalaxySettings')}\nreturn confirmedGalaxySettings;`)();
    const requested = { galaxySeed: 'a', galaxyScenarioId: 'tiny', galaxyShapeId: 'elliptical', galaxyDensity: 1, galaxyNebulae: 0 };
    const settings = { seed: 'a', scenarioId: 'tiny', shapeId: 'elliptical', density: 1, nebulae: 0 };
    assert.equal(confirmed(requested, settings), true); assert.equal(confirmed(requested, { ...settings, nebulae: 2 }), false);
});

test('UI wormhole factor clamps to source bounds, submits and requires a matching confirmed setting', () => {
    const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
    const body = name => client.slice(client.indexOf(`function ${name}(`), client.indexOf('\nfunction ', client.indexOf(`function ${name}(`) + 1));
    const nodes = new Map([
        ['galaxy-scenario',{value:'tiny'}],['galaxy-shape',{value:'elliptical',replaceChildren(){}}],['galaxy-density',{value:'1'}],
        ['galaxy-nebulae',{value:'0'}],['galaxy-guaranteed-colonies',{value:'0'}],['galaxy-gateway-frequency',{value:'1'}],
        ['galaxy-wormhole-pairs',{value:'8'}],['galaxy-habitable-multiplier',{value:'1'}],['galaxy-seed',{value:'wormhole-test'}]
    ]);
    const scenario={id:'tiny',shapes:['elliptical'],minimumDensity:1,maximumDensity:1,maximumNebulae:0,maximumGuaranteedColonies:0,
        gatewayFrequency:{minimum:0,maximum:5,default:1},wormholePairs:{minimum:1,maximum:5,default:2}};
    const runRender=new Function('state','$','element',`${body('renderGalaxyShapes')}\nrenderGalaxyShapes();`);
    runRender({galaxy:{scenarios:[scenario]}},id=>nodes.get(id),()=>({}));
    assert.equal(nodes.get('galaxy-wormhole-pairs').value,'5');assert.equal(nodes.get('galaxy-wormhole-pairs').min,'1');assert.equal(nodes.get('galaxy-wormhole-pairs').max,'5');
    const settingsFactory=new Function('state','$','decimalPrecisionWithin',`${body('hostStartSettings')}\nreturn hostStartSettings;`);
    const settings=settingsFactory({galaxy:{scenarios:[scenario]}},id=>nodes.get(id),decimalPrecisionWithin)();
    assert.equal(settings.galaxyWormholePairs,5);
    const confirmed=new Function(`${body('confirmedGalaxySettings')}\nreturn confirmedGalaxySettings;`)();
    const server={seed:'wormhole-test',scenarioId:'tiny',shapeId:'elliptical',density:1,nebulae:0,guaranteedColonies:0,gatewayFrequency:1,habitableMultiplier:1,wormholePairs:5};
    assert.equal(confirmed(settings,server),true);assert.equal(confirmed(settings,{...server,wormholePairs:4}),false);
    nodes.get('galaxy-wormhole-pairs').value='6';assert.equal(settingsFactory({galaxy:{scenarios:[scenario]}},id=>nodes.get(id),decimalPrecisionWithin)(),null);
});

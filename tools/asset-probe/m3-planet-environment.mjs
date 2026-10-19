import { parseVisualDefinitions, requireBlock, requireScalar } from './visual-definitions.mjs';

const classPath = 'common/planet_classes/00_planet_classes.txt';
const portraitPath = 'gfx/portraits/portraits/00_portraits_main.txt';
const layerNames = ['sky', 'l01', 'l02', 'l03', 'l04'];

export const CONTINENTAL_ENVIRONMENT_PATHS = Object.freeze([
    classPath,
    portraitPath,
    ...layerNames.map(layer => `gfx/portraits/environments/pc_continental_${layer}.dds`)
]);

const fail = message => { throw new Error(`Planet environment: ${message}`); };

function requireLowerLeftZeroPosition(layer, label)
{
    if (requireScalar(layer, 'orientation') !== 'lower_left') { fail(`${label}: orientation must remain lower_left.`); }
    const position = requireBlock(layer, 'position');
    const x = requireScalar(position, 'x');
    const y = requireScalar(position, 'y');
    if (x !== 0 || y !== 0) { fail(`${label}: position must remain { x = 0 y = 0 }.`); }
}

export function parseContinentalEnvironmentMetadata(texts)
{
    if (!(texts instanceof Map)) { fail('Source text Map is required.'); }
    for (const path of [classPath, portraitPath])
    {
        if (typeof texts.get(path) !== 'string') { fail(`Missing source text: ${path}.`); }
    }

    const classes = parseVisualDefinitions(texts.get(classPath), { allowQuotedWhitespace: true });
    const continental = requireBlock(classes, 'pc_continental');
    const picture = continental.filter(entry => entry.key === 'picture');
    if (picture.length !== 0) { fail('pc_continental picture override changes environment binding.'); }

    const portraits = parseVisualDefinitions(texts.get(portraitPath), { allowQuotedWhitespace: true });
    const planet = requireBlock(portraits, 'planet');
    const size = requireBlock(planet, 'size');
    if (requireScalar(size, 'x') !== 800 || requireScalar(size, 'y') !== 400) { fail('planet portrait size must remain 800x400.'); }

    const layers = planet.filter(entry => entry.key === 'sky' || entry.key === 'environment');
    if (layers.length !== 5 || layers[0].key !== 'sky' || layers.slice(1).some(entry => entry.key !== 'environment'))
    {
        fail('Expected one sky followed by exactly four environment layers.');
    }
    layers.forEach((entry, index) => requireLowerLeftZeroPosition(entry.value, layerNames[index]));

    return new Map(layerNames.map(layer => {
        const image = `pc_continental_${layer}`;
        return [`environment.pc_continental.${layer}`, Object.freeze({
            sourcePath: `gfx/portraits/environments/${image}.dds`,
            spriteId: `local-environment:${image}`,
            frames: 1,
            frame: 1,
            border: Object.freeze({ x: 0, y: 0 })
        })];
    }));
}

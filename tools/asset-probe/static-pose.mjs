// Ограниченная статическая bind pose: tx — inverse global bind, не local transform.
const EPSILON = 1e-6;

function reject(message)
{
    throw new Error(`Static pose: ${message}`);
}

function onlyProperties(node, names)
{
    if (Object.keys(node.properties).some(name => !names.includes(name))) { reject(`неизвестный атрибут ${node.name}.`); }
}

function scalar(values, label)
{
    if (!(values instanceof Int32Array) || values.length !== 1) { reject(`неверный ${label}.`); }
    return values[0];
}

function validateInverseBind(tx)
{
    if (!(tx instanceof Float32Array) || tx.length !== 12 || tx.some(value => !Number.isFinite(value))) { reject('tx требует 12 конечных float.'); }
    // Row-major affine 3x4 только для вычислений; исходные массивы не изменяются.
    const m = [tx[0], tx[3], tx[6], tx[9], tx[1], tx[4], tx[7], tx[10], tx[2], tx[5], tx[8], tx[11]];
    const [a, b, c, , d, e, f, , g, h, i] = m;
    const det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
    if (!Number.isFinite(det) || Math.abs(det) <= 1e-12) { reject('singular inverse-bind matrix.'); }
    const inverse = [(e * i - f * h) / det, (c * h - b * i) / det, (b * f - c * e) / det, 0, (f * g - d * i) / det, (a * i - c * g) / det, (c * d - a * f) / det, 0, (d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det, 0];
    for (let row = 0; row < 3; row++) { inverse[row * 4 + 3] = -(inverse[row * 4] * m[3] + inverse[row * 4 + 1] * m[7] + inverse[row * 4 + 2] * m[11]); }
    if (inverse.some(value => !Number.isFinite(value))) { reject('нечисловая обратная bind matrix.'); }
    for (const [left, right] of [[inverse, m], [m, inverse]])
    {
        for (let row = 0; row < 3; row++)
        {
            for (let column = 0; column < 4; column++)
            {
                const value = left[row * 4] * right[column] + left[row * 4 + 1] * right[4 + column] + left[row * 4 + 2] * right[8 + column] + (column === 3 ? left[row * 4 + 3] : 0);
                if (Math.abs(value - (row === column ? 1 : 0)) > EPSILON) { reject('inverse-bind residual превышает допуск.'); }
            }
        }
    }
    return m.some((value, index) => Math.abs(value - ([0, 5, 10].includes(index) ? 1 : 0)) > EPSILON);
}

function validateSkeleton(node)
{
    onlyProperties(node, []);
    if (node.children.length === 0 || node.children.length > 256) { reject('skeleton требует 1–256 bones.'); }
    const bones = new Map();
    const names = new Set();
    let nonIdentity = false;
    for (const child of node.children)
    {
        onlyProperties(child, ['ix', 'pa', 'tx']);
        if (!child.name || names.has(child.name) || child.children.length !== 0) { reject('неверное/повторное имя bone либо вложенный bone.'); }
        names.add(child.name);
        const id = scalar(child.properties.ix, 'bone ix');
        if (id < 0 || bones.has(id)) { reject('неверный/повторный bone ix.'); }
        const parent = Object.hasOwn(child.properties, 'pa') ? scalar(child.properties.pa, 'parent') : null;
        nonIdentity = validateInverseBind(child.properties.tx) || nonIdentity;
        bones.set(id, { name: child.name, parent, tx: child.properties.tx });
    }
    if ([...bones.values()].filter(bone => bone.parent === null).length !== 1) { reject('skeleton должен иметь один root.'); }
    const done = new Set();
    const pending = new Set();
    function visit(id)
    {
        if (done.has(id)) { return; }
        if (pending.has(id)) { reject('цикл skeleton parents.'); }
        const bone = bones.get(id);
        if (!bone) { reject('parent отсутствует в skeleton.'); }
        pending.add(id);
        if (bone.parent !== null) { visit(bone.parent); }
        pending.delete(id);
        done.add(id);
    }
    for (const id of bones.keys()) { visit(id); }
    return { bones, nonIdentity };
}

function sameSkeleton(left, right)
{
    if (left.bones.size !== right.bones.size) { return false; }
    for (const [id, bone] of left.bones)
    {
        const other = right.bones.get(id);
        if (!other || bone.name !== other.name || bone.parent !== other.parent || bone.tx.some((value, i) => Math.abs(value - other.tx[i]) > EPSILON)) { return false; }
    }
    return true;
}

export function validateStaticBindPose(parsed)
{
    if (!parsed || !Array.isArray(parsed.nodes) || !Array.isArray(parsed.meshes) || parsed.meshes.length === 0) { reject('ожидался результат parsePdxMesh.'); }
    const skeletons = new Map();
    const meshNodes = new Map();
    const allSkins = new Set();
    function collect(node, parent)
    {
        if (node.name === 'skeleton') { skeletons.set(node, validateSkeleton(node)); }
        if (node.name === 'mesh')
        {
            if (meshNodes.has(node.properties.p)) { reject('неоднозначное соответствие геометрии.'); }
            meshNodes.set(node.properties.p, { node, parent });
        }
        if (node.name === 'skin') { allSkins.add(node); }
        for (const child of node.children) { collect(child, node); }
    }
    for (const node of parsed.nodes) { collect(node, null); }
    const firstSkeleton = skeletons.values().next().value;
    for (const skeleton of skeletons.values()) { if (!sameSkeleton(firstSkeleton, skeleton)) { reject('повторные skeleton не совпадают.'); } }
    const reports = [];
    const validatedSkins = new Set();
    for (const mesh of parsed.meshes)
    {
        const source = meshNodes.get(mesh.positions);
        if (!source) { reject('геометрия не соответствует parsed nodes.'); }
        const skins = source.node.children.filter(child => child.name === 'skin');
        if (skins.length > 1 || mesh.skinned !== (skins.length === 1)) { reject('неоднозначный skin/status.'); }
        if (skins.length === 0)
        {
            reports.push({ name: mesh.name, staticBindPose: false, nonIdentityInverseBind: false });
            continue;
        }
        if (!source.parent) { reject('skin не имеет shape-владельца.'); }
        const siblings = source.parent.children.filter(child => child.name === 'skeleton');
        if (siblings.length !== 1) { reject('skin требует ровно один соседний skeleton.'); }
        const skeleton = skeletons.get(siblings[0]);
        const skin = skins[0];
        onlyProperties(skin, ['bones', 'ix', 'w']);
        if (skin.children.length !== 0 || scalar(skin.properties.bones, 'skin.bones') !== 1) { reject('поддерживается только rigid skin.bones=1.'); }
        const indices = skin.properties.ix;
        const weights = skin.properties.w;
        const count = mesh.positions.length / 3;
        if (!(indices instanceof Int32Array) || !(weights instanceof Float32Array) || indices.length !== count * 4 || weights.length !== count * 4) { reject('неверная длина skin ix/w.'); }
        for (let vertex = 0; vertex < count; vertex++)
        {
            let influences = 0;
            for (let slot = 0; slot < 4; slot++)
            {
                const offset = vertex * 4 + slot;
                const weight = weights[offset];
                const id = indices[offset];
                if (!Number.isFinite(weight) || weight < 0) { reject('неверный skin weight.'); }
                if (Math.abs(weight) <= EPSILON)
                {
                    if (id !== -1 && !skeleton.bones.has(id)) { reject('нулевой weight содержит неверный bone index.'); }
                    continue;
                }
                if (Math.abs(weight - 1) > EPSILON || !skeleton.bones.has(id)) { reject('неподдерживаемый вес/активный bone index.'); }
                influences++;
            }
            if (influences !== 1) { reject('rigid vertex требует одно влияние.'); }
        }
        validatedSkins.add(skin);
        reports.push({ name: mesh.name, staticBindPose: true, nonIdentityInverseBind: skeleton.nonIdentity });
    }
    if (validatedSkins.size !== allSkins.size) { reject('обнаружен skin вне проверенной геометрии.'); }
    // Bind×inverseBind=Identity: не запекаем tx повторно и не меняем shared arrays.
    return { meshes: parsed.meshes, layers: reports, skinnedLayerCount: validatedSkins.size, skeletonCount: skeletons.size };
}

// Узкая проверка rest geometry для pc_broken: сохраняет исходные позиции и проверяет
// Иерархию, обратные bind-матрицы, индексы и нормированные веса без повторного применения tx.
export function validateStaticRestGeometry(parsed, { expectedBonesPerVertex = 2, expectedSkins = 4 } = {})
{
    if (!parsed || !Array.isArray(parsed.nodes) || !Array.isArray(parsed.meshes) || parsed.meshes.length !== expectedSkins) { reject('ожидался точный skinned rest source.'); }
    const skeletons = new Map(); const meshNodes = new Map(); const allSkins = new Set();
    function collect(node, parent)
    {
        if (node.name === 'skeleton') { skeletons.set(node, validateSkeleton(node)); }
        if (node.name === 'mesh')
        {
            if (meshNodes.has(node.properties.p)) { reject('неоднозначное соответствие geometry rest.'); }
            meshNodes.set(node.properties.p, { node, parent });
        }
        if (node.name === 'skin') { allSkins.add(node); }
        for (const child of node.children) { collect(child, node); }
    }
    parsed.nodes.forEach(node => collect(node, null));
    if (skeletons.size !== 1) { reject('rest geometry требует единственный skeleton.'); }
    const skeleton = skeletons.values().next().value; const skins = new Set();
    for (const mesh of parsed.meshes)
    {
        const source = meshNodes.get(mesh.positions);
        if (!source || !mesh.skinned || !source.parent) { reject('rest mesh не соответствует source skin.'); }
        const directSkins = source.node.children.filter(child => child.name === 'skin');
        if (directSkins.length !== 1 || source.parent.children.filter(child => child.name === 'skeleton').length !== 1) { reject('rest mesh требует один skin и соседний skeleton.'); }
        const skin = directSkins[0]; onlyProperties(skin, ['bones', 'ix', 'w']);
        if (skin.children.length || scalar(skin.properties.bones, 'skin.bones') !== expectedBonesPerVertex) { reject('изменился active influence count.'); }
        const indices = skin.properties.ix; const weights = skin.properties.w; const count = mesh.positions.length / 3;
        if (!(indices instanceof Int32Array) || !(weights instanceof Float32Array) || indices.length !== count * 4 || weights.length !== count * 4) { reject('rest skin ix/w length.'); }
        for (let vertex = 0; vertex < count; vertex++)
        {
            let influences = 0; let sum = 0;
            for (let slot = 0; slot < 4; slot++)
            {
                const offset = vertex * 4 + slot; const weight = weights[offset]; const id = indices[offset];
                if (!Number.isFinite(weight) || weight < 0) { reject('rest skin weight должен быть finite и неотрицательным.'); }
                if (weight <= EPSILON)
                {
                    if (id !== -1 && !skeleton.bones.has(id)) { reject('нулевой rest weight ссылается на неизвестную bone.'); }
                    continue;
                }
                if (!skeleton.bones.has(id)) { reject('rest weight ссылается на неизвестную bone.'); }
                influences++; sum += weight;
            }
            if (influences === 0 || influences > expectedBonesPerVertex || Math.abs(sum - 1) > EPSILON) { reject('rest vertex требует нормализованные source influences.'); }
        }
        skins.add(skin);
    }
    if (skins.size !== expectedSkins || skins.size !== allSkins.size) { reject('обнаружен skin вне проверенной rest geometry.'); }
    return { meshes: parsed.meshes, skinnedLayerCount: skins.size, skeletonCount: skeletons.size, policy: 'StaticBrokenPlanetRestGeometryV1' };
}

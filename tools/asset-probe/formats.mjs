// Ограниченные readers стенда: установка игры только читается.
const MAX_BYTES = 64 * 1024 * 1024;
const MAX_VALUES = 8 * 1024 * 1024;

function source(input)
{
    if (!(input instanceof ArrayBuffer) || input.byteLength > MAX_BYTES)
    {
        throw new Error('Ожидался ArrayBuffer размером не более 64 MiB.');
    }
    return new DataView(input);
}

export function parsePdxMesh(input, { includeCollision = false, locatorOnly = false } = {})
{
    const view = source(input);
    const bytes = new Uint8Array(input);
    let offset = 0;
    const requireBytes = count =>
    {
        if (!Number.isSafeInteger(count) || count < 0 || offset + count > bytes.length)
        {
            throw new Error(`PDX: усечённые данные на смещении ${offset}.`);
        }
    };
    const byte = () => { requireBytes(1); return bytes[offset++]; };
    const uint = () => { requireBytes(4); const value = view.getUint32(offset, true); offset += 4; return value; };
    const text = count =>
    {
        requireBytes(count);
        const end = offset + count;
        let value = '';
        while (offset < end)
        {
            const code = bytes[offset++];
            if (code > 127) { throw new Error('PDX: неподдерживаемая кодировка имени.'); }
            value += String.fromCharCode(code);
        }
        return value;
    };
    requireBytes(4);
    if (text(4) !== '@@b@') { throw new Error('PDX: неизвестная сигнатура.'); }
    const root = { name: '$root', properties: Object.create(null), children: [] };
    const stack = [root];
    let recordCount = 0;
    while (offset < bytes.length)
    {
        if (++recordCount > 100000) { throw new Error('PDX: превышен лимит записей.'); }
        const token = byte();
        if (token === 91)
        {
            let depth = 1;
            while (offset < bytes.length && bytes[offset] === 91) { byte(); depth++; }
            if (depth > 32 || depth > stack.length) { throw new Error('PDX: недопустимая глубина узла.'); }
            let name = '';
            while (true)
            {
                const code = byte();
                if (code === 0) { break; }
                if (code > 127 || name.length >= 1024) { throw new Error('PDX: недопустимое имя узла.'); }
                name += String.fromCharCode(code);
            }
            if (!name) { throw new Error('PDX: пустое имя узла.'); }
            const node = { name, properties: Object.create(null), children: [] };
            stack.length = depth;
            stack[depth - 1].children.push(node);
            stack.push(node);
        }
        else if (token === 33)
        {
            const name = text(byte());
            const kind = String.fromCharCode(byte());
            const count = uint();
            if (!name || count > MAX_VALUES) { throw new Error('PDX: недопустимый размер свойства.'); }
            const target = stack.at(-1).properties;
            if (Object.hasOwn(target, name)) { throw new Error(`PDX: повтор свойства ${name}.`); }
            let values;
            if (kind === 'i' || kind === 'f')
            {
                requireBytes(count * 4);
                values = kind === 'f' ? new Float32Array(count) : new Int32Array(count);
                for (let i = 0; i < count; i++, offset += 4)
                {
                    values[i] = kind === 'f' ? view.getFloat32(offset, true) : view.getInt32(offset, true);
                    if (!Number.isFinite(values[i])) { throw new Error(`PDX: NaN/Infinity в ${name}.`); }
                }
            }
            else if (kind === 's')
            {
                if (count > 1024) { throw new Error('PDX: слишком много строк.'); }
                values = [];
                for (let i = 0; i < count; i++)
                {
                    const length = uint();
                    if (length < 1 || length > 4096) { throw new Error('PDX: недопустимая длина строки.'); }
                    const value = text(length);
                    if (!value.endsWith('\0') || value.slice(0, -1).includes('\0')) { throw new Error('PDX: повреждённая строка.'); }
                    values.push(value.slice(0, -1));
                }
            }
            else { throw new Error(`PDX: неизвестный тип ${kind}.`); }
            target[name] = values;
        }
        else { throw new Error(`PDX: неизвестный token ${token} на ${offset - 1}.`); }
    }
    const version = root.properties.pdxasset;
    if (!(version instanceof Int32Array) || version.length !== 2 || version[0] !== 1 || version[1] !== 0)
    {
        throw new Error('PDX: поддерживается только pdxasset 1.0.');
    }
    if (locatorOnly)
    {
        if (Object.keys(root.properties).length !== 1 || root.children.length !== 1 || root.children[0].name !== 'locator')
        {
            throw new Error('PDX locator-only: допустим только контейнер locator без geometry/skin.');
        }
        const container = root.children[0];
        if (Object.keys(container.properties).length !== 0 || container.children.length !== 1 || container.children[0].name !== 'part1')
        {
            throw new Error('PDX locator-only: ожидается единственный locator part1 без дублей.');
        }
        const node = container.children[0];
        const position = node.properties.p;
        const quaternion = node.properties.q;
        if (node.children.length !== 0 || Object.keys(node.properties).length !== 2 || !(position instanceof Float32Array) || position.length !== 3 || !(quaternion instanceof Float32Array) || quaternion.length !== 4)
        {
            throw new Error('PDX locator-only: part1 требует только p float3/q float4 без geometry/skin.');
        }
        const magnitude = Math.hypot(...quaternion);
        if (!Number.isFinite(magnitude) || magnitude === 0) { throw new Error('PDX locator-only: quaternion должен быть ненулевым и нормализуемым.'); }
        const locators = [{ name: node.name, position: Array.from(position), quaternion: Array.from(quaternion, value => value / magnitude) }];
        return { version: Array.from(version), nodes: root.children, meshes: [], skippedCollision: 0, unsupportedFeatures: [], locators };
    }
    const meshes = [];
    const unsupported = new Set();
    let skippedCollision = 0;
    function visit(node, objectName)
    {
        if (node.name === 'skin' || node.name === 'skeleton') { unsupported.add(node.name); }
        if (node.name === 'mesh')
        {
            const properties = node.properties;
            const positions = properties.p;
            const indices = properties.tri;
            if (!(positions instanceof Float32Array) || positions.length === 0 || positions.length % 3 !== 0 || !(indices instanceof Int32Array) || indices.length === 0 || indices.length % 3 !== 0)
            {
                throw new Error('PDX: mesh требует p float3 и tri int3.');
            }
            const vertexCount = positions.length / 3;
            for (const index of indices)
            {
                if (index < 0 || index >= vertexCount) { throw new Error('PDX: индекс вне массива вершин.'); }
            }
            for (const [key, stride] of [['n', 3], ['ta', 4], ['u0', 2]])
            {
                if (properties[key] && (!(properties[key] instanceof Float32Array) || properties[key].length !== vertexCount * stride))
                {
                    throw new Error(`PDX: неверный размер атрибута ${key}.`);
                }
            }
            const materialNodes = node.children.filter(child => child.name === 'material');
            if (materialNodes.length !== 1) { throw new Error('PDX: ожидается один material.'); }
            const material = Object.fromEntries(Object.entries(materialNodes[0].properties).map(([key, value]) => [key, Array.isArray(value) && value.length === 1 ? value[0] : value]));
            if (typeof material.shader !== 'string') { throw new Error('PDX: material без shader.'); }
            const collision = material.shader === 'Collision';
            if (collision && !includeCollision) { skippedCollision++; }
            else
            {
                meshes.push({ name: objectName, positions, normals: properties.n ?? null, tangents: properties.ta ?? null, uv: properties.u0 ?? null, indices: Uint32Array.from(indices), material, collision, skinned: node.children.some(child => child.name === 'skin') });
            }
        }
        for (const child of node.children) { visit(child, node.name === 'object' ? child.name : objectName); }
    }
    visit(root, null);
    if (meshes.length === 0) { throw new Error('PDX: нет отображаемых meshes.'); }
    return { version: Array.from(version), nodes: root.children, meshes, skippedCollision, unsupportedFeatures: [...unsupported] };
}

function inspectDdsLayout(input, { allowBgraMipChain = false } = {})
{
    const view = source(input);
    if (view.byteLength < 128 || view.getUint32(0, true) !== 0x20534444 || view.getUint32(4, true) !== 124 || view.getUint32(76, true) !== 32)
    {
        throw new Error('DDS: повреждённый заголовок.');
    }
    const height = view.getUint32(12, true);
    const width = view.getUint32(16, true);
    const mipCount = Math.max(1, view.getUint32(28, true));
    if (!width || !height || width > 8192 || height > 8192 || width * height > 16 * 1024 * 1024 || mipCount > Math.floor(Math.log2(Math.max(width, height))) + 1)
    {
        throw new Error('DDS: недопустимые размеры или mip count.');
    }
    const caps2 = view.getUint32(112, true);
    const isCubemap = caps2 === 0xfe00;
    if ((caps2 !== 0 && !isCubemap) || view.getUint32(24, true) > 1) { throw new Error('DDS: неподдерживаемый cubemap/volume CAPS2/depth.'); }
    if (isCubemap && width !== height) { throw new Error('DDS: cubemap требует квадратные faces.'); }
    const faceCount = isCubemap ? 6 : 1;
    if (view.getUint32(80, true) === 0x41)
    {
        if (isCubemap) { throw new Error('DDS: BGRA8 cubemap не поддерживается.'); }
        if (view.getUint32(84, true) !== 0 || view.getUint32(88, true) !== 32 || [0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000].some((mask, i) => view.getUint32(92 + i * 4, true) !== mask))
        {
            throw new Error('DDS: неподдерживаемый BGRA8 layout/masks.');
        }
        if (mipCount !== 1 && !allowBgraMipChain) { throw new Error('DDS: BGRA8 поддерживает только один mip.'); }
        const flags = view.getUint32(8, true);
        const hasPitch = (flags & 8) !== 0;
        const hasLinearSize = (flags & 0x80000) !== 0;
        if (hasPitch === hasLinearSize) { throw new Error('DDS: BGRA8 требует ровно один flag pitch/linear-size.'); }
        const rowBytes = width * 4;
        const declaredSize = view.getUint32(20, true);
        const rowPitch = hasPitch ? declaredSize : rowBytes;
        let payloadBytes = rowPitch * height;
        if (mipCount > 1)
        {
            if (rowPitch !== rowBytes) { throw new Error('DDS: BGRA8 mip chain требует tightly packed rows.'); }
            for (let mip = 1; mip < mipCount; mip++) { payloadBytes += Math.max(1, width >> mip) * Math.max(1, height >> mip) * 4; }
        }
        if (rowPitch < rowBytes || (hasLinearSize && declaredSize !== rowBytes * height) || view.byteLength !== 128 + payloadBytes)
        {
            throw new Error('DDS: длина BGRA8 payload/pitch не совпадает с заголовком.');
        }
        return { width, height, codec: 'BGRA8', mipCount, faceCount, decodedBytes: rowBytes * height, isCubemap, rowPitch };
    }
    if (view.getUint32(80, true) === 0x40 && view.getUint32(84, true) === 0 && view.getUint32(88, true) === 24 &&
        view.getUint32(92, true) === 0x00ff0000 && view.getUint32(96, true) === 0x0000ff00 && view.getUint32(100, true) === 0x000000ff && view.getUint32(104, true) === 0)
    {
        if (isCubemap || mipCount !== 1) { throw new Error('DDS: RGB24 поддерживает только одну 2D mip level.'); }
        const flags=view.getUint32(8,true),hasPitch=(flags&8)!==0,hasLinearSize=(flags&0x80000)!==0;
        if(hasPitch===hasLinearSize) { throw new Error('DDS: RGB24 требует ровно один flag pitch/linear-size.'); }
        const rowBytes=width*3,declaredSize=view.getUint32(20,true),rowPitch=hasPitch?declaredSize:rowBytes;
        if(rowPitch!==rowBytes||hasLinearSize&&declaredSize!==rowBytes*height||view.byteLength!==128+rowPitch*height) { throw new Error('DDS: длина RGB24 payload/pitch не совпадает с заголовком.'); }
        return {width,height,codec:'RGB24',mipCount,faceCount,decodedBytes:width*height*4,isCubemap:false,rowPitch};
    }
    const codec = String.fromCharCode(view.getUint8(84), view.getUint8(85), view.getUint8(86), view.getUint8(87));
    if (!(view.getUint32(80, true) & 4) || !['DXT1', 'DXT3', 'DXT5'].includes(codec)) { throw new Error(`DDS: неизвестный codec ${codec}.`); }
    const blockSize = codec === 'DXT1' ? 8 : 16;
    let faceStride = 0;
    let mipWidth = width;
    let mipHeight = height;
    for (let mip = 0; mip < mipCount; mip++)
    {
        faceStride += Math.ceil(mipWidth / 4) * Math.ceil(mipHeight / 4) * blockSize;
        mipWidth = Math.max(1, mipWidth >> 1);
        mipHeight = Math.max(1, mipHeight >> 1);
    }
    if (128 + faceStride * faceCount !== view.byteLength) { throw new Error('DDS: длина mip-цепочки не совпадает с заголовком.'); }
    return { width, height, codec, mipCount, faceCount, decodedBytes: width * height * 4 * faceCount, isCubemap, faceStride, blockSize };
}

function selectDdsLod(layout, options)
{
    if (options === null || typeof options !== 'object' || Array.isArray(options) || Object.keys(options).some(key => !['mipLevel', 'downsampleFactor', 'allowBgraMipChain'].includes(key)) || options.allowBgraMipChain !== undefined && typeof options.allowBgraMipChain !== 'boolean') { throw new Error('DDS: invalid LOD options.'); }
    const { mipLevel = 0, downsampleFactor = 1 } = options;
    if (!Number.isInteger(mipLevel) || mipLevel < 0 || mipLevel >= layout.mipCount || !Number.isInteger(downsampleFactor) || downsampleFactor < 1 || downsampleFactor > 8192 || (downsampleFactor & (downsampleFactor - 1)) !== 0) { throw new Error('DDS: invalid LOD mipLevel/downsampleFactor.'); }
    const nondefault = mipLevel !== 0 || downsampleFactor !== 1;
    if (nondefault && layout.codec === 'BGRA8' && (!options.allowBgraMipChain || downsampleFactor !== 1)) { throw new Error('DDS: BGRA8 LOD requires an explicit stored mip chain.'); }
    if (!layout.isCubemap && downsampleFactor !== 1) { throw new Error('DDS: 2D LOD uses stored mips only.'); }
    const selectedWidth = Math.max(1, layout.width >> mipLevel);
    const selectedHeight = Math.max(1, layout.height >> mipLevel);
    if (selectedWidth % downsampleFactor !== 0 || selectedHeight % downsampleFactor !== 0) { throw new Error('DDS: LOD downsampleFactor must divide the selected mip dimensions.'); }
    let mipOffset = 0;
    for (let mip = 0; mip < mipLevel; mip++) { mipOffset += layout.codec === 'BGRA8' ? Math.max(1, layout.width >> mip) * Math.max(1, layout.height >> mip) * 4 : Math.ceil(Math.max(1, layout.width >> mip) / 4) * Math.ceil(Math.max(1, layout.height >> mip) / 4) * layout.blockSize; }
    const width = selectedWidth / downsampleFactor;
    const height = selectedHeight / downsampleFactor;
    return { ...layout, width, height, rowPitch: layout.codec === 'BGRA8' && mipLevel > 0 ? width * 4 : layout.rowPitch, selectedWidth, selectedHeight, mipOffset, mipLevel, downsampleFactor, nondefault, sourceWidth: layout.width, sourceHeight: layout.height, decodedBytes: width * height * 4 * layout.faceCount };
}

// Validate the entire source payload before selecting LOD or allocating output pixels.
export function inspectDds(input, options = {})
{
    const layout = selectDdsLod(inspectDdsLayout(input, options ?? {}), options);
    const { width, height, codec, mipCount, faceCount, decodedBytes, isCubemap } = layout;
    return { width, height, codec, mipCount, faceCount, decodedBytes, isCubemap, ...ddsLodMetadata(layout) };
}

function ddsLodMetadata(layout)
{
    return layout.nondefault ? { sourceWidth: layout.sourceWidth, sourceHeight: layout.sourceHeight, downsampleFactor: layout.downsampleFactor, decodedMip: layout.mipLevel } : {};
}

export function decodeDds(input, options = {})
{
    const layout = selectDdsLod(inspectDdsLayout(input, options ?? {}), options);
    const { width, height, codec, mipCount, isCubemap, rowPitch } = layout;
    const view = source(input);
    if (codec === 'BGRA8')
    {
        const rowBytes = width * 4;
        const rgba = new Uint8Array(rowBytes * height);
        for (let y = 0; y < height; y++)
        {
            for (let x = 0; x < width; x++)
            {
                const offset = 128 + layout.mipOffset + y * rowPitch + x * 4;
                const target = y * rowBytes + x * 4;
                rgba[target] = view.getUint8(offset + 2);
                rgba[target + 1] = view.getUint8(offset + 1);
                rgba[target + 2] = view.getUint8(offset);
                rgba[target + 3] = view.getUint8(offset + 3);
            }
        }
        return { width, height, rgba, codec: 'BGRA8', mipCount, decodedMip: layout.mipLevel, ...ddsLodMetadata(layout) };
    }
    if(codec==='RGB24')
    {
        const rgba=new Uint8Array(width*height*4); let sourceOffset=128+layout.mipOffset;
        for(let y=0;y<height;y++) for(let x=0;x<width;x++)
        {
            const target=(y*width+x)*4; const blue=view.getUint8(sourceOffset++); const green=view.getUint8(sourceOffset++); const red=view.getUint8(sourceOffset++); rgba[target]=red; rgba[target+1]=green; rgba[target+2]=blue; rgba[target+3]=255;
        }
        return {width,height,rgba,codec:'RGB24',mipCount,decodedMip:layout.mipLevel,...ddsLodMetadata(layout)};
    }
    if (isCubemap)
    {
        const faces = Array.from({ length: 6 }, (_, face) => decodeDdsBlocks(view, layout, 128 + face * layout.faceStride + layout.mipOffset));
        return { width, height, faces, codec, mipCount, decodedMip: layout.mipLevel, isCubemap: true, ...ddsLodMetadata(layout) };
    }
    return { width, height, rgba: decodeDdsBlocks(view, layout, 128 + layout.mipOffset), codec, mipCount, decodedMip: layout.mipLevel, ...ddsLodMetadata(layout) };
}

function decodeDdsBlocks(view, { width, height, codec, blockSize, selectedWidth = width, selectedHeight = height, downsampleFactor = 1 }, baseOffset)
{
    const rgba = new Uint8Array(width * height * 4);
    const rgb565 = word =>
    {
        const red = (word >> 11) & 31;
        const green = (word >> 5) & 63;
        const blue = word & 31;
        return [(red << 3) | (red >> 2), (green << 2) | (green >> 4), (blue << 3) | (blue >> 2), 255];
    };
    let offset = baseOffset;
    for (let by = 0; by < selectedHeight; by += 4)
    {
        for (let bx = 0; bx < selectedWidth; bx += 4, offset += blockSize)
        {
            const colorOffset = offset + (codec === 'DXT1' ? 0 : 8);
            const c0 = view.getUint16(colorOffset, true);
            const c1 = view.getUint16(colorOffset + 2, true);
            const colors = [rgb565(c0), rgb565(c1)];
            if (c0 > c1 || codec !== 'DXT1')
            {
                colors.push(colors[0].map((value, i) => i === 3 ? 255 : Math.floor((2 * value + colors[1][i]) / 3)));
                colors.push(colors[0].map((value, i) => i === 3 ? 255 : Math.floor((value + 2 * colors[1][i]) / 3)));
            }
            else
            {
                colors.push(colors[0].map((value, i) => i === 3 ? 255 : Math.floor((value + colors[1][i]) / 2)));
                colors.push([0, 0, 0, 0]);
            }
            const colorBits = view.getUint32(colorOffset + 4, true);
            let alphaBits = 0;
            let alphaPalette;
            if (codec === 'DXT5')
            {
                const a0 = view.getUint8(offset);
                const a1 = view.getUint8(offset + 1);
                alphaPalette = [a0, a1];
                if (a0 > a1)
                {
                    for (let i = 1; i <= 6; i++) { alphaPalette.push(Math.floor(((7 - i) * a0 + i * a1) / 7)); }
                }
                else
                {
                    for (let i = 1; i <= 4; i++) { alphaPalette.push(Math.floor(((5 - i) * a0 + i * a1) / 5)); }
                    alphaPalette.push(0, 255);
                }
                for (let i = 0; i < 6; i++) { alphaBits += view.getUint8(offset + 2 + i) * 2 ** (8 * i); }
            }
            for (let pixel = 0; pixel < 16; pixel++)
            {
                const x = bx + pixel % 4;
                const y = by + Math.floor(pixel / 4);
                if (x >= selectedWidth || y >= selectedHeight || x % downsampleFactor !== Math.floor(downsampleFactor / 2) || y % downsampleFactor !== Math.floor(downsampleFactor / 2)) { continue; }
                const color = colors[(colorBits >>> (2 * pixel)) & 3];
                const target = (Math.floor(y / downsampleFactor) * width + Math.floor(x / downsampleFactor)) * 4;
                rgba.set(color, target);
                if (codec === 'DXT3') { rgba[target + 3] = ((view.getUint8(offset + Math.floor(pixel / 2)) >> (4 * (pixel % 2))) & 15) * 17; }
                if (codec === 'DXT5') { rgba[target + 3] = alphaPalette[Math.floor(alphaBits / 2 ** (3 * pixel)) % 8]; }
            }
        }
    }
    return rgba;
}

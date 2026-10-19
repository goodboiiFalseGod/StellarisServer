import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { parsePdxMesh, decodeDds, inspectDds } from './formats.mjs';

const arrayBuffer = value => value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength);

test('2D stored mip: offset/rectangular dimensions/full payload без downsample', () =>
{
    const data = Buffer.alloc(128 + 32 + 8 + 8);
    data.write('DDS ',0); data.writeUInt32LE(124,4); data.writeUInt32LE(4,12); data.writeUInt32LE(8,16);
    data.writeUInt32LE(3,28); data.writeUInt32LE(32,76); data.writeUInt32LE(4,80); data.write('DXT1',84);
    for (const [offset,color] of [[128,0xf800],[136,0xf800],[144,0xf800],[152,0xf800],[160,0x07e0],[168,0x001f]]) { data.writeUInt16LE(color,offset); }
    // 8x4 base содержит 2 blocks (16 bytes), затем 4x2 и 2x1 по8 bytes.
    const source = data.subarray(0,160); source.writeUInt16LE(0x07e0,144); source.writeUInt16LE(0x001f,152);
    const mip = decodeDds(arrayBuffer(source),{mipLevel:1});
    assert.equal(mip.width,4); assert.equal(mip.height,2); assert.equal(mip.decodedMip,1);
    assert.deepEqual([...mip.rgba.slice(0,4)],[0,255,0,255]);
    assert.deepEqual([...decodeDds(arrayBuffer(source),{mipLevel:2}).rgba.slice(0,4)],[0,0,255,255]);
    assert.throws(()=>decodeDds(arrayBuffer(source.subarray(0,-1)),{mipLevel:1}),/mip/);
    assert.throws(()=>decodeDds(arrayBuffer(source),{downsampleFactor:2}),/stored mips/);
});

test('BGRA8 stored mip chain: explicit opt-in/full layout, alpha и mipoffset', () =>
{
    const base = Buffer.from(bgraDds({width:4,height:2,mipCount:3}));
    const data = Buffer.concat([base,Buffer.from([10,20,30,40,50,60,70,80]),Buffer.from([90,100,110,120])]);
    assert.throws(()=>inspectDds(arrayBuffer(data)),/один mip/);
    const mip = decodeDds(arrayBuffer(data),{mipLevel:1,allowBgraMipChain:true});
    assert.equal(mip.width,2); assert.equal(mip.height,1); assert.equal(mip.mipCount,3);
    assert.deepEqual([...mip.rgba],[30,20,10,40,70,60,50,80]);
    assert.deepEqual([...decodeDds(arrayBuffer(data),{mipLevel:2,allowBgraMipChain:true}).rgba],[110,100,90,120]);
    assert.throws(()=>inspectDds(arrayBuffer(data.subarray(0,-1)),{allowBgraMipChain:true}),/payload/);
    assert.throws(()=>inspectDds(withDdsField(arrayBuffer(data),20,20),{allowBgraMipChain:true}),/tightly packed/);
});
const u32 = value => { const buffer = Buffer.alloc(4); buffer.writeUInt32LE(value); return buffer; };
function property(name, kind, values)
{
    const prefix = Buffer.concat([Buffer.from([33, name.length]), Buffer.from(name), Buffer.from(kind), u32(values.length)]);
    if (kind === 's') { return Buffer.concat([prefix, ...values.flatMap(value => [u32(value.length + 1), Buffer.from(value + '\0')])]); }
    const data = Buffer.alloc(values.length * 4);
    values.forEach((value, i) => kind === 'f' ? data.writeFloatLE(value, i * 4) : data.writeInt32LE(value, i * 4));
    return Buffer.concat([prefix, data]);
}
const node = (depth, name) => Buffer.from('['.repeat(depth) + name + '\0');
function mesh({ index = 2, coordinate = 0, shader = 'PdxMeshShip' } = {})
{
    return arrayBuffer(Buffer.concat([
        Buffer.from('@@b@'), property('pdxasset', 'i', [1, 0]), node(1, 'object'), node(2, 'ship'), node(3, 'mesh'),
        property('p', 'f', [coordinate, 0, 0, 1, 0, 0, 0, 1, 0]), property('n', 'f', [0, 0, 1, 0, 0, 1, 0, 0, 1]),
        property('ta', 'f', [1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1]), property('u0', 'f', [0, 0, 1, 0, 0, 1]),
        property('tri', 'i', [0, 1, index]), node(4, 'material'), property('shader', 's', [shader]), property('diff', 's', ['diffuse.dds'])
    ]));
}
function dds(codec = 'DXT5', { width = 4, height = 4, c0 = 0xf800, c1 = 0x07e0, colorBits = 0, alpha0 = 255, alpha1 = 0, alphaBits = 0 } = {})
{
    const data = Buffer.alloc(128 + (codec === 'DXT1' ? 8 : 16));
    data.write('DDS ', 0); data.writeUInt32LE(124, 4); data.writeUInt32LE(height, 12); data.writeUInt32LE(width, 16);
    data.writeUInt32LE(1, 28); data.writeUInt32LE(32, 76); data.writeUInt32LE(4, 80); data.write(codec, 84);
    const offset = codec === 'DXT1' ? 128 : 136;
    data.writeUInt16LE(c0, offset); data.writeUInt16LE(c1, offset + 2); data.writeUInt32LE(colorBits, offset + 4);
    if (codec === 'DXT5')
    {
        data[128] = alpha0; data[129] = alpha1;
        for (let i = 0; i < 6; i++) { data[130 + i] = Math.floor(alphaBits / 2 ** (8 * i)) % 256; }
    }
    if (codec === 'DXT3') { data.fill(0x88, 128, 136); }
    return arrayBuffer(data);
}

function bgraDds({ width = 2, height = 2, padding = 0, linearSize = false, mipCount = 0 } = {})
{
    const pitch = width * 4 + padding;
    const data = Buffer.alloc(128 + pitch * height, 0);
    data.write('DDS ', 0); data.writeUInt32LE(124, 4); data.writeUInt32LE(linearSize ? 0x81007 : 0x100f, 8);
    data.writeUInt32LE(height, 12); data.writeUInt32LE(width, 16); data.writeUInt32LE(linearSize ? pitch * height : pitch, 20);
    data.writeUInt32LE(mipCount, 28); data.writeUInt32LE(32, 76); data.writeUInt32LE(0x41, 80); data.writeUInt32LE(32, 88);
    [0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000].forEach((mask, i) => data.writeUInt32LE(mask, 92 + i * 4));
    for (let y = 0; y < height; y++)
    {
        for (let x = 0; x < width; x++) { data.set([10 + x, 20 + y, 100 + x + y, (x + y) * 60], 128 + y * pitch + x * 4); }
        data.fill(0xee, 128 + y * pitch + width * 4, 128 + (y + 1) * pitch);
    }
    return arrayBuffer(data);
}

function withDdsField(input, offset, value)
{
    const copy = input.slice(0);
    new DataView(copy).setUint32(offset, value, true);
    return copy;
}

function cubeDds(codec = 'DXT1', mipCount = 1)
{
    const block = new Uint8Array(dds(codec));
    const stride = (codec === 'DXT1' ? 8 : 16) * mipCount;
    const bytes = new Uint8Array(128 + stride * 6);
    bytes.set(block.subarray(0, 128));
    const view = new DataView(bytes.buffer);
    view.setUint32(112, 0xfe00, true); view.setUint32(28, mipCount, true);
    for (let face = 0; face < 6; face++)
    {
        for (let mip = 0; mip < mipCount; mip++) { bytes.set(block.subarray(128), 128 + face * stride + mip * (stride / mipCount)); }
        const offset = 128 + face * stride + (codec === 'DXT1' ? 0 : 8);
        view.setUint16(offset, [0xf800, 0x07e0, 0x001f, 0xffff, 0xffe0, 0xf81f][face], true);
        view.setUint16(offset + 2, 0, true);
        // Каждый ряд хранится в исходном порядке: 0,1,2,3 palette indices.
        view.setUint32(offset + 4, 0xffaa5500, true);
    }
    return bytes.buffer;
}

test('DDS cube: шесть distinct faces, порядок +X/-X/+Y/-Y/+Z/-Z, строки без flip', () =>
{
    for (const codec of ['DXT1', 'DXT3', 'DXT5'])
    {
        const input = cubeDds(codec, 3);
        assert.deepEqual(inspectDds(input), { width: 4, height: 4, codec, mipCount: 3, faceCount: 6, decodedBytes: 384, isCubemap: true });
        const image = decodeDds(input);
        assert.equal(image.rgba, undefined); assert.equal(image.faces.length, 6);
        assert.equal(new Set(image.faces.map(face => face.buffer)).size, 6);
        const colors = [[255,0,0], [0,255,0], [0,0,255], [255,255,255], [255,255,0], [255,0,255]];
        image.faces.forEach((face, i) =>
        {
            assert.deepEqual([...face.slice(0,3)], colors[i]);
            assert.deepEqual([...face.slice(16,19)], [0,0,0]);
            assert.deepEqual([...face.slice(48,51)], colors[i].map(value => Math.floor(value / 3)));
        });
    }
});

test('DDS inspect: malformed cube caps/layout/mips/payload отвергаются до decode', () =>
{
    const input = cubeDds();
    for (const caps of [0x200, 0xfc00, 0xfe01, 0x200000, 0x200 | 0x400])
    {
        assert.throws(() => inspectDds(withDdsField(input, 112, caps)), /cubemap\/volume/);
    }
    for (const [offset, value] of [[24, 2], [12, 3], [28, 4], [80, 0], [16, 8193]])
    {
        assert.throws(() => inspectDds(withDdsField(input, offset, value)), /DDS/);
    }
    assert.throws(() => inspectDds(input.slice(0,-1)), /mip/);
    const trailing = new Uint8Array(input.byteLength + 1); trailing.set(new Uint8Array(input));
    assert.throws(() => inspectDds(trailing.buffer), /mip/);
    assert.throws(() => inspectDds(withDdsField(bgraDds(), 112, 0xfe00)), /BGRA8/);
    const dx10 = input.slice(0); new Uint8Array(dx10).set(Buffer.from('DX10'),84);
    assert.throws(() => inspectDds(dx10), /codec/);
    assert.equal(inspectDds(dds()).decodedBytes, 64);
    assert.equal(inspectDds(bgraDds({ padding: 4 })).decodedBytes, 16);
});

test('Реальные star DDS: whole layout и base-only cube decode', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    for (const [name, codec, faces, mips] of [['lava_noise', 'DXT1', 6, 1], ['lava_d', 'DXT5', 1, 11], ['stone_d', 'DXT1', 1, 11]])
    {
        const input = arrayBuffer(await readFile(resolve(process.env.STELLARIS_GAME_ROOT, 'gfx/worldgfx', name + '.dds')));
        assert.deepEqual(inspectDds(input), { width: 1024, height: 1024, codec, mipCount: mips, faceCount: faces, decodedBytes: 1024 * 1024 * 4 * faces, isCubemap: faces === 6 });
        const result = decodeDds(input);
        assert.equal(faces === 6 ? result.faces.reduce((sum, face) => sum + face.length, 0) : result.rgba.length, inspectDds(input).decodedBytes);
    }
});

function lodCube(codec = 'DXT5')
{
    const size = codec === 'DXT1' ? 8 : 16;
    const stride = 7 * size; // 8x8, 4x4, 2x2, 1x1.
    const bytes = new Uint8Array(128 + 6 * stride);
    bytes.set(new Uint8Array(dds(codec)).subarray(0,128));
    const view = new DataView(bytes.buffer);
    view.setUint32(12,8,true); view.setUint32(16,8,true); view.setUint32(28,4,true); view.setUint32(112,0xfe00,true);
    for (let face=0;face<6;face++)
    {
        let offset=128+face*stride;
        for(let mip=0;mip<4;mip++)
        {
            const blocks=mip===0?4:1;
            for(let block=0;block<blocks;block++,offset+=size)
            {
                const co=offset+(codec==='DXT1'?0:8);
                view.setUint16(co,[0xf800,0x07e0,0x001f,0xffff,0xffe0,0xf81f][(face+mip+block)%6],true);
                let directionalBits=0;
                for(let pixel=0;pixel<16;pixel++){directionalBits+=((pixel%4+Math.floor(pixel/4))%4)*2**(2*pixel);}
                view.setUint16(co+2,0,true); view.setUint32(co+4,directionalBits>>>0,true);
                if(codec==='DXT5'){bytes[offset]=200-mip*30-face;bytes[offset+1]=20;}
                if(codec==='DXT3'){bytes.fill(0x95,offset,offset+8);}
            }
        }
    }
    return bytes.buffer;
}

test('Cube LOD: source mip offsets per face and direct nearest-center sampling preserve RGBA', () =>
{
    for(const codec of ['DXT1','DXT3','DXT5'])
    {
        const input=lodCube(codec);
        for(let mipLevel=0;mipLevel<4;mipLevel++)
        {
            const source=decodeDds(input,{mipLevel});
            for(const downsampleFactor of [1,2,4,8].filter(f=>source.width%f===0))
            {
                const result=decodeDds(input,{mipLevel,downsampleFactor});
                assert.equal(result.decodedMip,mipLevel);
                assert.equal(result.width,8/2**mipLevel/downsampleFactor);
                result.faces.forEach((face,i)=>
                {
                    for(let y=0;y<result.height;y++)for(let x=0;x<result.width;x++)
                    {
                        const p=((y*downsampleFactor+Math.floor(downsampleFactor/2))*source.width+x*downsampleFactor+Math.floor(downsampleFactor/2))*4;
                        assert.deepEqual(face.slice((y*result.width+x)*4,(y*result.width+x)*4+4),source.faces[i].slice(p,p+4));
                    }
                });
            }
            // Mip-specific palette proves that face stride did not skip into a neighboring face/base mip.
            const expected=[[255,0,0],[0,255,0],[0,0,255],[255,255,255],[255,255,0],[255,0,255]];
            source.faces.forEach((face,index)=>assert.deepEqual([...face.slice(0,3)],expected[(index+mipLevel)%6]));
        }
    }
});

test('Cube LOD: invalid options and complete payload checks precede output allocation', () =>
{
    const input=lodCube();
    for(const options of [null,[],{unknown:1},{mipLevel:-1},{mipLevel:4},{mipLevel:0.5},{mipLevel:NaN},{downsampleFactor:0},{downsampleFactor:3},{downsampleFactor:16},{downsampleFactor:Infinity},{downsampleFactor:'2'}])
    {
        assert.throws(()=>inspectDds(input,options),/LOD/);
        assert.throws(()=>decodeDds(input,options),/LOD/);
    }
    assert.throws(()=>decodeDds(dds(),{downsampleFactor:2}),/stored mips/);
    assert.throws(()=>decodeDds(bgraDds(),{downsampleFactor:2}),/stored mip chain/);
    assert.throws(()=>inspectDds(input.slice(0,-1),null),/mip/);
    assert.throws(()=>decodeDds(input.slice(0,-1),{mipLevel:3}),/mip/);
    const Native=globalThis.Uint8Array,allocations=[];
    globalThis.Uint8Array=new Proxy(Native,{construct(Target,args){allocations.push(args[0]);return new Target(...args);}});
    try
    {
        const info=inspectDds(input,{mipLevel:1,downsampleFactor:2});
        assert.equal(info.decodedBytes,96); assert.equal(info.sourceWidth,8); assert.equal(info.width,2);
        assert.deepEqual(allocations,[]);
        decodeDds(input,{mipLevel:1,downsampleFactor:2});
        assert.deepEqual(allocations,[16,16,16,16,16,16]);
    }
    finally {globalThis.Uint8Array=Native;}
});

test('Real presentation cube LOD: stars mip1 and sky_core stride2 allocate only six 1024 faces', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    for(const [path,options] of [['gfx/worldgfx/stars.dds',{mipLevel:1}],['gfx/map/sky_core.dds',{downsampleFactor:2}]])
    {
        const input=arrayBuffer(await readFile(resolve(process.env.STELLARIS_GAME_ROOT,path)));
        const info=inspectDds(input,options);
        assert.equal(info.width,1024);assert.equal(info.height,1024);assert.equal(info.decodedBytes,25165824);
        assert.equal(info.sourceWidth,2048);assert.equal(info.sourceHeight,2048);
        const Native=globalThis.Uint8Array,allocations=[];
        globalThis.Uint8Array=new Proxy(Native,{construct(Target,args){allocations.push(args[0]);return new Target(...args);}});
        try
        {
            const image=decodeDds(input,options);
            assert.equal(image.decodedMip,options.mipLevel??0);
            assert.equal(image.downsampleFactor,options.downsampleFactor??1);
            assert.equal(image.faces.length,6);
            assert.deepEqual(allocations,Array(6).fill(4194304));
        }
        finally {globalThis.Uint8Array=Native;}
    }
});

test('PDX: атрибуты, material, source names и индексы', () =>
{
    const result = parsePdxMesh(mesh());
    assert.equal(result.meshes.length, 1);
    const item = result.meshes[0];
    assert.equal(item.name, 'ship');
    assert.equal(item.positions.length, 9);
    assert.equal(item.normals.length, 9);
    assert.equal(item.tangents.length, 12);
    assert.equal(item.uv.length, 6);
    assert.deepEqual([...item.indices], [0, 1, 2]);
    assert.equal(item.material.diff, 'diffuse.dds');
    assert.equal(item.skinned, false);
    assert.deepEqual(result.unsupportedFeatures, []);
});

test('PDX: Collision не отображается по умолчанию', () =>
{
    assert.throws(() => parsePdxMesh(mesh({ shader: 'Collision' })), /нет отображаемых/);
    assert.equal(parsePdxMesh(mesh({ shader: 'Collision' }), { includeCollision: true }).meshes[0].collision, true);
});

test('PDX: повреждения, неизвестные tokens/types, bounds и NaN отвергаются', () =>
{
    assert.throws(() => parsePdxMesh(new ArrayBuffer(3)), /усечённые/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.from('BAD!'))), /сигнатура/);
    assert.throws(() => parsePdxMesh(mesh().slice(0, -1)), /усечённые/);
    assert.throws(() => parsePdxMesh(mesh({ index: 3 })), /индекс/);
    assert.throws(() => parsePdxMesh(mesh({ index: -1 })), /индекс/);
    assert.throws(() => parsePdxMesh(mesh({ coordinate: NaN })), /NaN/);
    assert.throws(() => parsePdxMesh(mesh({ coordinate: Infinity })), /NaN/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.from('@@b@X'))), /token/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.concat([Buffer.from('@@b@'), Buffer.from([33, 1, 112, 120]), u32(1)]))), /тип/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.from('@@b@[[bad\0'))), /глубина/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.from('@@b@[\0'))), /пустое/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.concat([Buffer.from('@@b@'), property('pdxasset', 'i', [2, 0])]))), /pdxasset/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.concat([Buffer.from('@@b@'), property('pdxasset', 'i', [1, 0]), property('pdxasset', 'i', [1, 0])]))), /повтор/);
    assert.throws(() => parsePdxMesh(arrayBuffer(Buffer.concat([Buffer.from('@@b@'), Buffer.from([33, 1, 112, 102]), u32(0xffffffff)]))), /размер/);
});

test('DDS: DXT1 RGB565 и прозрачная четвёртая палитра', () =>
{
    assert.deepEqual([...decodeDds(dds('DXT1')).rgba.slice(0, 4)], [255, 0, 0, 255]);
    assert.deepEqual([...decodeDds(dds('DXT1', { c0: 0, c1: 0xffff, colorBits: 3 })).rgba.slice(0, 4)], [0, 0, 0, 0]);
});

test('DDS: DXT3 explicit alpha; DXT5 обе интерполяции и последние 48-bit indices', () =>
{
    assert.equal(decodeDds(dds('DXT3')).rgba[3], 136);
    assert.equal(decodeDds(dds('DXT5', { alpha0: 255, alpha1: 0, alphaBits: 2 })).rgba[3], 218);
    assert.equal(decodeDds(dds('DXT5', { alpha0: 0, alpha1: 255, alphaBits: 6 })).rgba[3], 0);
    assert.equal(decodeDds(dds('DXT5', { alpha0: 0, alpha1: 255, alphaBits: 7 * 2 ** 45 })).rgba[63], 255);
    assert.equal(decodeDds(dds('DXT5', { c0: 0, c1: 0xffff, colorBits: 3 })).rgba[3], 255);
});

test('DDS: partial edge block и сохранение raw channels', () =>
{
    const result = decodeDds(dds('DXT5', { width: 3, height: 2, alpha0: 31 }));
    assert.equal(result.rgba.length, 24);
    assert.deepEqual([...result.rgba.slice(-4)], [255, 0, 0, 31]);
    assert.equal(result.codec, 'DXT5');
    assert.equal(result.decodedMip, 0);
});

test('DDS: неизвестные форматы, усечение, cubemap и размеры отвергаются', () =>
{
    assert.throws(() => decodeDds(new ArrayBuffer(127)), /заголовок/);
    assert.throws(() => decodeDds(dds('DX10')), /codec/);
    assert.throws(() => decodeDds(dds().slice(0, -1)), /mip/);
    assert.throws(() => decodeDds(dds('DXT5', { width: 0 })), /размеры/);
    const cube = dds(); new DataView(cube).setUint32(112, 512, true);
    assert.throws(() => decodeDds(cube), /cubemap/);
    const badMips = dds(); new DataView(badMips).setUint32(28, 10, true);
    assert.throws(() => decodeDds(badMips), /mip/);
    const badFlags = dds(); new DataView(badFlags).setUint32(80, 0, true);
    assert.throws(() => decodeDds(badFlags), /codec/);
    const volume = dds(); new DataView(volume).setUint32(24, 4, true);
    assert.throws(() => decodeDds(volume), /volume/);
});

test('DDS BGRA8: pitch/padding и linear-size, порядок каналов и непреумноженная alpha', () =>
{
    for (const options of [{}, { padding: 3 }, { linearSize: true }, { mipCount: 1 }])
    {
        const result = decodeDds(bgraDds(options));
        assert.equal(result.width, 2);
        assert.equal(result.height, 2);
        assert.equal(result.codec, 'BGRA8');
        assert.equal(result.mipCount, 1);
        assert.equal(result.decodedMip, 0);
        assert.deepEqual([...result.rgba], [100, 20, 10, 0, 101, 20, 11, 60, 101, 21, 10, 60, 102, 21, 11, 120]);
    }
});

test('DDS BGRA8: неизвестные masks, bit-depth, FourCC и pixel flags отвергаются', () =>
{
    for (const offset of [92, 96, 100, 104]) { assert.throws(() => decodeDds(withDdsField(bgraDds(), offset, 0)), /layout\/masks/); }
    assert.throws(() => decodeDds(withDdsField(bgraDds(), 88, 24)), /layout\/masks/);
    assert.throws(() => decodeDds(withDdsField(bgraDds(), 84, 1)), /layout\/masks/);
    for (const flags of [0, 0x40, 0x45, 0xc1]) { assert.throws(() => decodeDds(withDdsField(bgraDds(), 80, flags)), /codec/); }
});

test('DDS BGRA8: mip chains, flags, неверный pitch/linear-size и длина отвергаются', () =>
{
    assert.throws(() => decodeDds(bgraDds({ mipCount: 2 })), /один mip/);
    for (const flags of [0x1007, 0x8100f]) { assert.throws(() => decodeDds(withDdsField(bgraDds(), 8, flags)), /ровно один flag/); }
    for (const pitch of [0, 7, 0xffffffff]) { assert.throws(() => decodeDds(withDdsField(bgraDds(), 20, pitch)), /payload\/pitch/); }
    assert.throws(() => decodeDds(withDdsField(bgraDds({ linearSize: true }), 20, 15)), /payload\/pitch/);
    assert.throws(() => decodeDds(bgraDds({ linearSize: true, padding: 4 })), /payload\/pitch/);
    assert.throws(() => decodeDds(bgraDds().slice(0, -1)), /payload\/pitch/);
    const trailing = Buffer.concat([Buffer.from(bgraDds()), Buffer.from([0])]);
    assert.throws(() => decodeDds(arrayBuffer(trailing)), /payload\/pitch/);
    assert.throws(() => decodeDds(withDdsField(bgraDds(), 112, 512)), /cubemap/);
    assert.throws(() => decodeDds(withDdsField(bgraDds(), 24, 4)), /volume/);
    assert.throws(() => decodeDds(withDdsField(bgraDds(), 16, 8193)), /размеры/);
    assert.throws(() => decodeDds(withDdsField(withDdsField(bgraDds(), 16, 8192), 12, 8192)), /размеры/);
});

test('Реальный контент: все пять UI DDS M3 BGRA8', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const fixtures = [
        ['gfx/interface/icons/resources/energy.dds', 18, 18],
        ['gfx/interface/icons/resources/minerals.dds', 18, 18],
        ['gfx/interface/icons/resources/food.dds', 18, 18],
        ['gfx/interface/icons/fleet_size_icon.dds', 19, 19],
        ['gfx/interface/icons/planet.dds', 33, 23]
    ];
    for (const [file, width, height] of fixtures)
    {
        const data = await readFile(resolve(process.env.STELLARIS_GAME_ROOT, file));
        const result = decodeDds(arrayBuffer(data));
        assert.equal(result.codec, 'BGRA8');
        assert.equal(result.width, width);
        assert.equal(result.height, height);
        assert.equal(result.mipCount, 1);
        assert.equal(result.rgba.length, width * height * 4);
        assert.ok(result.rgba.some((value, i) => i % 4 === 3 && value < 255));
        for (let pixel = 0; pixel < width * height; pixel++)
        {
            const offset = pixel * 4;
            assert.deepEqual([...result.rgba.slice(offset, offset + 4)], [data[128 + offset + 2], data[128 + offset + 1], data[128 + offset], data[128 + offset + 3]]);
        }
    }
});

test('Реальный контент: корвет, каналы DDS, planet/star/Arkship metadata', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const files = [
        'gfx/models/ships/mammalian_01/mammalian_01_corvette_S1.mesh',
        ...['diffuse', 'normal', 'specular'].map(channel => `gfx/models/ships/mammalian_01/mammalian_01_corvette_${channel}.dds`),
        ...['diffuse', 'normals', 'spec'].map(channel => `gfx/models/planets/continental_01_${channel}.dds`),
        ...['diffuse', 'normal', 'specular'].map(channel => `gfx/models/ships/other/civilian_arkship_01_${channel}.dds`),
        'gfx/models/planets/planet_clouded.mesh', 'gfx/models/planets/base_star.mesh', 'gfx/models/ships/other/civilian_arkship_01_stage_1.mesh'
    ];
    for (const file of files)
    {
        const data = await readFile(resolve(process.env.STELLARIS_GAME_ROOT, file));
        const result = file.endsWith('.dds') ? decodeDds(arrayBuffer(data)) : parsePdxMesh(arrayBuffer(data));
        if (file.endsWith('corvette_S1.mesh'))
        {
            assert.equal(result.meshes.length, 1);
            assert.equal(result.meshes[0].positions.length / 3, 7156);
            assert.equal(result.meshes[0].indices.length / 3, 6644);
            assert.equal(result.skippedCollision, 1);
            assert.deepEqual(result.unsupportedFeatures, []);
        }
        if (file.endsWith('.dds'))
        {
            const corvette = file.includes('corvette');
            assert.equal(result.width, corvette ? 1024 : 2048);
            assert.equal(result.height, file.includes('civilian_arkship') ? 2048 : 1024);
            assert.equal(result.codec, file.endsWith('continental_01_diffuse.dds') ? 'DXT1' : 'DXT5');
            assert.equal(result.mipCount, corvette ? 11 : 12);
        }
        if (file.endsWith('planet_clouded.mesh') || file.endsWith('base_star.mesh') || file.endsWith('civilian_arkship_01_stage_1.mesh'))
        {
            assert.ok(result.unsupportedFeatures.includes('skin'));
            assert.ok(result.unsupportedFeatures.includes('skeleton'));
        }
        console.log(JSON.stringify({ file, bytes: data.length, sha256: createHash('sha256').update(data).digest('hex'), ...(result.meshes ? { meshes: result.meshes.length, unsupportedFeatures: result.unsupportedFeatures } : { width: result.width, height: result.height, codec: result.codec }) }));
    }
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { normalRrxg, specularApprox, centeredPositions, createDdsCubeTexture } from './adapters.mjs';
import * as THREE from 'three';
import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';

test('Cube adapter: borrowed buffers, explicit sampler settings, invalid input', () => {
  const faces = Array.from({length:6}, (_,i) => new Uint8Array(16).fill(i));
  const texture = createDdsCubeTexture({width:2,height:2,faces,isCubemap:true});
  assert.ok(texture.isCubeTexture); assert.equal(texture.image.length,6);
  texture.image.forEach((face,i) => { assert.ok(face.isDataTexture); assert.equal(face.image.data,faces[i]); assert.equal(face.flipY,false); });
  assert.equal(texture.flipY,false); assert.equal(texture.colorSpace,THREE.NoColorSpace);
  assert.equal(texture.generateMipmaps,false); assert.equal(texture.minFilter,THREE.LinearFilter);
  assert.equal(texture.magFilter,THREE.LinearFilter); assert.equal(texture.wrapS,THREE.ClampToEdgeWrapping);
  assert.equal(texture.wrapT,THREE.ClampToEdgeWrapping);
  assert.throws(() => createDdsCubeTexture({width:2,height:2,faces:faces.slice(1),isCubemap:true}), /шестигранный/);
  assert.throws(() => createDdsCubeTexture({width:2,height:3,faces,isCubemap:true}), /шестигранный/);
  assert.throws(() => createDdsCubeTexture({width:2,height:2,faces:faces.map(() => new Uint8Array(15)),isCubemap:true}), /шестигранный/);
  texture.dispose(); texture.image.forEach(face => face.dispose());
  assert.equal(faces[5].byteLength,16);
});

test('Cube adapter rejects fully sparse, partially sparse and inherited faces', () => {
  const dense = Array.from({length:6}, () => new Uint8Array(16));
  const partial = dense.slice(); delete partial[3];
  const inherited = dense.slice(); delete inherited[2];
  Object.setPrototypeOf(inherited, Object.assign(Object.create(Array.prototype), {2:new Uint8Array(16)}));
  for (const faces of [Array(6), partial, inherited]) {
    assert.throws(() => createDdsCubeTexture({width:2,height:2,faces,isCubemap:true}), /шестигранный/);
  }
});

test('Cube directions: WebGL +X/-X/+Y/-Y/+Z/-Z и unflipped rows; Three DataTexture upload convention', async () => {
  // Reference для прямого textureCube(noise, direction), не envMap с дополнительным flipEnvMap.
  const faces = Array.from({length:6}, (_,face) => Uint8Array.from(Array.from({length:16},(_,i) => i%4===0 ? face*10+Math.floor(i/4) : 255)));
  const texture = createDdsCubeTexture({width:2,height:2,faces,isCubemap:true});
  const sample = ([x,y,z]) => {
    const ax=Math.abs(x),ay=Math.abs(y),az=Math.abs(z); let face,s,t,major;
    if(ax>ay && ax>az){face=x>0?0:1;s=x>0?-z:z;t=-y;major=ax;}
    else if(ay>az){face=y>0?2:3;s=x;t=y>0?z:-z;major=ay;}
    else{face=z>0?4:5;s=z>0?x:-x;t=-y;major=az;}
    const col=Math.floor((s/major+1)*0.5*2),row=Math.floor((t/major+1)*0.5*2);
    return texture.image[face].image.data[(row*2+col)*4];
  };
  const centers=[[1,.1,.1],[-1,.1,.1],[.1,1,.1],[.1,-1,.1],[.1,.1,1],[.1,.1,-1]];
  assert.deepEqual(centers.map(sample),[0,11,23,31,41,50]);
  // Off-axis направления фиксируют обе координаты каждой face, а не только её цвет.
  const topLeft=[[1,.5,.5],[-1,.5,-.5],[-.5,1,-.5],[-.5,-1,.5],[-.5,.5,1],[.5,.5,-1]];
  assert.deepEqual(topLeft.map(sample),[0,10,20,30,40,50]);
  assert.deepEqual(topLeft.map(v=>v.map((n,i)=>Math.abs(n)===1?n:-n)).map(sample),[3,13,23,33,43,53]);
  const require=createRequire(import.meta.url);
  const upload=await readFile(new URL('../src/renderers/webgl/WebGLTextures.js', 'file:///'+require.resolve('three').replaceAll('\\','/')), 'utf8');
  assert.match(upload,/isDataTexture \? texture\.image\[ i \]\.image/);
  assert.match(upload,/_gl\.TEXTURE_CUBE_MAP_POSITIVE_X \+ i[^\n]*cubeImage\[ i \]\.data/);
  texture.dispose(); texture.image.forEach(face=>face.dispose());
});

test('Normal: X из G, инвертированный Y из A; B не подменяет normal', () => {
  const first = normalRrxg({ rgba: Uint8Array.from([0, 255, 0, 128]) }).rgba;
  const second = normalRrxg({ rgba: Uint8Array.from([255, 255, 255, 128]) }).rgba;
  assert.deepEqual(first, second); assert.equal(first[0], 255); assert.equal(first[1], 127); assert.equal(first[3], 255);
});
test('Spec: используется G²*.4, не цветовая маска R и не metalness B', () => {
  assert.deepEqual([...specularApprox({ rgba: Uint8Array.from([0, 255, 0, 255]) }).rgba], [102, 102, 102, 255]);
  assert.deepEqual([...specularApprox({ rgba: Uint8Array.from([255, 0, 255, 0]) }).rgba], [0, 0, 0, 255]);
});
test('Центрирование сохраняет относительные координаты и отвергает вырожденный bbox', () => {
  assert.deepEqual([...centeredPositions({ positions: Float32Array.from([2, 0, 0, 6, 0, 0]) })], [-1, 0, 0, 1, 0, 0]);
  assert.throws(() => centeredPositions({ positions: Float32Array.from([1, 1, 1]) }), /нулевой/);
});

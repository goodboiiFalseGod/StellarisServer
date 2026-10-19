import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import {createGeneratedMaterial} from './generated-world-material.mjs';

test('Emission mask только PlanetEmissive; Gas/Molten frozen flow/spec не создают fake emission',()=>
{
    for(const shader of ['PdxMeshPlanetEmissive','PdxMeshGasGiant','PdxMeshMoltenPlanet'])
    {
        const calls=[]; const textures=[];
        const material=createGeneratedMaterial({shader,diff:'d',n:'n',spec:'s'},(path,mode)=>
        {
            calls.push([path,mode]); const value=new THREE.DataTexture(new Uint8Array([128,128,255,255]),1,1); textures.push(value); return value;
        });
        const program={uniforms:{},fragmentShader:'#include <opaque_fragment>'}; material.onBeforeCompile(program);
        if(shader==='PdxMeshPlanetEmissive')
        {
            assert.ok(calls.some(([,mode])=>mode==='sourceEmission')); assert.ok(program.uniforms.sourceEmission);
            assert.match(program.fragmentShader,/sourceEmission,vMapUv/); assert.ok(material.normalMap);
        }
        else
        {
            assert.ok(!calls.some(([,mode])=>mode==='sourceEmission'||mode==='normal'));
            assert.equal(program.uniforms.sourceEmission,undefined); assert.equal(program.fragmentShader,'#include <opaque_fragment>');
            assert.equal(material.normalMap,null); assert.ok(material.map); assert.ok(material.specularMap);
        }
        material.dispose(); textures.forEach(texture=>texture.dispose());
    }
});

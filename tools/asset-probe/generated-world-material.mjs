import * as THREE from 'three';

export function sourceHsvRgb(hsv)
{
    if(!Array.isArray(hsv)||hsv.length!==3||hsv.some(v=>!Number.isFinite(v))||hsv[1]<0||hsv[1]>1||hsv[2]<0) { throw new Error('Generated source HSV.'); }
    const h=((hsv[0]%1)+1)%1*6,s=hsv[1],v=hsv[2],i=Math.floor(h),f=h-i,p=v*(1-s),q=v*(1-f*s),t=v*(1-(1-f)*s);
    return [[v,t,p],[q,v,p],[p,v,t],[p,q,v],[t,p,v],[v,p,q]][i%6];
}

export function findAtmosphereStarLayers(sample)
{
    const surface=sample?.meshes?.find(mesh=>mesh.material.shader==='PdxMeshStar'||mesh.material.shader==='PdxMeshBlackHole');
    const atmosphere=sample?.meshes?.find(mesh=>mesh.material.shader==='PdxMeshAtmosphereStar');
    return surface&&atmosphere?{surface,atmosphere}:null;
}

// Статическая адаптация: только PlanetEmissive использует source normal.b mask.
// Flow/UV time=0; native HDR/environment/CPU atmosphere defaults не заявляются.
export function createGeneratedMaterial(source, texture)
{
    const shader=source?.shader;
    if(shader==='PdxMeshPlanetRings')
    {
        const map=texture(source.diff,'diffuse'),normalMap=texture(source.n,'normal'),specularMap=texture(source.spec,'specular');
        for(const item of [map,normalMap,specularMap]) { item.wrapS=item.wrapT=THREE.RepeatWrapping; }
        return new THREE.MeshPhongMaterial({map,normalMap,specularMap,specular:0xffffff,shininess:30,transparent:true,blending:THREE.NormalBlending,depthWrite:false,side:THREE.DoubleSide});
    }
    if(['PdxMeshAlphaAdditiveAnimateUV','PdxMeshAlphaAdditiveAnimateUVNoDissolve'].includes(shader))
    {
        return new THREE.MeshBasicMaterial({map:texture(source.diff,'diffuse'),transparent:true,blending:THREE.AdditiveBlending,depthWrite:false,side:THREE.DoubleSide});
    }
    // Статический профиль чёрной дыры не воспроизводит линзирование, искажение и анимацию.
    if(shader==='PdxMeshBlackHole')
    {
        return new THREE.MeshPhongMaterial({map:texture(source.diff,'diffuse'),normalMap:texture(source.n,'normal'),specularMap:texture(source.spec,'specular'),specular:0xffffff,shininess:30,side:THREE.DoubleSide});
    }
    // StaticBrokenPlanetRestGeometryV1: отображаем source-текстуры terra без событий разрушения.
    if(shader==='PdxMeshTerra')
    {
        return new THREE.MeshPhongMaterial({map:texture(source.diff,'diffuse'),normalMap:texture(source.n,'normal'),specularMap:texture(source.spec,'specular'),specular:0xffffff,shininess:30,side:THREE.DoubleSide});
    }
    if(['PdxMeshBlackHoleBillboard','PdxMeshAlphaAdditive'].includes(shader))
    {
        return new THREE.MeshBasicMaterial({map:texture(source.diff,'diffuse'),transparent:true,blending:THREE.AdditiveBlending,depthWrite:false,side:THREE.DoubleSide});
    }
    if(!['PdxMeshPlanetEmissive','PdxMeshGasGiant','PdxMeshMoltenPlanet'].includes(shader)) { throw new Error('Generated material: неподдержанный source shader.'); }
    const flow=shader!=='PdxMeshPlanetEmissive';
    const material=new THREE.MeshPhongMaterial({map:texture(source.diff,'diffuse'),normalMap:flow?null:texture(source.n,'normal'),specularMap:texture(source.spec,'specular'),specular:0xffffff,shininess:30,side:THREE.DoubleSide});
    // Gas/Molten: согласованный frozen flow/spec adapter без выдуманного emission mix.
    if(flow) { return material; }
    const emission=texture(source.n,'sourceEmission');
    material.customProgramCacheKey=()=> 'generated-source-emission-v1';
    material.onBeforeCompile=program=>
    {
        if(!program.fragmentShader.includes('#include <opaque_fragment>')) { throw new Error('Generated material: неподдержанная Three shader revision.'); }
        program.uniforms.sourceEmission={value:emission};
        program.fragmentShader='uniform sampler2D sourceEmission;\n'+program.fragmentShader.replace('#include <opaque_fragment>',
            'outgoingLight=mix(outgoingLight,diffuseColor.rgb,clamp(texture2D(sourceEmission,vMapUv).b,0.0,1.0));\n#include <opaque_fragment>');
    };
    return material;
}

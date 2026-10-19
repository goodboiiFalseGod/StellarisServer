import * as THREE from 'three';

export function selectSourceBackgroundBand(distanceToCorePercent)
{
    if(!Number.isFinite(distanceToCorePercent)||distanceToCorePercent<0||distanceToCorePercent>1) { throw new Error('M3: source background distance must be finite within [0,1].'); }
    return distanceToCorePercent<.5?'core':distanceToCorePercent<.75?'mid':'rim';
}

export function createBackgroundParameters(background, texture)
{
    if (!texture?.isCubeTexture || texture.colorSpace !== THREE.NoColorSpace || !['linear-rgb', 'ycocg'].includes(background?.encoding) || !Array.isArray(background.hsvShift) || background.hsvShift.length !== 3 || !Array.from(background.hsvShift).every(Number.isFinite)) { throw new Error('M3: invalid presentation background.'); }
    return {
        uniforms: { sky: { value: texture }, packedYcocg: { value: background.encoding === 'ycocg' }, hsvShift: { value: new THREE.Vector3(...background.hsvShift) } },
        vertexShader: `varying vec3 direction;
            void main(){ direction=position; vec4 p=projectionMatrix*vec4(mat3(viewMatrix)*position,1.0); gl_Position=p.xyww; }`,
        fragmentShader: `uniform samplerCube sky; uniform bool packedYcocg; uniform vec3 hsvShift; varying vec3 direction;
            vec3 toHsv(vec3 c){ float hi=max(c.r,max(c.g,c.b)); float lo=min(c.r,min(c.g,c.b)); float d=hi-lo; float h=0.0; float s=0.0;
            if(d!=0.0){s=d/hi;if(hi==c.r){h=(c.g-c.b)/d+6.0;}else if(hi==c.g){h=(c.b-c.r)/d+2.0;}else{h=(c.r-c.g)/d+4.0;}}return vec3(h,s,hi); }
            vec3 fromHsv(vec3 c){vec3 h=clamp(abs(mod(c.x+vec3(0.0,4.0,2.0),6.0)-3.0)-1.0,0.0,1.0);return clamp((h-1.0)*c.y*c.z+c.z,0.0,1.0);}
            void main(){vec4 raw=textureCube(sky,normalize(direction));vec3 rgb=raw.rgb;
            if(packedYcocg){float scale=raw.b*(255.0/8.0)+1.0;float co=(raw.r-128.0/255.0)/scale;float cg=(raw.g-128.0/255.0)/scale;
            rgb=pow(abs(vec3(raw.a+co-cg,raw.a+cg,raw.a-co-cg)),vec3(2.2));}
            vec3 hsv=toHsv(rgb);hsv.x=mod(6.0+hsv.x+hsvShift.x*6.0,6.0);hsv.yz=clamp(hsv.yz*(vec2(1.0)+hsvShift.yz),0.0,1.0);
            gl_FragColor=vec4(fromHsv(hsv),1.0);
            #include <tonemapping_fragment>
            #include <colorspace_fragment>
            }`,
        side: THREE.BackSide, depthTest: false, depthWrite: false
    };
}

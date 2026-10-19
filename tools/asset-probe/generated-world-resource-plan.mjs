export const GENERATED_MEMORY_LIMIT = 512 * 1024 * 1024;

export function gpuResourceReservation(resources)
{
    const faces=new Set(resources.textures.filter(t=>t.isCubeTexture).flatMap(t=>t.images));
    let bytes=0;
    function imageBytes(image,mips)
    {
        if(!image||!Number.isSafeInteger(image.width)||!Number.isSafeInteger(image.height)||image.width<1||image.height<1) { throw new Error('GPU reservation: неверные texture dimensions.'); }
        let w=image.width,h=image.height,total=0;
        do { total+=w*h*4; if(!mips||w===1&&h===1)break; w=Math.max(1,w>>1);h=Math.max(1,h>>1); } while(true);
        return total;
    }
    for(const texture of new Set(resources.textures))
    {
        if(faces.has(texture)) { continue; }
        bytes+=texture.isCubeTexture?texture.images.reduce((n,face)=>n+imageBytes(face.image,texture.generateMipmaps),0):imageBytes(texture.image,texture.generateMipmaps);
    }
    const attributes=new Set();
    for(const geometry of resources.geometries) { Object.values(geometry.attributes).forEach(a=>attributes.add(a)); if(geometry.index)attributes.add(geometry.index); }
    for(const attribute of attributes) { bytes+=attribute.array.byteLength; }
    if(!Number.isSafeInteger(bytes)) { throw new Error('GPU reservation overflow.'); }
    return bytes;
}

export function requireGeneratedMemory(candidate,retained={})
{
    const values=[candidate.decodedBytes,candidate.convertedBytes,retained.decodedBytes??0,retained.convertedBytes??0];
    if(values.some(v=>!Number.isSafeInteger(v)||v<0)||values.reduce((a,b)=>a+b,0)>GENERATED_MEMORY_LIMIT) { throw new Error('Generated profile: общий CPU budget old+staged >512MiB.'); }
}

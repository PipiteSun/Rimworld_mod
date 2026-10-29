// Mechanical sprite-sheet extraction and registration on vanilla apparel canvases.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/25390/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const {bounds,separateSheet}=require('../../OccupationalArmor/Source/ImportTextures.cjs');
const mod=path.resolve(__dirname,'..'),game=path.resolve(mod,'../..');
const source=path.join(mod,'ArtSource/MoltenGuard'),out=path.join(mod,'Textures/FireGiant/MoltenGuard');
const ref=path.join(game,'output/ClassArmors/References');
async function canvas(sprite,box,size,destination){
 const fitted=await sharp(sprite).resize(box.width,box.height,{fit:'fill'}).png().toBuffer();
 await sharp({create:{width:size,height:size,channels:4,background:'#00000000'}}).composite([{input:fitted,left:box.left,top:box.top}]).png().toFile(destination);
}
(async()=>{
 fs.mkdirSync(source,{recursive:true});fs.mkdirSync(path.join(out,'Worn'),{recursive:true});
 const manifest=JSON.parse(fs.readFileSync(path.join(mod,'ArtSource/MoltenGuard-prompts.json')));
 for(const item of manifest.assets){
  const saved=path.join(source,item.name+'.png');if(!fs.existsSync(saved))fs.copyFileSync(item.source,saved);
  if(!(await sharp(saved).metadata()).hasAlpha)throw Error('Source lacks alpha: '+item.name);
  if(item.name==='Maul'){await sharp(saved).resize(256,256).png().toFile(path.join(out,'Maul.png'));continue;}
  const sprites=await separateSheet(saved,item.name,'fire-giant');
  await canvas(sprites[0],{left:10,top:8,width:108,height:112},128,path.join(out,item.name+'.png'));
  for(const [i,dir] of ['south','east','north'].entries()){
   if(item.name==='Helmet'){
    const box=await bounds(path.join(ref,'CataphractHelmet_'+dir+'.png'));
    await canvas(sprites[i+1],box,128,path.join(out,'Worn','Helmet_'+dir+'.png'));
   }else for(const body of ['Male','Female','Thin','Fat','Hulk']){
    const native=JSON.parse(fs.readFileSync(path.join(source,'PlateArmorBounds.json')))[body+'_'+dir];
    const box=Object.fromEntries(Object.entries(native).map(([key,value])=>[key,value*2]));
    await canvas(sprites[i+1],box,256,path.join(out,'Worn','Armor_'+body+'_'+dir+'.png'));
   }
  }
 }
 const icons=await Promise.all(['Maul','Helmet','Armor'].map(async(name)=>({input:await sharp(path.join(out,name+'.png')).resize(240,240,{fit:'inside'}).png().toBuffer(),left:20+['Maul','Helmet','Armor'].indexOf(name)*270,top:20})));
 await sharp({create:{width:820,height:280,channels:4,background:'#353b3e'}}).composite(icons).png().toFile(path.join(source,'Preview.png'));
 console.log('Imported 3 equipment icons and 18 worn directional textures with native alignment.');
})().catch(e=>{console.error(e);process.exitCode=1;});

const fs=require('fs'),path=require('path');const {chromium}=require('playwright');
const OUT=process.argv[2];const FONT=path.resolve(__dirname,'../fonts');
const D='#2B2140',O='#F28C28',B='#2F6FB5',G='#7BD389',P='#FFF4DE';
const L=(w=10)=>`stroke="${D}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round"`;
const star=(cx,cy,r,ri,fill,w=8)=>{let p=[];for(let i=0;i<10;i++){const a=-Math.PI/2+i*Math.PI/5,rr=i%2?ri:r;p.push((cx+rr*Math.cos(a)).toFixed(1)+','+(cy+rr*Math.sin(a)).toFixed(1))}return `<polygon points="${p.join(' ')}" fill="${fill}" ${L(w)}/>`};
const spark=(x,y,s,f='#FFE27A')=>`<path transform="translate(${x},${y}) scale(${s})" d="M0,-10 L3,-3 10,0 3,3 0,10 -3,3 -10,0 -3,-3Z" fill="${f}" stroke="${D}" stroke-width="${2.5/s}" stroke-linejoin="round"/>`;
const sword=(x,y,r,s=1)=>`<g transform="translate(${x},${y}) rotate(${r}) scale(${s})"><path d="M-12,40 L-12,-90 L0,-112 L12,-90 L12,40Z" fill="#E4E8EE" ${L()}/><path d="M0,-96 L0,30" stroke="#B8C0CC" stroke-width="5"/><rect x="-40" y="38" width="80" height="18" rx="9" fill="${O}" ${L()}/><rect x="-10" y="56" width="20" height="40" rx="7" fill="#7A4B2A" ${L()}/><circle cy="104" r="12" fill="${O}" ${L()}/></g>`;
global.L=L;global.spark=spark;global.star=star;global.sword=sword;Object.assign(global,{D,O,B,G,P});
const I={
frugal:`<path d="M128,22 C128,22 52,116 52,160 C52,204 86,236 128,236 C170,236 204,204 204,160 C204,116 128,22 128,22Z" fill="#4FA8E8" ${L()}/><path d="M84,160 C84,134 96,114 110,98" stroke="#fff" stroke-width="12" stroke-linecap="round" fill="none"/>
<g transform="translate(192,186)"><circle r="42" fill="${G}" ${L()}/><path d="M0,-22 L0,22 M-18,6 L0,24 L18,6" stroke="${D}" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" fill="none"/></g>`,
heavyHand:`<path d="M56,110 C56,84 74,70 96,70 L156,70 C190,70 214,92 214,128 C214,168 186,206 142,206 L108,206 C78,206 56,186 56,156Z" fill="#F0C8A0" ${L()}/>
<path d="M96,70 L96,46 C96,34 106,26 116,26 C126,26 136,34 136,46 L136,70 M136,70 L136,40 C136,28 146,20 156,20 C166,20 176,28 176,40 L176,72 M176,72 L176,50 C176,38 186,30 196,30 C206,30 214,38 214,50 L214,100" fill="#F0C8A0" ${L()}/>
<path d="M56,120 L22,96 C14,90 14,78 22,72 C30,66 40,68 46,74 L74,100" fill="#F0C8A0" ${L()}/>
<path d="M60,206 L212,206 L220,236 L52,236Z" fill="${O}" ${L()}/>
<path d="M226,150 L250,150 M226,176 L246,182 M226,124 L246,118" stroke="${D}" stroke-width="10" stroke-linecap="round"/>${spark(234,96,1.6)}`,
toughBody:`<path d="M128,214 C60,166 30,130 30,96 C30,62 56,42 82,42 C102,42 118,54 128,72 C138,54 154,42 174,42 C200,42 226,62 226,96 C226,130 196,166 128,214Z" fill="#E8504A" ${L()}/><path d="M66,82 C70,68 80,62 92,62" stroke="#fff" stroke-width="10" fill="none" stroke-linecap="round" opacity=".8"/><g transform="translate(176,170)"><circle r="38" fill="${G}" ${L()}/><path d="M0,-20 L0,20 M-20,0 L20,0" stroke="${D}" stroke-width="12" stroke-linecap="round"/></g>`,
sharpen:`${sword(132,112,38,1.15)}<rect x="24" y="184" width="120" height="40" rx="12" fill="#8C8F9E" ${L()}/><rect x="40" y="186" width="100" height="10" rx="5" fill="#B4B7C4"/>${spark(190,60,2)}${spark(212,110,1.3)}`,
swiftStep:`<path d="M70,60 L130,60 L132,150 C160,150 196,156 206,180 L210,206 L58,206 L58,176 C58,150 64,120 70,60Z" fill="#A86A3A" ${L()}/><path d="M58,190 L210,190" ${L(8)}/><rect x="64" y="50" width="74" height="24" rx="8" fill="${O}" ${L()}/>
<path d="M130,92 C170,60 214,58 236,70 C210,80 196,88 186,98 C206,98 220,104 228,114 C200,116 170,114 140,116Z" fill="#fff" ${L(8)}/>
<path d="M14,120 L44,120 M24,150 L50,150 M10,180 L40,180" stroke="${B}" stroke-width="10" stroke-linecap="round"/>`,
coolMind:`<path d="M66,30 L190,30 M66,226 L190,226" ${L(14)}/><path d="M76,36 C76,90 118,108 118,128 C118,148 76,166 76,220 L180,220 C180,166 138,148 138,128 C138,108 180,90 180,36Z" fill="#DDF1FF" ${L()}/><path d="M92,70 L164,70 C156,96 134,108 128,122 C122,108 100,96 92,70Z" fill="#6EC3F2"/><path d="M96,206 C104,176 120,170 128,166 C136,170 152,176 160,206Z" fill="#6EC3F2"/>
<g transform="translate(196,186)"><circle r="40" fill="${B}" ${L()}/><path d="M0,-24 L0,24 M-21,-12 L21,12 M-21,12 L21,-12" stroke="#fff" stroke-width="8" stroke-linecap="round"/></g>`,
deepBreath:`<path d="M30,96 C80,96 150,96 170,70 C190,44 160,20 138,36" fill="none" ${L(26)}/><path d="M30,96 C80,96 150,96 170,70 C190,44 160,20 138,36" fill="none" stroke="#9ED8F7" stroke-width="12" stroke-linecap="round"/>
<path d="M30,140 C100,140 170,138 196,158 C220,178 196,210 170,196" fill="none" ${L(26)}/><path d="M30,140 C100,140 170,138 196,158 C220,178 196,210 170,196" fill="none" stroke="#9ED8F7" stroke-width="12" stroke-linecap="round"/>
<path d="M90,170 C90,170 60,206 60,222 C60,240 74,248 90,248 C106,248 120,240 120,222 C120,206 90,170 90,170Z" transform="translate(-10,-20)" fill="${B}" ${L()}/>`,
medicBag:`<path d="M50,110 C50,90 64,80 84,80 L172,80 C192,80 206,90 206,110 L214,200 C214,216 202,226 186,226 L70,226 C54,226 42,216 42,200Z" fill="#B5763E" ${L()}/><path d="M50,108 L206,108 L200,140 C160,152 96,152 56,140Z" fill="#8F5A2C" ${L()}/><circle cx="128" cy="140" r="10" fill="${O}" ${L(6)}/>
<g transform="translate(128,52)"><rect x="-14" y="-30" width="28" height="22" rx="4" fill="#D8B48A" ${L(7)}/><path d="M-16,-8 L16,-8 L16,4 C34,12 40,26 40,40 C40,62 22,76 0,76 C-22,76 -40,62 -40,40 C-40,26 -34,12 -16,4Z" fill="#E8504A" ${L(8)}/><path d="M-22,30 C-20,22 -14,16 -6,14" stroke="#fff" stroke-width="7" fill="none" stroke-linecap="round" opacity=".8"/></g>
<path d="M128,170 L128,206 M110,188 L146,188" stroke="${P}" stroke-width="10" stroke-linecap="round"/>`,
campfire:`<path d="M128,30 C170,80 190,110 186,150 C182,186 158,204 128,204 C98,204 74,186 70,150 C66,116 92,98 100,66 C112,90 118,96 124,98 C126,74 124,52 128,30Z" fill="${O}" ${L()}/><path d="M128,104 C150,132 158,150 154,168 C150,186 140,194 128,194 C116,194 104,186 102,168 C100,152 112,138 118,122 C122,134 126,136 128,136Z" fill="#FFD25A"/>
<rect x="44" y="196" width="168" height="26" rx="13" fill="#8A5530" ${L()} transform="rotate(10 128 209)"/><rect x="44" y="196" width="168" height="26" rx="13" fill="#A86A3A" ${L()} transform="rotate(-10 128 209)"/>`,
bloodDrink:`<path d="M128,24 C128,24 56,116 56,160 C56,204 88,232 128,232 C168,232 200,204 200,160 C200,116 128,24 128,24Z" fill="#C9303A" ${L()}/><path d="M86,156 C86,134 98,116 110,102" stroke="#fff" stroke-width="10" fill="none" stroke-linecap="round" opacity=".7"/>
<g transform="translate(176,176)"><circle r="40" fill="${G}" ${L()}/><path d="M0,-20 L0,20 M-20,0 L20,0" stroke="${D}" stroke-width="12" stroke-linecap="round"/></g>`,
deadlyAim:`<circle cx="118" cy="138" r="86" fill="#fff" ${L()}/><circle cx="118" cy="138" r="56" fill="#E8504A" ${L(8)}/><circle cx="118" cy="138" r="26" fill="#fff" ${L(8)}/><path d="M118,36 L118,74 M118,202 L118,240 M16,138 L54,138 M182,138 L220,138" ${L(10)}/>
${star(196,58,40,18,'#FFD25A')}`,
mastery:`<path d="M128,96 L28,78 L28,210 L128,228Z" fill="${P}" ${L()}/><path d="M128,96 L228,78 L228,210 L128,228Z" fill="${P}" ${L()}/><path d="M48,110 L108,120 M48,140 L108,150 M48,170 L108,180 M148,120 L208,110 M148,150 L208,140" stroke="#B9A8D8" stroke-width="8" stroke-linecap="round"/>
<path d="M128,14 L178,66 L148,66 L148,104 L108,104 L108,66 L78,66Z" fill="${G}" ${L()}/>`,
burstKill:`<path d="M128,10 L150,70 L206,40 L184,100 L246,112 L190,146 L230,198 L166,184 L158,246 L128,196 L98,246 L90,184 L26,198 L66,146 L10,112 L72,100 L50,40 L106,70Z" fill="${O}" ${L()}/><path d="M128,58 L142,96 L178,82 L164,116 L200,128 L164,142 L180,176 L144,164 L128,198 L112,164 L76,176 L92,142 L56,128 L92,116 L78,82 L114,96Z" fill="#FFD25A"/>
<g transform="translate(128,132)"><path d="M-40,-6 C-40,-34 -22,-50 0,-50 C22,-50 40,-34 40,-6 C40,12 32,20 24,24 L24,40 L-24,40 L-24,24 C-32,20 -40,12 -40,-6Z" fill="#F4EEE2" ${L(8)}/><circle cx="-15" cy="-4" r="10" fill="${D}"/><circle cx="15" cy="-4" r="10" fill="${D}"/><path d="M-8,40 L-8,28 M8,40 L8,28" stroke="${D}" stroke-width="6"/></g>`,
phoenix:`<path d="M128,226 C96,190 60,170 22,166 C52,150 76,148 96,152 C70,128 44,96 36,52 C70,76 96,100 110,124 C108,90 112,58 128,24 C144,58 148,90 146,124 C160,100 186,76 220,52 C212,96 186,128 160,152 C180,148 204,150 234,166 C196,170 160,190 128,226Z" fill="${O}" ${L()}/><path d="M128,190 C112,170 100,160 86,154 C104,146 116,148 128,154 C140,148 152,146 170,154 C156,160 144,170 128,190Z" fill="#FFD25A"/><path d="M128,60 C136,84 138,108 136,134 L120,134 C118,108 120,84 128,60Z" fill="#FFD25A"/>${spark(64,206,1.6)}${spark(200,206,1.6)}`,
greatSwing:`<path d="M40,170 A96,96 0 1 1 196,196" fill="none" ${L(30)}/><path d="M40,170 A96,96 0 1 1 196,196" fill="none" stroke="#9ED8F7" stroke-width="16" stroke-linecap="round"/><path d="M176,214 L222,200 L200,160Z" fill="#9ED8F7" ${L(8)}/>${sword(128,124,-40,1.1)}`,
splitBolt:`<path d="M30,150 C70,150 100,130 122,128" fill="none" stroke="#B48CF0" stroke-width="16" stroke-linecap="round" opacity=".6"/><circle cx="120" cy="128" r="30" fill="#8E5BD8" ${L()}/><circle cx="112" cy="118" r="9" fill="#fff" opacity=".8"/>
<path d="M150,118 C176,92 190,74 206,62" stroke="#B48CF0" stroke-width="12" stroke-linecap="round" fill="none"/><path d="M150,140 C176,166 190,184 206,196" stroke="#B48CF0" stroke-width="12" stroke-linecap="round" fill="none"/>
<circle cx="212" cy="58" r="24" fill="#B48CF0" ${L()}/><circle cx="212" cy="200" r="24" fill="#B48CF0" ${L()}/>${spark(196,128,1.8)}`,
pierceArrow:`<ellipse cx="96" cy="128" rx="30" ry="70" fill="#9AA0AA" ${L()}/><ellipse cx="96" cy="128" rx="14" ry="36" fill="#C9CDD3"/><ellipse cx="166" cy="128" rx="26" ry="60" fill="#9AA0AA" ${L()}/><ellipse cx="166" cy="128" rx="12" ry="30" fill="#C9CDD3"/>
<path d="M14,128 L222,128" ${L(26)}/><path d="M14,128 L222,128" stroke="#C99560" stroke-width="12" stroke-linecap="round"/><path d="M212,104 L248,128 L212,152Z" fill="#E4E8EE" ${L(8)}/><path d="M14,128 L4,104 L40,110Z M14,128 L4,152 L40,146Z" fill="${G}" ${L(7)}/>`,
};
Object.assign(I,require("./newicons.js"));
const stardust=`${star(128,124,104,46,'#FFD25A',12)}<path d="M96,96 L116,86" stroke="#fff" stroke-width="10" stroke-linecap="round" opacity=".9"/>${spark(212,200,2.4,'#B48CF0')}${spark(40,206,1.8,'#9ED8F7')}${spark(222,40,1.6,'#fff')}`;
// card frames 360x520 @2x
const R={common:{edge:'#8C93A6',fill:P,band:'#C9CFDB',label:'普通'},rare:{edge:B,fill:'#EAF2FF',band:'#7FAAE0',label:'稀有'},epic:{edge:O,fill:'#FFF1DE',band:'#FFC36E',label:'史詩'}};
function card(k){const r=R[k];const glow=k==='epic'?`<rect x="6" y="6" width="348" height="508" rx="36" fill="none" stroke="#FFD25A" stroke-width="12" opacity=".7"/>`:'';
const gems=k==='common'?'':`<g>${[[36,36],[324,36],[36,484],[324,484]].map(([x,y])=>`<circle cx="${x}" cy="${y}" r="13" fill="${k==='rare'?'#9ED8F7':'#FFD25A'}" ${L(6)}/>`).join('')}</g>`;
return `<svg xmlns="http://www.w3.org/2000/svg" width="360" height="520" viewBox="0 0 360 520">${glow}
<rect x="14" y="14" width="332" height="492" rx="30" fill="${r.edge}" ${L(8)}/>
<rect x="30" y="30" width="300" height="460" rx="20" fill="${r.fill}" ${L(6)}/>
<rect x="44" y="44" width="272" height="432" rx="14" fill="none" stroke="${r.band}" stroke-width="5" stroke-dasharray="14 10" stroke-linecap="round"/>
<circle cx="180" cy="150" r="86" fill="${r.band}" opacity=".45"/>
${gems}</svg>`}
const lock=`<path d="M78,112 L78,86 C78,56 100,34 128,34 C156,34 178,56 178,86 L178,112" fill="none" ${L(22)}/><path d="M78,112 L78,86 C78,56 100,34 128,34 C156,34 178,56 178,86 L178,112" fill="none" stroke="#C9CFDB" stroke-width="10" stroke-linecap="round"/>
<rect x="46" y="104" width="164" height="124" rx="26" fill="#8C93A6" ${L()}/><rect x="62" y="118" width="132" height="40" rx="18" fill="#A6ACBD"/>
<circle cx="128" cy="160" r="20" fill="${D}"/><path d="M128,160 L128,200" stroke="${D}" stroke-width="18" stroke-linecap="round"/>`;
// 技能鈕右下角等級角標：膠囊底（9-slice 左右 18）＋預先排好的 Lv.1–4
const lvBg=(w=72,h=40)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><rect x="4" y="4" width="${w-8}" height="${h-8}" rx="${(h-8)/2}" fill="${O}" ${L(6)}/><rect x="10" y="9" width="${w-20}" height="${(h-18)/2}" rx="${(h-18)/4}" fill="#FFB36B" opacity=".8"/></svg>`;
const lvTxt=(n,w=72,h=40)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><rect x="4" y="4" width="${w-8}" height="${h-8}" rx="${(h-8)/2}" fill="${O}" ${L(6)}/><rect x="10" y="9" width="${w-20}" height="${(h-18)/2}" rx="${(h-18)/4}" fill="#FFB36B" opacity=".8"/><text x="${w/2}" y="${h/2+8}" text-anchor="middle" font-family="Fredoka" font-weight="700" font-size="24" fill="#fff" stroke="${D}" stroke-width="4" paint-order="stroke">Lv${n}</text></svg>`;
const wrapIcon=b=>`<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">${b}</svg>`;
(async()=>{const br=await chromium.launch();const pg=await br.newPage();
const b64=f=>fs.readFileSync(`${FONT}/${f}`).toString('base64');
const css=`<style>@font-face{font-family:Huninn;src:url(data:font/ttf;base64,${b64('huninn.ttf')})}@font-face{font-family:Fredoka;src:url(data:font/ttf;base64,${b64('fredoka.ttf')})}body{margin:0;background:transparent}</style>`;
const shot=async(file,svg,w,h)=>{fs.writeFileSync(file.replace('.png','.svg'),svg);await pg.setViewportSize({width:w,height:h});await pg.setContent(css+svg);await pg.screenshot({path:file,omitBackground:true,clip:{x:0,y:0,width:w,height:h}})};
for(const d of ['perks','cards','currency'])fs.mkdirSync(`${OUT}/${d}`,{recursive:true});
for(const [k,v] of Object.entries(I))await shot(`${OUT}/perks/perk_${k}.png`,wrapIcon(v),256,256);
fs.mkdirSync(`${OUT}/hud`,{recursive:true});await shot(`${OUT}/hud/skill_locked.png`,wrapIcon(lock),256,256);
await shot(`${OUT}/hud/skill_locked_128.png`,wrapIcon(lock).replace('width="256" height="256"','width="128" height="128"'),128,128);
await shot(`${OUT}/hud/skill_level_bg.png`,lvBg(),72,40);
for(let n=1;n<=9;n++)await shot(`${OUT}/hud/skill_level_${n}.png`,lvTxt(n),72,40);
await shot(`${OUT}/currency/stardust.png`,wrapIcon(stardust),256,256);
for(const k of Object.keys(R))await shot(`${OUT}/cards/card_${k}.png`,card(k),360,520);
// preview: 3 cards with sample perks + all icons grid
const img=f=>`data:image/png;base64,${fs.readFileSync(f).toString('base64')}`;
const perks=JSON.parse(fs.readFileSync(process.argv[3]));
const sample=[['common','sharpen'],['rare','deadlyAim'],['epic','phoenix']];
let h=`${css}<body style="background:#1d2a3f;font-family:Huninn;padding:24px;width:1200px"><div style="display:flex;gap:36px;justify-content:center">`;
for(const [r,id] of sample){const p=perks.find(x=>x.id==id);h+=`<div style="position:relative;width:270px;height:390px"><img src="${img(`${OUT}/cards/card_${r}.png`)}" style="width:270px;position:absolute"><img src="${img(`${OUT}/perks/perk_${id}.png`)}" style="position:absolute;left:55px;top:38px;width:160px"><div style="position:absolute;top:212px;width:270px;text-align:center;font-size:30px;color:#2B2140">${p.name}</div><div style="position:absolute;top:258px;left:40px;width:190px;text-align:center;font-size:19px;color:#4a4060;line-height:1.4">${p.desc}</div><div style="position:absolute;top:340px;width:270px;text-align:center;font-size:16px;color:#2B2140;opacity:.7">${R[r].label}</div></div>`}
h+=`</div><div style="display:flex;flex-wrap:wrap;gap:14px;margin-top:30px;justify-content:center">`;
for(const p of perks)h+=`<div style="width:132px;text-align:center;color:#FFF4DE;font-size:16px"><img src="${img(`${OUT}/perks/perk_${p.id}.png`)}" style="width:96px;background:#2a3a55;border-radius:16px;padding:8px"><div>${p.name}</div></div>`;
h+=`<div style="width:132px;text-align:center;color:#FFD25A;font-size:16px"><img src="${img(`${OUT}/currency/stardust.png`)}" style="width:96px;background:#2a3a55;border-radius:16px;padding:8px"><div>星塵</div></div></div></body>`;
await pg.setViewportSize({width:1250,height:900});await pg.setContent(h);await pg.waitForTimeout(400);await pg.screenshot({path:`${OUT}/preview.png`,fullPage:true});
await br.close()})();

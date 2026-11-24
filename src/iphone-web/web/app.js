'use strict';
const el=id=>document.getElementById(id);
const token=new URLSearchParams(location.hash.slice(1)).get('key')||sessionStorage.getItem('soundmesh.key');
if(token){sessionStorage.setItem('soundmesh.key',token);history.replaceState(null,'',location.pathname);}
let context,master,analyser,active=false;
const channels=new Map();
async function api(path,options={}){
    const response=await fetch('/api/'+path,{...options,headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'}});
    if(!response.ok)throw Error(response.status===401?'请重新扫描电脑上的二维码':(await response.json()).error||'连接失败');
    return await response.json();
}
function element(tag,text,css){
    const node=document.createElement(tag);node.textContent=text;if(css)node.className=css;return node;
}
function gainFor(c){if(c.audio)c.audio.gain.gain.setTargetAtTime(c.muted?0:c.volume/100,context.currentTime,.008);}
function add(info){
    if(channels.has(info.id))return;
    const c={id:info.id,volume:70,muted:false,wait:500};
    const row=element('div','','channel'),head=element('div','','channel-head'),title=element('div','','channel-info');
    title.append(element('h3',info.name));c.status=element('span','待监听','channel-status');title.append(c.status);
    const toggle=element('button','静音','mute');
    toggle.onclick=()=>{c.muted=!c.muted;toggle.setAttribute('aria-pressed',String(c.muted));toggle.textContent=c.muted?'恢复':'静音';gainFor(c);};
    head.append(title,toggle);row.append(head);
    const volume=element('div','','volume-row'),slider=document.createElement('input'),percent=element('output','70%');
    slider.type='range';slider.value='70';slider.setAttribute('aria-label',info.name+'音量');
    slider.oninput=()=>{c.volume=Number(slider.value);percent.textContent=c.volume+'%';gainFor(c);};
    volume.append(element('span','音量','gain-label'),percent,slider);row.append(volume);el('channels').append(row);
    channels.set(c.id,c);if(active)connect(c);
}
function connect(c){
    const ws=new WebSocket('ws://'+location.host+'/listen/'+encodeURIComponent(c.id));
    c.ws=ws;ws.binaryType='arraybuffer';c.audio??=new StereoChannel(context,master);gainFor(c);
    c.status.textContent='连接中';let received=false;
    ws.onopen=()=>ws.send(JSON.stringify({key:token}));
    ws.onmessage=e=>{
        if(c.ws!==ws||!active||typeof e.data==='string')return;
        if(!received){c.status.textContent='立体声 · 已连接';received=true;}c.wait=500;
        if(context.state==='running')c.audio.play(e.data);
    };
    ws.onclose=()=>{
        if(c.ws!==ws||!active)return;
        c.audio.reset();c.status.textContent='连接中断，正在重试';
        c.timer=setTimeout(()=>{if(active&&c.ws===ws)connect(c);},c.wait);c.wait=Math.min(8000,c.wait*2);
    };
}
function state(){
    el('play-label').textContent=active?'暂停监听':'开始监听';el('lamp-text').textContent=active?'监听中':'待监听';
    el('lamp').classList.toggle('live',active);el('play').dataset.playing=String(active);
}
el('play').onclick=async()=>{
    try{
        if(active){
            active=false;
            for(const c of channels.values()){clearTimeout(c.timer);c.ws?.close();c.audio?.reset();c.status.textContent='已暂停';}
            await context.suspend();
        }else{
            if(!context){
                const Audio=window.AudioContext||window.webkitAudioContext;
                if(!Audio)throw Error("此浏览器未提供网页音频，请使用 iPhone 上的 Safari 打开。");
                context=new Audio();
                master=context.createGain();analyser=context.createAnalyser();analyser.fftSize=256;
                const limiter=context.createDynamicsCompressor();limiter.threshold.value=-3;
                master.connect(limiter);limiter.connect(analyser);analyser.connect(context.destination);draw();
            }
            await context.resume();master.gain.value=Number(el('master').value)/100;
            active=true;for(const c of channels.values())connect(c);
        }
        state();el('hint').textContent=active?'保持页面打开以持续监听。':'点按开始播放。';
    }catch(e){el('hint').textContent=e.message;}
};
function draw(){
    const canvas=el('scope'),ctx=canvas.getContext('2d'),samples=new Uint8Array(analyser.fftSize);
    function paint(){canvas.width=canvas.clientWidth;canvas.height=canvas.clientHeight;analyser.getByteTimeDomainData(samples);
        ctx.strokeStyle='#bcf19d';ctx.beginPath();samples.forEach((v,i)=>{const x=i*canvas.width/(samples.length-1),y=v*canvas.height/256;i?ctx.lineTo(x,y):ctx.moveTo(x,y);});ctx.stroke();requestAnimationFrame(paint);}
    paint();
}
el('master').oninput=()=>{el('master-value').textContent=el('master').value+'%';if(master)master.gain.setTargetAtTime(Number(el('master').value)/100,context.currentTime,.008);};
el('add').onclick=()=>el('pair').showModal();
el('discover').onclick=async()=>{
    try{el('devices').replaceChildren();const {devices}=await api('discover');el('pair-status').textContent=devices.length?'选择发送端并输入配对码。':'附近暂无设备，可手动配对。';
        for(const d of devices){const b=element('button',d.name+' · '+d.host,'device');b.onclick=()=>{el('host').value=d.host;el('pin').focus();};el('devices').append(b);}
    }catch(e){el('pair-status').textContent=e.message;}
};
el('pair-form').onsubmit=async e=>{
    e.preventDefault();el('join').disabled=true;
    try{add(await api('sources',{method:'POST',body:JSON.stringify({host:el('host').value.trim(),pin:el('pin').value})}));el('pin').value='';el('pair').close();}
    catch(err){el('pair-status').textContent=err.message;}finally{el('join').disabled=false;}
};
(async()=>{try{const {sources}=await api('sources');sources.forEach(add);el('play').disabled=el('add').disabled=false;el('hint').textContent='耳机连到 iPhone 后，点按开始监听。';state();}
catch(e){el('hint').textContent=e.message;}})();
el('guide-open').onclick=()=>{
    const dialog=el('guide-dialog'),frame=dialog.querySelector('iframe');
    if(!frame.hasAttribute('src'))frame.src='/guide.html#iphone';
    dialog.showModal();
};

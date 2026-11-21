class StereoChannel {
    constructor(context, output) {
        this.context=context;
        this.gain=context.createGain();
        this.gain.connect(output);
        this.next=0;
        this.pending=new Set();
    }
    play(bytes) {
        const view=new DataView(bytes);
        const frames=view.byteLength/4;
        if(!Number.isInteger(frames)||frames===0)return;
        const now=this.context.currentTime;
        if(this.next>now+0.3)this.reset();
        if(this.next<now+0.005)this.next=now+0.08;
        const pcm=this.context.createBuffer(2,frames,48000);
        for(let channel=0;channel<2;channel++){
            const samples=pcm.getChannelData(channel);
            for(let i=0;i<frames;i++)samples[i]=view.getInt16(i*4+channel*2,true)/32768;
        }
        const source=this.context.createBufferSource();
        source.buffer=pcm;source.connect(this.gain);
        source.onended=()=>{this.pending.delete(source);source.disconnect();};
        this.pending.add(source);source.start(this.next);this.next+=frames/48000;
    }
    reset(){for(const s of this.pending){try{s.stop();}catch{}}this.pending.clear();this.next=0;}
}
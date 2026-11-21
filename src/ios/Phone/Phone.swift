import SwiftUI
import AVFoundation
import Network
import ReplayKit

final class Channel: ObservableObject, Identifiable {
    let id=UUID(), host:String, pin:String
    @Published var status="连接中"
    @Published var volume:Float=0.7 {didSet{player.volume=muted ? 0:volume}}
    @Published var muted=false {didSet{player.volume=muted ? 0:volume}}
    let player=AVAudioPlayerNode()
    private var connection:NWConnection?
    private var input=Data(), authenticated=false, running=true, scheduled=0, wait=1
    private let format:AVAudioFormat
    init(host:String,pin:String,format:AVAudioFormat){self.host=host;self.pin=pin;self.format=format;player.volume=volume}
    func connect(){
        guard running else{return}
        input.removeAll();authenticated=false;scheduled=0;status="连接中"
        let conn=NWConnection(host:NWEndpoint.Host(host),port:49632,using:.tcp)
        connection=conn
        conn.stateUpdateHandler = { [weak self,weak conn] state in
            guard let self=self,let conn=conn,self.connection===conn else{return}
            switch state{
            case .ready:
                conn.send(content:Data(("SOUNDMESH2 "+self.pin+"\n").utf8),completion:.contentProcessed{err in
                    if err==nil {self.read(conn)} else {self.retry(conn)}
                })
            case .failed: self.retry(conn)
            default: break
            }
        }
        conn.start(queue:.main)
        DispatchQueue.main.asyncAfter(deadline:.now()+8){[weak self,weak conn] in
            if let self=self,let conn=conn,self.connection===conn,!self.authenticated {self.retry(conn)}
        }
    }
    private func retry(_ conn:NWConnection){
        guard connection===conn,running else{return}
        connection=nil;conn.cancel();player.stop();scheduled=0
        status="已断开，正在重连";let delay=wait;wait=min(8,wait*2)
        DispatchQueue.main.asyncAfter(deadline:.now()+Double(delay)){[weak self] in self?.connect()}
    }
    private func read(_ conn:NWConnection){
        conn.receive(minimumIncompleteLength:1,maximumLength:7680){[weak self] data,_,eof,error in
            guard let self=self,self.connection===conn else{return}
            if let data=data{
                self.input.append(data)
                if !self.authenticated,let end=self.input.firstIndex(of:10){
                    guard String(decoding:self.input[..<end],as:UTF8.self)=="OK 48000 2" else{
                        self.stop();self.status="配对失败，请移除后重新输入配对码";return
                    }
                    self.input.removeSubrange(...end);self.authenticated=true;self.status="已连接";self.wait=1
                }
                if !self.authenticated && self.input.count>80 {self.retry(conn);return}
                while self.authenticated && self.input.count>=1920{
                    self.enqueue(Data(self.input.prefix(1920)),conn);self.input.removeFirst(1920)
                }
            }
            if eof || error != nil {self.retry(conn)} else {self.read(conn)}
        }
    }
    private func enqueue(_ frame:Data,_ conn:NWConnection){
        guard scheduled<16,let pcm=AVAudioPCMBuffer(pcmFormat:format,frameCapacity:480),let planes=pcm.floatChannelData else{return}
        pcm.frameLength=480
        frame.withUnsafeBytes{(bytes:UnsafeRawBufferPointer) in
            for i in 0..<480 {for c in 0..<2{
                let n=i*4+c*2
                planes[c][i]=Float(Int16(bitPattern:UInt16(bytes[n])|UInt16(bytes[n+1])<<8))/32768
            }}
        }
        scheduled+=1
        player.scheduleBuffer(pcm,completionCallbackType:.dataPlayedBack){[weak self] _ in
            DispatchQueue.main.async {guard let self=self,self.connection===conn else{return};self.scheduled=max(0,self.scheduled-1)}
        }
        if !player.isPlaying && scheduled>=7 {player.play()}
    }
    func stop(){running=false;let conn=connection;connection=nil;conn?.cancel();player.stop()}
}

final class Mixer:ObservableObject{
    @Published var channels:[Channel]=[] {didSet{Pairing.defaults?.set(!channels.isEmpty,forKey:"listening")}}
    @Published var message=""
    @Published var gain:Float=0.8 {didSet{engine.mainMixerNode.outputVolume=gain}}
    let engine=AVAudioEngine()
    let format=AVAudioFormat(standardFormatWithSampleRate:48000,channels:2)!
    init(){Pairing.defaults?.set(false,forKey:"listening")}
    func add(_ host:String,_ pin:String){
        if let heartbeat=Pairing.defaults?.double(forKey:"broadcastHeartbeat"),Date().timeIntervalSince1970-heartbeat<3 {
            message="请先在控制中心结束音频广播";return
        }
        guard IPv4Address(host) != nil,host != Pairing.ipv4(),pin.range(of:"^[0-9]{8}$",options:.regularExpression) != nil else{message="请检查对端局域网地址和八位配对码";return}
        guard !channels.contains(where:{$0.host==host}) else{return}
        let channel=Channel(host:host,pin:pin,format:format)
        do{
            let session=AVAudioSession.sharedInstance()
            try session.setCategory(.playback,mode:.default,options:[.mixWithOthers]);try session.setActive(true)
            engine.attach(channel.player);engine.connect(channel.player,to:engine.mainMixerNode,format:format)
            engine.mainMixerNode.outputVolume=gain
            if !engine.isRunning {try engine.start()}
            channels.append(channel);channel.connect();message=""
        }catch{if channel.player.engine != nil{engine.detach(channel.player)};message=error.localizedDescription}
    }
    func remove(_ c:Channel){
        c.stop();engine.detach(c.player);channels.removeAll{$0.id==c.id}
        if channels.isEmpty{engine.stop();try? AVAudioSession.sharedInstance().setActive(false,options:.notifyOthersOnDeactivation)}
    }
}
struct Strip:View{
    @ObservedObject var channel:Channel
    let remove:()->Void
    var body:some View{VStack(alignment:.leading){Text(channel.host).font(.headline);Text(channel.status).font(.caption);Slider(value:$channel.volume);HStack{Button(channel.muted ? "恢复":"静音"){channel.muted.toggle()};Spacer();Button("移除",action:remove)}}.buttonStyle(.borderless)}
}
struct BroadcastButton:UIViewRepresentable{
    func makeUIView(context:Context)->RPSystemBroadcastPickerView{let b=RPSystemBroadcastPickerView(frame:CGRect(x:0,y:0,width:54,height:54));b.preferredExtension=(Bundle.main.bundleIdentifier ?? "")+".Broadcast";b.showsMicrophoneButton=false;return b}
    func updateUIView(_ uiView:RPSystemBroadcastPickerView,context:Context){}
}
@main struct SoundMeshPhone:App{
    @StateObject private var mixer=Mixer()
    @State private var host="",pin=""
    var body:some Scene{WindowGroup{NavigationStack{Form{
        Section("耳机监听"){TextField("发送端 IPv4 地址",text:$host).keyboardType(.decimalPad);SecureField("八位配对码",text:$pin).keyboardType(.numberPad);Button("连接设备"){mixer.add(host,pin)};if !mixer.message.isEmpty{Text(mixer.message)}}
        Section("混音总音量 \(Int(mixer.gain*100))%"){Slider(value:$mixer.gain)}
        ForEach(mixer.channels){c in Section{Strip(channel:c,remove:{mixer.remove(c)})}}
        Section("共享 iPhone 应用声音"){
            Text("地址  "+Pairing.ipv4());Text("配对码  "+Pairing.pin())
            if mixer.channels.isEmpty {HStack{Text("点按系统广播按钮");Spacer();BroadcastButton().frame(width:50,height:50)}}
            else {Text("先移除监听设备，再启动系统广播。")}
            Text("在控制中心结束广播后可切回监听。受保护的内容可能无声。").font(.caption)
        }
    }.navigationTitle("声汇")}.onOpenURL{url in
        let parts=url.pathComponents.filter{$0 != "/"}
        if url.scheme=="soundmesh",url.host=="pair",parts.count>=2 {host=parts[0];pin=parts[1]}
    }}}
}

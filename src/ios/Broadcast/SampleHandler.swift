import ReplayKit
import AVFoundation
import Network

private final class Peer {
    let connection: NWConnection
    var active = false
    var sending = false
    var pending: [Data] = []
    init(_ connection: NWConnection) {self.connection=connection}
}

final class SampleHandler: RPBroadcastSampleHandler {
    private let queue = DispatchQueue(label:"SoundMesh.broadcast",qos:.userInitiated)
    private var listener: NWListener?
    private var peers: [UUID:Peer] = [:]
    private var converter: AVAudioConverter?
    private let output = AVAudioFormat(commonFormat:.pcmFormatInt16,sampleRate:48000,channels:2,interleaved:true)!
    private var remainder = Data()
    private var timer: DispatchSourceTimer?
    private var lastAudio = DispatchTime.now()
    private var lastHeartbeat:Double=0
    private var paused = false
    override func broadcastStarted(withSetupInfo setupInfo: [String:NSCoding]?) {
        queue.async {
            do {
                if Pairing.defaults?.bool(forKey:"listening")==true {
                    throw NSError(domain:"SoundMesh",code:2,userInfo:[NSLocalizedDescriptionKey:"请在声汇中移除监听通道后再开始广播"])
                }
                guard !Pairing.pin().isEmpty else {throw NSError(domain:"SoundMesh",code:1,userInfo:[NSLocalizedDescriptionKey:"请检查 App Group 签名"])}
                let listener=try NWListener(using:.tcp,on:49632)
                self.listener=listener
                Pairing.defaults?.set(Date().timeIntervalSince1970,forKey:"broadcastHeartbeat")
                listener.stateUpdateHandler = { [weak self] state in
                    if case .failed(let error)=state {self?.finishBroadcastWithError(error)}
                }
                listener.newConnectionHandler = { [weak self] in self?.accept($0) }
                listener.start(queue:self.queue)
                let timer=DispatchSource.makeTimerSource(queue:self.queue)
                timer.schedule(deadline:.now()+0.2,repeating:0.01)
                timer.setEventHandler { [weak self] in
                    guard let self=self else{return}
                    let now=Date().timeIntervalSince1970
                    if now-self.lastHeartbeat>1 {Pairing.defaults?.set(now,forKey:"broadcastHeartbeat");self.lastHeartbeat=now}
                    guard !self.peers.isEmpty else{return}
                    if self.paused || DispatchTime.now().uptimeNanoseconds-self.lastAudio.uptimeNanoseconds>200_000_000 {
                        self.offer(Data(count:1920))
                    }
                }
                self.timer=timer
                timer.resume()
            } catch {self.finishBroadcastWithError(error)}
        }
    }
    private func accept(_ connection:NWConnection) {
        let id=UUID(), peer=Peer(connection)
        peers[id]=peer
        connection.stateUpdateHandler = { [weak self] state in
            switch state {
            case .failed, .cancelled: self?.peers.removeValue(forKey:id)
            default: break
            }
        }
        connection.start(queue:queue)
        queue.asyncAfter(deadline:.now()+5){[weak self] in
            if self?.peers[id]?.active==false {connection.cancel()}
        }
        readHello(id,Data())
    }
    private func readHello(_ id:UUID,_ prefix:Data){
        guard let p=peers[id] else {return}
        p.connection.receive(minimumIncompleteLength:1,maximumLength:80-prefix.count){[weak self] data,_,eof,error in
            guard let self=self,let data=data,!data.isEmpty,error==nil else {p.connection.cancel();return}
            let bytes=prefix+data
            if let end=bytes.firstIndex(of:10){
                let line=String(decoding:bytes[..<end],as:UTF8.self)
                guard line=="SOUNDMESH2 "+Pairing.pin() else {
                    p.connection.send(content:Data("ERR\n".utf8),completion:.contentProcessed{_ in p.connection.cancel()});return
                }
                p.connection.send(content:Data("OK 48000 2\n".utf8),completion:.contentProcessed{error in
                    if error==nil {p.active=true} else {p.connection.cancel()}
                })
            } else if bytes.count<80 && !eof {self.readHello(id,bytes)} else {p.connection.cancel()}
        }
    }
    private func offer(_ frame:Data) {
        for p in peers.values where p.active {
            if p.pending.count>=10 {p.pending.removeFirst()}
            p.pending.append(frame);flush(p)
        }
    }
    private func flush(_ p:Peer){
        guard !p.sending,!p.pending.isEmpty else{return}
        p.sending=true
        p.connection.send(content:p.pending.removeFirst(),completion:.contentProcessed{[weak self] error in
            p.sending=false
            if error==nil {self?.flush(p)} else {p.connection.cancel()}
        })
    }
    override func processSampleBuffer(_ sampleBuffer:CMSampleBuffer,with sampleBufferType:RPSampleBufferType){
        guard sampleBufferType == .audioApp else {return}
        queue.sync {
            guard !paused,peers.values.contains(where:{$0.active}),
                  let desc=CMSampleBufferGetFormatDescription(sampleBuffer),
                  let asbd=CMAudioFormatDescriptionGetStreamBasicDescription(desc),
                  let input=AVAudioFormat(streamDescription:asbd),input.sampleRate>0 else{return}
            let frames=CMSampleBufferGetNumSamples(sampleBuffer)
            guard frames>0,let pcm=AVAudioPCMBuffer(pcmFormat:input,frameCapacity:AVAudioFrameCount(frames)) else{return}
            pcm.frameLength=pcm.frameCapacity
            guard CMSampleBufferCopyPCMDataIntoAudioBufferList(sampleBuffer,at:0,frameCount:Int32(frames),into:pcm.mutableAudioBufferList)==noErr else{return}
            if converter?.inputFormat != input {converter=AVAudioConverter(from:input,to:output);remainder.removeAll()}
            let capacity=AVAudioFrameCount(ceil(Double(frames)*48000/input.sampleRate))+128
            guard let result=AVAudioPCMBuffer(pcmFormat:output,frameCapacity:capacity),let converter=converter else{return}
            var supplied=false
            var failure:NSError?
            _=converter.convert(to:result,error:&failure){_,status in
                if supplied {status.pointee = .noDataNow;return nil}
                supplied=true;status.pointee = .haveData;return pcm
            }
            guard failure==nil,result.frameLength>0,let ptr=result.audioBufferList.pointee.mBuffers.mData else{return}
            lastAudio = .now();remainder.append(ptr.assumingMemoryBound(to:UInt8.self),count:Int(result.frameLength)*4)
            while remainder.count>=1920 {offer(Data(remainder.prefix(1920)));remainder.removeFirst(1920)}
        }
    }
    override func broadcastPaused(){queue.sync{paused=true;remainder.removeAll();converter?.reset()}}
    override func broadcastResumed(){queue.sync{paused=false}}
    override func broadcastFinished(){queue.sync{Pairing.defaults?.removeObject(forKey:"broadcastHeartbeat");timer?.cancel();listener?.cancel();for p in peers.values{p.connection.cancel()};peers.removeAll()}}
}

using NAudio.CoreAudioApi;
namespace SoundMesh;

// Scale other applications' existing levels on the default multimedia endpoint.
// Keep their individual starting levels and leave external changes intact on restore.
sealed class LocalSessions {
    readonly Dictionary<string,(float baseline,float applied)> saved=new();
    int percent=100;
    public void Apply(int value){
        percent=Math.Clamp(value,0,100);
        if(percent==100){Restore();return;}
        Visit(true);
    }
    public void Refresh(){if(percent!=100)Visit(true);}
    public void Restore(){
        percent=100;
        if(saved.Count==0)return;
        try{Visit(false);}finally{saved.Clear();}
    }
    void Visit(bool apply){
        using var endpoints=new MMDeviceEnumerator();
        using var current=endpoints.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
        foreach(var device in endpoints.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)){
            using(device){
                bool selected=device.ID==current.ID;
                if(!selected&&saved.Count==0)continue;
                var sessions=device.AudioSessionManager.Sessions;
                for(int i=0;i<sessions.Count;i++){
                    try{
                    using var session=sessions[i];
                    if(session.GetProcessID==Environment.ProcessId)continue;
                    var volume=session.SimpleAudioVolume;
                    if(volume==null)continue;
                    string id=session.GetSessionInstanceIdentifier;
                    float actual=volume.Volume;
                    bool known=saved.TryGetValue(id,out var previous);
                    if(!apply||!selected){
                        if(known&&Math.Abs(actual-previous.applied)<0.002f)volume.Volume=previous.baseline;
                        saved.Remove(id);continue;
                    }
                    float baseline=known?previous.baseline:actual;
                    if(known&&Math.Abs(actual-previous.applied)>0.002f)baseline=actual;
                    float next=baseline*percent/100f;
                    if(Math.Abs(next-actual)>0.0001f)volume.Volume=next;
                    saved[id]=(baseline,next);
                    }catch(System.Runtime.InteropServices.COMException){/* Sessions may end during enumeration. */}
                }
            }
        }
    }
}
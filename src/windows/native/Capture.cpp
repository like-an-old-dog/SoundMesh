#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <audioclientactivationparams.h>
#include <wrl.h>
#include <cstdio>
#include <fcntl.h>
#include <io.h>
#include <vector>
using namespace Microsoft::WRL;
class Activation final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IActivateAudioInterfaceCompletionHandler, FtmBase> {
public:
    HANDLE done = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    HRESULT result = E_FAIL;
    ComPtr<IAudioClient> client;
    ~Activation(){CloseHandle(done);}
    HRESULT STDMETHODCALLTYPE ActivateCompleted(IActivateAudioInterfaceAsyncOperation* op) override {
        ComPtr<IUnknown> unknown;
        HRESULT activation = E_FAIL;
        result = op->GetActivateResult(&activation, &unknown);
        if(SUCCEEDED(result)) result = activation;
        if(SUCCEEDED(result)) result = unknown.As(&client);
        SetEvent(done);
        return S_OK;
    }
};
HRESULT capture(DWORD exclude) {
    AUDIOCLIENT_ACTIVATION_PARAMS params{};
    params.ActivationType = AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK;
    params.ProcessLoopbackParams.TargetProcessId = exclude;
    params.ProcessLoopbackParams.ProcessLoopbackMode = PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE;
    PROPVARIANT value{};
    value.vt = VT_BLOB;
    value.blob = {sizeof(params), reinterpret_cast<BYTE*>(&params)};
    auto callback = Make<Activation>();
    ComPtr<IActivateAudioInterfaceAsyncOperation> op;
    HRESULT hr = ActivateAudioInterfaceAsync(VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK, __uuidof(IAudioClient), &value, callback.Get(), &op);
    if(FAILED(hr)) return hr;
    if(WaitForSingleObject(callback->done,7000)!=WAIT_OBJECT_0) return HRESULT_FROM_WIN32(ERROR_TIMEOUT);
    if(FAILED(callback->result)) return callback->result;
    auto client = callback->client;
    WAVEFORMATEX format{WAVE_FORMAT_PCM,2,48000,192000,4,16,0};
    hr = client->Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK|AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM,200000,0,&format,nullptr);
    if(FAILED(hr)) return hr;
    ComPtr<IAudioCaptureClient> reader;
    hr = client->GetService(IID_PPV_ARGS(&reader));
    if(FAILED(hr)) return hr;
    if(FAILED(hr=client->Start())) return hr;
    _setmode(_fileno(stdout),_O_BINARY);
    setvbuf(stdout,nullptr,_IONBF,0);
    fprintf(stderr,"READY\n"); fflush(stderr);
    std::vector<BYTE> silence;
    for(;;) {
        UINT32 frames=0;
        if(FAILED(hr=reader->GetNextPacketSize(&frames))) return hr;
        if(frames==0){Sleep(3);continue;}
        BYTE* data=nullptr; DWORD flags=0;
        if(FAILED(hr=reader->GetBuffer(&data,&frames,&flags,nullptr,nullptr))) return hr;
        size_t bytes=frames*4;
        if(flags&AUDCLNT_BUFFERFLAGS_SILENT){silence.assign(bytes,0);data=silence.data();}
        bool sent=fwrite(data,1,bytes,stdout)==bytes;
        reader->ReleaseBuffer(frames);
        if(!sent) return S_OK;
    }
}
int wmain(int argc,wchar_t**argv) {
    if(argc!=2)return 2;
    CoInitializeEx(nullptr,COINIT_MULTITHREADED);
    HRESULT hr=capture(wcstoul(argv[1],nullptr,10));
    if(FAILED(hr))fprintf(stderr,"WASAPI HRESULT 0x%08lx\n",hr);
    CoUninitialize();
    return FAILED(hr)?1:0;
}


using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoundMesh;

sealed record ReleaseUpdate(Version Version,Uri Url,string Digest,long Size){
    const string Repo="like-an-old-dog/SoundMesh";
    static readonly HttpClient http=new(){Timeout=TimeSpan.FromMinutes(8)};
    static ReleaseUpdate(){http.DefaultRequestHeaders.UserAgent.ParseAdd("SoundMesh/1.3");}
    public static Version Current=>typeof(ReleaseUpdate).Assembly.GetName().Version!;
    public static ReleaseUpdate Parse(JsonElement r){
        string tag=r.GetProperty("tag_name").GetString()??"";
        if(r.GetProperty("draft").GetBoolean()||r.GetProperty("prerelease").GetBoolean()||!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$"))throw new IOException("发布标签无效");
        var version=Version.Parse(tag[1..]);string name="SoundMesh-Windows-"+version+".zip";
        var a=r.GetProperty("assets").EnumerateArray().SingleOrDefault(x=>x.GetProperty("name").GetString()==name);
        if(a.ValueKind!=JsonValueKind.Object)throw new IOException("Release 缺少 "+name);
        var url=new Uri($"https://github.com/{Repo}/releases/download/{tag}/{name}");
        string digest=a.GetProperty("digest").GetString()??"";long size=a.GetProperty("size").GetInt64();
        if(url.AbsoluteUri!=a.GetProperty("browser_download_url").GetString()||!Regex.IsMatch(digest,"^sha256:[0-9a-f]{64}$")||size<=0||size>536870912)throw new IOException("资产校验信息无效");
        return new(version,url,digest[7..],size);
    }
    public static async Task<ReleaseUpdate> Latest(CancellationToken token){
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(15000);
        using var response=await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest",timeout.Token);
        if((int)response.StatusCode is 403 or 429){
            using var latest=await http.GetAsync($"https://github.com/{Repo}/releases/latest",HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            latest.EnsureSuccessStatusCode();var uri=latest.RequestMessage!.RequestUri!;
            var match=Regex.Match(uri.AbsolutePath,@"^/like-an-old-dog/SoundMesh/releases/tag/(v\d+\.\d+\.\d+)$");
            if(uri.Host!="github.com"||!match.Success)throw new IOException("GitHub 限流，请稍后重试");
            string tag=match.Groups[1].Value,name="SoundMesh-Windows-"+tag[1..]+".zip";
            string baseUrl=$"https://github.com/{Repo}/releases/download/{tag}/";
            string sums=await http.GetStringAsync(baseUrl+"SHA256SUMS.txt",timeout.Token);
            var line=Regex.Match(sums,@"(?im)^([0-9a-f]{64})[ \t]+\*?"+Regex.Escape(name)+@"\r?$");
            if(!line.Success)throw new IOException("校验清单缺少 Windows 资产");
            return new(Version.Parse(tag[1..]),new Uri(baseUrl+name),line.Groups[1].Value,-1);
        }
        response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));return Parse(doc.RootElement);
    }
    static string Store=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SoundMesh","versions");
    static string Exe(string folder)=>Path.Combine(Store,folder,"SoundMesh.exe");
    public static void Open(string exe)=>Process.Start(new ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(exe)!,UseShellExecute=false});
    public static bool Forward(){
        try{
            string folder=Lan.Read("installedRelease");
            var match=Regex.Match(folder,@"^(\d+\.\d+\.\d+)-[0-9a-f]{32}$");
            if(!match.Success||Version.Parse(match.Groups[1].Value)<=Current||!File.Exists(Exe(folder)))return false;
            Open(Exe(folder));return true;
        }catch(Exception){return false;}
    }
    public async Task<string> Stage(IProgress<int> progress,CancellationToken token){
        string folder=Version+"-"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Store);
        using var download=new FileStream(Path.Combine(Store,folder+".part"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,65536,FileOptions.DeleteOnClose);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(8));
        using var res=await http.GetAsync(Url,HttpCompletionOption.ResponseHeadersRead,timeout.Token);res.EnsureSuccessStatusCode();
        using var input=await res.Content.ReadAsStreamAsync(timeout.Token);
        using var sha=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer=new byte[65536];long count=0;int last=-1,n;
        while((n=await input.ReadAsync(buffer,timeout.Token))>0){
            count+=n;if(count>536870912||(Size>0&&count>Size))throw new IOException("下载大小异常");sha.AppendData(buffer,0,n);
            await download.WriteAsync(buffer.AsMemory(0,n),timeout.Token);
            long total=Size>0?Size:res.Content.Headers.ContentLength.GetValueOrDefault();int pct=total>0?(int)Math.Min(100,count*100/total):0;if(pct!=last){last=pct;progress.Report(pct);}
        }
        if((Size>0&&count!=Size)||!Convert.ToHexString(sha.GetHashAndReset()).Equals(Digest,StringComparison.OrdinalIgnoreCase))throw new IOException("SHA-256 不匹配");
        download.Position=0;using var zip=new ZipArchive(download,ZipArchiveMode.Read,true);
        var directory=Directory.CreateDirectory(Path.Combine(Store,folder));
        foreach(string file in new[]{"SoundMesh.exe","SoundMeshCapture.exe","guide.html"}){
            var entry=zip.Entries.Single(e=>e.FullName=="Windows/"+file);
            if(entry.Length<=0||entry.Length>536870912)throw new IOException("文件大小异常");
            entry.ExtractToFile(Path.Combine(directory.FullName,file));
        }
        var info=FileVersionInfo.GetVersionInfo(Exe(folder));
        if(new Version(info.FileMajorPart,info.FileMinorPart,info.FileBuildPart)!=Version)throw new IOException("程序与发布版本不匹配");
        return folder;
    }
    public static void Activate(string folder){
        var start=new ProcessStartInfo(Exe(folder)){WorkingDirectory=Path.GetDirectoryName(Exe(folder))!,UseShellExecute=false};
        start.ArgumentList.Add("--finish-update");start.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(start);Lan.Save("installedRelease",folder);
    }
}

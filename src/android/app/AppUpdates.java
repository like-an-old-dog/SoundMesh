package app.soundmesh.audio;

import android.app.*;
import android.content.*;
import android.content.pm.*;
import android.net.Uri;
import android.provider.Settings;
import android.widget.Toast;
import org.json.*;
import java.net.*;
import java.io.*;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.Arrays;
import java.util.concurrent.*;

final class AppUpdates {
    private static final String REPO="like-an-old-dog/SoundMesh";
    private final MainActivity ui;
    private volatile boolean manualRequested;
    private final ExecutorService worker=Executors.newSingleThreadExecutor();
    private boolean busy;
    private volatile boolean closed;
    private volatile HttpURLConnection active;
    private String readyVersion;
    AppUpdates(MainActivity activity){ui=activity;}
    void close(){closed=true;worker.shutdownNow();HttpURLConnection c=active;if(c!=null)c.disconnect();}
    void post(Runnable task){ui.runOnUiThread(()->{if(!closed&&!ui.isFinishing())task.run();});}
    HttpURLConnection get(String url)throws Exception{
        HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();
        active=c;c.setConnectTimeout(10000);c.setReadTimeout(20000);c.setRequestProperty("User-Agent","SoundMesh-Android/1.3");
        int code=c.getResponseCode();if((code==403||code==429)&&url.startsWith("https://api.github.com/")){c.disconnect();throw new RateLimit();}
        if(code!=200||!"https".equals(c.getURL().getProtocol())){c.disconnect();throw new IOException("GitHub 请求失败 HTTP "+code);}
        return c;
    }
    private static final class RateLimit extends IOException {}
    static int compare(String a,String b){
        String[] x=a.split("\\."),y=b.split("\\.");
        for(int i=0;i<3;i++){int d=Integer.compare(Integer.parseInt(x[i]),Integer.parseInt(y[i]));if(d!=0)return d;}return 0;
    }
    void error(Exception e){post(()->{busy=false;new AlertDialog.Builder(ui).setTitle("更新失败").setMessage(e.getMessage()).setPositiveButton("关闭",null).show();});}
    void check(boolean manual){
        if(busy){if(manual)manualRequested=true;return;}busy=true;manualRequested=manual;
        worker.execute(()->{try{
            HttpURLConnection c;try{c=get("https://api.github.com/repos/"+REPO+"/releases/latest");}catch(RateLimit limited){fallback(manual);return;}
            JSONObject release;
            try(InputStream in=c.getInputStream();ByteArrayOutputStream bytes=new ByteArrayOutputStream()){
                byte[] buf=new byte[8192];int n;while((n=in.read(buf))!=-1){bytes.write(buf,0,n);if(bytes.size()>1048576)throw new IOException("发布说明过大");}
                release=new JSONObject(bytes.toString("UTF-8"));
            }finally{c.disconnect();}
            String tag=release.getString("tag_name");
            if(release.getBoolean("draft")||release.getBoolean("prerelease")||!tag.matches("v[0-9]+\\.[0-9]+\\.[0-9]+"))throw new IOException("正式版本标签无效");
            String ver=tag.substring(1);
            String installed=ui.getPackageManager().getPackageInfo(ui.getPackageName(),0).versionName;
            if(compare(ver,installed)<=0){post(()->{busy=false;if(manualRequested)Toast.makeText(ui,"已是最新版本 "+installed,Toast.LENGTH_LONG).show();});return;}
            String name="SoundMesh-Android-"+ver+".apk";
            JSONArray assets=release.getJSONArray("assets");JSONObject asset=null;
            for(int i=0;i<assets.length();i++)if(name.equals(assets.getJSONObject(i).getString("name")))asset=assets.getJSONObject(i);
            if(asset==null)throw new IOException("Release 缺少 "+name);
            String url="https://github.com/"+REPO+"/releases/download/"+tag+"/"+name;
            String digest=asset.getString("digest");long size=asset.getLong("size");
            if(!url.equals(asset.getString("browser_download_url"))||!digest.matches("sha256:[0-9a-f]{64}")||size<=0||size>536870912L)throw new IOException("资产校验信息无效");
            post(()->{busy=false;new AlertDialog.Builder(ui).setTitle("声汇 "+ver).setMessage("从 GitHub 下载并安装？安装会结束当前融合。")
                .setPositiveButton("下载并安装",(d,w)->download(ver,url,size,digest.substring(7))).setNegativeButton("稍后",null).show();});
        }catch(Exception e){if(manualRequested)error(e);else post(()->busy=false);}});
    }
    private void fallback(boolean manual)throws Exception{
        HttpURLConnection c=get("https://github.com/"+REPO+"/releases/latest/download/SHA256SUMS.txt");
        String digest=null,version=null;
        java.util.regex.Pattern pattern=java.util.regex.Pattern.compile("^([0-9a-f]{64})\\s+\\*?SoundMesh-Android-([0-9]+\\.[0-9]+\\.[0-9]+)\\.apk$");
        try(BufferedReader reader=new BufferedReader(new InputStreamReader(c.getInputStream(),"UTF-8"))){
            for(int i=0;i<100;i++){String line=reader.readLine();if(line==null)break;
                java.util.regex.Matcher m=pattern.matcher(line.trim());if(m.matches()){digest=m.group(1);version=m.group(2);break;}}
        }finally{c.disconnect();}
        if(version==null)throw new IOException("正式发布校验清单缺少安卓安装包");
        String installed=ui.getPackageManager().getPackageInfo(ui.getPackageName(),0).versionName;
        if(compare(version,installed)<=0){post(()->{busy=false;if(manualRequested)Toast.makeText(ui,"已是最新版本 "+installed,Toast.LENGTH_LONG).show();});return;}
        final String ver=version,hash=digest,url="https://github.com/"+REPO+"/releases/download/v"+ver+"/SoundMesh-Android-"+ver+".apk";
        post(()->{busy=false;new AlertDialog.Builder(ui).setTitle("声汇 "+ver).setMessage("下载并安装新版？安装会结束当前融合。")
            .setPositiveButton("下载并安装",(d,w)->download(ver,url,-1,hash)).setNegativeButton("稍后",null).show();});
    }
    private void verifyArchive(File file,String ver)throws Exception{
        PackageManager pm=ui.getPackageManager();
        PackageInfo next=pm.getPackageArchiveInfo(file.getPath(),PackageManager.GET_SIGNING_CERTIFICATES);
        PackageInfo now=pm.getPackageInfo(ui.getPackageName(),PackageManager.GET_SIGNING_CERTIFICATES);
        if(next==null||!now.packageName.equals(next.packageName)||!ver.equals(next.versionName)||next.getLongVersionCode()<=now.getLongVersionCode()||
            next.signingInfo==null||!Arrays.equals(next.signingInfo.getApkContentsSigners(),now.signingInfo.getApkContentsSigners()))
            throw new IOException("APK 包名、版本或签名不匹配");
    }
    void download(String ver,String url,long size,String digest){
        if(busy)return;busy=true;
        android.app.ProgressDialog progress=new android.app.ProgressDialog(ui);
        progress.setTitle("下载声汇 "+ver);progress.setProgressStyle(android.app.ProgressDialog.STYLE_HORIZONTAL);progress.setCancelable(false);progress.show();
        worker.execute(()->{
            File temp=null;HttpURLConnection c=null;
            try{
                temp=File.createTempFile("soundmesh-", ".apk",ui.getCacheDir());c=get(url);
                MessageDigest hash=MessageDigest.getInstance("SHA-256");
                long count=0;int last=-1;
                try(InputStream in=c.getInputStream();OutputStream out=new FileOutputStream(temp)){
                    byte[] buf=new byte[32768];int n;while((n=in.read(buf))!=-1){
                        if(closed||Thread.interrupted())throw new IOException("已取消下载");count+=n;if(count>536870912L||(size>0&&count>size))throw new IOException("下载大小异常");
                        hash.update(buf,0,n);out.write(buf,0,n);long total=size>0?size:c.getContentLengthLong();int pct=total>0?(int)Math.min(100,100*count/total):0;
                        if(pct!=last){last=pct;post(()->progress.setProgress(pct));}
                    }
                }
                StringBuilder hex=new StringBuilder();for(byte b:hash.digest())hex.append(String.format(java.util.Locale.ROOT,"%02x",b&255));
                if((size>0&&count!=size)||!digest.equals(hex.toString()))throw new IOException("SHA-256 校验失败");
                verifyArchive(temp,ver);
                Files.move(temp.toPath(),UpdateProvider.apk(ui).toPath(),StandardCopyOption.REPLACE_EXISTING);
                post(()->{progress.dismiss();busy=false;readyVersion=ver;ui.getSharedPreferences("update",0).edit().putString("ready",ver).apply();install();});
            }catch(Exception e){post(progress::dismiss);error(e);}
            finally{if(c!=null)c.disconnect();if(temp!=null)temp.delete();}
        });
    }
    void afterPermission(){
        readyVersion=ui.getSharedPreferences("update",0).getString("ready",null);
        if(readyVersion==null)return;
        if(ui.getPackageManager().canRequestPackageInstalls())install();
        else Toast.makeText(ui,"安装需要允许声汇安装应用，可从工具 → 检查更新重试",Toast.LENGTH_LONG).show();
    }
    private void install(){try{
        if(!ui.getPackageManager().canRequestPackageInstalls()){
            ui.startActivityForResult(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,Uri.parse("package:"+ui.getPackageName())),707);return;
        }
        verifyArchive(UpdateProvider.apk(ui),readyVersion);
        Intent installer=new Intent(Intent.ACTION_VIEW).setDataAndType(UpdateProvider.URI,"application/vnd.android.package-archive")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        installer.setClipData(ClipData.newRawUri("声汇更新",UpdateProvider.URI));ui.startActivity(installer);
    }catch(Exception e){error(e);}}
}
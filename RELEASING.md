# SoundMesh 正式版发布

客户端读取 [like-an-old-dog/SoundMesh 的最新正式 Release](https://github.com/like-an-old-dog/SoundMesh/releases/latest)。提交代码和推送分支后，还需发布 Release，客户端下次检查时才能发现新版本。旧版 1.2.2 及以下需要手动安装一次 1.3.0。

1. 选一个递增的三段版本号，例如 1.3.1。修改 Windows 的 SoundMesh.csproj Version 和安卓 AndroidManifest.xml 的 versionName，并让 versionCode 严格递增（1.3.0 为 10）。更新 README、更新记录和指南。
2. 在 Windows 上运行 src/tests/run.ps1，构建自包含 win-x64 EXE、对应的捕获助手、guide.html 及安卓 APK。安卓必须沿用已分发安装包的包名 app.soundmesh.audio 和签名证书。把原 keystore 放在受控、已备份的构建环境。发布前用 apksigner verify --print-certs 比较证书 SHA-256。仓库、Release 和源码包中都不要放签名私钥。
3. Windows 压缩包必须包含 Windows/SoundMesh.exe、Windows/SoundMeshCapture.exe 和 Windows/guide.html，路径区分大小写。主 EXE 的文件版本前三段应与标签一致。保留现有许可和说明。
4. 上传四个独立 Release assets。以 1.3.1 为例，精确名称为 SoundMesh-Windows-1.3.1.zip、SoundMesh-Android-1.3.1.apk、SoundMesh-Source-1.3.1.zip 和 SHA256SUMS.txt。不要使用浏览器产生的 .1 等后缀。
5. 创建标签 v1.3.1 指向本次构建的提交。可先保存草稿，等资产全部上传再发布，并将该正式版设为 Latest。草稿和预发布不参与客户端自动更新。若发现坏包，递增版本和 versionCode 发修订版。
6. 在已有 1.3.0 的设备上点「工具 → 检查更新」，测试确认、下载、安装、重开及原配对信息。Windows 使用当前用户目录，旧启动入口会转到已安装的新版。安卓由系统完成最后一次安装确认。

在装有 PowerShell 的发布资产目录生成校验清单（把版本号替换为本次版本）：

```powershell
$version = '1.3.1'
$names = @("SoundMesh-Windows-$version.zip", "SoundMesh-Android-$version.apk", "SoundMesh-Source-$version.zip")
$lines = foreach ($name in $names) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $name -ErrorAction Stop).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path (Get-Location) 'SHA256SUMS.txt'), [string[]]$lines, [Text.UTF8Encoding]::new($false))
```

普通更新读取 GitHub Releases API 提供的 sha256 digest。匿名 API 返回限流状态时，应用尝试读取同一仓库 Latest Release 的 SHA256SUMS.txt。若缺资产、摘要错误、Windows 主程序版本错误或 APK 签名不符，安装中止。检查和下载均需用户网络能够访问 GitHub 以及其发布资产 CDN。

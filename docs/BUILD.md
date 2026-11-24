# 构建指南

## 目录

- [环境要求](#环境要求)
- [Windows 客户端](#windows-客户端)
- [Windows 进程采集中间层](#windows-进程采集中间层)
- [Android](#android)
- [iOS](#ios)
- [测试](#测试)
- [打包发布](#打包发布)
- [常见问题](#常见问题)

---

## 环境要求

| 目标 | 需要 |
| --- | --- |
| Windows 客户端 | .NET SDK 8.0、Windows 10/11 x64 |
| Windows 采集进程 | Visual Studio 2022（含 C++ 桌面开发）+ Windows 11 SDK（`AUDIOCLIENT_ACTIVATION_PARAMS` 依赖它） |
| Android | JDK 21、Android SDK Platform 35、Build Tools 35.0.0 |
| iOS | macOS + Xcode 16+ + [XcodeGen](https://github.com/yonaskolb/XcodeGen) |
| 测试 | .NET SDK 8.0、JDK 21（Java 侧互通测试） |

本项目**不使用 Gradle**，Android 侧由 `src/android/build.ps1` 直接调用 `javac → d8 → aapt2 → zipalign → apksigner`。这是刻意的：整条链路只依赖 SDK 命令行工具，没有额外构建系统。

---

## Windows 客户端

```powershell
dotnet publish src/windows/SoundMesh.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Windows
```

产物：

- `Windows/SoundMesh.exe` —— 单文件自包含，约 189 MB（含 .NET 运行时）。
- `Windows/SoundMeshCapture.exe` —— 需要单独构建，见下一节。

依赖（NuGet，无需手动安装）：NAudio 2.2.1、ZXing.Net 0.16.11。iPhone 网页桥接通过 `<FrameworkReference Include="Microsoft.AspNetCore.App" />` 使用框架自带的 Kestrel，网页资源以 `EmbeddedResource` 嵌入程序集。

> 想减小体积，可以只做框架依赖发布（去掉 `--self-contained` 与 `PublishSingleFile`），但目标机必须预装 .NET 8 Desktop Runtime。

---

## Windows 进程采集中间层

`SoundMeshCapture.exe` 用 WASAPI 的 `ActivateAudioInterfaceAsync` + `PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE` 采集"除本进程树以外"的系统声音，PCM 从 stdout 输出。

1. 打开 **x64 Native Tools Command Prompt for VS 2022**。
2. 执行：

   ```bat
   cd src\windows\native
   build.cmd
   ```

3. 把生成的 `SoundMeshCapture.exe` 复制到 `SoundMesh.exe` 所在目录。

它的调用约定是 `SoundMeshCapture.exe <要排除的进程ID>`，就绪时向 stderr 打印 `READY`，之后持续向 stdout 写 48 kHz / 16 bit / 2ch 交织 PCM。

**必须与主程序同目录**，否则「共享时静音电脑当前输出」与双向模式会报"无法启动独立采集"。

---

## Android

```powershell
./src/android/build.ps1 `
  -AndroidJar 'C:/Android/sdk/platforms/android-35/android.jar' `
  -BuildTools 'C:/Android/sdk/build-tools/35.0.0'
```

可选参数：

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-JavaHome` | `C:\Program Files\Java\jdk-21` | JDK 路径 |
| `-BuildDirectory` | `src/android/build` | 中间产物目录 |
| `-OutputApk` | `../../SoundMesh-Android.apk` | 输出 APK |

流程：`javac`（source/target 8）→ `jar` 打包 classes → `d8 --release --min-api 29` 生成 dex → `aapt2 link` 生成资源与清单 → 塞入 `classes.dex` → `zipalign` → `apksigner` 签名 → `apksigner verify`。

签名密钥在 `build/prototype.jks`（密码都是 `android`，别名 `prototype`），**只在本地首次构建时生成**。它不是你自己的发布密钥，仅供开发安装：

- 换一个构建目录 = 换一把新密钥 = **无法覆盖安装**已签名的版本，必须先卸载。
- 想长期分发，请把 `prototype.jks` 换成你自己的密钥库，并妥善保存。

`AndroidManifest.xml` 里的 `versionCode` / `versionName` 需要手动改：当前为 `8` / `1.2.2`。

---

## iOS

iOS 原生版只有源码工程，没有预编译 IPA。

```bash
cd src/ios
# 先把 project.yml 里的 PRODUCT_FAMILY_ID 改成你自己可签名的标识
# APP_GROUP_ID 必须是同一开发团队已开通、主应用与扩展共用的 App Group
xcodegen generate
open SoundMesh.xcodeproj
```

在 Xcode 里为**两个 target**（`Phone` 与 `Broadcast`）选择同一个 Team，确认 App Groups 能力，然后编译运行。测试分发走团队的 TestFlight。

细节见 [src/ios/README.md](../src/ios/README.md)。

**使用教程**：`src/help/guide.html` 被 `SoundMesh.csproj` 同时作为 `Content`（复制到输出目录，`ExcludeFromSingleFile`）与 `EmbeddedResource`（`Web.guide.html`）处理——前者供 Windows 端直接打开，后者供局域网桥接。安卓侧由 `GuideActivity` 读取随包资源，三端共用同一份 HTML。

---

## 测试

```powershell
# C# 侧单元与集成检查
./src/tests/run.ps1

# 追加 Windows 采集烟测（需要 SoundMeshCapture.exe 已构建）
./src/tests/run.ps1 -CaptureSmoke
```

测试工程是 `src/tests/AudioTests.csproj`（控制台程序，无测试框架依赖），覆盖：

- 缓冲预滚、PCM 有符号小端转换、积压上限、陈旧样本丢弃、欠载静音
- 逐路增益 / 静音 / 总音量的组合，66 路混音不越界，总静音输出为零
- 超大握手与截断帧被拒绝，错误配对码被拒绝
- 跨语言互通：`src/tests/WireProbe.java` 与 C# 端互相收发

Android 侧另有一套 JVM 测试（AudioRing / MixBus / Timeline / Wire 的纯 Java 逻辑），可直接用 java 跑，不需要模拟器。

推送到 GitHub 后，.github/workflows/audio-ci.yml 会在 windows-latest 上自动执行：dotnet build 客户端、JVM 音频与时间轴测试、C# 检查、以及跨语言协议测试。

---

## 图标与品牌素材

`src/brand` 里是原始素材与生成脚本：

| 文件 | 用途 |
| --- | --- |
| `mark.png` / `wave.png` | 主标记与会话声波插图 |
| `make_assets.py` | 生成 Windows 多分辨率 `.ico`、Android 自适应启动器（`mipmap-*`）、通知图标与 `res/values/brand.xml` |

生成后的资源已经提交在仓库里，正常构建不需要再跑一次；换品牌素材时才需要：

```bash
python src/brand/make_assets.py
```

Windows 客户端通过 `<ApplicationIcon>SoundMesh.ico</ApplicationIcon>` 与嵌入资源（`Brand.Icon` / `Brand.Mark` / `Brand.Wave`）使用这些素材；`SoundMesh.csproj` 里的 `EmbeddedResource` 负责把它们打进程序集。

## 打包发布

1.2.2 的发布产物是四个文件，均由上面的构建流程产生：

| 文件 | 来源 |
| --- | --- |
| `SoundMesh-Windows-1.2.2.zip` | `Windows/` 目录（两个 exe + `THIRD-PARTY/` + README）整包压缩 |
| `SoundMesh-Android-1.2.2.apk` | `build.ps1` 输出，重命名 |
| `SoundMesh-Source-1.2.2.zip` | `src/` + `THIRD-PARTY/` + 文档，不含构建产物 |

发布前请确认：`src/android/AndroidManifest.xml` 的版本号已更新、`docs/TEST-RESULTS-1.2.2.txt` 是最新一次验证的日志、SHA256 已重新计算。

---

## 常见问题

**`dotnet publish` 报缺少 Windows Desktop 目标框架**
需要 .NET SDK 8.0（不是 Runtime）；`SoundMesh.csproj` 使用 `net8.0-windows` + `UseWindowsForms`。

**`build.cmd` 找不到 `audioclientactivationparams.h`**
Windows 11 SDK 没装，或没有在 x64 Native Tools 环境里执行。

**`d8.bat` / `aapt2.exe` 找不到**
`-BuildTools` 指向的目录不对，应形如 `C:\Android\sdk\build-tools\35.0.0`（目录里直接有这些 exe）。

**APK 装上去提示"应用未安装"**
签名不一致。旧版本是别的密钥签的，先卸载旧版再装。

**`run.ps1 -CaptureSmoke` 失败**
`SoundMeshCapture.exe` 不在主程序目录，或当前系统版本低于 build 20348（不支持进程回环）。

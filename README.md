<div align="center">

# 声汇 · SoundMesh

**把同一个局域网里几台设备的声音，汇到一副耳机里。**

Windows · Android · iPhone 网页试听 —— 一次「开始融合」同时收发，多路时间戳同步，48 kHz / 16 bit 立体声

[![Release](https://img.shields.io/github/v/release/like-an-old-dog/SoundMesh?style=flat-square&label=release)](https://github.com/like-an-old-dog/SoundMesh/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20%C2%B7%20Android%2010%2B%20%C2%B7%20iOS%2016%2B-1c1f43?style=flat-square)](#平台支持)
[![Audio](https://img.shields.io/badge/audio-48%20kHz%20%C2%B7%2016%20bit%20%C2%B7%20stereo-1c1f43?style=flat-square)](#音质延迟与带宽)
[![Status](https://img.shields.io/badge/status-1.3.0%20release-4c43c1?style=flat-square)](#已知限制)
[![License](https://img.shields.io/badge/license-All%20Rights%20Reserved-8b3a3a?style=flat-square)](NOTICE.md)

[快速开始](#快速开始) · [功能](#能做什么) · [工作原理](#工作原理) · [构建](#从源码构建) · [已知限制](#已知限制) · [English](README.en.md)

</div>

![声汇 Windows 客户端](docs/images/windows-session.png)

---

## 这是什么

电脑上的音乐、手机里的视频、平板上的游戏——通常它们各自只从自己的扬声器出声。**声汇 SoundMesh 把这些来自不同设备的声音收进同一条局域网音频链路，在一台设备上合成一路混音，送进你正戴着的那副耳机。**

1.1 起每台设备只有一种模式：**「开始融合」= 同时把本机声音发出去、同时接收别人的声音**。之前版本要在「共享 / 监听」之间切换，现在两端点同一个按钮，配对一次就双向通了。

1.2 再往前一步：**时间戳同步播放**。发送端给每帧打上采集时刻，接收端把所有远端通道放进同一条播放时间轴，几台设备的声音不再各差几十毫秒，混在一起也不会互相错位。

- 🎧 **一副耳机听所有设备**——耳机在电脑上，手机声音也进电脑；耳机在手机上，电脑声音也进手机。
- 🎚️ **四层音量**——每台远端设备、其他设备合计、本机发送、本机应用（Windows 还能按比例压低其他应用的音量）。
- 📱 **系统媒体卡片联动**——Android 下拉通知栏拖动媒体进度条就是远端混音总音量，轻点展开完整调音面板。
- 🕒 **多路对齐播放**——各远端通道按统一时间轴取样，单路迟到只丢它自己的一段，不拖累其他通道。
- 🌐 **iPhone 免安装试听**——Safari 扫码即听，无需 IPA、无需开发者签名。
- 📖 **内置离线教程**——Windows、安卓与 iPhone 网页都有入口，7 章 HTML，开始融合之前就能打开。
- 🩺 **环境检测**——「工具 → 检查环境」对局域网对端做 20 次应用层探测，给出成功耗时、超时、P95 与波动范围，用来判断"是网络不通还是协议不对"。
- 🔄 **应用内更新**——启动时自动查 GitHub 最新正式版，也可手动「工具 → 检查更新」；下载后校验 SHA-256、版本号与签名再安装，Windows 按用户目录并存新旧版本，安卓交给系统安装器。

> 没有账号、没有云端、没有中转服务器：音频帧只在局域网内从发送端直连到接收端。链路是**明文**的，请只在可信网络使用。

---

## 能做什么

| 场景 | 怎么做 | 结果 |
| --- | --- | --- |
| **耳机在电脑上** | 两台设备都点「开始融合」并配对，在电脑点「把监听切到本机」 | 手机声音进电脑耳机，电脑自己的声音照常播放 |
| **耳机在手机上** | 同上，只是把监听切到手机 | 电脑声音进手机耳机 |
| **大家一起看片** | 任意台设备接管监听，其余设备同样「开始融合」 | 一路混音同时输出，逐路调音量 |
| **三台以上** | 每两台之间连接一次（A–B、A–C、B–C） | 每台都能独立混合其余设备的来源 |
| **只想快速听一下 iPhone** | 电脑「设备连接」→「邀请 iPhone 网页监听」→ 扫码 | Safari 里直接播放，还能加别的设备一起混 |

### 界面

| 混音台 | 设备连接 |
| --- | --- |
| ![混音台](docs/images/windows-session.png) | ![设备连接](docs/images/windows-devices.png) |

| 输出设置 | 音量小窗（`Ctrl+Alt+M`） |
| --- | --- |
| ![输出设置](docs/images/windows-output.png) | ![音量小窗](docs/images/quick-mixer.png) |

| iPhone 浏览器监听（Safari 中打开） |
| --- |
| ![iPhone 网页监听](docs/images/iphone-web.png) |

### 三个值得单独说的点

**监听与本地播放是分开的。** 接管监听只影响"来自其他设备"的那部分声音，本机其他应用仍由系统直接播放。你不用为了听远端的声音，把自己的声音绕一圈再回来。

**发送端不会把自己的混音送回去。** Windows 用独立的 `SoundMeshCapture.exe` 做 WASAPI 进程回环，并排除声汇自身的进程树；Android 用 `AudioPlaybackCapture` 的 UID 排除。否则 A 听到的 B 会被再次编码发回 B，形成回声。

**同步播放与独立缓冲是两套机制。** 两端都是 1.2 时走时间戳协议：连接时用 5 次往返取最短 RTT 校时，之后每分钟复测；接收端按统一播放时间（同步 100 / 150 / 200 ms，默认 150 ms）从时间轴取各通道样本，多台设备因此天然对齐。与旧版本或 iPhone 网页连接时自动回退到**独立缓冲**：起始 25 / 70 / 90 ms，断流时临时抬高，上限 160 ms；积压就丢旧样本——延迟不会随时间增长，代价是极端抖动下会有一次轻微断音。

---

## 平台支持

| 平台 | 状态 | 采集方式 | 系统要求 |
| --- | --- | --- | --- |
| **Windows x64** | ✅ 正式版 | WASAPI 进程回环（独立 native 进程） | Windows 10 / 11 x64 |
| **Android** | ✅ 正式版 | `MediaProjection` + `AudioPlaybackCapture` | Android 10（API 29）及以上 |
| **iPhone 网页监听** | ⚠️ 桌面 Chromium 已测 | 由电脑端桥接转发 | iOS 16+ Safari，同一局域网 |
| **iOS 原生 App** | 🧪 上一版开发稿 | ReplayKit Broadcast Upload | iOS 16+，需 Xcode 自行签名 |

- 「开始融合」在 Windows 上使用进程隔离采集，需要 **build 20348+（通常 Win11）**；低版本会在启动时给出提示。
- Android 与 1.0 使用相同签名，可覆盖安装；覆盖 0.3 也可。
- 协议与 0.2/0.3 一致，可与旧版本互连；0.1 是单声道协议，不兼容。

---

## 快速开始

### 你需要

- 两台以上设备接在**同一个** Wi-Fi / 有线局域网，且彼此能互相访问（访客 Wi-Fi、AP 隔离、企业网络常见地不行）。
- Windows：解压整个 `SoundMesh-Windows-1.3.0.zip`，运行 `Windows\SoundMesh.exe`，并让 `SoundMeshCapture.exe` 与 `guide.html` **留在同一目录**。
- Android：安装 `SoundMesh-Android-1.3.0.apk`。

### 三步连起来

1. **每台都点「开始融合」**（Android 按系统提示授权共享应用音频）。
2. **配对一次**：在任一端选「附近设备」，或扫对方的配对二维码。一对设备配对一次，之后就同时收发了。
3. **耳机接哪台，就在哪台点「把监听切到本机」**。

之后只需要第 1 步和第 3 步。

### 如果连不上

| 现象 | 先检查 |
| --- | --- |
| 「附近设备」一直是空的 | 两端是否真的在同一子网；Windows 防火墙是否放行 UDP 49633 与 TCP 49632 |
| 列表里有地址，却连不上 | 电脑有多块网卡（无线/有线/VPN/虚拟网卡）时，用「手动配对」直接填对端的局域网 IP |
| 手机是 `192.168.3.7`，电脑却显示 `192.168.56.1` | 电脑连错网卡了，多半是 VPN 或虚拟网卡。设备连接页可以手动改选地址 |
| 时断时续 | 把缓冲切到「稳定 · 90 ms」，并远离 2.4 GHz 拥塞信道 |
| 只有一边有声音 | 确认耳机所在设备点了「把监听切到本机」，且该页的「在本机监听」是勾选状态 |

发现走 UDP 广播 49633，音频走 TCP 49632。请只在可信网络使用。

---

## 调音

| 滑杆 | 作用 |
| --- | --- |
| 每台远端设备 | 该设备声音在当前监听端的音量，可单独静音 |
| **其他设备合计** | 所有远端通道的总音量（Android 媒体卡片拖的就是它） |
| **本机发送** | 本机发出去的音量，所有接收方都会听到变化 |
| **本机应用**（仅 Windows） | 按比例压低默认输出上**其他应用**的音量，跳过声汇自己的会话；结束融合时恢复 |
| **本机 · 系统媒体输出**（仅 Android） | 调节当前音频路由的系统媒体音量，也会影响耳机里声汇的声音 |

- **Windows**：`Ctrl+Alt+M` 呼出音量小窗，下拉列表可选「远端混音总音量 / 本机应用 / 本机 · 发给其他设备 / 某台设备」，`Esc` 收起。
- **Android**：下拉展开系统媒体卡片，拖动进度条调远端混音总音量；轻点卡片打开「声汇音量」面板，可分别拖本机发送、系统媒体输出、远端总音量和各设备。不同厂商的紧凑布局可能隐藏进度条，Android 13+ 需允许通知。

> Android 无法只静音其他应用的本地扬声器，所以用「本机 · 系统媒体输出」做折中：它会连声汇自己的播放一起调小。想保持采集电平又不想外放，把发送端媒体路由到闲置的有线耳机或 USB 声卡更稳。

---

## 音质、延迟与带宽

| 项目 | 数值 |
| --- | --- |
| 采样格式 | 48 000 Hz，16 bit，立体声 |
| 音频帧 | 10 ms / 帧（1920 字节） |
| 单路码率 | ≈ **1.54 Mbps** + 协议开销 |
| 同步播放缓冲 | 100 / 150（默认） / 200 ms，按统一时间轴对齐多路 |
| 独立缓冲（旧设备 / 网页） | 起始 25 / 70 / 90 ms，断流自适应，上限 160 ms |
| 断线重试 | 1 → 2 → 4 → 8 s 指数退避 |
| 并发路数 | 协议不设上限；实测混音 66 路不削波，真实容量取决于网络与设备 |

端到端延迟 = 系统采集 + 网络 + 接收缓冲 + 系统播放（+ 蓝牙耳机自身缓冲）。界面里的进度条显示的是**接收队列中当前积压的音频时长**，不是端到端延迟。

---

## 工作原理

```mermaid
flowchart LR
  subgraph 设备 A（融合中）
    A1[系统声音] --> A2[采集<br/>WASAPI 进程回环 / MediaProjection]
    A2 --> A3[48 kHz · 16 bit · 立体声<br/>10 ms 分帧]
    A3 --> A4[(TCP 49632)]
  end
  subgraph 局域网
    A4 <--> N[UDP 49633 发现<br/>TCP 49632 音频]
  end
  subgraph 设备 B（融合中）
    N <--> B1[自适应缓冲<br/>AudioRing]
    B1 --> B2[逐路增益 · 静音]
    B2 --> B3[其他设备合计]
    B3 --> B4[耳机输出]
    N -.->|TCP 49634 HTTP + WebSocket| W[iPhone Safari<br/>Web Audio]
  end
```

**协议概要**（细节见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)）：

| 用途 | 端口 | 形式 |
| --- | --- | --- |
| 设备发现 | UDP 49633 | 广播 `SOUNDMESH FIND 2`，应答 `SM2\|<id>\|<name>` |
| 时间戳音频 | TCP 49632 | SOUNDMESH3 <pin> [<reversePin>] → OK TIME3，随后 5 次 PING 校时；帧为 1928 字节（8 字节时间戳 + 1920 字节 PCM） |
| 兼容音频 | TCP 49632 | SOUNDMESH2 <pin> → OK 48000 2，1920 字节纯 PCM 帧，接收端独立缓冲 |
| 监听接管 | TCP 49632 | SM2-FOCUS <pin> → OK FOCUS，通知对端暂停监听 |
| 时钟复测 | TCP 49632 | 短连接 CLOCK，对端应答 5 次校时 |
| iPhone 桥接 | TCP 49634 | HTTP 静态资源 + 带令牌的 WebSocket，令牌只存在于 URL fragment |

配对码是设备本地生成并持久化的 8 位数字（Windows 存于 `HKCU\Software\SoundMesh\Pairing`）。二维码内容是 `soundmesh://pair/<ip>/<pin>/<id>`，扫描不经过任何第三方服务。

---

## 从源码构建

完整步骤见 [docs/BUILD.md](docs/BUILD.md)，这里是最短路径。

**Windows 客户端**（.NET 8 + NAudio + ZXing.Net，WinForms）：

```powershell
dotnet publish src/windows/SoundMesh.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Windows
```

**Windows 采集进程**（进程回环，需要 Windows 11 SDK）：在 *x64 Native Tools Command Prompt* 中执行 `src\windows\native\build.cmd`，把产出的 `SoundMeshCapture.exe` 放到主程序旁边。

**Android**（JDK 21 + SDK Platform 35 + Build Tools 35.0.0，无需 Gradle）：

```powershell
./src/android/build.ps1 -AndroidJar 'C:/Android/sdk/platforms/android-35/android.jar' `
  -BuildTools 'C:/Android/sdk/build-tools/35.0.0'
```

**测试**：`./src/tests/run.ps1 -CaptureSmoke`

**图标与插图**：`src/brand` 里有品牌素材与 `make_assets.py`，用于生成 Windows 多分辨率图标、Android 自适应启动器与通知图标。

**iOS**：在 Mac 上用 XcodeGen 打开 `src/ios`，详见 [src/ios/README.md](src/ios/README.md)。仓库里不含 IPA。

> Android 构建脚本会把测试签名密钥留在本机 `build/` 目录。换目录重新构建会得到不同密钥，**无法覆盖安装**已有版本。

---

## 测试与验证

日志见 [docs/TEST-RESULTS-1.3.0.txt](docs/TEST-RESULTS-1.3.0.txt)。要点：

- **1.2.1 的两个修复**：采集积压突然增大时，发送端时钟沿上一块继续前进（不再回退），接收端对校时后的时间做范围校验，早于接收时刻 2 秒或晚于 50 ms 的块按非法时间戳丢弃并计数。
- **1.2.2 的教程**：Windows ZIP 与 APK 内嵌同一份七章离线 HTML；模拟器验证「首页打开教程 → 返回首页」，Chromium 移动视口验证网页教程的章节导航与返回混音台。
- **1.3.0 的环境检测**：安卓模拟器作为应答端 20/20 成功，Windows 端探测 20/20，中位数 1 ms、P95 4 ms、波动 7 ms（模拟器网络）。
- **1.3.0 的更新链路**：Windows 更新器解析线上 GitHub 最新版 v1.2.2 与发布摘要，拒绝被篡改的包，并正确暂存三个生产文件；安卓在受控 HTTPS 源上完成摘要／签名／版本校验，经私有只读内容提供器交给系统安装器，versionCode 9 → 10。

- **31 项 C# 检查通过**：预缓冲、PCM 有符号转换、积压上限、陈旧样本丢弃、欠载静音、逐路/总/发送增益组合、66 路混音不越界、总静音、超大握手与截断帧拒绝、错误配对拒绝。
- 新增的**发送侧音量**在 Java 与 C# 两侧都验证了：只影响出站信号，不改变远端混音；能把出站压到静音。
- **同步与对齐**：两路来源在 100 / 150 / 200 ms 缓冲下，即使其中一路反复出现 80 ms 突发停顿，混音输出仍逐样本对齐；迟到片段被跳过且下一秒有效片段能恢复；重连会清掉旧时间轴。
- **跨语言互通**：Android Java 编解码器 ↔ C# 发送端/接收端，分片流、断连检测、强制重连后混音设置与双向提议都保留。
- **Android 12L 模拟器**：签名 APK 覆盖安装到 1.0 成功；系统媒体卡片实际拖动把混音从 80 改到 37 并由媒体会话上报；轻点卡片打开音量面板；横屏紧凑布局会隐藏系统进度条。
- **Windows**：发布版 x64 二进制带多分辨率图标；热键注册与 `WM_HOTKEY` 派发正常，主窗口与小窗滑杆双向同步；进程排除采集连续 30 帧；WASAPI → 本地 TCP 接收端。
- **互通**：Windows 音频引擎与 1.2 Android 客户端配对，带时间戳双向交换 500 帧，反向连接、校时、时间轴缓冲与监听接管都验证通过；APK 与 1.1 同签名并覆盖安装成功。

旧版日志保留在 [docs/TEST-RESULTS-1.1.txt](docs/TEST-RESULTS-1.1.txt)。

---

## 已知限制

**1.1 是正式版，但仍有明确的边界。**

- ⚠️ **同步只作用于参与声汇的远端音轨**：本机其他应用、iPhone 网页与旧版本设备不参与时间戳对齐，各自走独立缓冲。
- ⚠️ **iPhone 只验证到链路层**：网页播放链路在桌面 Chromium 上通过，实体 iPhone Safari 的实际播放、后台行为、蓝牙延迟尚未验证。
- ⚠️ **iOS 原生版是上一版开发稿**：只有源码与工程描述，没有编译、签名或装机测试。
- ⚠️ **未测量项**：各厂商 OEM 通知栏布局、真实声学输出电平、蓝牙多点耳机仲裁、Wi-Fi 抖动与端到端延迟。
- ⚠️ **明文传输**：配对码与音频都在局域网明文传输，无加密、无前向保密。请在可信网络使用。
- ⚠️ **Android 无法只静音其他应用的扬声器**，只能通过系统媒体音量折中（会连同声汇的播放一起变小）。
- Android 受保护的媒体内容与通话声音受系统限制，无法采集。
- 没有固定的并发路数上限，实际容量取决于网络与设备。

---

## 路线图

- [ ] iPhone Safari 真机验证，补齐 Web Audio 自动播放策略
- [ ] iOS 原生版：签名流程、TestFlight 分发、ReplayKit 采集验证
- [ ] 三设备拓扑与蓝牙多点耳机接管的实机测试
- [ ] 测量真实采集到耳机的端到端延迟，而不只是应用层补偿
- [ ] 在已装旧版的设备上把「检查更新 → 下载 → 重开」的完整用户路径走通（目前只验证了各环节）
- [ ] 校园网 / 手机热点下的探测数据现场复测
- [ ] 可选的链路加密（仍然不依赖服务器）
- [ ] 安装包体积优化（Windows 单文件自包含 ≈ 190 MB）

---

## 参与

- 版本变化见 [CHANGELOG.md](CHANGELOG.md)，使用细节见 [docs/USAGE.md](docs/USAGE.md)，完整方案与使用手册见 [docs/MANUAL.md](docs/MANUAL.md)。
- 漏洞与安全问题请先私下联系作者，不要直接开公开 issue。
- 提交前请确认 `./src/tests/run.ps1` 通过。源码包内含 `.github/workflows/audio-ci.yml`，把它加入仓库后，push 与 PR 会自动跑 Windows 构建、JVM 音频测试、C# 测试与跨语言协议检查。

## 作者

本项目由两位作者共同设计、实现与发布：

- **like-an-old-dog** — [@like-an-old-dog](https://github.com/like-an-old-dog)
- **2222223233** — [@2222223233](https://github.com/2222223233)

## 版权与第三方组件

本项目**保留所有权利**（All Rights Reserved），未采用开源许可证。源码公开用于阅读与评估，未经作者书面许可不得再分发或用于商业用途，详见 [NOTICE.md](NOTICE.md)。

界面布局参考了 Microsoft Fluent 2 的间距与分组、以及 Android Developers 的 Material 卡片用法；**图标与会话声波插图为声汇原创**。

第三方组件及许可证全文见 [THIRD-PARTY/](THIRD-PARTY/)：.NET 8 与 Windows Desktop Runtime（MIT）、NAudio 2.2.1（MIT）、ZXing.Net 0.16.11（Apache-2.0）、ZXing core（Apache-2.0）。

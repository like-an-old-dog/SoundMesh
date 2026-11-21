# 声汇 SoundMesh 1.1

**把同一个局域网里几台设备的声音，汇到一副耳机里。** 这次是一次交互与调音体系的重构：不再区分「共享端 / 监听端」，每台设备点同一个「开始融合」，配对一次即双向通。

> 本版本由 **like-an-old-dog**（[@like-an-old-dog](https://github.com/like-an-old-dog)）与 **2222223233**（[@2222223233](https://github.com/2222223233)）联合发布。

## 下载

| 文件 | 大小 | 用法 |
| --- | --- | --- |
| [`SoundMesh-Windows-1.1.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.1.0/SoundMesh-Windows-1.1.zip) | 76 MB | **整包解压**后运行 `Windows\SoundMesh.exe`，需与 `SoundMeshCapture.exe` 同目录 |
| [`SoundMesh-Android-1.1.apk`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.1.0/SoundMesh-Android-1.1.apk) | 473 KB | Android 10+，与 1.0 同签名，可覆盖安装 |
| [`SoundMesh-Source-1.1.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.1.0/SoundMesh-Source-1.1.zip) | 1.1 MB | 源码快照（与 tag `v1.1.0` 一致） |
| [`SHA256SUMS.txt`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.1.0/SHA256SUMS.txt) | — | 校验值 |

```
5dca19984e492c93f83a9906c31c76d17ec5e79d6e7c790ae6e5961c36a15bc5  SoundMesh-Windows-1.1.zip
8d92a6fd782fa55ba04b007c0d7fcd8163d74e111a0128f975cd35ce2aa5c363  SoundMesh-Android-1.1.apk
467370f866675e1855eaebe4bcc2c5b15543306b81440430053783e08ef5f62a  SoundMesh-Source-1.1.zip
```

## 1.1 的主要变化

- **单一融合会话**：每台设备都点「开始融合」，同时发送本机声音、同时接收对端声音。配对一次即打通收发两个方向，不用再反向配一次。
- **全新界面**：三页导航（混音台 / 设备连接 / 输出设置）＋ 顶部会话状态区；Windows 采用 Fluent 2 的间距与分组，深靛蓝主色，新增原创图标与会话声波插图。
- **四层增益**：每台远端设备、其他设备合计、本机发送、本机应用。发送侧增益只影响出站，不改变本地混音。
- **Windows「本机应用」**：按比例压低默认输出上其他应用的音量会话，跳过声汇自身；结束融合自动恢复，期间手动改过的会话不会被覆盖。
- **Android 系统媒体卡片**：媒体会话的音量就是远端混音总音量，下拉通知栏直接拖进度条即可；轻点打开「声汇音量」面板，含本机发送、系统媒体输出、远端总音量与逐设备滑杆。
- **监听接管**：耳机所在设备点「把监听切到本机」，通知对端暂停监听（`SM2-FOCUS`），避免同一份声音在两副耳机里重复。
- 播放缓冲档位保留：灵敏 25 ms / 均衡 70 ms（默认）/ 稳定 90 ms，断流时自适应抬高，上限 160 ms。

## 兼容性

- 音频协议 `2` 未变，**可与 0.2 / 0.3 设备互连**；0.1 是单声道协议，不兼容。
- Android 沿用 1.0 的签名，可覆盖安装 1.0 与 0.3。
- Windows「开始融合」使用进程隔离采集，需 **build 20348+**（通常 Windows 11）；低版本会在启动时提示。

## 验证情况

完整日志：[`docs/TEST-RESULTS-1.1.txt`](https://github.com/like-an-old-dog/SoundMesh/blob/main/docs/TEST-RESULTS-1.1.txt)

- **23 项 C# 检查通过**，含新增的发送侧增益检查：只影响出站、不改变远端混音、可压到静音。
- **跨语言互通**：Android Java 编解码器 ↔ C# 收发端，分片流、断连检测、强制重连后混音设置与反向提议都保留。
- **Android 12L 模拟器**：签名 APK 覆盖安装到 1.0 成功；系统媒体卡片实际拖动把混音从 80 改到 37 并由媒体会话上报；轻点打开音量面板；横屏紧凑布局会隐藏系统进度条。
- **Windows**：发布版 x64 二进制带多分辨率图标；热键注册与 `WM_HOTKEY` 派发正常，主窗口与小窗滑杆双向同步；进程排除采集连续 30 帧。
- **互通**：Windows 音频引擎与 1.1 Android 客户端配对，双向交换 500 帧并确认监听接管。

## 已知限制

- **iPhone 只验证到链路层**：网页播放链路在桌面 Chromium 上通过，实体 iPhone Safari 的实际播放、后台行为、蓝牙延迟尚未验证。
- **iOS 原生版仍是上一版开发稿**：没有编译、签名或装机测试。
- **未测量项**：各厂商 OEM 通知栏布局、真实声学输出电平、蓝牙多点耳机仲裁、Wi-Fi 抖动与端到端延迟。
- **明文传输**：配对码与音频都在局域网明文传输，无加密。请只在可信网络使用。
- **Android 无法只静音其他应用的扬声器**，只能用系统媒体音量折中（会连同声汇的播放一起变小）。
- Android 受保护的媒体内容与通话声音受系统限制，无法采集。

## 安装提示

**Windows**：解压整个 zip 再运行，不要单独把 exe 拖出来，「开始融合」需要同目录的 `SoundMeshCapture.exe`。首次运行请允许防火墙的专用网络访问。

**Android**：若提示"应用未安装"，说明旧版本是用其他密钥签名的，先卸载旧版。

---

<sub>问题反馈请开 Issue；安全相关问题请私下联系作者。</sub>

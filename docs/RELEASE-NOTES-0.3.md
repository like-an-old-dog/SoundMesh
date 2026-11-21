# 声汇 SoundMesh 0.3

**把同一个局域网里几台设备的声音，汇到一副耳机里。** Windows、Android 与 iPhone 网页试听，48 kHz / 16 bit 立体声多路混音。

> 本版本由 **like-an-old-dog**（[@like-an-old-dog](https://github.com/like-an-old-dog)）与 **2222223233**（[@2222223233](https://github.com/2222223233)）联合发布。


## 下载

| 文件 | 大小 | 用法 |
| --- | --- | --- |
| [`SoundMesh-Windows-0.3.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v0.3.0/SoundMesh-Windows-0.3.zip) | 76 MB | **整包解压**后运行 `SoundMesh.exe`，需与 `SoundMeshCapture.exe` 同目录 |
| [`SoundMesh-Android-0.3.apk`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v0.3.0/SoundMesh-Android-0.3.apk) | 256 KB | Android 10+，可覆盖安装 0.2 |
| [`SoundMesh-Source-0.3.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v0.3.0/SoundMesh-Source-0.3.zip) | 633 KB | 源码快照（与 tag `v0.3.0` 一致） |
| [`SHA256SUMS.txt`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v0.3.0/SHA256SUMS.txt) | — | 校验值 |

```
bff8ee509e7071cb73f49caa0fe5a3641c4d859497e68599f9bed917b8f6d0b5  SoundMesh-Windows-0.3.zip
22eb795492c0efc63463b4782c9b803d4873b5eb6d94bb585ecc22026c067d83  SoundMesh-Android-0.3.apk
a0ebfe7bce8419aa468cc5e0e062dbcdb9293e645ad6a03cae0adc7738345425  SoundMesh-Source-0.3.zip
```

## 0.3 新增

- **自适应缓冲**：起始缓冲分为「灵敏 25 ms」「均衡 70 ms（默认）」「稳定 90 ms」三档；断流时临时抬高，稳定 30 s 后逐步回落，上限 160 ms。积压超过 200 ms 时丢弃旧样本，延迟不会随时间增长。
- **快捷调音**：Android 通知栏提供 −5% / 静音·恢复 / +5%；Windows 按 `Ctrl+Alt+M` 呼出音量小窗，`Esc` 收起。
- **设备卡片更详细**：显示断流次数与当前接收队列中积累的音频时长。
- **iPhone 浏览器监听**：Windows 内置桥接（TCP 49634），iPhone 用系统相机扫码即可在 Safari 中收听，并支持把其他设备加入混音台逐路调音。无需安装 App、无需签名。
- **iOS 原生工程源码**：SwiftUI 监听端 + ReplayKit Broadcast Upload Extension。未签名，未做装机验证。

## 兼容性

- 音频协议与 0.2 一致，可与 0.2 设备互连；与 0.1（单声道）**不兼容**。
- Android 沿用 0.2 的包名与签名，可直接覆盖安装。
- Windows 需 Windows 10/11 x64；「共享时静音电脑当前输出」与双向模式需 build 20348+（通常 Windows 11）。

## 验证情况

- 21 项 C# 检查通过（预缓冲、PCM 转换、积压上限、陈旧样本丢弃、欠载静音、增益/静音/总音量组合、66 路混音不越界、超大握手与截断帧拒绝、错误配对拒绝）。
- Android Java 编解码器与 C# 发送端/接收端互通，分片流与断连检测正常。
- Android API 32 模拟器：签名 APK 覆盖安装成功；通知栏四个音量按钮触发真实 `PendingIntent`；媒体验证采集；Beta 反向链路（ADB 端口转发）。模拟测试音左 500 Hz / 右 900 Hz，8 秒 800 个音频块双声道频率均正确。
- Windows：进程排除采集连续 30 帧、WASAPI → 本地 TCP 接收端、热键注册与 `WM_HOTKEY` 派发、主界面滑杆与小窗双向同步。
- 抖动对比（14 s 规律性 50–90 ms 卡顿）：0.2 欠载 2 次 / 缺失 60 ms；0.3 欠载 1 次 / 缺失 50 ms。
- 网页链路：真实 Kestrel 桥接 + WebSocket + Web Audio analyser 播放，逐声道静音与暂停验证通过（桌面 Chromium）。

完整日志：[`docs/TEST-RESULTS-0.3.txt`](https://github.com/like-an-old-dog/SoundMesh/blob/main/docs/TEST-RESULTS-0.3.txt)

## 已知限制

- **iPhone 只验证到链路层**：桌面 Chromium 上播放正常，但实体 iPhone Safari 的实际播放、后台行为、蓝牙延迟尚未验证；Windows 的 WebKit 测试运行时缺少 `Web Audio`。
- **双向空间仍是 Beta**：三台实体设备、蓝牙多点耳机接管、真实声学延迟未完整实测；设备间不支持直接蓝牙音频传输。
- **iOS 原生版只有源码**，没有 IPA。需要 Xcode 自行签名。
- **明文传输**：配对码与音频在局域网明文传输，无加密。请只在可信网络使用。
- Android 无法可靠地只静音其他应用的本地扬声器；受保护内容与通话声音采集不到。

## 安装提示

**Windows**：解压整个 zip 再运行，不要单独把 exe 拖出来。首次运行请允许 Windows 防火墙的专用网络访问。若「附近设备」一直为空，检查是否同一子网、是否被 AP 隔离，或用「手动配对」直接填 IP。

**Android**：安装时若提示"应用未安装"，说明旧版本是用其他密钥签名的，先卸载旧版。

---

<sub>0.3 是预览版本。问题反馈请开 Issue；安全相关问题请私下联系作者。</sub>

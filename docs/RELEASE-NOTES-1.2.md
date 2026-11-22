# 声汇 SoundMesh 1.2

**把同一个局域网里几台设备的声音，汇到一副耳机里。** 1.2 的核心是**多路时间戳同步播放**：几台设备的声音不再各差几十毫秒，混在一起也不再互相错位。

> 本版本由 **like-an-old-dog**（[@like-an-old-dog](https://github.com/like-an-old-dog)）与 **2222223233**（[@2222223233](https://github.com/2222223233)）联合发布。

## 下载

| 文件 | 大小 | 用法 |
| --- | --- | --- |
| [`SoundMesh-Windows-1.2.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.0/SoundMesh-Windows-1.2.zip) | 76 MB | **整包解压**后运行 `Windows\SoundMesh.exe`，需与 `SoundMeshCapture.exe` 同目录 |
| [`SoundMesh-Android-1.2.apk`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.0/SoundMesh-Android-1.2.apk) | 477 KB | Android 10+，与 1.1 同签名，可覆盖安装 |
| [`SoundMesh-Source-1.2.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.0/SoundMesh-Source-1.2.zip) | 1.1 MB | 源码快照（与 tag `v1.2.0` 一致） |
| [`SHA256SUMS.txt`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.0/SHA256SUMS.txt) | — | 校验值 |

```
6157b98640b82bf2cef684841a5440992e263c30491403bc5ae5e0392ac54180  SoundMesh-Windows-1.2.zip
2e15513f2146b2bafc5b35ed3d2d6d87617b8b0f6576fcf1164efd942ab8af41  SoundMesh-Android-1.2.apk
e1c984cdffdc73d05479179edb6d70c64de3a73b569e8077f6c669302396e92d  SoundMesh-Source-1.2.zip
```

## 1.2 的主要变化

- **时间戳同步播放**：连接时先尝试新协议 `SOUNDMESH3`（→ `OK TIME3`），用 5 次 `PING` 取最短往返估算时钟偏移，之后每分钟用短连接 `CLOCK` 复测。每帧带 8 字节采集时刻，接收端把所有远端通道放到同一条播放时间轴。
- **统一播放时间轴**：按采样序号索引的 2 秒环形表。相邻块长度抖动时做线性插值；时间戳早于播放位置的片段记为迟到并丢弃，缺失记为零并计入断流。**单路迟到只丢它自己那一段，不阻塞其他通道。**
- **播放缓冲新增同步档**：同步 100 / 150（默认）/ 200 ms。原来的灵敏 25 / 均衡 70 / 稳定 90 ms 保留，仅用于旧版本设备与 iPhone 网页的独立缓冲。
- **状态更明确**：设备卡片区分「已校时 · 立体声」与「兼容模式 · 独立缓冲」，缓冲与断流读数分别对应时间轴与独立环形缓冲。
- 测试增至 **28 项 C# 检查**，并新增 Java 侧双路对齐测试。

## 兼容性

- 1.2 之间使用时间戳协议；与 1.1 / 1.0 / 0.3 连接时**自动回退**到旧协议与独立缓冲，仍可互通。
- iPhone 网页与 iOS 开发稿使用兼容协议，不参与时间轴对齐。
- Android 沿用 1.0 的签名，可覆盖安装 1.1 与 0.3。
- Windows「开始融合」使用进程隔离采集，需 **build 20348+**（通常 Windows 11）。

## 验证情况

完整日志：[`docs/TEST-RESULTS-1.2.txt`](https://github.com/like-an-old-dog/SoundMesh/blob/main/docs/TEST-RESULTS-1.2.txt)

- **28 项 C# 检查通过**，含发送侧增益、逐路/总增益组合、66 路混音余量、超大握手与截断帧拒绝、错误配对拒绝。
- **对齐测试（Java 与 C# 两套生产混音器）**：两路来源在 100 / 150 / 200 ms 缓冲下，其中一路反复出现 80 ms 突发停顿，4 秒测试中启动后所有样本仍逐点匹配；迟到片段被跳过且下一秒有效片段能恢复；重连会清掉旧时间轴。
- **跨语言互通**：Android Java 编解码器 ↔ C# 收发端，分片流与断连检测正常。
- **模拟器**：Windows 音频引擎与 1.2 Android 客户端配对，带时间戳双向交换 500 帧，反向连接、校时、时间轴缓冲与监听接管都验证通过；APK 与 1.1 同签名并覆盖安装成功。
- **Windows**：进程排除采集连续 30 帧、WASAPI → 本地 TCP 接收端、热键与小窗同步。

## 已知限制

- **同步只作用于参与声汇混音的远端音轨**：本机其他应用由系统直接输出，iPhone 网页与旧版本设备走独立缓冲，不参与对齐。
- **测试测的是应用层播放行为**：未测真实采集到耳机的端到端延迟、蓝牙缓冲，也未覆盖用户实际网络。
- **iPhone 只验证到链路层**：桌面 Chromium 上播放正常，实体 iPhone Safari 仍未验证。
- **iOS 原生版仍是上一版开发稿**，没有编译、签名或装机测试。
- **明文传输**：配对码与音频都在局域网明文传输，无加密。
- Android 受保护的媒体内容与通话声音受系统限制，无法采集。

## 安装提示

**Windows**：解压整个 zip 再运行，不要只把 exe 拖出来，「开始融合」需要同目录的 `SoundMeshCapture.exe`。首次运行请允许防火墙的专用网络访问。

**Android**：若提示"应用未安装"，说明旧版本是用其他密钥签名的，先卸载旧版。

**升级建议**：两端都升到 1.2 才能享受同步；只升一端时另一端会走兼容协议，音频照常但不对齐。

---

<sub>问题反馈请开 Issue；安全相关问题请私下联系作者。</sub>

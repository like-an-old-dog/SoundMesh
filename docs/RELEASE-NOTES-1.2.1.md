# 声汇 SoundMesh 1.2.1

**把同一个局域网里几台设备的声音，汇到一副耳机里。** 1.2.1 是一个针对时间戳同步的修复版本：解决采集积压抖动导致的丢音，并给接收端加上时间戳范围校验；同时把音频测试接进了 CI。

> 本版本由 **like-an-old-dog**（[@like-an-old-dog](https://github.com/like-an-old-dog)）与 **2222223233**（[@2222223233](https://github.com/2222223233)）联合发布。

## 下载

| 文件 | 大小 | 用法 |
| --- | --- | --- |
| [`SoundMesh-Windows-1.2.1.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.1/SoundMesh-Windows-1.2.1.zip) | 76 MB | **整包解压**后运行 `Windows\SoundMesh.exe`，需与 `SoundMeshCapture.exe` 同目录 |
| [`SoundMesh-Android-1.2.1.apk`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.1/SoundMesh-Android-1.2.1.apk) | 477 KB | Android 10+，与 1.2 同签名，可覆盖安装 |
| [`SoundMesh-Source-1.2.1.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.1/SoundMesh-Source-1.2.1.zip) | 1.1 MB | 源码快照（与 tag `v1.2.1` 一致），含 CI 配置 |
| [`SHA256SUMS.txt`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.1/SHA256SUMS.txt) | — | 校验值 |

```
f398736e7acc4fa974f17a7e7e54eb05c02855db77ecf6f35a97c540626491c4  SoundMesh-Windows-1.2.1.zip
ac54a8904bb5d813a26350e64d1e337acf976c16ea5d221b2fabe64c91856be5  SoundMesh-Android-1.2.1.apk
8a10f35f537d6bc2c6076505239f2279c70ba2f2c0aa275ca0c2e19c03298876  SoundMesh-Source-1.2.1.zip
```

## 修了什么

**1. 采集积压突变会让时间戳倒退，丢掉约 0.3 秒音频**

发送端原来用「当前时刻减采集积压」估算每帧的采集时间。当采集缓冲从 10 ms 突然涨到 300 ms（一次系统卡顿就会发生），估算值会**倒退** 290 ms；接收端判定这些块早于播放位置，整段按迟到丢弃——而这段音频其实采到了。

现在发送端的时钟改为单调：向前跳变超过 50 ms 仍视为真实断点并重新对齐，**向后跳变超过 50 ms 则沿上一块继续前进**，保留小幅漂移修正。积压抖动不再造成断音。

**2. 接收端不再无条件相信对端时间戳**

新增 `AcceptFrame` 入口校验：校时后的本地时间必须落在 `[到达时刻 − 2 秒, 到达时刻 + 50 毫秒]` 内，并做整数溢出检查。越界的块计入 `InvalidStamps` 后丢弃，既不推进播放位置，也不进入任何音频队列。上限 50 ms 是给校时误差留的余量。

**3. 源码包附带 CI 工作流**

`SoundMesh-Source-1.2.1.zip` 里含 `.github/workflows/audio-ci.yml`：把它放进仓库后，每次 push 和 PR 都会在 windows-latest 上跑 Windows 客户端构建、JVM 音频与时间轴测试、C# 检查与跨语言协议测试，并校验 APK 签名。本仓库暂未启用（推送工作流文件需要 `workflow` 权限的 token）。

## 兼容性

- 协议未变，与 1.2 / 1.1 / 1.0 / 0.3 互通；旧版本对端自动回退独立缓冲。
- Android 沿用 1.0 的签名，可覆盖安装 1.2 与更早版本。
- Windows「开始融合」仍需 build 20348+（通常 Windows 11）。

## 验证情况

完整日志：[`docs/TEST-RESULTS-1.2.1.txt`](https://github.com/like-an-old-dog/SoundMesh/blob/main/docs/TEST-RESULTS-1.2.1.txt)

- **29 项 C# 检查通过**（1.2 为 28 项），新增「帧时钟 → 接收校验 → 播放」整条链路的用例。
- Java 侧时间轴测试扩展：双路在 100 / 150 / 200 ms 下逐样本对齐、80 ms 突发停顿、非法时间戳拒绝、重连清空时间轴。
- 本地 CI 等价流程通过：Windows 自包含发布与 Android APK 构建均成功，APK 签名证书与 1.2 一致。
- **未测**：真实 Wi-Fi 抖动、蓝牙耳机缓冲、采集到耳机的端到端延迟。

## 已知限制

- **同步只作用于参与声汇混音的远端音轨**：本机其他应用由系统直接输出，iPhone 网页与旧版本设备走独立缓冲，不参与对齐。
- **Windows 10 用户暂时用不了「开始融合」**：1.2 起采集固定走进程隔离，需要 build 20348+。已在路线图上（计划失败时回退普通回环）。
- **iPhone 只验证到链路层**：桌面 Chromium 上播放正常，实体 iPhone Safari 仍未验证。
- **iOS 原生版仍是旧开发稿**，没有编译、签名或装机测试。
- **明文传输**：配对码与音频都在局域网明文传输，无加密。
- Android 受保护的媒体内容与通话声音受系统限制，无法采集。

## 升级建议

- 两端都升到 1.2.1 才能拿到本轮修复；只升一端时另一端按兼容协议工作，音频照常但不对齐。
- 从 1.2 升级：Android 直接覆盖安装，Windows 解压覆盖即可，配对码与已保存的设备会保留。

---

<sub>问题反馈请开 Issue；安全相关问题请私下联系作者。</sub>

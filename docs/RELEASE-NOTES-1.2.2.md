# 声汇 SoundMesh 1.2.2

**把同一个局域网里几台设备的声音，汇到一副耳机里。** 1.2.2 加了一份**离线使用教程**：Windows、安卓与 iPhone 网页都能在开始融合之前打开，7 章 HTML，断网也能看。

> 本版本由 **like-an-old-dog**（[@like-an-old-dog](https://github.com/like-an-old-dog)）与 **2222223233**（[@2222223233](https://github.com/2222223233)）联合发布。

## 下载

| 文件 | 大小 | 用法 |
| --- | --- | --- |
| [`SoundMesh-Windows-1.2.2.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.2/SoundMesh-Windows-1.2.2.zip) | 76 MB | **整包解压**后运行 `Windows\SoundMesh.exe`，`SoundMeshCapture.exe` 与 `guide.html` 需同目录 |
| [`SoundMesh-Android-1.2.2.apk`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.2/SoundMesh-Android-1.2.2.apk) | 481 KB | Android 10+，与 1.2.1 同签名，可覆盖安装 |
| [`SoundMesh-Source-1.2.2.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.2/SoundMesh-Source-1.2.2.zip) | 1.1 MB | 源码快照（与 tag `v1.2.2` 一致） |
| [`SHA256SUMS.txt`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.2.2/SHA256SUMS.txt) | — | 校验值 |

```
38c336d7505aa0d6f066643726b8da58bf2137e47d46bf75faf78a7593e55ccb  SoundMesh-Windows-1.2.2.zip
044c7d123587abf18088b32185e37e48df233c8ee0422b535c7af83b55503192  SoundMesh-Android-1.2.2.apk
dcd3b3ed910c89687451334b2ce5d803095652cfb8bd6291f4dc80a87e2bca83  SoundMesh-Source-1.2.2.zip
```

## 新增：离线使用教程

一个新用户最常卡在三件事上：怎么配对、耳机该在哪台设备接管、卡顿时该调哪个缓冲。教程就是按这个顺序写的，7 章：

1. **第一次把手机和电脑连起来** —— 最短路径，从两台设备到听到声音
2. **扫码、手动配对和第三台设备** —— 二维码、8 位配对码、三台以上的逐对连接
3. **把耳机切到另一台设备** —— 「把监听切到本机」到底做了什么
4. **选对音量滑杆** —— 本机应用 / 本机发送 / 其他设备合计 / 每台设备，四层往下缺一层都听不对
5. **卡顿时怎样选缓冲** —— 同步 100/150/200 ms 与旧三档各自适用什么
6. **用 iPhone 的 Safari 监听** —— 扫码、权限、保持前台的注意点
7. **搜不到设备、已连接却无声** —— 常见故障对照

三端入口：

- **Windows**：首页顶部「使用教程」，打开程序目录下的 `guide.html`；文件缺失时会提示重新完整解压。
- **Android**：首页顶部「使用教程」，应用内离线阅读（`GuideActivity`）。
- **iPhone 网页监听页**：同名入口，直接跳到 Safari 操作那一章，关闭后回到混音台。

同一份 HTML 既是 Windows 的输出文件，也被嵌入 Windows 程序集并通过局域网桥接（`/guide.html`）提供给网页端，所以三端内容不会走样。

## 兼容性

- 音频协议未变，与 1.2.1 / 1.2 / 1.1 / 1.0 / 0.3 互通；旧版本对端自动回退独立缓冲。
- Android 沿用 1.0 的签名，可覆盖安装 1.2.1 与更早版本。
- Windows「开始融合」仍需 build 20348+（通常 Windows 11）。

## 验证情况

完整日志：[`docs/TEST-RESULTS-1.2.2.txt`](https://github.com/like-an-old-dog/SoundMesh/blob/main/docs/TEST-RESULTS-1.2.2.txt)

- **29 项 C# 检查通过**（沿用 1.2.1 的全部用例）。
- Windows Release 发布通过；**Windows ZIP 与 APK 内嵌同一份七章离线 HTML**。
- **Android 模拟器**：开始融合之前从首页打开教程，并能返回首页。
- **Chromium 移动视口**：网页教程弹层、章节导航、返回混音台均通过。
- APK 签名证书与 1.2.1 一致。
- **未测**：实体 iPhone、真实 Wi-Fi 抖动、蓝牙耳机与端到端延迟。

## 已知限制

- **Windows 10 用户暂时用不了「开始融合」**：采集固定走进程隔离，需要 build 20348+。
- **同步只作用于参与声汇混音的远端音轨**：本机其他应用、iPhone 网页与旧版本设备走独立缓冲。
- **iPhone 只验证到链路层**：桌面 Chromium 上播放正常，实体 Safari 仍未验证。
- **iOS 原生版仍是旧开发稿**，没有编译、签名或装机测试。
- **明文传输**：配对码与音频都在局域网明文传输，无加密。
- Android 受保护的媒体内容与通话声音受系统限制，无法采集。

---

<sub>问题反馈请开 Issue；安全相关问题请私下联系作者。</sub>

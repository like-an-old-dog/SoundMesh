# 声汇 SoundMesh 1.3.0

**把同一个局域网里几台设备的声音，汇到一副耳机里。** 1.3.0 加了两件"用得久"才需要的东西：**环境检测**（网络不通时能说清是哪一段不通）和**应用内更新**（之后不用再手动下载安装包）。

> 本版本由 **like-an-old-dog**（[@like-an-old-dog](https://github.com/like-an-old-dog)）与 **2222223233**（[@2222223233](https://github.com/2222223233)）联合发布。

## 下载

| 文件 | 大小 | 用法 |
| --- | --- | --- |
| [`SoundMesh-Windows-1.3.0.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.3.0/SoundMesh-Windows-1.3.0.zip) | 76.7 MB | **整包解压**后运行 `Windows\SoundMesh.exe`；`SoundMeshCapture.exe` 与 `guide.html` 需同目录 |
| [`SoundMesh-Android-1.3.0.apk`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.3.0/SoundMesh-Android-1.3.0.apk) | 493 KB | Android 10+，与 1.2.x 同签名，可覆盖安装 |
| [`SoundMesh-Source-1.3.0.zip`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.3.0/SoundMesh-Source-1.3.0.zip) | 1.09 MB | 源码快照（与 tag `v1.3.0` 一致） |
| [`SHA256SUMS.txt`](https://github.com/like-an-old-dog/SoundMesh/releases/download/v1.3.0/SHA256SUMS.txt) | — | 校验值 |

```
3e877cbabc4923e50417ce1c7c2c329274c536e4e6c0e28429b044a02c965a76  SoundMesh-Windows-1.3.0.zip
f1a4f2d71033592d884ae6fd3d3c4a00e394efe8ecc84655509b42e9e4657d4f  SoundMesh-Android-1.3.0.apk
c7a6e413e6cfc2fdafbf72656ab7b2773102d6bae275faf4b33740892ad579fe  SoundMesh-Source-1.3.0.zip
```

> **从 1.2.2 或更早版本升级，需要手动装一次 1.3.0。** 应用内更新从这个版本才开始有，装好之后就不用再手动下了。

## 新增：环境检测

「工具 → 检查环境」对局域网对端发 20 次**应用层**探测（走的就是音频握手那条路径），给出成功耗时、超时次数、P95 与波动范围。

- 两端互测可以区分「A 到 B 不通」和「双向都不通」。
- 全部超时 → 先查防火墙与 AP 隔离；耗时忽高忽低 → 无线干扰或信道拥塞；两边结果不对称 → 一侧上行被限速。
- 它测的是"能否在一秒内完成一次应用层握手"，不是 ICMP、丢包率或吞吐。

## 新增：应用内更新

- 启动时自动查 GitHub 最新**正式** Release，也可手动「工具 → 检查更新」。
- 下载后依次校验：SHA-256 → 版本号 →（安卓）包名与签名证书 →（Windows）包内主程序版本。任一不符就中止。
- Windows 把三个文件装到当前用户目录的新版本文件夹并以新版重开，旧入口会转交启动；安卓把 APK 交给系统安装器，首次可能需要允许声汇安装应用。
- 更新需要能访问 GitHub 与发布资产 CDN。

配套的发布规范写进了仓库的 [`RELEASING.md`](https://github.com/like-an-old-dog/SoundMesh/blob/main/RELEASING.md)：标签、Latest 状态、四个附件的精确命名、ZIP 内三个必需路径与校验清单格式——客户端的解析逻辑与它一一对应，命名或状态不符就不会升级。

## 其它改动

- 发送端限制时间戳方向的积压深度，减少"发出去的其实是几百毫秒前音频"的情况。
- 按设备标识配对的通道在重试期间会重新发现对端 IP，不再依赖首次配对时的地址。
- 教程扩写到七章，补了环境检测与更新的说明；新增 30 KB 的《完整方案与使用手册》（仓库 `docs/MANUAL.md`）。

## 兼容性

- 音频协议未变，与 1.2.2 / 1.2.1 / 1.2 / 1.1 / 1.0 / 0.3 互通；旧版本对端自动回退独立缓冲。
- Android 沿用 1.0 的签名，可覆盖安装 1.2.x 与更早版本；versionCode 10。
- Windows「开始融合」仍需 build 20348+（通常 Windows 11）。

## 验证情况

完整日志：[`docs/TEST-RESULTS-1.3.0.txt`](https://github.com/like-an-old-dog/SoundMesh/blob/main/docs/TEST-RESULTS-1.3.0.txt)

- **31 项 C# 检查通过**（1.2.2 为 29 项）。
- **环境检测**：安卓模拟器作为应答端 20/20；Windows 端探测 20/20，中位数 1 ms、P95 4 ms、波动 7 ms（模拟器网络）。
- **更新链路**：Windows 更新器解析到线上最新的 v1.2.2 及其发布摘要，能拒绝被篡改的包，并正确暂存三个生产文件；安卓在受控 HTTPS 源上完成摘要／签名／版本校验，经私有只读内容提供器交给系统安装器，versionCode 9 → 10。
- APK 签名证书与 1.2.2 一致。
- **未测**：应用内更新的完整用户路径（真实旧版上 下载 → 系统确认 → 重开 → 保留配对）、校园网与手机热点的现场数据、实体 iPhone、蓝牙耳机与端到端延迟。

## 已知限制

- **Windows 10 用户暂时用不了「开始融合」**：采集固定走进程隔离，需要 build 20348+。
- **同步只作用于参与声汇混音的远端音轨**；本机其他应用、iPhone 网页与旧版本设备走独立缓冲。
- **iOS 原生版仍是旧开发稿**，没有编译、签名或装机测试。
- **明文传输**：配对码与音频都在局域网明文传输，无加密。
- Android 受保护的媒体内容与通话声音受系统限制，无法采集。

---

<sub>问题反馈请开 Issue；安全相关问题请私下联系作者。</sub>

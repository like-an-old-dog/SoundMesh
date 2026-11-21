# 版权与授权说明 · Copyright and Permission

声汇 SoundMesh（以下简称"本项目"）由 like-an-old-dog 与 2222223233 共同创作，**保留所有权利（All Rights Reserved）**。

本项目**没有**采用 MIT、Apache-2.0、GPL 等开源许可证。本仓库中的源码、文档、截图与构建产物均受著作权法保护。

## 允许

- 阅读、学习、审阅本仓库中的源码与文档。
- 通过 GitHub 的 Fork 功能创建个人副本，用于阅读、研究或向本项目提交改进。
- 从 [Releases](https://github.com/like-an-old-dog/SoundMesh/releases) 下载并安装官方构建，在**个人使用**场景下运行评估。
- 在文章、评测、课程或视频中引用本项目的名称、截图与说明，并注明出处与作者。

## 需要事先获得书面许可

- 将本项目（含源码、二进制的修改版本或未修改版本）再分发，包括但不限于上架应用商店、打包进别的产品、发布到其他下载站或网盘。
- 任何商业用途，包括内部商业部署、SaaS、硬件预装、付费培训与商业集成。
- 修改后再分发，或基于本项目源码衍生出新的作品并对外发布。
- 使用"声汇"或"SoundMesh"作为其他产品的名称、商标或应用包名（本项目的 Android 包名为 `app.soundmesh.audio`）。

## 申请许可

如果你需要上述任何一项授权，请通过仓库 Issue 或作者邮箱联系，说明使用场景、分发范围与预期规模。

## 无担保

本项目按"现状"提供，不附带任何明示或默示的担保，包括但不限于适销性、特定用途适用性与不侵权的担保。因使用本项目导致的任何直接或间接损失，作者不承担责任。

在部分司法辖区，上述限制可能不完全适用。

## 第三方组件

本项目使用或打包了以下第三方组件，它们各自遵循自己的许可证，且**不**受上文的"保留所有权利"限制。完整许可证文本见 [THIRD-PARTY/](THIRD-PARTY/)。

| 组件 | 版本 | 许可证 | 用途 |
| --- | --- | --- | --- |
| .NET 8 / Windows Desktop Runtime | 8.0 | MIT | 运行时与 WinForms |
| NAudio | 2.2.1 | MIT | WASAPI 采集与播放 |
| ZXing.Net | 0.16.11 | Apache-2.0 | 二维码生成与识别 |
| ZXing core (`zxing-core-3.5.3.jar`) | 3.5.3 | Apache-2.0 | Android 扫码 |
| ASP.NET Core (Kestrel) | 8.0 | MIT | iPhone 网页桥接 |

分发本项目的二进制构建时，必须随包保留 `THIRD-PARTY/` 目录下的许可证文件。

## 商标

"声汇"与"SoundMesh"为本项目的非注册商标。Android 应用包名 `app.soundmesh.audio` 与 Windows 注册表键 `HKCU\Software\SoundMesh` 属本项目标识，保留用于官方版本。

---

*This project is **not** open-source licensed. It is copyright like-an-old-dog and 2222223233, all rights reserved. You may read, study, fork for study, and run official builds for personal evaluation. Redistribution, modification-and-redistribution, and any commercial use require prior written permission. Third-party components remain under their own licenses (see above and `THIRD-PARTY/`).*

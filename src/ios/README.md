# 声汇 iPhone 原生开发版

iOS 16 及以上。工程包含 SwiftUI 监听端及 ReplayKit Broadcast Upload Extension。监听支持多个 0.2/0.3 发送端、独立音量、静音和自动重连。手动输入对端局域网地址及配对码，或由系统相机打开声汇原生配对链接。共享时启动系统广播，接收端在「手动配对」中输入 iPhone 页面的地址和八位码。广播只处理 ReplayKit 提供的应用声音。两种模式分别使用。受保护内容、后台保活和耳机切换需在目标 iPhone 上验证。

在 Mac 上安装 Xcode 和 XcodeGen。先把 project.yml 中 PRODUCT_FAMILY_ID 改为自己可签名的唯一标识符。APP_GROUP_ID 应为相同开发团队已开通、分配给主应用和扩展的 App Group。从本目录运行 `xcodegen generate`，打开生成的 SoundMesh.xcodeproj，在两个 target 的 Signing & Capabilities 选择相同 Team，确认 App Groups 后选择 iPhone 编译运行。测试分发可经团队的 TestFlight 流程完成。

本次交付为源码和工程描述。Windows 环境缺少 Apple SDK，未生成、签名或验证 IPA。直接试听可在 Windows 声汇「共享声音」选择「iPhone 浏览器监听」，用系统相机扫码进入 Safari。网页版在页面内控制音量。
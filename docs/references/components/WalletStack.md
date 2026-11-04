# WalletStack 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/wallet-stack（Arc Pro，Registry 返回 404，无公开版本号），2026-11-03 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch9/wallet-stack/`。SHA-256：`markdown.txt` fd13068049030ef93ca2cce7797ebbcfb5879694a814023b25922608f2a30c81；`page.html` 4b5feec9640ce97499b549eab5835be63f2a836d2e255d6216ad827b773ac6a3。
- 批次：2026-11-03 第九批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 姿态 | 静止 s=1-.05rel，y=100-11.2rel；扇开 +44.8×(1-2rel/(n-1)) | 运行观察（matrix） | 一致；`PoseOf` 有测试 |
| 打开 | 选中 .88/0，其余 .94-.02j / 377-6j | 运行观察 | 一致 |
| 卡片 | 296×186.7，圆角 11，字号与位置按页面计算样式 | 运行观察 | 一致；替身字体致字宽差 |
| 材质 | 金属、黑、玻璃、彩色的渐变参数 | 页面 CSS 确认 | 纹理为本项目程序生成，不是 Arc 的 SVG 噪声 |
| 高度 | 总高 436.7，详情从 220.3 起 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/WalletStack.md)。回归：`tests/ArcWpf.Tests/WalletStackTests.cs`，Gallery 页 `WalletStackPage`。

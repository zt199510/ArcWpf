# RealtimeStream 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/realtime-stream（Arc Pro，Registry 返回 404，无公开版本号），2026-11-03 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch9/realtime-stream/`。SHA-256：`markdown.txt` 982dd2a0584a0b8264d285ee1b7983a5d4450c5a23273b35d15d9f5c65c3106e；`page.html` ae95b5c0e927d20383a17c45dd105305680b4fbfbe06b7a457d456714332eb52。
- 批次：2026-11-03 第九批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 主示例 | 卡片 521.6，内边距 24，圆角 34；图高 200，窗口 30s，间隔 500ms | 运行观察 | 一致 |
| 线与填充 | 线 #1F7DCF，异常 #7128A5（浅色） | 运行观察取样 | 一致；深色值未测量 |
| 纵轴 | 数据范围下留 18%、上留 30%，步长 nice(span/2.5) | 运行观察反推 | 近似，待核对 |
| 异常标记 | 同一段连续异常只出一个标签，每点一个 r3.5 圆点 | 运行观察 | 一致 |
| 状态 | 悬停或暂停显示 `Paused, n new` 与暂停图标 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/RealtimeStream.md)。回归：`tests/ArcWpf.Tests/RealtimeStreamTests.cs`，Gallery 页 `RealtimeStreamPage`。

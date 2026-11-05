# AttachmentTray 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/blocks/file-upload（免费，MIT），2026-11-04 采集；快照入库 `tests/Baselines/References/2026-11-04-file-upload/`。
- 芯片布局另参考 Arc Pro `blocks/ai-composer` 的公开文档与显示几何（第十一批参考卡），未获取 Pro 源码。
- SHA-256：`markdown.txt` 4320a2535cca379f78c2fae6f09686c4ca01ab3ed3dfd94fd369259e021d96fe；`page.html` 92a2f734ce2ce6d02c685426204daf1e096bf75e8019944ca6dc79a8b342c1a1。
- 批次：2026-11-04 第十二批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 行外框 | 上外边距 6，内边距 10/10/10/12，圆角 18，表面 SurfaceMuted | 源码确认 | 一致 |
| 行内列 | 图标 24，间距 12，状态槽 26，移除 26 | 源码确认 | 一致；缩略图 36 为 WPF 适配 |
| 进度条 | 高 3，上 5 下 1，弹簧 smooth 驱动条与百分比 | 源码确认 | 一致 |
| 落地 | 读到 100% 即换 Uploaded，条折叠，对勾出现 | 源码确认 | 对勾为缩放淡入，Arc 为描线 |
| 芯片 | 高 44，圆角 14，文字最宽 170；上传中为圆环、名为 Cancel upload | 运行观察/文档描述 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/AttachmentTray.md)。回归：`tests/ArcWpf.Tests/AttachmentTrayTests.cs`，Gallery 页 `AttachmentTrayPage`。

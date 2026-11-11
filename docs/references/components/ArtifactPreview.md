# ArtifactPreview 参考卡

## 来源与固定范围

- **Arc 没有同名组件**，目录搜索 artifact 无对应项，也没有 Markdown 组件。本控件是 ArcWpf 的组合：Tabs、CodeBlock、JSONViewer（均来自 Arc 免费组件的既有复刻）加回复渲染器。
- 2026-11-04 复核 code-block 与 json-viewer 页面。SHA-256：code-block `markdown.txt` 76f5088a2de9b5df777597c305d3b63ae50564a0ce946f079de8402fd28cdda0，`page.html` 3759c69a8bed5ea7e6f1aeb8af90d73973784ff077353fcade674a2a817b5e99；json-viewer `markdown.txt` 32c691e779a9345b25594f44b4a6c976182d3ef7a675091b22078d04fb183ea9，`page.html` bcd9e3d19cd0a70ce6dab9052df6e84b18b31f88f61d5ddf27973aa50251909f。
- 批次：2026-11-04 第十二批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| Code | 直接用 CodeBlock，头部 53 高，Copy code | 运行观察 | 一致 |
| JSON | 直接用 JSONViewer，保留其搜索与复制 | 运行观察 | 一致 |
| Markdown | 头部沿用 CodeBlock 样式，正文走回复渲染器 | WPF适配 | 无原版可比，非 Arc 同名控件复刻 |
| File | 文件信息、Open、Download、可选摘录或图片 | WPF适配 | 无原版可比 |

## WPF差异和回归入口

差异见[组件规格](../../components/ArtifactPreview.md)。回归：`tests/ArcWpf.Tests/ArtifactPreviewTests.cs`，Gallery 页 `ArtifactPreviewPage`。

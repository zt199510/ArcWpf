# ToolCallCard 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/tool-call（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch11/tool-call/`。SHA-256：`markdown.txt` 32f67aefed1c6e08c9bfa1bb2ff0b744b92fd3f68cf28c42b4a0f8f6b8689f01；`page.html` 6ee0aa63e5ad18d12bf1aa77443b6635a840b300bd4076ed906ae24f875e206d。
- 批次：2026-11-04 第十一批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 行 | 高 40，标记 x12，标题 x38 | 运行观察 | 一致 |
| 输入/输出井 | 圆角 10，等宽 12/18 | 运行观察 | 一致 |
| 分组头 | 高 48，标记圆 20，步进 16 | 运行观察 | 一致 |
| 自动收起 | 450ms | 运行观察 | 一致 |
| 窄宽 | 小于 440 时操作换行 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/ToolCallCard.md)。回归：`tests/ArcWpf.Tests/ToolCallCardTests.cs`，Gallery 页 `ToolCallCardPage`。

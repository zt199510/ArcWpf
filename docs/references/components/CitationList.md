# CitationList 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/source-cards（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch11/source-cards/`。SHA-256：`markdown.txt` 38197a6577a0c9f226970213dad3f6ed6551687770487d9779951e6aa502de8f；`page.html` 37b3ed05b1f99e59e26436e11e20faa0ae35f51e2649e06f0ca3def0eac3ae34。
- 批次：2026-11-04 第十一批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 来源卡 | 84 高，圆角 14 | 运行观察 | 一致 |
| 栏数 | 小于 540 时 2 栏，否则 Max+1 | 运行观察 | 一致 |
| 浮层 | 304 宽，圆角 16 | 运行观察 | 一致 |
| 时序 | 悬停 320ms，高亮 2.6s | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/CitationList.md)。回归：`tests/ArcWpf.Tests/CitationListTests.cs`，Gallery 页 `CitationListPage`。

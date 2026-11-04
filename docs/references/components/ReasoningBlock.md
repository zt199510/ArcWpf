# ReasoningBlock 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/reasoning-block（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch11/reasoning-block/`。SHA-256：`markdown.txt` b8c7605901b677e8b702db17fb9b5ede8c24ecb4840618ad44c553f700ad37fd；`page.html` 0070fa3bc779345aaf265e27126bba70d7d84caae948476227b1f55b069368ae。
- 批次：2026-11-04 第十一批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 卡片 | 圆角 18，头高 44 | 运行观察 | 一致 |
| 步骤 | 圆点 7，标题 14/500，正文 14/22 | 运行观察 | 一致 |
| 页脚 | 28 高 | 运行观察 | 一致 |
| 标签 | Thought for 7s | 页面文本确认 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/ReasoningBlock.md)。回归：`tests/ArcWpf.Tests/ReasoningBlockTests.cs`，Gallery 页 `ReasoningBlockPage`。

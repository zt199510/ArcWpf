# PromptInput 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/blocks/ai-composer（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch11/block-ai-composer/`。SHA-256：`markdown.txt` 943dc929b492f83fceadadbc1c324e20e67f3c34f77f659536f365c4388b88b4；`page.html` 53dab880ad8387ea6f81fffe76997cc5a25f6dd1844b843ace999f79a92a6be2。
- 批次：2026-11-04 第十一批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 外框 | 圆角 26，内边距 11 | 运行观察 | 一致 |
| 输入区 | 16/24，最小高 40，约八行后滚动 | 文档描述 + 运行观察 | 一致 |
| 令牌 | 高 20，圆角 6，前景 12% | 运行观察 | 一致 |
| 命令行 | 约 298 宽，行高 46.4，圆角 15 | 运行观察 | 一致 |
| 字数环 | 80% 出现，95% 警告，超限不可发送 | 文档描述 | 一致 |
| 按键 | Enter、Shift+Enter、Tab、Esc 顺序、令牌跳过 | 文档描述 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/PromptInput.md)。回归：`tests/ArcWpf.Tests/PromptInputTests.cs`，Gallery 页 `PromptInputPage`。

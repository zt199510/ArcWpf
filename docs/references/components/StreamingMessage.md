# StreamingMessage 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/blocks/ai-chat、/blocks/ai-answer（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch11/block-ai-chat/`。SHA-256：`markdown.txt` 3765ba0f7ec252d0731453a3851078ed40f8113d533fd6a6d276c211e3944ab3；`page.html` 9d6be1a7f12688a20ac2144f60ba8678fb4eba0c9d6ceb85ac08b9f66b76620a。
- 另参考 `block-ai-answer`：`markdown.txt` 788f1b28720b0f55b3e38a6c40e690383a2bea1fba39c13e1dcf43cf3b2964c9；`page.html` 5323598c7621ef2e56975589cf243bb9297c1cd04e465656c27df6b784f2e705。
- 批次：2026-11-04 第十一批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 回复文字 | 16/25.6 | 运行观察 | 一致 |
| 行内代码 | 13.76 等宽，圆角 6，前景 7% | 运行观察 | 用 SurfaceMuted 近似 |
| 揭示 | 34ms，整词 | 文档描述 | 一致 |
| 失败 | Reply failed + 原因 + Retry | 页面文本确认 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/StreamingMessage.md)。回归：`tests/ArcWpf.Tests/StreamingMessageTests.cs`，Gallery 页 `StreamingMessagePage`。

# DiffView 参考卡

## 来源与固定范围

- **Arc 没有 diff 组件**：2026-11-05 查 `https://uiarc.dev/llms.txt`（目录）无对应条目；猜测的 `/r/diff.json`、`/r/diff-viewer.json`、`/r/code-diff.json` 均为 404。没有原版可比，本控件是 ArcWpf 的设计，只判断视觉语言是否一致。
- **交互参考：DeepSeek Harness**（MIT，Copyright (c) 2026 DeepSeek），提交 `fcc0ad462172522aaf5c93a7b2e01f6b9f0b0159`。只读，未复制代码或样式：
  - `packages/client/ui-deliverables/src/client/FileDiff.tsx`，SHA-256 `96d2cd177bd5319ee7c19d02ac37cd1be7ce9b7ff9dc5a7d8b86d50b0d2e0ca8`
  - `packages/client/ui-deliverables/src/client/FileDiff.module.css`，SHA-256 `a3d8aa354bf27c5652ceb1878b32e165b8ebbd9810b2adbeaa7abb78237e9d33`
  - `packages/client/ui-primitives/src/DiffBlock.module.css`，SHA-256 `a6fe1ff69c1264d64dd30ab116d580178ef325d90e40b83300d716c5eba2aacb`
- 只读了源码，**没有运行 DSH**，所以下面都是源码确认，不是运行观察。

## 取自 DSH 的结论

| 项目 | DSH 的做法 | 证据 | ArcWpf |
|---|---|---|---|
| 并排配对 | 每段删除与其后的新增按行对齐，上下文两侧都有 | 源码确认 `splitRows` | 相同规则，自行实现 |
| 单向改动 | 只增或只删时用单栏，不改用户选的布局 | 源码确认 `FileDiff` 注释 | 相同 |
| 行数上限 | 每个对比最多 5000 行，超出标记截断 | 源码确认 `MAX_RENDERED_LINES` | 虚拟化后放宽到默认 20000，保留截断提示 |
| 行样式 | 增删行有底色和 3px 左边线，并有 `+`/`-` 符号 | 源码确认 `DiffBlock.module.css` | 相同结构，颜色用 ArcWpf 令牌 |
| 新建/删除/无改动 | 在内容上方写一行说明 | 源码确认 `noteOf` | 相同 |

## 可对照的主示例

没有原版主示例。Gallery 的示例内容是自拟的。

| 项目 | 值 | 依据 |
|---|---|---|
| 字体 | `ArcWpf.Font.Mono` 13 | 与 CodeBlock 同一字体，WPF 适配 |
| 行高 | 20 | 暂定设计 |
| 外框 | 1px `Border`，圆角 14 | 暂定设计；CodeBlock 的 26 会切掉滚动内容的角 |
| 增删颜色 | 从 `ArcSuccess`、`ArcDanger` 混入 Surface | 沿用 Arc 语义色的混色做法 |

## WPF差异和回归入口

差异见[组件规格](../../components/DiffView.md)。回归：`tests/ArcWpf.Tests/DiffViewTests.cs`，Gallery 页 `DiffViewPage`。

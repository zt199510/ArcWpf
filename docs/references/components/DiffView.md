# DiffView 参考卡

## 来源与固定范围

- **视觉参考：Arc 的 Code suggestion（Pro）**：2026-11-05 只读公开文档和页面样式，未取得 Pro 源码。文档 SHA-256 `ff453aedf9fbd36173c4414a37b433cc54f7516386b382be5b158c2aa66174b2`，页面 HTML `1fca649580e489483c35dba9b2e44d8485395f371b6f01c595b88da2d037a2c4`，样式 `cb80187771872db2.css`，内容前缀 `66ccd0d3d1741814`。它面向单文件、逐 hunk 接受或拒绝，文档建议大型多文件和并排对比用专门工具，所以功能没有原版，行样式有。
- **更正：** 此处原写“Arc 没有 diff 组件”，是错的。当时用 `head -20` 截断了目录搜索结果，漏掉了 Code suggestion。
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
| 行样式 | 增删行有底色和 3px 左边线，并有 `+`/`-` 符号 | 源码确认 `DiffBlock.module.css` | 只取底色和符号；左边线已按 Code suggestion 改为加深的行号栏 |
| 新建/删除/无改动 | 在内容上方写一行说明 | 源码确认 `noteOf` | 相同 |

## 可对照的主示例

Gallery 的示例内容是自拟的，没有和 Code suggestion 的示例做逐项截图比对。

## 取自 Code suggestion 的值（公开页面样式确认）

| 项目 | Arc | ArcWpf |
|---|---|---|
| 新增行底色 / 行号栏 | success 混入背景 10% / 16% | 相同，令牌 `DiffAdded`、`DiffAddedStrong` |
| 删除行底色 / 行号栏 | danger 混入背景 9% / 15% | 相同，令牌 `DiffRemoved`、`DiffRemovedStrong` |
| 左边线 | 无 | 无（已移除） |
| 行高 | 20 | 相同 |
| 删除行文字 | `TextSecondary`，语法色不生效 | 相同 |
| 符号 | success / danger 色 | 相同 |
| 列表上下内边距 | 6 | 相同 |
| 统计为 0 | `TextMuted` | 相同 |

有意的差异：保留行内改动高亮（ArcWpf 扩展，Arc 的单 hunk 场景不需要）；行号栏宽度按行号位数计算，不用固定的 58；行号不降透明度。

| 项目 | 值 | 依据 |
|---|---|---|
| 字体 | `ArcWpf.Font.Mono` 13 | 与 CodeBlock 同一字体，WPF 适配 |
| 行高 | 20 | Code suggestion |
| 外框 | 1px `Border`，圆角 14 | 暂定设计；CodeBlock 的 26 会切掉滚动内容的角 |
| 增删颜色 | 从 `ArcSuccess`、`ArcDanger` 混入 Surface | Code suggestion 的比例 |

## WPF差异和回归入口

差异见[组件规格](../../components/DiffView.md)。回归：`tests/ArcWpf.Tests/DiffViewTests.cs`，Gallery 页 `DiffViewPage`。

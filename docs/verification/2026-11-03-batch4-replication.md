# 2026-11-03 RichTextEditor / SignaturePad / CommandMenu / OnboardingChecklist 复刻

状态：**待验收**。环境：Wine + Xvfb 离屏渲染，字体为 Liberation 改名替身（文字宽度、字重、纵向 1px 差异属环境差异），不是真实窗口验收。

## 已运行

- 构建：测试项目与 Gallery 均 0 错误。
- 专项测试（未跑全量）：`SignaturePadTests`、`OnboardingChecklistTests`、`CommandMenuTests`、`RichTextMarkupTests`、`RichTextEditorTests`，99/99 通过。
- `SignaturePad` 路径黄金值：Arc 页面实绘一笔的路径字符串与 `StrokePath` 输出完全相等。
- RichTextEditor：演示内容的 HTML、Markdown 与 Arc 一致，字数 45；块坐标与 Arc 实测对齐（如 h2 文字框底 217.4 对 217.14）。

## 截图差异（灰度差 > 32 的像素占比）

| 状态 | 占比 | 状态 | 占比 |
|---|---|---|---|
| rte-demo | 5.90% | check-demo | 3.62% |
| rte-toolbar | 6.28% | check-group | 3.67% |
| rte-link | 6.17% | check-done | 2.79% |
| rte-slash | 3.39% | check-hidden | 3.20% |
| rte-blocks | 2.57% | sig-demo | 1.73% |
| cmd | 4.95% | sig-blue | 1.59% |
| cmd-in | 2.15% | sig-wipe | 3.82% |
| cmd-empty | 1.53% | sig-cleared | 1.58% |

差异主要来自替身字体，占比不能单独作为通过结论。

rte-blocks 的 Arc 截图首行是 `# H one`：Arc 在全选删除后保留 h1，WPF 对比脚本用 `ClearContent` 清空，所以这一行不可比。

## 未运行 / 待验证

- 真实窗口、DPI、真实键盘输入、IME、触控、压感、真实剪贴板。测试里的输入走 `TypeForTest`，粘贴走 `PasteContent`，都绕过了系统输入与剪贴板。
- 动效只核对终态，未逐帧对比。

## 已知差异

见各组件规格的“已知差异”。最明显的一项是 RichTextEditor 行内 code 内边距不占宽度，Wine 下演示第一行因此多放下了 `type /`。

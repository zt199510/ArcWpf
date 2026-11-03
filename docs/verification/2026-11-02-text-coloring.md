# TextColoring 验证记录

日期：2026-11-02。环境：Linux + Wine + xvfb，离屏渲染。**真实 Windows 窗口未运行**，原因是本环境没有 Windows。截图在 `artifacts/2026-11-02-next-controls-pixel/tc-*.png`（不入库）。

## 已运行

| 检查 | 结果 |
|---|---|
| 相关测试（TextColoring、ColorEmoji、NextArcControls、Textarea） | 49 项通过，未跑全量 |
| 原生绘制 vs 重画（换行、Tab、字距对；420×96） | 差异 0.0% |
| 解除附加后 vs 原生绘制 | 差异 0.0% |
| 分段、彩色表情、肤色、ZWJ、旗帜、键帽 | 用 Twemoji 检查通过 |
| 文本变化后重画 | 通过 |
| 滚动后只画可见行并裁剪到视口 | 通过 |
| 选区高亮 | 通过 |
| 从右到左文本回退原生 | 通过 |
| `Input`、`Textarea`、`MentionInput` 附加生效 | 程序化通过；Textarea 另有截图 |
| Gallery：Textarea 分段示例、EmojiPicker 消息框 | 离屏通过；这里替换成了 Twemoji |

## 未运行/待验证

- Segoe UI Emoji 与 WPF 原生字体回退的组合：表情的原生宽度、组合序列是否被拆开、光标是否停在序列内部。
- 光标闪烁、键盘选择、撤销和输入法的实际操作。这些仍走原生 TextBox，但没有实际操作过。
- 高 DPI 下重画与原生位置是否仍然一致。这里只跑了 96 DPI。
- Wine 没有中文字体，截图里的中文显示为方框，所以中文的重画效果没有视觉验证。

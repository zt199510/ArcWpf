# RichTextEditor 参考卡

- 页面：[https://uiarc.dev/components/rich-text-editor](https://uiarc.dev/components/rich-text-editor)
- 来源批次：`tests/Baselines/References/2026-11-03-rich-text-editor`（Arc Free，MIT）
- 复用方式：依据 Registry 源码改写为 WPF，不包含 React/Motion 运行时代码。
- TKCode.Arc：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（MIT），仅作移植经验参考，未复制代码。

## 事实

- 源码确认：Markdown 快捷语法、浮动工具栏与链接面、斜杠菜单 9 项、历史（≤300，1 秒内合并输入）、粘贴规则、净化白名单、URL 规范化、Markdown/HTML 转换、弹簧参数 MORPH physical(.42,.12)、GLIDE physical(.3,0)、GROW physical(.4,0)。
- 运行观察：演示卡 521.63×680、工具栏 289×40、链接面 316×40、斜杠菜单宽 240、标题行高 30.8/24.2/19.8（页面全局样式覆盖了模块 CSS 的 1.25）、斜杠高度受书写区裁剪（275.67）。
- 运行观察 + 源码确认：`.editor p` 和 `.editor ul/ol` 优先级高于 `.editor > * + *`，所以段落与列表默认无上边距，只有段落紧跟标题时有 .45em。
- WPF适配：基于 `RichTextBox`/`FlowDocument`，不是 contentEditable；圆角 code 与 pre 背景画在透明编辑器后面。
- 待核对：Arc 实际使用的 Lucide 版本。

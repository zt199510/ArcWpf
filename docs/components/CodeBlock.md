# CodeBlock 组件规格

状态：已按 Arc 线上页面重做外观（见 [像素对照记录](../verification/2026-11-02-data-controls-pixel.md)）。离屏渲染与 Arc 截图几何对齐；真实窗口选择/滚动、DPI 和逐帧视觉未运行。

公开 API：

- `Code : string` 必填语义，默认空字符串。
- `Filename : string`，默认 `Source code`。
- `Language : string`，默认 `tsx`，用于头部标签和轻量 token 规则。
- `MaxLines : int`，默认 0 表示不折叠；正值且源码行数更多时显示展开按钮。
- `ShowLineNumbers : bool`，默认 `False`。这是 WPF 扩展：Arc 原版没有行号，默认关闭才与来源一致。
- 只读 `IsExpanded`；`ToggleExpanded()` 命令/方法用于展开和收起。

模板部件：`PART_FileName`、`PART_Language`、`PART_CopyButton`、`PART_ExpandButton`、`PART_CodeEditor`。源码区域是有限高度的只读 `RichTextBox`，水平滚动只发生在控件内部；开启行号时，行号与代码在同一 FlowDocument 行中，避免两个滚动宿主失步。滚动条悬停才显示且不占布局。代码区域保留 Tab 聚焦和选择复制，CopyButton 复制原始 `Code`。

视觉：表面使用 Surface/Border/SurfaceRaised 语义资源；圆角 26；头部总高 53 DIP（含底边）；CopyButton 为头部专用次级样式（高 36、图标 18 在左、间距 8、圆角 18）；代码使用 `ArcWpf.Font.Mono`（Windows 浏览器同样解析到 Consolas）、14 字号、23.8 行高和 20 DIP 内边距。来源在小于 420 DIP 时缩小内边距/字号，本实现未做。token 颜色分别映射注释、字符串、数字、关键字、类型、函数、属性、标签和标点，并在明暗主题中保持对比。

动效：展开/收起高度使用可清理的布局动画，Chevron 旋转。Reduced/Off 直接落点；卸载、换模板和更新内容停止旧时钟。高亮失败或未知语言回退为普通文本，不阻断展示。

验收场景：空代码、长行横向滚动、长代码 `MaxLines` 展开/收起、复制值不含行号、Tab/Enter/Space、语言切换、文件名长文本、明暗主题、Full/Reduced/Off、换模板和卸载清理。

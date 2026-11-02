# CodeBlock 组件规格

状态：Arc Registry 已冻结；开始实现 CodeBlock 的源码展示、轻量高亮、复制、行号、内部横向滚动、`maxLines` 折叠和明暗主题。真实窗口选择/滚动、DPI 和逐帧视觉待验收。

公开 API：

- `Code : string` 必填语义，默认空字符串。
- `Filename : string`，默认 `Source code`。
- `Language : string`，默认 `tsx`，用于头部标签和轻量 token 规则。
- `MaxLines : int`，默认 0 表示不折叠；正值且源码行数更多时显示展开按钮。
- 只读 `IsExpanded`；`ToggleExpanded()` 命令/方法用于展开和收起。

模板部件：`PART_FileName`、`PART_Language`、`PART_CopyButton`、`PART_ExpandButton`、`PART_CodeEditor`。源码区域是有限高度的只读 `RichTextBox`，水平滚动只发生在控件内部；行号与代码在同一 FlowDocument 行中，避免两个滚动宿主失步。代码区域保留 Tab 聚焦和选择复制，CopyButton 复制原始 `Code`。

视觉：表面使用 Surface/Border/SurfaceRaised 语义资源；头部最小高 52 DIP，代码使用等宽字体、1.7 行高和 20 DIP 内边距；小于 420 DIP 时缩小内边距/字号。token 颜色分别映射注释、字符串、数字、关键字、类型、函数、属性、标签和标点，并在明暗主题中保持对比。

动效：代码内容变化时 Full 淡入上移；文件名和展开文案替换保留布局槽位；展开/收起高度使用可清理的布局动画，Chevron 旋转。Reduced/Off 直接落点；卸载、换模板和更新内容停止旧时钟。高亮失败或未知语言回退为普通文本，不阻断展示。

验收场景：空代码、长行横向滚动、长代码 `MaxLines` 展开/收起、复制值不含行号、Tab/Enter/Space、语言切换、文件名长文本、明暗主题、Full/Reduced/Off、换模板和卸载清理。

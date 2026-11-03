# RichTextEditor

状态：待验收。自动化测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/RichTextEditor.md)，验证见[记录](../verification/2026-11-03-batch4-replication.md)。

## 范围与 API

API：`Value`、`DefaultMarkdown`、`Placeholder`、`BlockHint`、`IsReadOnly`、`AutoFocus`、`Label`；事件 `ValueChanged`（Html、Markdown、Text、Empty）、`HistoryChanged`；方法 `GetHtml`、`GetMarkdown`、`GetText`、`SetHtml`、`SetMarkdown`、`ClearContent`、`Undo`、`Redo`、`FocusEditor`。静态转换在 `RichTextMarkup`。

结构：结构性命令走“读取节点树 → 修改 → 重建 FlowDocument → 按字符偏移恢复选区”；普通输入、IME、选区和拼写检查用 WPF 原生能力。行内格式写在 Run 上，序列化时读回。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 选区出现 | 工具栏 opacity .16 enter、scale .94→1、y ±4→0 snappy；源码 | `ShowBar` | 截图 rte-toolbar | 无 |
| 选区移动 | tx/ty GLIDE physical(.3,0) | `PhysicalSpring` | 未单独截帧 | 未逐帧核对 |
| 格式面 ↔ 链接面 | 宽高 MORPH physical(.42,.12)，x ±18，opacity、blur 2 | `SwitchFace` | 截图 rte-link 终态 | x 位移用 bounce 0 采样弹簧，非 .12 |
| 斜杠菜单 | opacity .16、scale .96→1、y ∓4；高度 GROW；高亮 GLIDE | `RefreshSlash` | 截图 rte-slash | 无 |
| 关闭 | opacity、scale，.144 standard | `HideBar`、`HideSlashPanel` | 未截帧 | 未逐帧核对 |

## 已知差异

- 行内 code 的左右内边距（.38em）不占布局宽度，只画在背景上，其后文字比 Arc 左移约 10px，可能影响折行。FlowDocument 内联元素没有 padding；用占位元素会多出光标停靠点，因此未采用。
- `pre` 自动换行，Arc 为横向滚动。
- 标题缺少 -.02em 字距：FlowDocument 不支持。
- `blockquote` 内的嵌套块显示为同一段落内的换行行。
- 外部粘贴的 HTML 没有行内样式之外的兼容测试。

# Textarea 多行文本框规格

状态：已实现并完成程序化验证，待实际窗口验收。

`Textarea : TextBox` 是 ArcWpf 的多行文本字段。它以 WPF 原生 TextBox 为编辑核心，默认接受回车、自动换行、垂直滚动和垂直调整大小，同时提供 Arc 的标签、说明、错误和占位文案。

## API

- `Label`：字段标签，可为空。
- `Placeholder`：空文本时的呈现文案，不写入 `Text`。
- `Description`：辅助说明，可与错误同时显示。
- `ErrorMessage`：显式错误；为空时读取 WPF Validation 错误。
- `HasError` / `EffectiveErrorMessage`：只读错误状态。
- `IsPlaceholderVisible`：只读呈现状态。
- 继承 `TextBox` 的 `Text`、`TextWrapping`、`AcceptsReturn`、`MaxLength`、`IsReadOnly`、滚动、选择、绑定和命令行为。

## 视觉与交互

默认编辑面最小高度110 DIP、圆角18、描边1 DIP、内边距12 DIP。标签与说明/错误从外层左边界开始；多行文本在编辑面内部起点开始。悬停和键盘焦点只改变边框颜色，错误状态优先使用危险色。禁用状态使用主题的浅底并降低透明度。

不在本期实现字符计数滚动、消息词语动效、业务校验引擎或自定义提交状态机。

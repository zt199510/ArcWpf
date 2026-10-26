# Textarea 参考基准

来源：[Arc Textarea](https://uiarc.dev/components/textarea)、[冻结批次](../../../tests/Baselines/References/2026-10-26-textarea/manifest.json)、[源码快照](../../../tests/Baselines/References/2026-10-26-textarea/textarea.json)。

| 维度 | Arc事实 | WPF落地 |
|---|---|---|
| 语义 | 用于备注、描述和较长文本的多行字段 | `Textarea : TextBox`，保留 WPF Text、绑定、选择、撤销、IME 和自动化 |
| 外形 | 宽度由表单容器决定；最小高度约110；圆角18；1 DIP描边 | 样式默认 `MinHeight=110`、`CornerRadius=18`；Gallery外层最大宽度540 |
| 文本 | 14px、正常行高、内边距12；可垂直调整大小 | `TextWrapping=Wrap`、`AcceptsReturn=True`、垂直滚动条自动、水平滚动条关闭 |
| 标签 | 14px、字重500、下间距8 | `Label` 与编辑面左边界对齐 |
| 辅助文案 | 说明与错误各自占行，错误颜色使用危险色 | `Description`、`ErrorMessage`，校验错误自动回退到 WPF Validation |
| 占位 | 占位文案位于编辑面内部起点 | `Placeholder` 仅为呈现层，不写入 `Text` |
| 动效 | 源码包含消息行与字词变化动效 | 首期保留稳定布局与可读反馈；复杂字词动效暂缓 |

### 证据边界

网页预览用于确认视觉意图，WPF 原生 TextBox 决定编辑、绑定和输入机制。当前规格通过程序化测试确认，实际窗口、DPI、IME 和屏幕视觉仍需用户验收。


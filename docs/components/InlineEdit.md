# InlineEdit 组件规格

状态：实现完成，InlineEditTests 3/3 通过；实际窗口待人工验收。来源冻结于 `tests/Baselines/References/2026-10-29-inline-edit/`。

## API

`InlineEdit : Control` 保留原生 TextBox 的 IME、撤销和键盘编辑能力，并增加：

- `Value`、`Label`、`Placeholder`、`Variant`、`Multiline`、`ReadOnly`。
- `SaveAsync`：保存委托，返回 `Task` 时显示 saving/saved/failed。
- `Validate`：保存前返回错误文本。
- `IsEditing`、`Phase`、`Error`：只读状态。
- `Saved`、`SaveFailed`、`EditCancelled`：状态事件。

## 范围矩阵

| 交付项 | 状态 | 证据/剩余差异 |
|---|---|---|
| 展示态/编辑态与原位布局 | 已实现 | 专项通过；窗口尺寸待验收 |
| Enter/Escape/失焦/多行 Shift+Enter | 已实现 | 专项覆盖；IME/窗口焦点待验收 |
| 异步保存、成功、失败回滚 | 已实现 | generation 防旧保存覆盖；窗口待验收 |
| 验证错误、禁用/只读、主题 | 已实现 | 语义资源和模板；窗口待验收 |
| 文案/宽度/消息动效 | 已实现 | WPF 适配；实际观感待验收 |
| caret-at-point 精确命中、移动端响应式 | 暂缓 | WPF 首版进入编辑默认全选；不复制 Web 媒体查询 |



## 2026-10-31 Arc 对齐修正

- idle frame 改为透明边框，hover 才显示 SurfaceMuted；编辑态使用 Foreground 边框和 Surface 背景。
- 文本和编辑框右侧固定预留动作槽空间，避免进入编辑态时文字与 Check/X 重叠。
- 增加 saving spinner、saved check、failed 状态图标；状态消息和 frame 边框按来源 tone 显示。


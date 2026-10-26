# Alert 提示规格

状态：已实现并完成程序化验证，待实际窗口验收。

`Alert : ContentControl` 用于在页面内展示需要注意的状态。它不创建窗口、不抢占焦点，默认保持打开；使用方可以绑定 `IsOpen`，也可以通过 `Dismiss()` 关闭。

## API

- `Title`：标题。
- `Description`：说明文案，可为空。
- `Content`：可选的自定义 WPF 内容，适合复杂说明。
- `Tone`：`Info`、`Success`、`Warning`、`Danger`。
- `IsDismissible`：是否显示关闭按钮。
- `IsOpen`：双向绑定的打开状态。
- `IsPresented`：只读，表示视觉元素仍在布局中。
- `Dismiss()`：请求关闭。
- `Dismissed`：关闭完成后触发。

## 视觉与交互

默认使用 Surface、Foreground、Border、TextSecondary 和语气色资源。标题为 14/500，说明为 12/18；关闭按钮为 28 DIP 点击区域。提示不自动关闭，不默认获得键盘焦点；关闭按钮可通过键盘激活。

Full 动效为透明度和 Y 位移过渡；Reduced 使用更短淡化；Off 直接到达终态。复杂内容高度弹簧和字词替换动效暂不实现。

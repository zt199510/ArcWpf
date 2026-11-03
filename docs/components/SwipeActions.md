# SwipeActions

状态：实现完成，真实触摸窗口和动效验收待执行。

Arc Free 来源批次为 `2026-11-03-swipe-actions`。WPF 提供 `SwipeActions` 列表、`SwipeActionsRow` 行和 `SwipeAction` 操作模型；支持 leading/trailing 操作、水平鼠标/触摸拖拽、full swipe 阈值、操作回调、保留行和 More 按钮替代路径。一次只保持一个行打开。

当前 WPF 适配已补齐鼠标捕获、触摸捕获、边界橡皮筋、释放速度采样、Full/Reduced/Off 回弹过渡，以及包含 leading/trailing 全部动作的原生 More ContextMenu；揭示区域由真实动作按钮组成，随拖拽距离连续伸展，外侧动作可覆盖长拖拽区域。菜单视觉主题和真实触摸窗口验收仍待视觉收口。

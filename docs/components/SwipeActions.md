# SwipeActions

状态：按 Arc 源码重写，离屏对比通过；真实拖拽、触摸和多 DPI 验收待执行。

Arc Free 来源批次为 `2026-11-03-swipe-actions`。WPF 提供 `SwipeActions` 列表、`SwipeActionsRow` 行和 `SwipeAction` 操作模型；支持 leading/trailing 操作、水平鼠标/触摸拖拽、full swipe 阈值、操作回调、保留行和 More 按钮替代路径。一次只保持一个行打开。

当前 WPF 适配已补齐鼠标捕获、触摸捕获、边界橡皮筋、释放速度采样、Full/Reduced/Off 回弹过渡，以及包含 leading/trailing 全部动作的原生 More ContextMenu；揭示区域由真实动作按钮组成，随拖拽距离连续伸展，外侧动作可覆盖长拖拽区域。菜单视觉主题和真实触摸窗口验收仍待视觉收口。

## 2026-11-03 复刻审计后的实现

本节取代上文与它冲突的旧说明。此前的实现距 Arc 很远，测试通过并不代表复刻达标，详见 [验证记录](../verification/2026-11-03-replica-batch-audit.md)。

新增公开成员：`SwipeAction.KeepRow`（已有）配合 `SwipeActionsRow.Collapse()`、`Restore()`、`IsLeavingOrGone`。WPF 的 `Items.Remove` 会立即销毁容器，无法像 AnimatePresence 那样保留退场，所以动作的 `OnSelect` 要调用 `Collapse()`；不调用时，行在 1400ms 后回到原位。菜单是自绘 Popup（176×120），不再用原生 ContextMenu。触摸依赖 WPF 的 Stylus 到鼠标提升；WPF 没有 grab 光标，静止用 Hand，拖动用 SizeWE。

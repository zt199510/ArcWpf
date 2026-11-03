# RadioCards

状态：按 Arc 源码重写，离屏对比通过；真实窗口与键盘验收待执行。

Arc Free 来源采集批次为 `2026-11-03-radio-cards`，冻结文件见 `tests/Baselines/References/2026-11-03-radio-cards/manifest.json`。控件提供 `Options`、`SelectedValue`、`Layout`、`MinColumnWidth`、`GroupLabel` 和 `IsGroupDisabled`；选项支持 label、description、meta、icon、disabled 和 disabledReason。WPF 使用原生 `Control` + `ItemsControl`，保留单选、禁用项、Tab/方向键/Home/End/Enter/Space 语义。

当前 WPF 适配已加入独立 overlay 选中环，按卡片实际布局位置移动，并支持 Full/Reduced/Off 动效级别；禁用卡片会显示 `disabledReason`，视觉结构、选中点、主题状态、禁用语义、最小列宽和列表/网格布局已建立。网格模式的 `meta` 位于卡片底部，列表模式位于右侧；Tab 顺序保持单一可用 stop，Home/End 跳到首尾可用项。真实窗口中的环边界、DPI 和键盘焦点仍待验收。

## 2026-11-03 复刻审计后的实现

本节取代上文与它冲突的旧说明。此前的实现距 Arc 很远，测试通过并不代表复刻达标，详见 [验证记录](../verification/2026-11-03-replica-batch-audit.md)。

网格为 CSS auto-fill（`RadioCardsPanel`），同行等高；卡片圆角 20，一个 1.5px 选中环以 physical(.42) 滑动，按取整后的卡片盒定位，尺寸变化时直接跳到新位置。`PART_Items` 现为 `RadioCardsPanel`，旧的 `PART_Overlay`、`PART_Ring`、`RadioCardsItems` 已移除。

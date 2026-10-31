# EmptyState 组件规格

状态：已按 2026-10-31 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/EmptyState.md)。

## API

`EmptyState : Control`：`Title`、`Description`、`Icon`（`Geometry` 按 lucide 24 网格描边，或任意内容；null 为 folder）、`IconStrokeThickness`（1.5，WPF 适配：对应 lucide strokeWidth）、`Action`（任意内容，通常为按钮）、`Label`（区域无障碍名称）；静态 `FolderIcon`。旧版 `ActionContent`/`ActionCommand`/`ActionCommandParameter`/`IsActionVisible` 已移除（原版 action 为任意节点）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 48 圆形图标框、16/500 标题、14 次要说明（最大 288）、动作区 | 已实现 |
| 首次出现图标 settle-in（.48，Reduced 无） | 已实现 |
| 图标切换弹入/缩出（snappy、blur 2） | 已实现 |
| 文案滚动（新行升入、旧行脱离布局升出）+ 高度 smooth 弹簧 | 已实现（共享 `HeightMorph`） |
| padding clamp(32, 8vw, 48) | 适配：固定为桌面上限 48 × 20 |
| text-wrap: balance | 已知差异：WPF 无均衡换行 |
| Gallery 主示例 Saved views 切换 | 已实现；预览宽度 720 待核对 |
| 实际窗口、明暗主题、Reduced/Off、读屏 | 未运行/待验证 |

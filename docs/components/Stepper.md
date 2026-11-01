# Stepper 组件规格

状态：Arc Registry 已冻结；WPF 状态与点击规则部分对齐，布局和动效仍有明确缺项。来源见[参考卡](../references/components/Stepper.md)。

`Stepper` 支持 Steps、CurrentIndex、AllowStepClick、Next/Previous、CurrentStepChanged 和 UIA 状态；步骤显示标题、说明和进度图标。

2026-11-01 修正：每步按索引绘制已完成勾号、当前前景圆盘或待进行序号；点击可用步骤更新 CurrentIndex，Next/Previous 与之共用同一状态。窗口模板状态测试已通过；来源连接线和专属动效仍待核对。

后续真实模板按钮回归发现 WPF `Button` 被同命名空间类型遮蔽，导致点击不改变索引；已使用完整类型名修复，并验证 `AllowStepClick` 开/关两条路径。

2026-11-01 来源对齐：`CurrentIndex == Steps.Count` 显示全部完成，`Next()` 可从最后一步到达该状态；模板按钮仅允许选择已完成步骤，未来步骤不可跳转。`StepperStep.Error` 覆盖说明并显示警示标记；标题、说明、错误和启用状态变更会刷新视图。Gallery 主例使用 Arc 文档的 Cart / Shipping / Payment 内容。当前横向模板仍缺少来源中的连接线填充、外环、纵向/紧凑/响应式布局及逐项动效，这些是未实现项，不能标为仅待视觉验收。

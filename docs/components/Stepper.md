# Stepper 组件规格

状态：WPF fallback 实现，Arc Registry/CSS 当前网络不可访问，未宣称网页完全对齐。

`Stepper` 支持 Steps、CurrentIndex、AllowStepClick、Next/Previous、CurrentStepChanged 和 UIA 状态；步骤显示标题、说明和进度图标。

2026-11-01 修正：每步按索引绘制已完成勾号、当前前景圆盘或待进行序号；点击可用步骤更新 CurrentIndex，Next/Previous 与之共用同一状态。窗口模板状态测试已通过；来源连接线和专属动效仍待核对。

后续真实模板按钮回归发现 WPF `Button` 被同命名空间类型遮蔽，导致点击不改变索引；已使用完整类型名修复，并验证 `AllowStepClick` 开/关两条路径。

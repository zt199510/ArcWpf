# Stepper 组件规格

状态：Arc Registry 已冻结；WPF 状态与点击规则部分对齐，布局和动效仍有明确缺项。来源见[参考卡](../references/components/Stepper.md)。

`Stepper` 支持 Steps、CurrentIndex、AllowStepClick、Next/Previous、CurrentStepChanged 和 UIA 状态；步骤显示标题、说明和进度图标。

2026-11-01 修正：每步按索引绘制已完成勾号、当前前景圆盘或待进行序号；点击可用步骤更新 CurrentIndex，Next/Previous 与之共用同一状态。窗口模板状态测试已通过；来源连接线和专属动效仍待核对。

后续真实模板按钮回归发现 WPF `Button` 被同命名空间类型遮蔽，导致点击不改变索引；已使用完整类型名修复，并验证 `AllowStepClick` 开/关两条路径。

2026-11-01 来源对齐：`CurrentIndex == Steps.Count` 显示全部完成，`Next()` 可从最后一步到达该状态；模板按钮仅允许选择已完成步骤，未来步骤不可跳转。`StepperStep.Error` 覆盖说明并显示警示标记；标题、说明、错误和启用状态变更会刷新视图。Gallery 主例使用 Arc 文档的 Cart / Shipping / Payment 内容。

横向等宽列、纵向排列、连接线终态、当前外环和 480 DIP 以下自动紧凑状态已落地；`Orientation`、`Details`、`Compact`、`CompleteLabel` 为 WPF 展示属性，`CurrentLabel`/`CurrentDescription`/`IsCompact` 为只读状态。方向键、Home/End 可在已完成步骤间移动焦点，横向 RTL 交换左右方向。

进度切换保留步骤视觉实例，连接线使用独占 ScaleTransform 与 OwnedMotion 的 0.42s 弹簧采样，跨步按每条 0.08s 错峰；当前外环透明度 0.16s、尺寸 0.26s。反向从动画当前值继续，完成后清除时钟，Off 即时落点；换模板、卸载、横纵方向切换会停止旧时钟。数值为 WPF 对冻结动效令牌的近似，未承诺速度连续。来源的勾号描边、描述换行高度弹簧仍未实现；真实窗口视觉/鼠标/键盘仍待验收。

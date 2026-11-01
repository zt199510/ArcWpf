# UsageMeter 组件规格

状态：WPF fallback 实现，Arc Registry/CSS 当前网络不可访问，未宣称网页完全对齐。

`UsageMeter` 支持 Used、Limit、Label、Unit、WarningThreshold、Percentage 和 IsWarning；用于配额/存储使用量，复用 Progress 主题语义。

2026-11-01 修正：Used/Limit 拒绝负数及非有限值，WarningThreshold 限制在 0..1 且动态重算状态，Unit 变更同步更新可见读数；窗口模板测试已通过。Arc 阈值颜色、排版和动效仍待来源确认。

同日进一步改为控件自有的 8 DIP 胶囊轨道与填充层，避免系统 ProgressBar 皮肤覆盖主题。填充变换为每个模板实例单独创建，Full 过渡 350ms、Reduced 120ms、Off 即时；这些时长是 WPF 适配值，不作为 Arc 来源参数。卸载或换模板停止自有时钟。

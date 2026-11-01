# UsageMeter 验证记录（2026-11-01）

来源 Registry 当前网络访问被拒绝，未伪造网页参数。

待运行：Controls/Gallery 构建、UsageMeter 专项、阈值、主题和实际窗口视觉。

2026-11-01 补充：窗口中动态调整 WarningThreshold 与 Unit，状态及可见读数同步变化；NaN 输入被拒绝。UsageMeter 专项 5/5 通过。实际进度条外观和 Arc 动效仍待核对。

同日轨道修正：首次显示窗口测试发现 XAML 中的 ScaleTransform 被 WPF 冻结，无法写入动画终态；改由 `OnApplyTemplate` 创建实例专属 Transform。Off 模式下 25%→75% 的实际 ScaleX 与时钟清理回归通过；专项 6/6。真实窗口视觉仍待用户验收。

后续 Full 时钟回归：值从 25% 到 75% 时动画属性启动，Dispatcher 推进 600ms 后 ScaleX 保持 0.75、自有时钟清除，再修改阈值不回跳；专项 7/7 通过。动态观感仍待人工验收。

# UsageMeter 验证记录（2026-11-01）

来源：`tests/Baselines/References/2026-11-01-usage-meter/manifest.json`，已冻结 Arc Registry、CSS、Markdown、页面与动效令牌，采集日期 2026-11-01。

待运行：Controls/Gallery 构建、UsageMeter 专项、阈值、主题和实际窗口视觉。

2026-11-01 补充：窗口中动态调整 WarningThreshold 与 Unit，状态及可见读数同步变化；NaN 输入被拒绝。UsageMeter 专项 5/5 通过。实际进度条外观和 Arc 动效仍待核对。

同日轨道修正：首次显示窗口测试发现 XAML 中的 ScaleTransform 被 WPF 冻结，无法写入动画终态；改由 `OnApplyTemplate` 创建实例专属 Transform。Off 模式下 25%→75% 的实际 ScaleX 与时钟清理回归通过；专项 6/6。真实窗口视觉仍待用户验收。

后续 Full 时钟回归：值从 25% 到 75% 时动画属性启动，Dispatcher 推进 600ms 后 ScaleX 保持 0.75、自有时钟清除，再修改阈值不回跳；轨道改为 8 DIP 胶囊并使用实例独占 ScaleTransform。UsageMeter 专项 7/7 通过。动态观感仍待人工验收。

来源多分段对齐：三段数据和旧 `Used` 回退的总量/剩余/near/over 状态回归通过。显示窗口测量轨道段宽与 2 DIP 分隔、四项图例、类别点击后的读数及超额遮纹/限额线；路由键事件验证方向焦点和 Escape 清除固定项。Full 模式分段值变更启动 Width/Canvas.Left 时钟，Dispatcher 650ms 后视觉实例保留、终态正确且时钟清理。UsageMeter 专项 11/11 通过；实际物理输入、数字滚轮与主题/DPI 观感未验收。

主题与组合复验：已显示分段的动态画刷随 `ArcThemeResources.Mode` 从明到暗改变；UsageMeter 专项 12/12、Button/UsageMeter 组合 28/28、五控件精确过滤器最终 35/35 通过。完整测试运行 496 项中 465 通过、31 失败；失败涉及 Avatar 下载的 WinINet `0x80072EE4`、Drawer、Gallery 导航及多个既有动效/模板类，未将全量标为通过。Drawer 单独复跑 9/9 通过，说明全量串跑结果至少有环境/状态相互影响；其余失败未逐一归因。

# UsageMeter 组件规格

状态：Arc Registry 已冻结；WPF 已覆盖多分段用量、剩余/超额状态、交互图例、逐位数字滚动、状态徽标宽度过渡与首次滚入视野揭示；数字边缘羽化及真实输入仍待核对。来源见[参考卡](../references/components/UsageMeter.md)。

`UsageMeter` 支持 Used、Limit、Label、Unit、WarningThreshold、Percentage 和 IsWarning；用于配额/存储使用量，复用 Progress 主题语义。

2026-11-01 修正：Used/Limit 拒绝负数及非有限值，WarningThreshold 限制在 0..1 且动态重算状态，Unit 变更同步更新可见读数；窗口模板测试已通过。阈值颜色、排版和动效需继续按冻结 CSS 对齐。

同日进一步改为控件自有的 8 DIP 胶囊轨道与填充层，避免系统 ProgressBar 皮肤覆盖主题。填充变换为每个模板实例单独创建，Full 过渡 350ms、Reduced 120ms、Off 即时；这些时长是 WPF 适配值，不作为 Arc 来源参数。卸载或换模板停止自有时钟。

来源对齐后，`Segments : ObservableCollection<UsageMeterSegment>` 提供 Id/Label/Value；集合非空时总量取分段之和，空集合仍使用 `Used`。`Total`、`Free`、`Over`、`Status`、`StatusText` 与 `Caption` 为只读状态；`Decimals` 默认 1，`WarningThreshold` 默认 .9，`FreeLabel`/`OverLabel` 可覆盖。状态区分 Normal、Near、Over；超额有文字、遮纹和限额标记，不仅靠颜色。14 DIP 胶囊轨道上的分段采用 2 DIP 可见间隔，颜色映射到 `UsageSegment0..3`，徽标/轨道使用 `UsageNearBadge`、`UsageOverBadge`、`UsageTrack` 动态主题资源。图例可鼠标预览、点击固定、方向键/Home/End 移动焦点、Escape 清除固定；触摸轨道可切换固定项。分段宽度与位置使用 OwnedMotion 0.42s 近似弹簧，Off 即时；旧单段 `PART_Fill` 保持兼容。

顶部总量/类别预览和各图例数值复用内部 OdometerNumber；整数/小数位稳定，进位/倒退与位数宽度采用 0.42s 近似弹簧，明暗主题字形画刷动态更新。窄宽度的数字与说明采用 `Auto + *` 网格，说明在剩余宽度内省略。顶部状态徽标为 28 DIP 胶囊，Near 使用圆形警示、Over 使用三角警示，宽度在 0.4s 近似弹簧中从当前值重定向，文字在右侧保持位置；结束后归还 Auto。Full 下首次达到可见面积 40% 时分段按每项 70ms 错峰展开，顶部与图例数字从 0 滚向最新值；等待期间的数据更新取最终值，揭示只发生一次。Reduced/Off 不等待视口，直接显示终态；卸载和换模板停止顶部、图例与徽标时钟。来源的数字羽化边缘仍未实现；真实物理鼠标/触摸、视觉与多 DPI 尚待验收。

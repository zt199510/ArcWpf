# RadioButton / RadioGroup 参考卡

来源：[Arc Radio group](https://uiarc.dev/components/radio-group)。[冻结清单](../../../tests/Baselines/References/2026-10-26-radio/manifest.json)，批次2026-10-26-radio，不是上游版本号。Registry免费，许可页MIT，初始批次保留完整许可。证据为源码/HTML/CSS，不含实际窗口或浏览器操作。

| 对象 | 来源事实 | WPF映射 |
|---|---|---|
| 主示例 | Review mode；Focused review / One reviewer at a time.（默认选中）；Open review / Everyone can comment together.；Async review / Reviewers leave notes when they can. | 同文案/初值 |
| 选项 | padding12、border1、radius-control18、内部gap12；行间8 | RadioButton整行模板，RadioGroup竖向排列 |
| 圆环 | 17×17，border1，居中于整行 | CornerRadius8.5，非CSS9999 |
| 圆点 | 7×7，圆端，选中ControlGlyph | 绝对中心对齐；不选scale.4/opacity0 |
| 文案 | label14/Medium，未选TextSecondary、选中Foreground；说明12/TextMuted、上间距2；共享line-height1.4 | 19.6/16.8行高，可换行 |
| 分组标题 | label14/Medium，下间距8 | HeaderedItemsControl.Header |
| 选中框 | 一份共享高亮：ControlOn描边1、5%透明背景；morph .42移动及变高 | 模板共享层跟随选中容器，初次/尺寸变化即时定位 |
| 动效 | dot scale snappy .26；选中淡入160ms/取消120ms；按压未选预览opacity.22，按压环scale.88/100ms，释放580ms；颜色160ms，标题240ms | 内部专有属性持久基值，清理真实时钟；Reduced/Off即时 |

原版选中高亮处于行底色上、文字下；WPF共享层置于透明默认行后，选中行去除自己的描边。悬停层仅用于未选行，选中保持高亮，不把透明色覆盖到文字上；属于明确适配。字体沿用Windows回退。

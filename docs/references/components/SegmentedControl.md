# SegmentedControl参考卡

[文档](https://uiarc.dev/components/segmented-control/markdown)、[Registry](https://uiarc.dev/r/segmented-control.json)，Arc免费MIT。冻结批次tests/Baselines/References/2026-10-27-segmented-control，manifest记录时间/来源/哈希，无官方版本号。源码/文档确认；未操作浏览器，不宣称原站运行观察，字体/DPI环境未知。

主例文档RangeToggle：label Range（仅可访问名称），Day/Week/Month，Week选中。Gallery主预览自然宽度左对齐是项目容器选择，非原站截图对照。

| 来源结构/参数 | WPF映射 |
|---|---|
| root inline-flex border1/radius18/surface-muted | Border外框，整体可收缩 |
| track flex gap2/padding3/overflow-auto | ScrollViewer + Grid + 单行Panel |
| button minheight36/padding12/radius14 | SegmentedItem模板，透明命中背景 |
| selection inset0/border1/surface/shadow-resting | 共享Border，在ItemsPresenter下方，随目标位置/宽高变化 |
| label flex gap6 | Icon/Content/Accessory并排，缺槽折叠 |
| fade-start/end20 | ScrollViewer按实际隐藏方向设渐变OpacityMask |

动效：源码motionTokens.spring.morph(.42,bounce0)→ControlSpring解析临界阻尼，位置和宽度反向保速，首次与布局变化即时。颜色hover/active/selected→Foreground，160ms ease-standard。字重和项大小不随选中变化，无按压缩放。选中显露浏览器smooth→内部滚动偏移240ms补间（WPF选择）；Reduced/Off即时。无选中时隐藏背景，不自造入退场。

有意差异：原生ListBox/ListBoxItem Selection模式；Label映射UIA Name。禁用、Icon、ItemsSource、RTL为WPF扩展。CSS viewport断点不套用控件宽度；现有主题和字体令牌沿用。2026-10-27按用户黑框反馈移除额外FocusVisualStyle；鼠标/键盘选择均使用共享浅色选中背景和原版细边框，不添加深色外圈。阴影使用项目Resting。没有新增全局主题令牌。

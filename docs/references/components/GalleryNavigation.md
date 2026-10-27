# GalleryNavigation参考卡

参考页面https://uiarc.dev/components/reactions，2026-10-27 HTTP采集HTML和公开CSS；本地work/references/2026-10-27-gallery-navigation。浏览器未操作，未知实际窗口/DPI，参数仅标HTML/CSS确认，不声称运行对照。HTML/CSS是观察资料，网站框架代码许可未确认，不复制其源码/图标到产品；独立编写WPF模板与基本几何。

确认：aside右边框1，面板无卡片外圈；搜索height32/radius10；section32、group28、row30；字体section13/group12/row13。组图标起点8、引导线x15、行容器x22、文字x30；条目radius8。分组小徽标height16/font10。搜索与section/group粘滞，底部40淡出；无可见滚动条。选中pill包含左侧1×16标记；hover/keyboard-focus浅底，CSS色过渡140/160ms。折叠高度/opacity在HTML有动画状态，运行时长未确认，WPF采用200ms无过冲过渡；Reduced/Off即时。

WPF应用：沿用现有四个中文组及组件名称；组件顶栏固定、滚动中的当前分组标题粘滞；Ctrl+K代替⌘K；无商业tier/Docs/Blocks/What's new。搜索/计数/条目功能适配当前页面数据，不生成源站数量。背景复用SurfaceMuted/AccentSubtle，混色为WPF近似，不增加全局令牌。搜索图标和chevron为基本几何自绘，无外部素材拷贝。

页面/样式URL及哈希见[采集清单](GalleryNavigation.sources.json)。站点HTML/CSS仅留work缓存，未纳入有许可的组件源码基准，远程未来变化不保证可复现当前完整内容。

# AvatarGroup 规格

状态：本次实现完成，待用户人工验收。来源已冻结为项目批次2026-10-27-avatar-group（无官方版本号），[参考卡](../references/components/AvatarGroup.md)。Arc免费MIT参考改编，复用Avatar，不做PresenceStack/账户菜单。只运行AvatarGroup专项，不操作浏览器/桌面，实际界面由用户人工验收。

## API与结构

Control基类，Members为IEnumerable<AvatarGroupMember>（默认每实例独立ObservableCollection）；响应INotifyCollectionChanged。AvatarGroupMember实现INotifyPropertyChanged，属性DisplayName/string、Source/ImageSource、Status/AvatarStatus；子头像显式绑定，不接管组的DataContext。Size用AvatarSize默认Medium，仅Small/Medium/Large有效；Max/int默认4，负数强制为0，不回写绑定；Label/string默认Team members。只读OverflowCount/int。空集合/Max0有效，null集合视为空。

身份按成员对象引用及其出现次数保持，允许重复名字（WPF修正：源码用name key要求唯一）。重新提供同一批对象保持容器，改名/状态/Source不重新创建头像；重新排序立即重排，来源未声明重排动画。只创建前Max个头像及至多一个溢出项；退场对象短暂保留。集合更新限UI线程，不增加后台同步协议。

模板PART_Items为AvatarGroupPanel；AvatarGroupItem为模板宿主，独立模板部件Lift/Avatar/Count/OldCount/Tip/Ring。根组不换行，默认横向左对齐。直径28/36/48；左Padding6，槽自然宽D-6，头像相对槽x=-6（来源重叠），n槽总宽6+n*(D-6)，第一头像最终x0、后项x按D-6递增。头像2 DIP Surface边框、额外1 DIP Border圆环；溢出圆同直径、SurfaceMuted/TextSecondary、字号10/12/14。复用Avatar，未改变其代码。显式窄宽裁剪遵循父容器，不自动改Max或内部滚动。

姓名tip在头像上方8 DIP，水平居中，Padding8,4，Foreground背景/Background文字、12号/Medium；不参与组测量，不接受命中。hover时对应槽置顶，无焦点框/点击/Tab。UIA Group名称Label，当前可见头像Image姓名+状态、溢出Image名称“N more label小写”；退出项不进入自动化树，无自动播报。

## 动效逐项合同（均为必做）

| 触发 | 来源参数 | WPF实现与差异 |
|---|---|---|
| 新增/移除可见头像及溢出槽 | width0↔auto、opacity0↔1、scale.9↔1，spring.morph .42、bounce0，初次无动画 | 内部槽Width+Opacity+Scale；采样临界阻尼近似，连续打断从有效值重定向，不保物理速度；退出完成才删除 |
| 溢出数增减 | 新+.4em*方向→0、opacity0→1、blur2→0，240ms enter；旧→-.4em*方向/0/blur2，160ms standard | 独立当前/旧文字，不参与控件宽度；增长向上、减少向下 |
| 组hover扇开 | (index-(count-1)/2)*4 DIP，240ms standard | 内部TranslateTransform，布局宽度不变 |
| 指向项/邻项 | 指向项y=-2，之前x额外-3、之后+3；240ms | 原生鼠标事件，触笔不启动；前后项以当前显示顺序计算 |
| 指向描边 | Border→BorderStrong，160ms standard | 私有外环画刷颜色过渡，不改共享资源 |
| 姓名tip | opacity0→1、y3→0，160ms standard，进入延迟60ms；退出无延迟 | 独立非命中覆盖层，文字变化重测位置 |
| Reduced | 槽即时、无扇开/抬升/让位；计数120ms淡化；tip即时、ring仍160ms | 源码CSS Reduced保留ring反馈 |
| Off/不可见/卸载/换模板 | 全部当前终态，移除退场槽与旧计数、解除订阅/时钟 | 不保留永久帧或Timer |

每个动效操作内部属性，保留Members/Max/尺寸等外部绑定。图像下载/失败和状态点沿用Avatar。快速删除再加入同一个对象复用尚未退场的槽，反向时钟清理。

## 完整度

| 项目 | 范围 | 实现状态 | 证据 |
|---|---|---|---|
| 集合/属性绑定、Max、三尺寸、溢出 | 必做 | 已实现 | 专项 |
| 外框/重叠/图像/姓名/溢出内容 | 必做 | 已实现 | 坐标及离屏 |
| 槽伸缩/数值滚动/hover/tip/ring/清理 | 必做 | 已实现 | STA时钟与程序化路由事件 |
| UIA/主题/原版文档主样本/Gallery | 必做 | 已实现 | 专项和离屏 |
| 实际界面/多DPI/读屏/性能 | 用户人工验收 | 未运行 | 不用computer-use |

原版照片未取得固定授权副本，主样本Maya使用AvatarPage原创几何人像并标注。Windows字体/行高、模糊和采样弹簧不同于Web；不宣称逐帧一致。尚未建立用户批准的回归基准。

## 实施与证据

src/ArcWpf.Controls/AvatarGroup包含控件/成员模型、槽容器、布局Panel和独立主题；Gallery反馈组新增AvatarGroup，未修改Avatar/Badge实现或旧导航结构。Members通过绑定或C#集合赋值使用，不提供直接XAML子项语法；这是集合型Control的WPF适配。

专项9/9通过，实际尺寸/位移/时钟/UIA缓存与明暗离屏证据见[验证记录](../verification/2026-10-27-avatar-group.md)。来源Avatar依赖Registry SHA-256与上一轮Avatar冻结副本一致（8D128C779FA302D8ED8997B3380D651DA4F7E8000FF2F65929C7D25C7F573D7D）。

无新增全局颜色令牌；使用Surface/SurfaceMuted/Foreground/Background/TextSecondary/Border/BorderStrong。图像和状态点复用Avatar，已完成的Avatar专项此次未重复执行。普通IEnumerable修改不会自动发通知，需要ObservableCollection、通知型集合或重新赋值；集合操作必须在控件Dispatcher线程。重复名字/同对象重复出现均有独立槽。

几何容差0.1DIP、动画终态0.001为本项目程序化检查值，不是原站像素级标准。组原始宽度不计视觉扇开/提示溢出，宿主需留空间；不擅自增加Popup。槽位使用采样临界阻尼近似、保位置不保速度；RTL利用WPF流向，坐标按外层祖先核对。Reduced的计数为120ms淡化且ring保留反馈；Off也不展开布局，提示与描边即时。

UIA更新会重置子节点缓存并失效相关Peer，退出项仅留视觉、不继续读为当前成员。修复前缓存仍返回4个旧子节点，修复后Max0立即只返回1个溢出项。未实际运行读屏，不自动播报集合变化。

真实鼠标命中/边缘悬停稳定性、60ms提示延迟观感、实际窗口滚动/Tab、跨DPI、读屏、高对比度、大规模性能未验收。源码必做实现无暂缓缺项；不声称完整视觉还原。未提交或推送。
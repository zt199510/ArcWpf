# Badge 组件规格

状态：本次实现完成、待人工验收；本次完整实现，实际窗口由用户人工验收。来源已固定为项目采集批次2026-10-27-badge，无官方版本号；[参考卡](../references/components/Badge.md)。Arc免费MIT源码作视觉/行为参考与WPF改编，不引入React/Motion依赖。

## 范围与公开契约

原版是普通span状态标签：五种Tone（Neutral/Success/Info/Warning/Danger）、Small/Medium、可选前置Icon、文本/数字滚动替换、任意静态Content。没有通知圆点、角标计数定位、关闭按钮、点击/选中或焦点框。本次不添加这些功能。

WPF使用ContentControl；Content/ContentTemplate/ContentTemplateSelector/ContentStringFormat沿用原生契约。Tone默认Neutral、Size默认Medium，独立BadgeSize只含Small/Medium；Icon为object可绑定，IconKey为可选身份键（默认按对象类型识别，对应React组件类型；同类型不同图形需显式不同键）。IconBrush为Brush/null，默认跟随Foreground，Info样式使用Accent；可显式覆盖。所有新增DP默认单向，不写回数据，不替换DataContext。Focusable/IsTabStop默认false，FocusVisualStyle为空；原生IsEnabled不添加来源未定义的灰化。UIA Text角色只暴露当前标签/显式AutomationProperties.Name，图标和退场复制不作为子节点，不自动播报。

模板部件PART_Frame、PART_Body、PART_Label、PART_LabelGhost、PART_Icon、PART_IconGhost、PART_IconSlot；缺失可选部件安全降级。内部动效不修改外部Width、Content或颜色资源绑定。卸载、不可见、换模板、Off清除时钟与帧订阅。

## 布局与状态

Border外壳→Padding→BadgeBody自然宽度测量→水平StackPanel→图标槽/文字槽。Medium最小26 DIP、水平Padding10、12字号、5间距；Small最小22、8、11、4。1 DIP边框，半实际高度圆角；内容超高可自然增长。文字不换行，长文案在外部有限宽度下裁剪。图标尺寸由内容决定（来源无固定图标尺寸），主示例12。无图标不保留图标或间距。外部Width约束整个控件。

默认/五色调按冻结CSS；悬停只改变背景和描边；无按压或焦点视觉。主题来自语义资源，Badge专属调色键不修改既有控件颜色。

## 动效合同（均为本次必做）

| 触发 | 参数与行为 | WPF映射/差异 |
|---|---|---|
| 字符串/数字改变 | 新文字0→1、y=+.3em→0、blur4→0，240ms enter；旧文字1→0、y→-.3em、blur→2，160ms standard | 独立内部presenter与退出快照；重入移除上一次退出副本，新过渡从当前可见结果衔接，无速度保留 |
| 图标身份改变 | 新图标opacity0/scale.6/blur2→正常，snappy .26无反弹；旧图标160ms退场 | WPF采样弹簧近似；同类型同key更新静态显示，无图标整体移除不保留槽（源码一致） |
| 内容导致自然宽改变 | morph .42、bounce0；静止auto，字体/父约束被动重排直接适应 | 内部自然测量宿主+解析临界阻尼弹簧；连续改目标保留宽度/速度，步长上限50ms，误差<.01 DIP/速度<.05收敛；与Motion求解器非逐帧等价 |
| Tone/hover/主题变化 | background/border/foreground 240ms standard | 私有画刷过渡，资源目标仍动态；颜色插值为WPF sRGB，静态色由OKLab混合转换 |
| Reduced | 120ms文字/图标淡入淡出，宽度立即到位，色彩120ms | 无位移/缩放/模糊 |
| Off/卸载/不可见/换模板 | 到达当前目标并清理 | 无永久时钟或帧订阅；不动画初次挂载 |

仅原始文字/数字Content且无自定义DataTemplate时启用滚动。自定义内容静态呈现；WPF字体采用项目Segoe UI/中文回退，不模拟CSS -.01em tracking。控件内部背景/前景/描边由私有画刷动画，公开属性仍由样式/绑定控制。

## 完整度与验收

| 交付项 | 范围 | 实现状态 | 证据/剩余项 |
|---|---|---|---|
| API与绑定、可替换模板 | 必做 | 已实现 | Badge专项 |
| 五色两尺寸、图标和文本、胶囊/对齐 | 必做 | 已实现 | 测量及离屏，实际DPI待人工 |
| 文字/图标/宽度/颜色动效、打断清理 | 必做 | 已实现 | STA时钟，动态观感待人工 |
| 非交互语义、UIA、主题 | 必做 | 已实现 | 专项，读屏待人工 |
| 文档主示例与扩展示例/新版导航 | 必做 | 已实现 | Draft↔Live与扩展区 |
| 真实窗口/多DPI/性能 | 本次由用户人工验收 | 未运行 | 不使用computer-use，不操作桌面/浏览器 |

只运行Badge专项，不执行其他控件或全量测试。离屏输出不是已批准回归基准，尚未建立用户通过基准。

## 实施与证据（2026-10-27）

控件位于src/ArcWpf.Controls/Badge，主题汇总已接入；Gallery复用当前4480e1e的导航，只新增反馈组中的Badge项。8项Badge专项通过，布局与时钟、离屏明暗/Live均已检查。详见[验证记录](../verification/2026-10-27-badge.md)。未运行其他专项或全量，未使用computer-use，未提交/推送。

主题覆盖：独立ArcWpf.Brush.Badge{Tone}{Foreground/Background/Border/HoverBackground/HoverBorder}共25个资源；不改变旧控件使用的颜色值。静态Info配色采用冻结的neutral accent；应用若自定义强调色，应同时覆盖BadgeInfo对应调色键（与已有全局Accent键不自动联动，IconBrush除外）。这是WPF资源映射差异，不宣称自动支持全部Web data-accent方案。自定义复杂Content应提供AutomationProperties.Name；默认文本角色不会遍历图标/内容子树，不自动触发live announcement。

已知适配差异：WPF字号/文本行高/抗锯齿与Web字体不同；快照退场与Motion保留DOM节点不同，快速重入只保留一个退场副本；宽度为临界阻尼近似，图标弹入为采样近似；静态OKLab颜色转换有sRGB裁剪，过渡用WPF颜色插值。此次来源范围内无未实现必做功能。实际hover、图标光学对齐、动态观感、读屏、100%/150%/200% DPI和性能未验收。

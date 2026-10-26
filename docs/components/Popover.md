# Popover：M1 开发规格 v1

状态：代码、Gallery和程序化回归已实现；实际窗口与视觉验收按用户要求延期。2026-10-25，里程碑 M1。

## 来源与范围

项目采集批次 `2026-10-25-initial`，不是官方版本。来源与时间/哈希/许可见 [报告](../references/2026-10-25-initial.md) 和 [清单](../references/2026-10-25-initial.manifest.json)。

- [Arc源码](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/popover/popover.tsx)
- [Arc样式](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/popover/popover.module.css)
- [Arc文档](../../tests/Baselines/References/2026-10-25-initial/arc/docs/popover.md)
- [TKCode组件参考](../../tests/Baselines/References/2026-10-25-initial/tkcode/src/TKCode.Arc/Overlays/Popover/Popover.cs)，固定提交 `1c9b1d94c1351da0db381c7d0a29835c9a1899aa`；仅作实现参考。
- [共享令牌与动效](../references/foundation-motion.md)。Arc/TKCode MIT声明随快照保存；Popover几何及动效参数已用于WPF适配，详见第三方声明。

## 参考依据与差异

- 源码确认：align=start，sideOffset=6，collisionPadding=10；采用Radix Portal。
- 源码确认：进入从朝向锚点5px和scale=.97开始，离开scale=.98并减半位移。
- 源码确认：浮层锚点禁止按压缩放；退场期间禁用指针事件。
- 文档确认：默认非模态，Enter/Space打开，Escape关闭并归还焦点；这些依赖Radix，不是已观察行为。
- 冲突：文档退场140ms，CSS的duration-fast为160ms。暂按源码160ms，运行待核对。

以上源码/文档结论不代表完成运行观察。已下载源码和文档固定，网站部署版本与依赖包版本未固定；截图与交互观察按用户要求本阶段不执行，不阻塞规格与编码；后续验证另行安排。

## 实现范围与类型

遵循 [共同契约](contracts.md)。`ArcWpf.Controls.Popover : ContentControl`，Content是浮层内容，PlacementTarget是锚点；不复制React Root/Trigger/Content API。首期非模态、同一应用单个活动Popover、显式IsOpen控制及关闭；嵌套、多层、模态、自动触发绑定暂缓。

## 公开 API

| 成员 | 类型/默认值 | 契约 |
|---|---|---|
| IsOpen（新增DP） | bool=false | 默认TwoWay；代表期望逻辑状态，不等于退场动画仍在显示 |
| PlacementTarget（新增DP） | FrameworkElement?=null | OneWay；有效已加载且可见的同一宿主窗口锚点 |
| Side（新增DP） | PopoverSide.Bottom | Top/Bottom/Left/Right；偏好方向，可翻转 |
| Alignment（新增DP） | PopoverAlignment.Start | Start/Center/End；水平对齐随FlowDirection解释 |
| SideOffset（新增DP） | double=6 | 有限且>=0，单位DIP |
| CollisionPadding（新增DP） | double=10 | 有限且>=0，距显示器工作区边缘 |
| CloseOnOutsideClick（新增DP） | bool=true | 外部左/右指针按下请求关闭，不吞掉外部动作 |
| CloseOnEscape（新增DP） | bool=true | 内容内未被处理的Escape关闭并标记已处理 |
| Content/ContentTemplate | 继承 | 不允许内容同时有另一个可视父级 |
| ActualSide（只读DP） | PopoverSide.Bottom | 最近一次定位结果，关闭时保留 |
| IsPresented（只读DP） | bool=false | Opening/Open/Closing时true，宿主真正关闭后false |
| Opened | RoutedEventHandler，冒泡 | 宿主显示且初始定位/焦点处理后发一次，不等待进入动画 |
| Closed | EventHandler<PopoverClosedEventArgs>，冒泡路由事件 | 宿主真正移除后一次，携带Reason |
| Close() | 公共方法 | 请求关闭，Reason=Programmatic；不增加命令和可取消Closing事件 |

枚举和事件参数均在ArcWpf.Controls。PopoverCloseReason：Programmatic、Escape、OutsidePointer、AnchorUnavailable、OwnerDeactivated、OwnerClosed、Unloaded、TemplateChanged、Replaced。PopoverClosedEventArgs继承RoutedEventArgs，Reason只读。

## 宿主开发选择与必要技术验证

M1先采用模板内WPF Popup（AllowsTransparency=true、PopupAnimation=None、StaysOpen=true）作为内部宿主，因为要求跨宿主窗口边界。点击外部与关闭动画由控制器协调；不只靠StaysOpen=false直接移除宿主。Popup是内部实现，不作为公开API。

首个Popup技术验证必须覆盖跨窗口定位、资源/DataContext桥接、外部点击检测和焦点退出。若无法满足契约，可改内部宿主但不得静默退化为窗口内裁剪；记录决策并重验。跨进程检测由Owner失活关闭处理，不安装全局键盘/鼠标钩子。

`PART_Popup : Popup`必要，`PART_Surface : FrameworkElement`可选动效目标。缺Popup时不打开、IsPresented=false、把IsOpen保留绑定地复位false并诊断，不发伪Opened/Closed。缺Surface时跳过动效。重新套用模板先安全关闭旧宿主，原因TemplateChanged。

控件本体不占页面布局空间。Content的DataContext随Popover变化；资源、FlowDirection、主题、ArcMotion.Level跨Popup边界显式保持一致。原生Popup详情见 [微软文档](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup)。

## 打开/关闭状态机

状态Closed → Opening → Open → Closing → Closed；IsOpen是目标，IsPresented是实际承载。IsOpen=true但锚点不存在/未加载/不可见时复位false、给出诊断且不排队打开。Loaded以后调用方可再次请求。

打开时验证锚点和宿主；若有同Owner的其他活动Popover先以Replaced关闭；创建宿主、定位、将焦点移入、发Opened，再完成视觉进入。重复true不重发。所有处理都在UI线程，控制器使用代次标识防止事件回调中的重入覆盖最新请求。

关闭请求先设置IsOpen=false并保存Reason，进入Closing；表面立即停止接受输入，按关闭原因处理焦点，然后退场完成关闭Popup、设置IsPresented=false、发Closed。同一次退场仅一个Closed；OwnerClosed/Unloaded/AnchorUnavailable/TemplateChanged立即移除不等待动画。关闭事件处理器重新请求打开时作为新周期处理。

Closing期间重新打开且宿主仍有效：取消退场、恢复输入，从当前视觉值反向；尚未真正关闭，不发Closed或第二个Opened。已完成关闭后再打开则是新周期。

## 焦点及关闭原因

打开保存原焦点，优先聚焦内容第一个可见可用可聚焦元素，若没有则聚焦内部Surface。Tab不形成模态陷阱；离开内容时转到锚点所在窗口逻辑顺序中的下/上一可聚焦元素，不自动关闭。这个跨Popup边界导航需技术验证。

| 关闭原因 | 焦点处理 |
|---|---|
| Escape | 有效锚点优先；否则仍有效的原焦点；处理后不再交给父层关闭其他对象 |
| Programmatic | 只有焦点仍在内容中且Owner活动时归还锚点/原焦点，避免抢用户新焦点 |
| OutsidePointer | 不归还锚点，不抢外部点击目标焦点，原指针事件继续 |
| OwnerDeactivated/OwnerClosed/Replaced | 不主动抢焦点 |
| AnchorUnavailable/Unloaded/TemplateChanged | Owner仍活动且焦点在内容中时尝试有效原焦点，否则不强制 |

Owner失活默认关闭（M1固定行为），不是系统全局模态。锚点再次触发的开关由调用方控制，Popover不自动绑定Click；同一次锚点点击不同时认作外部点击。用Arc Button作锚点时设置IsPressAnimationEnabled=false。

## 布局与动效

默认表面MinWidth192、MaxWidth352 DIP、Padding16、圆角26，边框1。实际可用区域更小时允许低于192；最大宽高受工作区扣除两侧CollisionPadding约束。竖向空间不足时内部滚动，避免整个面板不可达。比较偏好侧与相反侧的可用空间，优先能完整容纳的一侧，否则选空间较大侧，再将切向位置夹在边界内；对齐计算区分RTL。

定位采用目标显示器工作区，DIP/屏幕像素正确转换；锚点布局、Owner移动/缩放/DPI变化时重新计算，不使用永久空闲轮询。外部内容滚动使锚点不可见时关闭。阴影绘制外扩不能改变SideOffset定义：间距从锚点边缘至可见面板边缘测量。

Full进入朝锚点方向偏移5 DIP、scale=.97→1，opacity按160ms，transform参考冻结CSS的580ms曲线；离开偏移2.5 DIP、scale=.98、160ms。Reduced仅120ms透明度，Off立即。精确源码与文档140ms冲突按160ms源码值处理。动画驱动Surface内部属性，不改变锚点测量矩形。

## 开发验收（未运行）

| ID | 操作 | 必须结果 |
|---|---|---|
| P01 | 无效锚点请求打开；有效后再次请求 | 第一次false且无Opened，第二次显示并只发一次Opened |
| P02 | Escape/外部点击/程序关闭分别执行 | Reason正确；焦点分别按表处理，不吞外部动作 |
| P03 | 退场途中重开，再完成关闭 | 未真正关闭不发Closed；最终关闭一次，绑定保留 |
| P04 | Owner关闭、锚点卸载、换模板 | 立即释放宿主与事件，IsOpen/IsPresented=false，无孤窗 |
| P05 | 主题/DataContext动态更换 | 内容绑定、资源、FlowDirection和动效等级保持一致 |
| P06 | 工作区边缘、RTL、跨DPI、空间小于最小宽度 | 翻转/夹紧正确，可访问内容，阴影不撑大间距 |
| P07 | 同Owner打开第二个Popover；原Opened处理器重入 | 前者Replaced且不抢焦点，无旧回调破坏新状态 |
| P08 | Tab/Shift+Tab跨内容边界 | 不锁死焦点、不丢到无效窗口，原生文本编辑仍可用 |

P02/P06/P08实际窗口与截图本阶段不执行。先实现纯定位/状态机与绑定测试，再运行窗口技术验证；宿主替换不改变以上公开契约。

## 开发前完整度与技术验证（规格v2补充）

参考入口：[Popover基准](../references/components/Popover.md)。实现与程序化验证已完成；实际窗口验收状态见下表及验证记录。文档样本可先用于技术验证，实时主预览未冻结时不能宣称同画面还原。

| 交付项 | 本次范围 | 实现状态 | 验证方式/退出条件 |
|---|---|---|---|
| API/绑定/原因与事件顺序 | 必做 | 已实现，实际窗口待验收 | 绑定保留、无重复事件、重入后的最终逻辑状态正确 |
| Popup宿主、资源和DataContext | 必做 | 已实现，实际窗口待验收 | 独立Consumer示例；开着时更换主题/数据源仍更新 |
| 面板外形、锚点间距和阴影 | 必做 | 已实现，实际窗口待验收 | 测可见表面边缘，不测阴影外框；尺寸不足时不溢出 |
| 单活动层、关闭原因及焦点规则 | 必做 | 已实现，实际窗口待验收 | P01-P08对应场景；实际焦点不能用纯状态测试代替 |
| 进退场/反向重开/等级/清理 | 必做 | 已实现，实际窗口待验收 | Closing重开不重发Opened；真正移除才发Closed；无孤立宿主 |
| Gallery文档样本与扩展用例 | 必做 | 已实现，实际窗口待验收 | Share文档样本标明来源，另设边缘/绑定/状态用例 |
| 嵌套、多层、模态、自动触发绑定 | 明确暂缓 | 不在M1 | 不因框架支持就自动扩大范围 |
| 实际跨窗口/DPI/外部输入验收 | 必做验收，执行按用户既定延期 | 未运行 | 编码可继续，但最终宿主/视觉验收不得提前标通过 |

### 实现顺序与停止扩展条件

1. 先写独立定位计算和状态/事件测试，目标工作区、锚点、内容尺寸、RTL均可注入；单位与坐标系在方法边界明确。
2. 建最小Popup技术样本：一个不缩放锚点、一个含原生Input的内容区。核对跨窗口、资源继承、外部点击与Tab退出；这些未实际运行时明确标待验证，不以静态截图替代。
3. 再完成尺寸/阴影/动效和同源对照主示例，不能先做漂亮外壳掩盖宿主或焦点问题。
4. 必做合同若因宿主限制无法满足，记录具体失败与替代方案，完成可独立部分；不得静默改为窗口内裁剪或吞掉外部点击。

### 可量测的验收约束

- 未碰撞且停稳时，SideOffset从锚点可见边缘至面板边缘测量，目标6 DIP；每个坐标转换边界记录单位。1 DIP作为初始布局测试容差（项目选择），跨DPI另记像素舍入误差，不直接套同一像素容差。
- 工作区扣除两侧10 DIP后，面板与可滚动内容仍可达；包含负屏幕坐标、窄于192 DIP工作区、四侧方向和RTL的计算用例。
- Enter/Space激活一次，外部按下触发关闭但不消费外部动作；锚点本次点击不得同时算外部点击。需实际窗口事件验证。
- Closing反向后从当前视觉值接续；Off立即终态；卸载、Owner关闭、失效锚点与模板替换立即移除宿主，解除监听。
- 原版首屏预览文字/布局待采集；若无法获取，继续以冻结文档Share样本实现，交付中保留这项视觉证据缺口。

### 动效合同索引

进入：触发有效IsOpen=true，偏移朝锚点5 DIP/scale.97/opacity0→终态，opacity160ms、transform580ms；退出：偏移2.5/scale.98/opacity0，160ms；Reduced仅120ms透明度，Off即时。引用既有CSS事实，精确缓动转换和反向保持当前位置的实现决策在编码时记录，不承诺尚未测量的速度连续性或帧率。

## 2026-10-25 实现收口

代码与Gallery已接入；13项Popover程序化回归，完整证据与未测项见 [验证记录](../verification/2026-10-25-popover.md)。浅色阴影使用单层主阴影、CSS弹簧采样映射WPF关键帧；官网实时主预览展开内容未取得，因此保留Share文档样本。嵌套、多层和模态仍不在M1。

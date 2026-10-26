# Dialog：M2规格

状态：代码及程序化验证完成，待用户实际窗口验收。见[验证记录](../verification/2026-10-26-dialog.md)。[参考卡](../references/components/Dialog.md)。本次必做模态对话框、遮罩、居中/滚动、标题说明、关闭按钮、可取消关闭、绑定、进退场/文案动效、焦点归还、Gallery/Consumer。

## WPF公开契约

Dialog : ContentControl；DialogHost : ContentControl作为窗口根内容包装。Host.Content是背景，Host.Dialog是当前对话框（逻辑子节点），一个Host同一时刻仅一个Dialog；替换强制关闭旧项。Dialog.Show(FrameworkElement origin)沿树寻找Host，找不到抛明确异常；支持直接声明Host.Dialog并绑定IsOpen。Show不打开独立OS窗口，不返回阻塞结果。

Dialog.IsOpen(bool,false,默认双向)、Title/Description(string,空)、CloseOnEscape/CloseOnOverlayClick(bool,true)、InitialFocusElement(IInputElement,null)；只读IsPresented覆盖进入/显示/退场。Close()与静态CloseCommand；Closing事件含Reason/Cancel，Opened/Closed事件。CloseReason区分Programmatic、Escape、Overlay、CloseButton、HostUnavailable、Replaced、TemplateChanged。生命周期强制关闭不允许取消。内部改变IsOpen保留绑定。Content/ContentTemplate/DataContext沿原生继承。

IsOpen在未附加Host时保留意图，附加并应用模板后执行；缺必要面板模板时降级立即，不能残留遮罩。卸载/更换模板/替换宿主清理事件、时钟、排队焦点和遮罩。Host布局自动限定宽高；不修改消费内容Height/Effect/Transform/IsEnabled。模板内背景Presenter负责禁用/模糊，Dialog自身管理模态键盘范围。

焦点进入InitialFocusElement（必须在本Dialog内）或首个可聚焦后代，回退面板；Tab/Shift+Tab循环，Escape在子控件未处理时关闭；关闭后归还打开前焦点。Host预览焦点事件防止后台程序抢焦点。真实焦点/IME/UIA窗口路径待验收。自动化Group并声明IsDialog、名称优先AutomationProperties.Name否则Title，HelpText优先显式否则Description。

## 有意适配与边界

模态范围为DialogHost；推荐包住整个Window内容。保留标题栏移动/关闭和其他窗口操作；不宣称OS级ShowDialog。退场期间继续阻止背景输入，避免点击穿透；允许程序/绑定重开，旧关闭回调不能清理新打开状态。相较原版退场点击穿透，这是桌面安全交互选择。多层堆叠、非模态、拖动/调整大小及系统文件选择器不在本次范围。

模糊只影响Host内部WPF背景，HwndHost和独立Popup不保证参与；进入Dialog前应关闭外部浮层，弹层组合实际验收未完成。使用方Content不重建，关闭后编辑状态保留。正文过长用原生ScrollViewer，不将Web滚动条逐像素移植。

## 动效合同与模板

PART_Surface(FrameworkElement)、PART_Overlay(Border)、PART_CloseButton(ButtonBase)；内部Title/Description双层文本由DialogSwapText承载。宿主PART_Background(ContentPresenter)、PART_Dialog(ContentPresenter)。各部分仅内部独占Opacity/RenderTransform/Blur，不动画使用方公开属性。

进入0→1遮罩240ms，面板160ms；y8→0和scale.96→1为.4spring采样。退出160ms到opacity0/y4/scale.98。重定向从当前值起步，不保连续速度；代次保护完成回调。Reduced只120ms透明度；文字即时。Off同步终态；清时钟后基值持久，卸载/模板替换不留退场对象。标题/说明更新Full按参考卡双层淡化/位移/模糊，连续更新仅保留最近旧层。

## 完整度矩阵

| 交付项 | 范围 | 实现状态 | 证据/剩余差异 |
|---|---|---|---|
| Host/模态/绑定/关闭事件 | 必做 | 已实现，程序化通过 | STA及绑定/生命周期测试；真实输入未测 |
| 几何/长内容/主样本 | 必做 | 已实现，程序化通过 | 有限尺寸布局测量；视觉未测 |
| 进退场/文案/重开/清理 | 必做 | 已实现，程序化通过 | Dispatcher真实时钟、后续刷新 |
| 焦点/自动化/主题 | 必做 | 已实现，程序化通过 | Peer/资源程序化，真实焦点读屏未测 |
| Gallery/Consumer | 必做 | 已实现，程序化通过 | 同源预览、表单/长文本/文案切换/关闭取消 |
| 实际窗口/DPI/截图/性能 | 延期 | 未运行 | 延续用户约定；不以单测代替 |

没有已审查的WPF截图基准；首期实现交付与用户验收分别记录。


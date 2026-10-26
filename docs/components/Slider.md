# Slider

状态：本次实现完成，Slider专项11/11通过；实际窗口待用户人工验收。[参考卡](../references/components/Slider.md)已冻结。本次只运行Slider专项，不运行其他控件测试或computer-use；用户明确实际界面自行测试。

## 契约

ArcWpf.Controls.Slider继承System.Windows.Controls.Slider，保留RangeBase的Minimum/Maximum/Value、ValueChanged、双向默认Value、原生命令和SliderAutomationPeer RangeValue。默认Minimum=0、Maximum=100、Value=0。水平单滑块，Orientation强制Horizontal；支持IsDirectionReversed与FlowDirection。外部值按WPF范围强制，不强行吸附；用户输入按Minimum起点的Step吸附且可到两个端点。Step默认1、有限正值；LargeChange默认10（WPF明确值，区别于原版自动范围十分之一），SmallChange对齐Step。标签Label、ShowValue=true、ValueFormat="0.##"（.NET数字格式，按Language文化），ValueText只读；StartContent/EndContent可放原生内容。Marks为IEnumerable<SliderMark>，record(Value,Label=null)，监听集合变更，非法或范围外标记忽略，重复值采用第一项。

鼠标点击轨道定位并可继续拖动；抓住thumb保留抓取偏移，拖动连续视觉、实时Value步进；释放一次ValueCommitted路由事件。方向键按Step，Shift方向/Page按LargeChange，Home/End到端点，每次有效键盘变更提交；命令同样保留绑定。禁用停止捕获/动效，不响应用户输入。取消捕获不产生正常提交。标题和气泡不拦截轨道输入。

交互原生机制：原生Slider提供范围、命令、焦点、自动化；原生Track用于部件兼容与布局参考。为实现原版连续位置/步长值分离、轨道点击接续拖动，使用WPF鼠标捕获管理此次手势，不依赖Web指针API或更改用户Value绑定。Touch专用垂直滚动手势与粗指针气泡偏移不在本次承诺。

模板主要部件：PART_Surface（44高命中行）、PART_Track、PART_ThumbVisual、PART_FillClip、PART_Bubble、PART_ReadoutHost、PART_BubbleNumberHost、PART_Marks、PART_Ticks/PART_FillTicks。可选部件缺失安全；更换模板解除事件，卸载/禁用/不可见停止Rendering和延时，释放捕获。

## 完整度矩阵

| 项目 | 范围与验收 | 状态 |
|---|---|---|
| 范围/步长/绑定 | 有限值、非零下限、负值、小数、不可整除端点；用户改值不拆绑定 | 已实现，程序化验证通过；实际观感待人工 |
| 输入/提交 | 点击、抓取偏移、拖动、释放/取消、键盘、RangeValue | 已实现，程序化验证通过；实际观感待人工 |
| 布局 | header+44控制行；6轨道/22thumb；窄框长标签/端点气泡不越界 | 已实现，程序化验证通过；实际观感待人工 |
| 内容 | label/readout/bubble、格式、marks、前后装饰 | 已实现，程序化验证通过；实际观感待人工 |
| 位置动效 | 直接跟手、rubber、点击追赶、保速回弹、最多一格动量、边界strain | 已实现，程序化验证通过；实际观感待人工 |
| 内容动效 | 数字轮和数位槽进退、bubble/scale、颜色和阴影 | 已实现，程序化验证通过；实际观感待人工 |
| 主题/等级/清理 | 明暗、Full/Reduced/Off、反向、卸载重载/换模板 | 已实现，程序化验证通过；实际观感待人工 |
| Gallery | Volume60%主例、绑定/小数/窄宽/禁用/反向扩展 | 已实现，程序化验证通过；实际观感待人工 |
| 双滑块/垂直/专用触摸 | 本次不纳入 | 不适用 |

窗口真实拖动/键鼠观感、多DPI、读屏待用户测试。离屏与程序化事件不作为真实操作通过结论。

ThumbShadow为可覆盖的DropShadowEffect资源入口，默认采用SliderThumb语义阴影；控件最小宽44 DIP为WPF命中适配。ValueCommitted只用于有效键盘/命令/刻度选择与正常拖动释放；外部Value和UIA RangeValue更新不合成用户提交。测试与已知差异见[验证记录](../verification/2026-10-26-slider.md)。

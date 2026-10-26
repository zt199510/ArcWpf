# Switch：M1 开发规格 v1

状态：首版代码、Gallery与程序化回归已完成；实际界面与专项输入验收待做。2026-10-25，里程碑 M1。

## 来源与范围

项目采集批次 `2026-10-25-initial`，不是官方版本。来源与时间/哈希/许可见 [报告](../references/2026-10-25-initial.md) 和 [清单](../references/2026-10-25-initial.manifest.json)。

- [Arc源码](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/switch/switch.tsx)
- [Arc样式](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/switch/switch.module.css)
- [Arc文档](../../tests/Baselines/References/2026-10-25-initial/arc/docs/switch.md)
- [TKCode组件参考](../../tests/Baselines/References/2026-10-25-initial/tkcode/src/TKCode.Arc/Toggles/Switch/Switch.cs)，固定提交 `1c9b1d94c1351da0db381c7d0a29835c9a1899aa`；仅作实现参考。
- [共享令牌与动效](../references/foundation-motion.md)。Arc/TKCode MIT声明随快照保存；Switch几何及动效参数已用于WPF实现，许可保留于根目录THIRD-PARTY-NOTICES.md。

## 参考依据与差异

- 源码确认：track=42×24px，padding=3px，thumb=18×18px，travel=18px，stretch=5px。
- 源码确认：glide 的 visualDuration=.3、bounce=0；宽度使用 snappy=.26。程序切换可有1→1.16→1的.34秒形变。
- 源码确认：按压后250ms内切换避免叠加第二次拉伸；Reduced 禁止伸长并即时移动。
- 文档确认：Space/Enter 切换，按住 Space 拉伸；精确原站键盘过程尚未观察。
- 冲突：文档称CSS运动无逐帧工作，源码使用 Motion 值与动画；性能不能采信该概述。

以上源码/文档结论不代表完成运行观察。已下载源码和文档固定，网站部署版本与依赖包版本未固定；截图与交互观察按用户要求本阶段不执行，不阻塞规格与编码；后续验证另行安排。

## 实现范围与类型

遵循 [共同契约](contracts.md)。`ArcWpf.Controls.Switch : ToggleButton`。M1两态、标签、命令、禁用、程序赋值与可打断滑块动画；不实现三态。继承Content承载标签，不再增加重复的Label属性。

## 公开 API

| 成员 | 类型/默认值 | 契约 |
|---|---|---|
| IsChecked | 继承bool?，false | 沿用默认双向绑定；M1将null强制为false，不写回业务源以修正其数据 |
| IsThreeState | 继承bool，false | M1有效值强制false；规格明确不支持true |
| Content/ContentTemplate | 继承 | 标签和轨道共用一个交互区域 |
| IsEnabled/IsPressed | 继承 | IsPressed只读；不可由外部设置 |
| Command/CommandParameter/CommandTarget | 继承 | 命令执行与切换仍走原生激活路径 |
| Checked/Unchecked/Click | 继承路由事件 | 不增加重复的Changed事件 |

保留绑定表达式进行强制转换；null的有效控件状态为Off，源仍可为null。不允许把状态回写循环当作数据清洗。IsChecked默认双向依据 [WPF源码](https://source.dot.net/presentationframework/System/Windows/Controls/Primitives/ToggleButton.cs.html)。

## 状态与事件顺序

有效禁用或命令不可执行时禁止用户切换；程序赋值仍允许刷新状态。初次加载直接呈现当前值，不从Off补播。交互提交时通过一次基类OnClick/OnToggle路径切换IsChecked，Checked/Unchecked事件观察到新值，然后原生Click/Command继续；不要在输入事件里另行再切换。

外部程序赋值只触发相应状态变化/Checked或Unchecked，不合成Click或执行Command。数据源回写的具体时间不与路由事件作跨绑定保证。事件处理器再次修改状态时，以最终有效IsChecked为动画目标，不无限回放旧回调。

Space按下拉伸、释放切换；Enter按键按下完成一次切换，键盘重复事件忽略。鼠标左键在有效区域释放完成一次切换；移出/取消/失焦清除拉伸。补充Enter支持时不得与基类事件重复触发。Label整块可点击，Tab仅一个停靠点。

## 模板与资源

可选 `PART_Thumb : FrameworkElement` 提供位移与宽度动画，`PART_Track : FrameworkElement`为轨道；ContentPresenter显示标签。缺少可选部件时状态/命令仍有效，外观由替代模板负责，动画跳过。

默认轨道42×24 DIP、padding3、滑块18、行程18；全控件MinHeight44、标签间隔12、字号14。资源使用ControlTrack/ControlTrackHover/ControlOn/ControlThumb/ControlThumbOn和Pill圆角，禁用整体opacity=.5。焦点为独立可见提示，是WPF可访问性适配，不复制源码中可能不可见的焦点效果。

## 动效状态机

Full：目标位置Off=0、On=18。按压额外宽度5；On方向保持远端边缘，位置相应减5。位移glide(.3,bounce0)，宽度snappy(.26,bounce0)。动画改变模板内部几何，周围布局不变化。

程序切换可播放scaleX 1→1.16→1，340ms，关键时刻0/.4/1。距上次实际按压释放250ms以内不叠加这次伸缩。逻辑状态即时改变，动画不延迟事件或绑定。

反向时保留当前位移/速度作为新起点，单一调度更新，不创建叠加Timer；长帧步长受限并以位置/速度阈值收敛。弹簧求解器应独立测试；visualDuration不能直接当作固定Tween时长。

Reduced/Off立即到最终位置、无拉伸；切换到这些等级清理正在进行的运动。禁用中程序更新允许反映新状态但不播放按压。卸载、模板替换取消订阅，重载直接显示最终逻辑状态。

## 自动化与验收（程序化部分通过，实际输入待验收）

沿用ToggleButton的TogglePattern，并通过Peer提供“开关”本地化说明；不要臆造Windows不存在的Switch控制类型。Name优先AutomationProperties.Name，否则取可读Content。

| ID | 操作 | 必须结果 |
|---|---|---|
| S01 | UI切换与程序IsChecked赋值分别执行 | UI一次状态变化/Click/命令；程序更新不执行Click或命令 |
| S02 | Space按住释放、Enter重复、鼠标移出取消 | 无双切换，无残留伸长；取消不提交 |
| S03 | null绑定、IsThreeState=true | 有效状态false/两态，源绑定不被移除 |
| S04 | 连续反向、事件内再改状态、切换Off等级 | 最终位置匹配最新值，旧完成回调不覆盖新状态 |
| S05 | 替换模板/卸载、无PART_Thumb模板 | 行为仍可用，无旧帧订阅；重新加载无入场漂移 |
| S06 | 自动化Toggle与标签点击 | 同一逻辑路径，名称可读，状态反馈一致 |

先写确定性弹簧/状态测试；真实键盘、UIA和DPI表现后续窗口验证，本阶段不执行观察。

## 实现记录与差异

- 公共API按规格实现，无新增业务状态属性。Gallery新增Switch页签；Consumer也含独立Switch示例。
- 内部解析临界阻尼弹簧分别处理位置和宽度，角频率按3.89/visualDuration近似标定，不宣称等同Motion求解器。每帧保留当前速度；单次步长上限50ms，位置差<.01且速度<.05时吸附目标并解除Rendering订阅。较长暂停会延长恢复时间，不瞬间跳过模拟。
- 程序状态变化的340ms横向脉冲使用WPF关键帧；250ms按压释放窗口抑制重复脉冲。旧回调通过代次保护；结束、卸载、换模板和减少动效清除时钟。
- 轨道/滑块颜色即时跟随资源与状态，尚未复刻原版轨道颜色交叉淡化与滑块阴影；焦点可见提示为WPF适配。无三态与系统动效偏好自动检测。
- Toggle自动化由自定义ToggleButtonAutomationPeer提供；明确走OnClick以统一切换/命令入口，保留Windows Toggle控制类型并提供“开关”说明。
- 测试的弹簧与模板取消逻辑直接驱动内部引擎，不把它等同于可见窗口中的实际按压或帧率验收。详见 [Switch首版验证](../verification/2026-10-25-switch.md)。

## 主预览布局修正（2026-10-25）

重新读取原站HTML/CSS确认：主预览为初始开启的Weekly digest，surface使用justify-items:center；预览宽度上限620px，状态行间隔16px、最小高度18px、字号12px且居中。Gallery主预览按此结构修改，测试列表放入“更多状态与绑定示例”折叠区。外围卡片、标题和200 DIP最小展示高度为Gallery容器设计，不属于Switch控件固定几何，也不宣称与原站外壳尺寸一致。未执行截图或可见窗口观察。

## 轨道外形修正

用户提供截图确认轨道被画成椭圆，目标为胶囊。根因为把CSS超大圆角直接作为WPF Border.CornerRadius。现改为轨道42×24 / CornerRadius=12；滑块18高 / CornerRadius=9，按压伸至23宽时仍保留9半径。原规格中“使用Pill圆角”的泛化约定由此修正，不再直接引用9999资源。新增离屏WPF渲染测试，检查顶部平直段的像素覆盖与外角透明，同时检查伸长后的圆角。该测试不是实际窗口截图验收。

## 完整度矩阵（规范收口）

参考入口：[Switch基准](../references/components/Switch.md)。

| 交付项 | 范围 | 实现状态 | 证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| 两态/绑定/null强制/命令 | 本次必做（已交付） | 已实现 | 状态、绑定与命令测试 | 原生输入专项未逐项确认 |
| 胶囊/滑块/标签/禁用 | 本次必做（修复已交付） | 已实现 | 用户图、布局/像素回归、修复后用户通过 | 跨DPI待验收 |
| 保速位移/伸长/脉冲 | 本次必做（已交付） | 已实现 | 数学及清理测试 | WPF解析近似；非Motion同一求解器 |
| 轨道淡化/滑块阴影 | 明确暂缓（首版差异） | 未实现 | 源码差异记录 | 用户通过不代表缺失项不存在 |
| 主预览与扩展区域 | 本次必做（已交付） | 同文案/初始On，扩展折叠 | 页面读取记录、Gallery | 外层卡片为项目设计 |
| 真键鼠/读屏/DPI | 明确暂缓 | 总体用户通过，专项未知 | 用户原话记录 | 未来专项按环境补证据 |

轮廓验收：42×24轨道有平直段而非椭圆，滑块18高圆角9、伸至23宽时半径不扩大；这些结论由渲染/几何结果验证，不只断言CornerRadius赋值。

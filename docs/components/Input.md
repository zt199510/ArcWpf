# Input：M1 开发规格 v1

状态：首版已实现，10项Input程序化测试通过；实际中文输入、键鼠与DPI验收待做。2026-10-25，里程碑 M1。

## 来源与范围

项目采集批次 `2026-10-25-initial`，不是官方版本。来源与时间/哈希/许可见 [报告](../references/2026-10-25-initial.md) 和 [清单](../references/2026-10-25-initial.manifest.json)。

- [Arc源码](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/input/input.tsx)
- [Arc样式](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/input/input.module.css)
- [Arc文档](../../tests/Baselines/References/2026-10-25-initial/arc/docs/input.md)
- [TKCode组件参考](../../tests/Baselines/References/2026-10-25-initial/tkcode/src/TKCode.Arc/TextFields/Input/Input.cs)，固定提交 `1c9b1d94c1351da0db381c7d0a29835c9a1899aa`；仅作实现参考。
- [共享令牌与动效](../references/foundation-motion.md)。Arc/TKCode MIT声明随快照保存；标签、编辑面和消息的尺寸/颜色规则已用于WPF实现，许可见根目录THIRD-PARTY-NOTICES.md。

## 参考依据与差异

- 源码确认：InputProps 的专用属性为必需 label、可选 description/error，其他来自原生 input。
- 源码确认：没有专门前后缀属性；不将先前的采集建议误当成已有 API。
- 源码确认：边框聚焦反馈、错误 danger 边框、禁用 opacity=.5；说明和错误可同时存在。
- 源码确认：消息行高度使用 smooth，变化词与数字有独立动画；Reduced 首帧直接按完整高度挂载。
- 文档确认：真实 label 关联输入，错误有通知语义；Windows 读屏等效方案尚待设计。

以上源码/文档结论不代表完成运行观察。已下载源码和文档固定，网站部署版本与依赖包版本未固定；截图与交互观察按用户要求本阶段不执行，不阻塞规格与编码；后续验证另行安排。

## 实现范围与类型

遵循 [共同契约](contracts.md)。`ArcWpf.Controls.Input : System.Windows.Controls.TextBox`。首期单行文本、标签、占位、说明、错误、只读/禁用及原生编辑。前后缀、密码、数值格式化、多行、消息逐词/逐数字动画暂缓；保留继承属性但不宣称多行模板已支持。

## 公开 API

| 成员 | 类型/默认值 | 契约 |
|---|---|---|
| Text | 继承string/原生默认值 | 不重新注册；默认双向及LostFocus源更新时机沿用TextBox |
| Label（新增DP） | string=空字符串 | OneWay；空值不占位，使用方仍需提供可访问名称 |
| Placeholder（新增DP） | string=空字符串 | OneWay；仅Text空且没有正在显示的IME预编辑文本时显示，不成为Text |
| Description（新增DP） | string=空字符串 | OneWay；与错误可并存 |
| ErrorMessage（新增DP） | string=空字符串 | OneWay；显式错误，空白视为未提供 |
| HasError（只读DP） | bool=false | 显式错误或Validation.HasError为true |
| EffectiveErrorMessage（只读DP） | string=空字符串 | 计算后的显示文字，不写回外部错误源 |
| IsReadOnly/IsEnabled/MaxLength/SelectionStart/SelectionLength | 继承 | 保留原生契约 |
| TextChanged/SelectionChanged | 继承事件 | 不增加每键提交或自动保存事件 |

不增加SubmitCommand，Enter不隐式提交整个表单。默认Text绑定不实时提交；应用需要实时验证时显式写 `UpdateSourceTrigger=PropertyChanged`，需要确认按钮统一提交时使用Explicit并由应用调用UpdateSource。原生时机见 [微软说明](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-control-when-the-textbox-text-updates-the-source)。

## 错误优先级与状态

有效错误文字：非空白ErrorMessage → Validation.Errors中第一个具有非空字符串ErrorContent的错误 → 只要存在验证错误但无文本则显示“输入值无效”。若应用需要本地化兜底，覆盖资源 `ArcWpf.String.InvalidInput`。不直接将异常堆栈ToString暴露到界面。

显式错误清空后立即回退到绑定验证错误；所有错误消失才清除HasError。使用方负责在绑定中开启其需要的ValidatesOnNotifyDataErrors/ValidationRules等，控件不反射或替换绑定对象。监听目标控件的Validation.Errors变化，不依赖只有NotifyOnValidationError=true才发出的事件。

Disabled阻止编辑并采用禁用透明度；错误仍保留显示。边框优先Error > Focus > Hover > Normal；默认键盘焦点通过边框颜色显示，不额外绘制外环（按原站CSS与用户参考图修正）。ReadOnly允许焦点、选区、复制和导航，不采用禁用逻辑。Description在Error存在时仍显示。

## 编辑与通知顺序

原生编辑先改变Text和TextChanged；源回写按Binding配置发生，验证错误再驱动派生状态。不得强制逐字符UpdateSource、重新写入Text或移动选区。源更新失败时保持用户正在编辑的文本并显示验证结果，不自动回滚输入。

IME开始时隐藏占位，结束后根据实际Text重算；选择、撤销、粘贴均沿用TextBox，不用预览按键拼装文字。异步验证到达时只重算错误呈现，不覆盖Text。错误状态改变可通知自动化，但避免每个动画词重复朗读。

## 模板和可访问性

必要 `PART_ContentHost` 为WPF TextBox支持的ScrollViewer/内容宿主；缺失时诊断模板错误，不能用普通TextBlock替代编辑器。标签、说明、错误、占位为模板区域，不要求公共部件名；占位IsHitTestVisible=false且不可聚焦。

只给编辑表面应用Surface/BorderStrong、圆角18、最小高度44、水平padding12 DIP；标签和消息在表面之外。标签下间距8，消息上间距8；标签/输入14、辅助12 DIP。保留原生选择画刷、光标、横向滚动。标签点击聚焦编辑器，AutomationProperties.Name优先于Label；描述和错误作为辅助文本。

默认模板内部显示错误，默认样式清除重复的外部Validation.ErrorTemplate；应用显式提供ErrorTemplate时尊重其覆盖，不修改Validation.Errors。换模板/卸载释放验证集合和IME订阅。

## 动效

M1消息行直接按内容测量，Full允许淡入淡出，Reduced至多120ms透明度，Off立即呈现。消息高度弹簧和逐词效果暂缓，记录为有意差异；不能为了动画克隆原生编辑器或重建Text绑定。

## 开发验收（程序化部分已运行）

| ID | 操作 | 必须结果 |
|---|---|---|
| I01 | 默认Text绑定输入后查看源，再移焦 | 编辑中目标更新，源按LostFocus更新；显式PropertyChanged场景实时更新 |
| I02 | 绑定验证错误存在时设置/清空ErrorMessage | 显式错误优先，清空回退到验证错误，源错误集合不变 |
| I03 | 无文本ErrorContent或异步错误到达 | HasError准确，兜底可覆盖，Text和选区不被控件重写 |
| I04 | 只读与禁用分别设置 | 只读可选中复制；禁用不可编辑；错误信息不丢失 |
| I05 | 重新应用模板/卸载重载；切换主题 | 无重复订阅，Text绑定和应用资源保持 |
| I06 | 后续实际窗口输入中文、粘贴、撤销 | IME与选区正确，占位不覆盖预编辑文本，不额外提交 |

I06和截图/DPI本阶段不执行。公开API/错误优先级已足够进入编码；自动化通知的具体平台表现由实现后的测试证明。

## 首版实现记录

- 原生TextBox与PART_ContentHost保留；标签点击聚焦，占位不参与命中，不改变Text、DataContext或源更新时机。
- 只读呈现属性IsPlaceholderVisible新增为模板状态，不能由调用方赋值。IME起始/更新时隐藏占位，提交/失焦后重算；真实中文预编辑、候选框、取消组合尚未实测。
- 错误通过Validation.Errors的弱集合监听和依赖属性变化同步，不要求NotifyOnValidationError。卸载释放IME/集合/标签监听，加载重连；模板替换只解除旧标签部件，目标控件的验证状态持续有效。
- TextBoxAutomationPeer保留Value/Text等原生模式，Name优先显式AutomationProperties.Name，再回退Label；HelpText合并说明及有效错误。有监听者时错误变化触发LiveRegionChanged，实际读屏播报待验收。
- 默认模板将标签、说明和错误放在44 DIP编辑面外，错误可与说明共存；默认移除重复的外部ErrorTemplate，使用方本地覆盖仍保留。
- 消息和边框颜色即时变化；按首版范围暂缓消息高度、逐词/数字动画。Gallery邮箱检查仅为演示规则，不是完整邮箱地址验证。
- 表面圆角18 DIP；聚焦只改变边框。Gallery主示例为Project name / Arc launch，最大宽度540 DIP；邮箱、移焦绑定、绑定验证、只读、禁用、无标签示例位于展开区。字体仍为系统回退，不宣称像素一致。
- 验证详见 [Input首版记录](../verification/2026-10-25-input.md)。

## 用户参考图对齐修正

依据用户提供的Project name截图及重新读取的原站Input HTML，主示例改为Project name / Arc launch / Use a short, descriptive name.。字段最大宽度540（可随窄窗口缩小），外层浅灰预览、白色编辑面，外围去除多余卡片描边。标签14/Medium、行高20、下间隔8；说明12、行高18、上间隔8；编辑面最小44高、圆角18、水平内边距12。移除此前额外加入的焦点外环，保留错误优先的边框反馈。原站字体未打包，系统字形仍可能有差异。本次构建与43项测试通过，实际画面尚待用户查看。

## 输入与占位起点修正

离屏布局测量复现：原占位起点X=13，输入首字符X=27。去除Surface重复Padding，改为编辑宿主和占位各使用同一公开Padding，并补偿原生TextBoxView的2 DIP水平文本留白。默认输入与占位起点均为X=13，纵向位置也通过首字符矩形对比。保留原生ScrollViewer/ScrollContentPresenter编辑宿主。新增回归后44项测试通过。外部标签和说明仍按原站与字段左边框对齐，不加输入区内部Padding。

## 完整度矩阵（规范收口）

参考入口：[Input基准](../references/components/Input.md)。最新对齐修复尚待用户确认，不继承Button/Switch的通过结论。

| 交付项 | 范围 | 实现状态 | 证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| Text绑定/标签/占位/说明/错误 | 本次必做 | 已实现 | Input程序化测试 | 实际移焦输入仍待验证 |
| 验证优先级与异步错误 | 本次必做 | 已实现 | MarkInvalid/INotifyDataErrorInfo回归 | 非真实服务器调用 |
| 外形与文字起点 | 本次必做 | 已修复 | 用户参考图；首字符与占位坐标差<=0.5 DIP | 这是项目测试容差；跨DPI待验 |
| 只读/禁用/自动化名称 | 本次必做 | 已实现 | 原生模式及模板测试 | 读屏实际行为待验 |
| Project name主示例 | 本次必做 | 已按图对齐文案/宽度约束 | 参考卡、Gallery | 系统字体与浏览器字形不同 |
| 消息高度/逐词数字动画 | 明确暂缓（原规格） | 未实现 | 初始范围说明 | 不能以即时显示算动画完成 |
| 真IME/粘贴撤销/键鼠/DPI | 明确暂缓 | 未实测 | 验证边界记录 | 保留人工步骤 |

对齐验收：输入与占位共享内部文本起点；标签、说明和错误按字段外边缘对齐。避免为了让两类位置“全部同起点”而消除正确的输入内边距。

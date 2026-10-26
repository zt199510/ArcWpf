# Button：M1 开发规格 v1

状态：首版已实现，21 项程序化测试通过；来源文本已固定，截图和实际交互验收按用户要求延期。2026-10-25，里程碑 M1。

## 来源与范围

项目采集批次 `2026-10-25-initial`，不是官方版本。来源与时间/哈希/许可见 [报告](../references/2026-10-25-initial.md) 和 [清单](../references/2026-10-25-initial.manifest.json)。

- [Arc源码](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/button/button.tsx)
- [Arc样式](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/button/button.module.css)
- [Arc文档](../../tests/Baselines/References/2026-10-25-initial/arc/docs/button.md)
- [TKCode组件参考](../../tests/Baselines/References/2026-10-25-initial/tkcode/src/TKCode.Arc/Buttons/Button/Button.cs)，固定提交 `1c9b1d94c1351da0db381c7d0a29835c9a1899aa`；仅作实现参考。
- [共享令牌与动效](../references/foundation-motion.md)。Arc/TKCode MIT声明随快照保存；令牌与 Button 样式已用于 WPF 实现，许可见根目录 THIRD-PARTY-NOTICES.md；TKCode 仅作实现经验参考。

## 参考依据与差异

- 源码确认：ButtonVariant 为 primary/secondary/ghost/danger，默认 primary；size 为 sm/md/lg，默认 md。
- 源码确认：loading 保留焦点并拦截点击；disabled 是独立状态。主样式使用 foreground/background。
- 源码确认：pressVariants 按宽度选择 .985/.96/.97，按下采用 instant，恢复使用 snappy；锚定浮层的触发按钮不缩放。
- 文档确认：Enter/Space 激活；仅图标按钮需要可访问名称。
- 冲突：文档将按压概括为 snappy，精确按下分支按源码处理，实际节奏待观察。

以上源码/文档结论不代表完成运行观察。已下载源码和文档固定，网站部署版本与依赖包版本未固定；截图与交互观察按用户要求本阶段不执行，不阻塞规格与编码；后续验证另行安排。

## 实现范围与类型

遵循 [共同契约](contracts.md)。`ArcWpf.Controls.Button : System.Windows.Controls.Button`。M1 包含四种变体、三种尺寸、内容/命令、加载、禁用、键盘、按压反馈。标签宽度变形、逐词模糊、自动图标识别暂缓，不作为 M1 完成条件。

## 公开 API

| 成员 | 类型与默认值 | 绑定/契约 |
|---|---|---|
| Variant（新增DP） | ButtonVariant.Primary；枚举Primary/Secondary/Ghost/Danger | OneWay；仅改变外观 |
| Size（新增DP） | ArcWpf.ControlSize.Medium | OneWay；最小高度36/44/50，不强制压缩长内容 |
| IsLoading（新增DP） | bool=false | OneWay；由使用方驱动，不自动执行异步任务 |
| IsPressAnimationEnabled（新增DP） | bool=true | OneWay；浮层锚点设false，保留颜色反馈 |
| Content/ContentTemplate | 继承 | 不增加Text或Icon专用属性，使用内容组合 |
| Command/CommandParameter/CommandTarget | 继承 | 原生命令和CanExecute机制 |
| IsEnabled/IsDefault/IsCancel/ClickMode | 继承 | 保留WPF含义，不重新定义 |
| Click | 继承路由事件 | 不另造Clicked事件 |

ButtonVariant 位于 ArcWpf.Controls。新增DP均验证枚举合法性，样式可设置；只为加载添加控件行为，不修改调用者的 IsEnabled 值。

## 状态优先级与执行顺序

1. 有效 IsEnabled=false：阻断用户激活，呈现禁用；与加载同时发生时禁用外观优先，停止spinner。IsLoading值保留，重新启用后恢复加载呈现。
2. IsLoading=true且启用：保持可聚焦、原有键盘焦点和内容测量，显示spinner，隐藏标签视觉但保留可访问名称；阻止由控件发起的Click和命令执行。
3. 普通按压/悬停：原生有效激活经统一OnClick入口；先检查禁用与加载，放行后只调用一次基类路径，交给WPF触发Click和命令。不手工再执行Command。
4. 鼠标、Space/Enter、默认按钮和UIA Invoke必须使用同一激活门控。UIA加载期间不触发动作；不得靠仅处理MouseDown实现阻断。
5. 不拦截应用直接调用Command.Execute或人为RaiseEvent。命令CanExecute=false导致原生禁用时，不为保焦点绕过它；调用方需区分“加载”和“命令不可用”。

## 模板与资源

默认模板含内容presenter和spinner；无强制命名部件。可选 PART_Spinner : FrameworkElement 提供加载旋转容器，缺失则不旋转。可选 `PART_PressTarget : FrameworkElement` 提供内部缩放容器，缺失则无缩放；行为和命令不依赖它。

Primary=Foreground填充/Background文字；Secondary=Surface+Border；Ghost透明背景；Danger文字使用Danger。具体悬停/禁用色遵循冻结CSS；键盘焦点由独立可见指示提供。只禁用外观不能隐藏边框至难以识别。

默认padding水平Small=12、Medium=16、Large=20 DIP，间隔8 DIP，字号14。spinner覆盖中心，保留原内容占位，避免加载造成宽度跳变。Content为复杂对象时由AutomationProperties.Name覆盖名称。

## 动效与清理

Full：宽度>220 DIP按至.985；0<宽度<=48按至.96；其他.97。按下120ms、standard曲线，恢复snappy(.26,bounce0)；IsPressAnimationEnabled=false不缩放。未测量或无可选部件时跳过尺度效果。
Reduced无缩放，允许120ms加载透明度；Off立即变化。M1 Full spinner可持续旋转，Reduced/Off采用静态加载指示；不可见或卸载停止帧/时钟。卸载取消按压，重载不补播。

## 开发验收场景（程序化部分已运行，实际输入待验收）

| ID | 准备和操作 | 必须结果 |
|---|---|---|
| B01 | 绑定Command，分别鼠标/Space/Enter激活 | 每次一次Click和一次命令；保留原生CanExecute |
| B02 | 聚焦后设IsLoading=true，再经鼠标、键盘、UIA激活 | 不执行Click/命令，焦点保留；false后恢复 |
| B03 | 同时禁用与加载，再解除禁用 | 禁用优先，属性不被改写，重新启用显示加载 |
| B04 | 设置IsDefault并加载；触发默认激活 | 同样阻断；使用方直接执行命令不作保证 |
| B05 | 换模板、移除可选缩放部件、卸载重载 | 无异常/重复事件，绑定仍在，无闲置旋转 |
| B06 | 变体/尺寸/主题/内容动态更新 | 原生内容显示，资源覆盖有效，不重新赋值DataContext |

实际截图、DPI和交互观察本阶段不执行，后续按共同验证规范记录。上述API和事件契约可用于先写集成测试。

## 首版实现差异与结果

2026-10-25 补查：四种基础 Variant 已覆盖，但首版并非完整还原。Gallery 已补充前置添加图标、后置箭头、带图标的模拟上传与复制链接确认反馈。复制功能由用户点击后写入剪贴板；上传仅为模拟。随后按补齐计划加入 WPF 内容切换、模糊/位移与宽度过渡，适配细节见下文。本次网络读取超时，依据为本地冻结 button.tsx/button.module.css 及此前读取的页面文本，未做新的运行观察。

- 接入、构建和示例用法见根目录 README；应用需要合并公共主题资源。
- 按下沿用 120ms 曲线；恢复采用临界阻尼解析近似，约 260ms 达到 90%、520ms 收尾，不宣称与 Motion 求解器完全一致。
- 背景/边框/文字的纯色变化采用160ms过渡；非纯色画刷立即更新。主按钮悬停透明度与阴影、加载标签显示仍由模板即时切换。Full 旋转周期 700ms，Reduced/Off 静止。
- 默认字体使用 Segoe UI / Microsoft YaHei UI 回退，尚未分发原站字体。OKLCH 转 sRGB 后裁剪超色域通道，未进行视觉比对。
- 程序化验证记录见 [Button 首版验证](../verification/2026-10-25-button.md)。鼠标、真实键盘焦点、默认按钮 Enter、显示中的动画清理、DPI 和截图均待验收。

## Button 动效补齐（2026-10-25）

- 可选 PART_LabelSlot（Border）、Label（ContentPresenter）、PART_PreviousLabel（Image）配合内容过渡；缺失时保留原生即时内容更新。原有 PART_PressTarget 为 Border 时，其背景和描边由控件按当前公共属性提供，包括临时过渡画刷；自定义模板不希望接管画刷时可省略该可选部件。
- Full：文字进入240ms，从下方4 DIP、模糊4 DIP淡入；退出160ms，向上3 DIP淡出。UIElement 内容采用图标形变分支（.6→1，模糊2 DIP），这是WPF适配策略，不解析任意业务内容树判断它是否纯图标。
- 旧内容使用当前 DPI 下的标签位图，仅用于短暂退场，完成/取消后释放。它没有额外输入和自动化对象；超过4096像素的任一维度跳过旧图像。首次加载、隐藏、卸载和加载状态中的内容变更不补播。
- 只在 Content 引用/值改变时触发；复杂内容内部数据变化不做 Web labelKey 的递归检测。
- 自然宽度按钮的内部槽位约420ms完成90%变化、840ms收尾，临界阻尼解析近似、不超调；不改变公共 Width 或其绑定。显式 Width 的按钮只切换内容。父容器拉伸仍由WPF布局决定。
- Reduced 仅120ms淡化；Off立即切换。关闭动效、禁用、进入加载、不可见、卸载与更换模板取消时钟及排队回调；快速内容变化取消旧回调，从当前槽位宽度接续，不保持前段速度。
- 背景/边框/文字160ms颜色过渡使用控件自己的画刷副本；共享冻结资源不被动画修改。主题切换和反向状态变化从当前颜色接续。完成或关闭动效清除动画时钟。
- Gallery 可连续点击“继续 / 确认并继续下一步 / 完成”检查长短标签变化；另有复制确认、图标和加载组合。
- 程序化验证21项通过。此次新增测试直接调用内部过渡引擎，不显示窗口；不等于真实Loaded/渲染、视觉效果、屏幕阅读器或帧率验收通过。Web求解器、曲线与完整加载动效仍存在适配差异。

## 完整度矩阵（规范收口）

参考入口：[Button基准](../references/components/Button.md)。本表区分历史实现与剩余差异，不重新定义原版，也不修改既有用户反馈。

| 交付项 | 范围 | 实现状态 | 证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| 变体/尺寸/命令/加载门控 | 本次必做（已交付） | 已实现 | 程序化测试、用户反馈 | 真实键盘/读屏专项范围未确认 |
| 图标/图文内容与禁用状态 | 本次必做（已交付） | 已实现 | Gallery、用户反馈 | 使用系统字体，不作像素承诺 |
| 按压/内容/宽度/颜色过渡 | 本次必做（补齐已交付） | 已实现 | 过渡引擎测试、用户反馈 | WPF求解器近似；复杂内容检测差异 |
| 悬停透明度/阴影及加载细节过渡 | 明确暂缓（历史差异） | 未完整实现 | 源码对照 | 不称全动效已还原 |
| 明暗主题/资源覆盖 | 本次必做（已交付） | 已实现 | 动态资源测试 | 跨DPI未验收 |
| 同文案原版主示例 | 本次必做（下一次主预览调整） | 尚未建立；当前中文分组为测试示例 | 参考卡说明 | 本次只收口规范，不改已确认页面 |
| 截图/实际专项输入 | 明确暂缓 | 用户总体通过，专项未逐项确认 | 验证记录 | 不与程序化结果合并 |

验收补充：矢量图标中心按可见边界核查；内容宽度过渡后公共Width绑定仍有效；缺可选部件时行为继续工作。不要仅以四个Variant数量判断完整度。

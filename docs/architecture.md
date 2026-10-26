# 架构与开发规范

状态：实施基线，M1 验证后更新。此文中的项目/API名称未实现部分均为设计建议。

遇到多来源冲突时执行 [参考优先级](sources.md)，按问题类型选择依据，并在组件规格记录 WPF 适配差异。

## 1. 项目边界

计划分为 `src/ArcWpf.Core`（主题、令牌、共享设施）、`src/ArcWpf.Controls`（公开控件）、`samples/ArcWpf.Gallery`（示例）、`tests/`（测试）。图标数量与打包需求确认后再拆 `ArcWpf.Icons`。Controls 依赖 Core，Core 不依赖 Controls 或 Gallery。只有两个以上独立场景需要或有明确基础协议时才抽取共享设施。

Core 可以包含 WPF 相关服务，不将其宣称为跨平台库。需要复用的纯数学部分保持不依赖 Dispatcher/视觉树，是否另拆程序集由实际需求决定。

## 2. 控件与 API

- 优先继承 Button、TextBox、ToggleButton、Selector 等适当 WPF 基类，保留命令、输入和自动化行为。纯视觉修改可先使用样式；需要稳定新增 API 时定义控件类型。
- 公共可绑定/可样式化状态使用 DependencyProperty；只读依赖状态使用 DependencyPropertyKey。包装器只 GetValue/SetValue，变更逻辑放元数据回调。只有语义确实需要时设置双向默认绑定。
- 内部行为更新公开属性时保护使用方绑定，可评估 SetCurrentValue；不要无意以本地值覆盖样式。集合默认值不能在元数据中共享可变实例。
- 不设置控件自身 DataContext。模板绑定控件属性使用 TemplateBinding 或 RelativeSource TemplatedParent，复杂或双向场景明确绑定模式。
- 公共类型命名用英文 PascalCase；命名空间以 ArcWpf 为根。XAML 中统一示例前缀 `arc`，实际 XML namespace 映射在 M1 确定，不使用未拥有的网络地址作品牌承诺。
- 可复用控件的外观可由 ControlTemplate 替换。约定必要模板部件和视觉状态；缺少可选部件不崩溃。OnApplyTemplate 先解除旧部件事件，再绑定新部件。
- 使用命令与参数承载操作，必要交互采用路由事件；不要让控件依赖某个业务 ViewModel 或 DI 容器。
- 合理继承原生 AutomationPeer；自定义交互补充名称、角色和支持的 UI Automation 模式，避免丢失键盘与可访问行为。

## 3. 框架转换

| 来源 | WPF 方向 | 特别检查 |
|---|---|---|
| StyledProperty | DependencyProperty | 元数据、值优先级、默认绑定 |
| Avalonia DirectProperty | 只读 DP / 普通内部属性，按用途选择 | 通知和对外绑定需求 |
| ControlTheme | Style + ControlTemplate | DefaultStyleKey、Generic.xaml、资源查找 |
| 伪类 | Trigger / VisualState / 明确 DP | 不机械复制选择器 |
| CSS变量/Arc Token | 语义 ResourceKey + DynamicResource | 动态切换及覆盖优先级 |
| React effect | 属性回调、Loaded/Unloaded 或生命周期方法 | 重复加载、事件解绑 |
| AnimatePresence | 明确的进入/退出状态协调 | 退出完成前保留可视对象 |
| 动效值与弹簧 | WPF Animation / 专用调度 | 打断、绑定、结束清理 |
| ArcPopup | Popup / Adorner / 窗口覆盖层 | 焦点、DPI、资源、窗口边界 |

## 4. 主题与视觉

使用语义令牌（背景、表面、前景、边框、强调色、状态色、字号、间距、圆角），在基础资源中维护值。控件模板不散落调色板常量；自绘和派生几何允许可解释的计算常量。

建议资源覆盖顺序为基础值 → 明暗主题 → 强调色 → 应用覆盖。M1 验证实际合并和查找行为；切换主题不能丢失应用覆盖。需要动态切换的资源使用适当 DynamicResource 或等效更新机制。不能 Freeze 仍需变更或动画的共享资源。

每个控件独立主题字典，由 Themes/Generic.xaml 汇总。资源键避免普通字符串碰撞；M1 选择并固定 ComponentResourceKey 或统一命名策略。基础控件主题与应用隐式样式的组合方式用消费应用测试。

图标优先矢量，字体嵌入和回退必须验证中文、字重与许可。令牌值从已固定来源提取，不依赖模型凭记忆编造“Arc 标准”。

## 5. 动效

普通过渡使用 WPF 动画系统；弹簧与持续手势按需求使用单一可释放调度服务。统一约定 Full / Reduced / Off，系统偏好映射在 M1 验证；未接通时不要声称自动遵循系统设置。

优先动画 RenderTransform/Opacity；确需布局变化时限定范围并测量。弹簧按实际时间步进，限制长时间停顿后的步长，定义收敛条件；交互打断从当前值和必要速度继续。动画完成/取消后恢复有效基值并清除不再需要的时钟，避免破坏绑定。卸载、不可见或 Off 时停止不必要的帧订阅。

## 6. 浮层与输入

浮层不统一强制一种实现：先按是否跨窗口边界、是否模态、是否跟随滚动/缩放选择宿主。测试 Escape、点击外部、Tab、焦点进入与归还、锚点移动、屏幕边缘、关闭宿主和多显示器 DPI。

Input 保留 WPF TextBox 的选区、撤销和 IME 合成；输入过程不能每个字符强制重写 Text。数值/日期控件定义文化格式、非法中间态、提交与验证时机。Password 不能简单暴露与普通 TextBox 相同的双向明文契约。

## 7. 变更记录

当 M1 验证推翻当前选择时，更新本文件，并在相关组件规格记录决策理由、替代方案与影响。普通内部重构不要求新增审批流程。影响公开 API/依赖/支持平台的决策需保留记录。

## 官方参考

- [WPF 控件开发](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/control-authoring-overview)
- [依赖属性](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/dependency-properties-overview)
- [样式与模板](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview)
- [动画](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-overview)

实现时查询与目标版本相符的官方 API，不从其他 XAML 框架推断可用性。

### WPF胶囊圆角适配

不要把CSS的border-radius:9999px直接映射为WPF Border.CornerRadius=9999；Switch截图回归表明会出现椭圆外形。已知固定高度的胶囊按高度的一半设置几何圆角，例如24高轨道用12、18高滑块用9。ArcWpf.Radius.Pill旧资源保留兼容，但不能作为任意长宽比Border的通用实现。

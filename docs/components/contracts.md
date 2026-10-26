# M1 共同开发契约

版本：2026-10-25，规格 v1。本文规定 M1 契约；Button 首版已实现，其他组件仍待实现，验证结果分别记录。用户已明确本阶段不做截图和实际交互观察；它们不是规格完成或进入编码的前置条件。后续运行验收仍需另行记录。

## 类型、项目和资源

- 控件程序集 ArcWpf.Controls；四个控件均位于命名空间 ArcWpf.Controls。Core 中的公共基础设施位于 ArcWpf。示例先用 `xmlns:arc="clr-namespace:ArcWpf.Controls;assembly=ArcWpf.Controls"`，无需网络 XML namespace。
- `ControlSize` 枚举位于 ArcWpf：Small、Medium、Large；Button 的属性名 Size。`ArcMotionLevel`：Full、Reduced、Off；`ArcMotion.Level` 为继承的附加属性，默认 Full。M1 不承诺自动识别系统减少动画设置。
- 新增 DP 默认 OneWay；需要默认双向的属性在组件表明确标注。只读 DP 不接受外部设置；枚举拒绝非法值，几何属性拒绝 NaN/Infinity/不允许的负值。
- 不改变控件自身 DataContext。内部操作使用保留绑定的更新方式。所有 UI 属性更新在 Dispatcher 线程，控件不自动跨线程封送。
- 资源键使用带 ArcWpf 前缀的字符串，采用 DynamicResource。应用在 App.Resources.MergedDictionaries 中合并一次 ArcWpf.ArcThemeResources（Core 程序集）；默认控件模板由 Generic.xaml 自动发现，不需要合并 Gallery 或控件样式。主题用 Mode 切换，覆盖键放在应用主字典，优先于合并字典。M1最低集合：ArcWpf.Brush.Background/Foreground/Surface/SurfaceMuted/SurfaceRaised/TextSecondary/TextMuted/Border/BorderStrong/Danger/ControlOn/ControlTrack/ControlTrackHover/ControlThumb/ControlThumbOn/Focus；ArcWpf.Radius.Control/Panel/Pill；ArcWpf.Font.Body；ArcWpf.FontSize.Small/ExtraSmall；ArcWpf.ControlHeight.Small/Medium/Large。
- 颜色由冻结 foundation 提取，不改为模型推测值。几何以16px根字号映射至WPF DIP：按钮最小高度36/44/50、控件圆角18、面板26、小字14、辅助文字12。这是项目设计选择，非网站运行测量。主题覆盖不得静默丢失应用资源。
- 控件文件按 `src/ArcWpf.Controls/<Component>/` 组织，主题在其 Themes 子目录；公共 Themes/Generic.xaml 汇总。Gallery 页面按控件分开。实际命令以解决方案创建后的路径为准。

## 模板与行为

公开状态是契约，模板内部视觉树不是契约。默认模板满足视觉要求，自定义模板只需遵守必要部件。可选部件缺失时跳过相应效果；必要部件缺失时发出可诊断信息，不在设计器中无条件抛异常。Input 缺少原生内容宿主时无法编辑，不能假装可降级。

状态可通过 Trigger 或内部状态协调呈现，不强制特定 VSM 组名。不得在同时动画的模板属性上再放冲突 Trigger。动画使用模板内部变换，不覆盖使用者设置在控件上的 RenderTransform。

Disabled 优先阻止用户动作；其他只读状态和程序赋值仍正常工作。焦点可见性是独立层，不被悬停/按下覆盖；保留原生自动化模式。仅图标内容必须显式提供 AutomationProperties.Name。

不对 WPF 绑定回写、源 ViewModel setter 和事件处理器之间承诺不存在的全局执行顺序。规格保证事件触发时控件当前值已更新，自动化测试分别检查事件顺序与最终绑定结果。外部直接调用 ICommand.Execute 不属于控件可阻断范围。

## 统一动效与生命周期

Full 按组件规格；Reduced 去掉空间运动和形变，允许120ms透明度；Off 立即达到终态。切换等级到 Off 时取消现有动画并落到当前逻辑目标。未加载时仅保存状态，首次显示不补播历史动画。

卸载/换模板解除旧部件、宿主、输入和帧事件，清除时钟并取消未完成回调。重载只反映最新逻辑状态。定时回调必须用代次标识避免旧动画完成回调覆盖新状态。

## 开发与检查顺序

1. 建最小 Core/Controls/Gallery/Tests 和独立消费示例。
2. Button：主题资源、命令、加载和模板路径。
3. Switch：两态、输入顺序与可中断弹簧。
4. Input：原生编辑、验证显示和绑定时机。
5. Popover：先验证宿主最小场景，再补主题和动画。

每项先完成 API 与逻辑测试，再加入动效。缺少运行环境的验收标记未运行，不能因规格完成就报告组件可用。当前不执行截图/实际交互；规格中的场景供后续实现阶段使用。


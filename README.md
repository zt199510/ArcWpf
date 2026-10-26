# ArcWpf

将 Arc 的视觉语言与交互体验实现为 .NET 10 WPF 原生控件库。

当前已建立解决方案并实现 Button 首版：Primary / Secondary / Ghost / Danger，三种尺寸、命令、加载状态、明暗主题、按压反馈、内容切换、宽度与颜色过渡及减少动效。Switch 首版已加入，包含两态绑定、标签、命令、按压伸长和可反向弹簧。Input首版已加入原生文本编辑、标签、占位、说明和验证错误。Popover、CheckBox、RadioButton/RadioGroup、Select/SelectItem、Tabs/TabItem首版已实现。截图与实际交互观察按用户要求暂缓；本版尚未完成视觉和输入验收。

## 构建与运行

在 Windows 上安装 .NET SDK 10.0.402（global.json 精确固定），在仓库根目录执行：

~~~powershell
dotnet restore ArcWpf.slnx --locked-mode
dotnet build ArcWpf.slnx -c Release --no-restore
dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj -c Release --no-build
dotnet run --project samples/ArcWpf.Gallery/ArcWpf.Gallery.csproj
~~~

独立消费示例：

~~~powershell
dotnet run --project samples/ArcWpf.Consumer/ArcWpf.Consumer.csproj
~~~

Gallery 通过 Button / Switch / Input / Popover / CheckBox / RadioButton / Select / Tabs 页签浏览组件，支持主题/动效等级切换、状态和绑定示例。

## 在 WPF 应用中接入

引用 src/ArcWpf.Controls/ArcWpf.Controls.csproj。在 App.xaml 中声明 xmlns:core="clr-namespace:ArcWpf;assembly=ArcWpf.Core"，并合并公共主题资源：

~~~xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <core:ArcThemeResources Mode="Light" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
~~~

Button 模板由 WPF 自动发现，无需引入 Gallery 或手动合并 Button 样式。公共主题资源必须合并一次；仅引用程序集不自动修改应用资源。

页面声明 xmlns:arc="clr-namespace:ArcWpf.Controls;assembly=ArcWpf.Controls"：

~~~xml
<arc:Button Content="保存" Variant="Primary" Size="Medium"
            Command="{Binding SaveCommand}"
            IsLoading="{Binding IsSaving}" />
~~~

IsLoading 保留内容测量并阻止控件发起的 Click/Command；不阻止应用直接执行 ICommand。基类保留 WPF 原生命令和输入机制。只含图标时请设置 AutomationProperties.Name。

改变已合并的 ArcThemeResources.Mode 切换明暗主题。覆盖资源放在应用主字典中，例如 ArcWpf.Brush.Foreground；其优先级高于合并的主题字典。也可通过控件本地属性覆盖 Background、Padding 等。窗口上的 ArcMotion.Level 可设 Full、Reduced、Off；等级向子控件继承。原站字体尚未打包，目前采用 Windows 字体回退。

## 项目与资料

- src/ArcWpf.Core：语义资源、尺寸与动效等级。
- src/ArcWpf.Controls：WPF 原生控件与默认模板。
- samples/ArcWpf.Gallery：组件演示；samples/ArcWpf.Consumer：独立接入示例。
- tests/ArcWpf.Tests：STA 程序化测试。
- [Button 首版验证记录](docs/verification/2026-10-25-button.md)：已测与未测范围。
- [实施计划](docs/plan.md)、[架构规范](docs/architecture.md)、[共同契约](docs/components/contracts.md)。
- [Button](docs/components/Button.md)、[Switch](docs/components/Switch.md)、[Input](docs/components/Input.md)、[Popover](docs/components/Popover.md)。
- [来源报告](docs/references/2026-10-25-initial.md)、[第三方许可](THIRD-PARTY-NOTICES.md)。
- [智能体入口](AGENTS.md)：项目 Skills 位于 .agents/skills，随仓库维护。

社区移植项目，不宣称与 Arc、TKCode 或其作者存在官方关系。尚未发布 NuGet 包。


Switch 用法（IsChecked 默认双向绑定）：

~~~xml
<arc:Switch Content="接收通知" IsChecked="{Binding Notifications}" />
~~~

Button 已收到用户测试通过反馈；Switch 仍待实际界面验收，详见 [Switch 验证记录](docs/verification/2026-10-25-switch.md)。

Input用法（默认Text绑定移焦更新；实时验证需显式指定UpdateSourceTrigger）：

~~~xml
<arc:Input Label="邮箱" Placeholder="you@example.com"
           Text="{Binding Email, UpdateSourceTrigger=PropertyChanged}"
           Description="用于接收账户通知。" ErrorMessage="{Binding EmailError}" />
~~~

Switch已收到用户测试通过反馈。Input实际编辑验收待做，见 [Input验证记录](docs/verification/2026-10-25-input.md)。

## 后续复刻工作入口

- [复刻与交付规则](docs/replication-workflow.md)：问题定位、WPF适配、完整度和证据分层。
- [参考基准模板](docs/templates/reference-baseline.md)、[组件规格模板](docs/templates/component-spec.md)、[可复用任务提示](docs/templates/component-task-prompt.md)。
- 参考卡：[Button](docs/references/components/Button.md)、[Switch](docs/references/components/Switch.md)、[Input](docs/references/components/Input.md)、[Popover](docs/references/components/Popover.md)。

当前Button/Switch已有用户通过反馈；Input最新输入与占位对齐修复已通过程序化回归，待用户确认。Popover已完成代码及程序化测试，实际窗口与视觉验收待做。

## Popover 用法

~~~xml
<arc:Button x:Name="ShareAnchor" Content="Share" Variant="Secondary"
            IsPressAnimationEnabled="False" Click="ToggleShare" />
<arc:Popover PlacementTarget="{Binding ElementName=ShareAnchor}"
             IsOpen="{Binding ShareOpen}" Side="Bottom" Alignment="Start">
  <TextBlock Text="Anyone with the link can view." TextWrapping="Wrap" />
</arc:Popover>
~~~

ToggleShare将视图模型ShareOpen取反；属性需通知更新。锚点和Popover应在同一已加载Window中；无效锚点请求会复位为false。Close()程序关闭；Closed事件的Reason区分关闭原因。内容较宽时使用自适应宽度以配合工作区夹紧。

Gallery现有Popover页；代码与13项新增回归已完成，真实窗口及视觉待验收。参见 [Popover验证记录](docs/verification/2026-10-25-popover.md)。


## CheckBox 用法

~~~xml
<arc:CheckBox Content="Include archived projects"
              Description="Show projects that are no longer active."
              IsChecked="{Binding IncludeArchived}" />
<arc:CheckBox Content="三态选择" IsThreeState="True"
              IsChecked="{Binding Selection}" />
~~~

IsChecked默认双向绑定；三态模型使用bool?。保留WPF原生循环、命令和事件；Content是标签，Description是可换行说明。无标签时提供AutomationProperties.Name。自定义强调色时配套覆盖ArcWpf.Brush.ControlOn与ArcWpf.Brush.ControlGlyph。

Gallery → CheckBox包含官网同源主样本及扩展示例。代码和程序化验证完成，实际视觉待用户验收：[规格](docs/components/CheckBox.md)、[验证记录](docs/verification/2026-10-26-checkbox.md)。

## RadioButton / RadioGroup 用法

~~~xml
<arc:RadioGroup Header="Review mode" SelectedValue="{Binding ReviewMode}">
  <arc:RadioButton Content="Focused review" Description="One reviewer at a time." Value="focused" />
  <arc:RadioButton Content="Open review" Description="Everyone can comment together." Value="open" />
</arc:RadioGroup>
~~~

SelectedValue默认双向绑定，模型需通知更新。默认null为无选择；分组隔离组名，完整共享选中框使用RadioGroup。独立单项使用原生GroupName和IsChecked；保留Command及原生事件。ItemsSource数据项可直接用作SelectedValue，或通过ItemContainerStyle将Value绑定到数据键；避免重复Value。

Gallery → RadioButton；[规格](docs/components/RadioButton.md)、[参考卡](docs/references/components/RadioButton.md)、[验证记录](docs/verification/2026-10-26-radio.md)。程序化验证完成，实际视觉待验收。

## Select 用法

~~~xml
<arc:Select Label="Team" Description="Choose the team that owns this work."
            SelectedValuePath="Tag" SelectedValue="{Binding Team}">
  <arc:SelectItem Content="Design" Tag="design" />
  <arc:SelectItem Content="Engineering" Tag="engineering" />
  <arc:SelectItem Content="Research" Tag="research" />
</arc:Select>
~~~

对象数据源可使用ItemsSource、DisplayMemberPath和SelectedValuePath；未选中时显示Placeholder。禁用项设置SelectItem.IsEnabled=false，禁用字段设置Select.IsEnabled=false。这是不可编辑单选控件，不是搜索框或多选框。MaxDropDownHeight默认320，较长列表使用原生滚动条。

Gallery → Select；[规格](docs/components/Select.md)、[参考卡](docs/references/components/Select.md)、[验证记录](docs/verification/2026-10-26-select.md)。当前程序化验证通过，实际窗口和视觉待验收。


Tabs用法（继承原生TabControl，SelectedIndex/SelectedItem/SelectedValue绑定）：

~~~xml
<arc:Tabs SelectedValuePath="Tag" SelectedValue="{Binding CurrentView}">
  <arc:TabItem Header="Overview" Tag="overview" Content="Workspace overview" />
  <arc:TabItem Header="Settings" Tag="settings">
    <arc:Input Label="Project name" Text="Arc launch" />
  </arc:TabItem>
</arc:Tabs>
~~~

当前支持水平顶端标签、选中框与内容/高度过渡、溢出滚动、RTL和动效等级。Tabs新增14项程序化测试，全量110项通过；实际窗口尚待用户验收。见[Tabs规格](docs/components/Tabs.md)和[验证记录](docs/verification/2026-10-26-tabs.md)。

Dialog用法：用DialogHost包住窗口内容，在Dialog属性声明对话框并绑定IsOpen。也可创建Dialog后调用Show(触发按钮或页面)，由其寻找祖先Host。

~~~xml
<arc:DialogHost>
  <arc:DialogHost.Dialog>
    <arc:Dialog Title="Publish workspace"
                Description="Your team will see these changes immediately."
                IsOpen="{Binding IsPublishOpen}">
      <arc:Button Content="Done" Command="{x:Static arc:Dialog.CloseCommand}" />
    </arc:Dialog>
  </arc:DialogHost.Dialog>
  <!-- 放置窗口原有页面内容 -->
  <Grid />
</arc:DialogHost>
~~~

支持Closing取消关闭、遮罩/Escape关闭、进退场及标题说明切换动效；模态范围为Host。Dialog新增16项测试，全量126项通过；实际窗口待验收。见[规格](docs/components/Dialog.md)与[验证记录](docs/verification/2026-10-26-dialog.md)。Gallery已新增Dialog页。

Tooltip用法：包裹可聚焦触发控件，Tip提供补充文字，Side默认Top，也支持Bottom/Left/Right。

~~~xml
<arc:Tooltip Tip="Only workspace members can see this project" Side="Top"
             HorizontalAlignment="Left">
  <arc:Button Content="Project visibility" Variant="Secondary" />
</arc:Tooltip>
~~~

无需DialogHost；支持悬停/焦点、Escape、共享延迟、文案和尺寸动效。提示内不放交互控件；图标按钮仍需AutomationProperties.Name。Gallery已新增Tooltip页，F2示例可观察动态文案。新增15项测试，全量141项通过；实际窗口仍待验收，见[Tooltip规格](docs/components/Tooltip.md)与[验证记录](docs/verification/2026-10-26-tooltip.md)。

Toast用法：在页面放置ToastHost，通过Show创建通知。默认4.5秒自动关闭，Duration=0可关闭计时。

~~~xml
<arc:ToastHost x:Name="Toasts" />
~~~

~~~csharp
Toasts.Show("Changes saved", "Synced to all devices.");
~~~

ToastHost支持多条通知和DismissAll；Toast支持IsOpen绑定、手动Close和Closing事件。首期未加入滑动、动作按钮和类型变体。新增4项测试，全量145项通过；实际窗口待验收。见[Toast规格](docs/components/Toast.md)与[验证记录](docs/verification/2026-10-26-toast.md)。


MultiSelect 用法（原生多选列表与 Search；SelectedValues 默认双向绑定）：

~~~xml
<arc:MultiSelect Label="Labels" Options="{Binding LabelOptions}"
                 SelectedValues="{Binding SelectedLabels}" MaxVisible="2" />
~~~

Options 使用 `IEnumerable<MultiSelectOption>`，例如 `new MultiSelectOption("bug", "Bug")`；SelectedLabels 为可通知属性的 `IList<string>`。用户选择回写新的去重集合；外部 ObservableCollection 增删会刷新，普通列表需重新赋值。默认支持搜索与标签删除；`IsSearchEnabled="False" CanRemoveTags="False"` 对照原版。窄宽度会自动将多余标签合并到 +N，MaxVisible 是上限。

Gallery → MultiSelect，或启动时传 `--page=multi-select`。见[规格](docs/components/MultiSelect.md)、[参考卡](docs/references/components/MultiSelect.md)、[验证记录](docs/verification/2026-10-26-multiselect.md)。实际交互由用户人工验收，未提交或推送。

ComboBox 用法（可搜索单选，SelectedValue 默认双向绑定）：

~~~xml
<arc:ComboBox Label="Time zone" Options="{Binding TimeZones}"
              SelectedValue="{Binding SelectedZone}" />
~~~

Options 使用 `IEnumerable<ComboBoxOption>`，例如 `new ComboBoxOption("cet", "Central European", Keywords: ["zurich", "berlin"])`。输入按标签和关键词过滤；查询与选择分离，Enter提交，Escape丢弃查询，清除后重新打开列表。可绑定QueryText；Text是当前编辑/显示文字，不作为选中值绑定。

Gallery → ComboBox，或参数 `--page=combobox`。本轮只运行ComboBox专项，9/9通过；实际交互由用户人工验收，不做computer-use。见[规格](docs/components/ComboBox.md)、[参考卡](docs/references/components/ComboBox.md)、[验证记录](docs/verification/2026-10-26-combobox.md)。

# CheckBox：M2 组件规格

状态：代码与程序化验证完成，待用户验收。来源已冻结，实际窗口/截图按用户既定要求延期。参考[基准卡](../references/components/CheckBox.md)。

## 公开契约

ArcWpf.Controls.CheckBox继承System.Windows.Controls.CheckBox；保留Content/ContentTemplate、IsChecked(bool?，默认false，默认双向)、IsThreeState(false)、Command/CommandParameter/CommandTarget、Click/Checked/Unchecked/Indeterminate原生事件。新增Description字符串DP默认空。Content承担标签，不另造Label别名。

二态原生切换false→true→false；可设置null显示不确定，二态下一次操作null→false。三态按WPF false→true→null→false，属于桌面适配，与Radix从mixed切到true不同。Space切换，Enter不额外触发；Tab与访问键遵循WPF。禁用/CanExecute=false阻止点击与命令。内部更新保持绑定；不接管DataContext。

自动化角色CheckBox、Toggle支持三态；Name优先显式AutomationProperties.Name，其次Content，空标签回退CheckBox；HelpText优先显式值，其次Description。UIA修改保留原生规则。

模板可替换；PART_Box/Fill是Border，PART_Mark是Path，均可缺失并降级为原生行为。旧模板释放帧订阅和动画；未加载/不可见/禁用/Reduced/Off立即终态。无永久Timer。

## 布局与状态

- 点击区44×44，方框18×18、半径5、border1；标签起点45，首行中心y22；说明与标签gap2。无标签/仅说明/自定义ContentTemplate/长文本都支持；Description不作强制必填。
- 标签14 Medium、首行19.6；说明12、行16.8；有限宽度换行，方框始终顶端第一行对齐。RTL镜像排布，勾号图形保持原方向。
- 默认Surface/BorderStrong，选中及null使用ControlOn/Glyph；未选悬停边框TextMuted；禁用仅方框区opacity.5。键盘焦点环围绕44区，不围整段文字。
- 纯内容/说明整个控件可点击：WPF原生ContentControl适配，说明区也参与点击，与Web说明文本不触发存在差异。

## 动效合同

| 项目 | Full | Reduced/Off | 打断与结束 |
|---|---|---|---|
| 填充与路径绘制 | 临界阻尼visualDuration.26；填充scale.6→1，路径按弧长0→1 | 即时 | 保当前位置/速度，收敛解除帧订阅 |
| 勾号与横线 | 三点坐标morph.42 | 即时 | 不替换两个静态图标；途中反向保持值/速度 |
| mark/fill透明度 | 开120ms/关160ms，standard曲线 | 即时 | 从当前值过渡；完成移除时钟 |
| 边框颜色 | 160ms standard | 即时 | 从当前颜色开始；主题/外部属性更新跟随 |
| 按压与释放 | .95按下120ms standard；释放580ms spring采样 | 无缩放 | 独立方框Transform；不占用使用方RenderTransform |

源参数已确认，WPF临界阻尼求解不是Motion visualDuration精确复现；禁止声称速度/帧率与Web完全一致。动画结束不占用共享画刷或外部属性。

## 验收与完整度

| 交付项 | 范围 | 当前状态 | 验证出口 |
|---|---|---|---|
| API/绑定/命令/三态 | 必做 | 已实现 | 循环、null、事件、命令、禁用、绑定保留 |
| 外形与布局/组合 | 必做 | 已实现 | 最终布局中心、长文本、无标签、仅说明、RTL |
| 动效/清理 | 必做 | 已实现 | 反向、Off、卸载、模板替换、主题更新 |
| 输入/自动化/主题 | 必做 | 已实现 | UIA程序化；实际键鼠/DPI待用户后续验收 |
| Gallery/Consumer | 必做 | 已实现 | 原站主样本与独立扩展区，独立消费构建 |
| CheckBoxGroup/树父子汇总逻辑 | 暂缓 | 不在本次 | 业务逻辑不放入单个控件 |

实际鼠标/触屏/Space/读屏、100/150/200%DPI、动画视觉与性能未测不得填通过。当前还原目标为冻结源码，尚未建立审查通过的WPF截图基准。

## 实施结果

源码位于src/ArcWpf.Controls/CheckBox，默认模板由Generic合并。Gallery及Consumer已接入，CheckBox新增12项测试，全量69项通过。详见 [验证记录](../verification/2026-10-26-checkbox.md)。实际窗口、视觉与性能不在上述通过范围。

### 取消选中闪烁修复（2026-10-26）

修正填充与勾号Opacity在动画清除后恢复默认1的问题；这两个内部模板部件的Opacity归动效引擎管理，保存持久终态。公开控件属性与绑定不受影响。新增真实Dispatcher时钟回归，修复前失败、修复后通过，全量70项通过；用户视觉复测待确认。详情见验证记录。

# PasswordInput

状态：本次实现完成；11项专项通过，实际窗口待用户人工验收。来源见[参考卡](../references/components/PasswordInput.md)。测试限定PasswordInput专项；不用computer-use，实际界面由用户人工验收。

## API与原生机制

Control组合原生PasswordBox与PasswordRevealBox（TextBox派生）。默认遮蔽。Password是显式CLR string读写属性，非DP；SecurePassword返回调用方负责Dispose的独立SecureString副本；SetSecurePassword复制输入，不取得调用方对象所有权；Clear清空；PasswordChanged为不带明文的路由事件。内部以SecureString保留模板更换间的数据，正常隐藏编辑直接读取PasswordBox.SecurePassword快照。显示期间存在原生TextBox明文；隐藏、不可见、禁用、卸载或更换模板清空该TextBox，关闭其撤销历史。显式取Password或原生PasswordBox写入时不可避免产生托管字符串，不承诺零明文内存。

DP：Label/Placeholder/Description/ErrorMessage默认空，IsPasswordRevealed默认false双向，MaxLength默认0（原生0不限；限制用户输入，不截断外部赋值）、PasswordChar默认●，ShowPasswordText="Show password"、HidePasswordText="Hide password"。只读HasPassword、HasError、EffectiveErrorMessage、IsPlaceholderVisible、RevealButtonText。不接管DataContext。外部验证可绑定ErrorMessage，另支持Validation.Errors；默认不增加密码明文绑定。禁用阻止用户输入与切换，外部Password赋值仍有效。

焦点：Tab进入活动编辑器，再到显示按钮；Enter/Space切换。鼠标点击按钮保留原编辑器焦点（切换编辑器时迁移），键盘操作保留按钮焦点。标签点击聚焦编辑器。原生PasswordBox不公开任意选区，切换后定位到末尾/原生可实现状态，不能承诺同一原生编辑器的完整选区与撤销历史。显示编辑器禁用Undo；IME合成期间暂缓显示状态切换，不重写正在合成的编辑文本。

自动化：容器为Group，两个编辑器均保持密码语义，Value读取返回空；显示编辑器不提供可读TextPattern，避免作为普通TextBox暴露文本；原生密码框保留WPF自身peer。按钮Toggle语义和名称跟随当前显示状态。真实读屏待人工，不从peer测试推断读屏通过。

## 完整度

| 项目 | 本次范围 | 状态 |
|---|---|---|
| 存储/取值/事件 | 显式Password/SecurePassword、原生输入、模板保持、无明文DP | 已实现；程序化证据见验证记录 |
| 显示/焦点/键盘 | 遮蔽默认、按钮切换、合成保护、Tab/Enter/Space、禁用 | 已实现；程序化证据见验证记录 |
| 外形 | 44输入壳、30按钮/18图标、稳定操作槽、长值/窄宽/标签说明 | 已实现；程序化证据见验证记录 |
| 状态/验证 | placeholder、明暗资源、显式错误/Validation.Errors | 已实现；程序化证据见验证记录 |
| 动效 | eye斜线/遮罩、颜色/按压、resolve、消息字词与高度 | 已实现；程序化证据见验证记录 |
| 生命周期/等级 | 快速反向、Full/Reduced/Off、卸载/重载/换模板 | 已实现；程序化证据见验证记录 |
| Gallery | 文档Password空主例、演示值、窄框、禁用、动态说明/错误 | 已实现；程序化证据见验证记录 |
| 强度评分/密码管理器/默认明文绑定 | 不属于本次范围 | 不适用 |

主要模板部件：PART_PasswordBox、PART_RevealBox、PART_RevealToggle、PART_Shell、PART_EditorVisual、PART_Label、PART_DescriptionHost、PART_ErrorHost。缺失可选部件安全降级；无编辑器时仍可显式取值/清空。未实现IsReadOnly；不将PasswordBox不具备的属性伪装为原生支持。

验证证据见[验证记录](../verification/2026-10-27-password-input.md)。真实Tab/Space、中文IME、粘贴、DPI与读屏尚未人工验证。


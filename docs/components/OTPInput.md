# OTPInput

状态：本次实现完成；OTPInput专项8/8通过，当前交付版本已获用户人工测试通过反馈（2026-10-27），未明确覆盖的专项仍待验证。不使用computer-use。

来源为Arc免费OTP input，冻结批次2026-10-27-otp-input；见[参考卡](../references/components/OTPInput.md)。原站数字/文本双模式，本次只做已授权数字模式。

## API与输入契约

Control组合原生TextBox，每格一个字符。Length默认6，支持1..12（WPF有界布局选择）；Value字符串默认空、默认双向绑定，保留ASCII 0..9并截断到Length；Label、Description、ErrorMessage默认空；IsReadOnly默认false，IsEnabled沿用原生。只读IsComplete。ValueChanged路由事件通知任何有效值变化；Completed只在用户编辑/粘贴产生新的完整值时触发，外部赋值/长度变化不触发业务提交。Clear()、FocusDigit(index)。

数字输入替换当前格并前进；多字符输入/粘贴从当前格连续覆盖、截断，过滤非数字。左右/Home/End导航；Backspace清除当前，空格则清前格并退回；Delete清当前。值为连续字符串，中间删除后右侧左移（对应原版join行为），点击尾部空格从首个空位续填，不产生隐形空洞。Tab逐格导航；保留原生每格选择/复制；数字替换由控件协调，不承诺跨格撤销。数字模式禁用IME，WPF不模拟短信自动填充。只读允许选择/复制/导航但阻止修改。

## 交付矩阵

| 项目 | 本次范围 | 状态 | 证据与差异 |
|---|---|---|---|
| API/绑定/完成事件 | 必做 | 已实现；专项通过 | 数字模式；完成事件为WPF扩展 |
| 外形/状态 | 必做 | 已实现；专项通过 | 42×48格、8间隔、18圆角；标签/消息独立行；窄宽等比压缩 |
| 焦点/输入/UIA | 必做 | 已实现；专项通过 | 原生TextBox与每格名称，Group容器 |
| 动效 | 必做 | 已实现；专项通过 | 色160ms、glyph240ms/wave、退场120ms、ring滑动/缩放、错误摇动360ms、消息字词/高度 |
| 生命周期/主题 | 必做 | 已实现；专项通过 | Full/Reduced/Off、卸载/隐藏/禁用/换模板清理 |
| Gallery | 必做 | 已实现；专项通过 | 原版Verification code空值、说明；扩展绑定/四位/禁用/错误/窄宽 |
| 字母模式/短信自动填充/业务校验 | 不适用 | 不实现 | 本次范围外；服务端验证由应用完成 |

模板部件PART_Row(Grid)、PART_Ring(Border)、PART_DescriptionHost/ PART_ErrorHost(Panel)。公开Width/Height属于整个控件；格高只作用于输入行。小于默认总宽时格缩小，gap可随极窄宽度缩小；不将浏览器viewport断点解释为WPF控件宽度断点。实际鼠标、剪贴板、DPI与读屏待用户验证。


证据见[验证记录](../verification/2026-10-27-otp-input.md)。未执行其他控件回归；实际键鼠、多DPI、高对比度与读屏待验收。


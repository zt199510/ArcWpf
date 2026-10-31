# HoldToConfirm 验证记录（2026-10-29）

## 实现

- 新增 `HoldToConfirm` 控件、主题、Gallery 页面和来源冻结批次。
- 覆盖 Idle/Holding/Confirmed、提前松开回退、鼠标移出取消、Space/Enter、失焦、禁用和主题 tone。

## 程序化结果

- Controls 构建：成功，0 警告/错误。
- Gallery 构建：成功，4 个既有未相关警告。
- HoldToConfirm 专项：3/3 通过。

## 待运行

- 实际窗口的按住时长、鼠标捕获、键盘释放、明暗主题、Reduced/Off 和长文案布局。


未使用 computer-use；程序化结果与真实窗口验收分开记录。




## 2026-10-29 Arc 对齐修正

- 根因：旧实现用 ScaleTransform.ScaleX 缩放整层填充，造成中间文字/图标横向压扁；填充宽度还受 Surface Padding 内容测量影响。
- 修复：采用固定完整尺寸 face + PART_FillClip 外层裁剪；Progress 只改变裁剪宿主 Width。
- 修复：确认文案淡入/上浮、勾号 dash 绘制、快速重新按住续填、旧回退时钟清理和 tone 图标颜色。
- HoldToConfirm 专项复验：3/3 通过。
- 实际窗口：仍待人工观察填充边界、长文案、明暗主题和 Reduced/Off。


## 2026-10-29 鼠标捕获修复

- 用户反馈：按住完成后整个 Gallery 窗口无法继续点击。
- 根因：确认态再次按下时 Begin 直接返回，但旧逻辑仍无条件调用 CaptureMouse；抬起时没有 active hold，捕获未释放。
- 修复：仅在成功开始 hold 后捕获鼠标；确认态抬起路径主动释放残留捕获；新增 OnLostMouseCapture 清理路径。
- 专项复验：HoldToConfirm 4/4 通过。

## 2026-10-31 按冻结源码重做

- 复核：冻结源码与线上 `https://uiarc.dev/r/hold-to-confirm.json` 逐文件一致。
- 旧实现与源码的差异：按压缩放只作用于 Surface（填充层不随之缩放而错位）；完成勾号 `StrokeDashOffset=24` 从未动画，实际不可见；标签宽度瞬跳、无旧文案退场；受控 Confirmed 切换时填充瞬跳；hover 用 SurfaceMuted 而非 6% 填充色混合且无过渡；danger 描边缺失；禁用透明度只作用于 Surface；无 HelpText 提示；自绘垃圾桶图标与 lucide trash-2 不同。
- 修复：见 [组件规格](../components/HoldToConfirm.md) 范围矩阵。Gallery 主示例改为源站 `Hold to delete`（accent）+ 下方提示，tone 示例移入扩展区并提供 Reset。
- 新增测试：PressScalesWholeButtonSoFillStaysAligned、ConfirmingMorphsLabelAndDrawsCheck、DeclinedControlledConfirmationRewinds。

### 程序化结果

- Linux（EnableWindowsTargeting）构建 ArcWpf.slnx：成功，0 错误；警告均为既有 CS0067。
- 专项测试：**未运行**。本机无 Windows；尝试 wine 9.0 + Windows Desktop Runtime 10.0.13 运行，WPF 在 TypefaceMap.MapUnresolvedCharacters 处 FailFast（wine DirectWrite 字体回退不完整），无法得到有效结果。
- 实际窗口：未运行，待在 Windows 上观察填充边缘换色、按压、完成 morph、勾号绘制、明暗主题与 Reduced/Off。

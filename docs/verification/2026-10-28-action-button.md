# ActionButton 验证记录（2026-10-28）

## 实现

- 新增 `ArcWpf.Controls.ActionButton`，复用 `Button` 的原生命令、键盘、禁用和 Automation 基类行为。
- 增加异步 Action、Pending/Success/Error 状态、成功自动复位、失败路由事件、Arrow/Check 模板。
- Gallery 新增 ActionButton 页面，覆盖成功自动复位、成功保持和失败状态。

## 验证

- 构建命令：`dotnet build ArcWpf.slnx --no-restore --verbosity quiet`。
- ActionButton 专项：待运行。
- 实际窗口：已打开 Gallery 复核初始/成功态；Pending 中间帧因外部窗口抢占未完成，不能标记为完整视觉通过。

## 未覆盖

中文读屏 LiveRegion、真实鼠标/键盘闭环、100/150/200% DPI、实际动效观感和长文案布局仍待验收。


## 2026-10-28 动效对齐修正

用户反馈 Web 与 WPF 交互/动画不一致。对照 Arc Action button 页面复核后确认：原 WPF 模板隐藏 Pending 文案、把 Spinner 居中覆盖、未让 Spinner 随 `IsPending` 旋转，且没有使用 Button 的 `PART_LabelSlot`/`PART_Label`，因此既有文字 blur/宽度过渡未生效。

本次修正：

- Pending 保留文案显示，Spinner 改为文案右侧独立布局单元。
- 模板改用 `PART_LabelSlot` + `PART_Label`，复用 Button 的内容淡入、位移、模糊和内部宽度过渡。
- Pending 接入独立 Spinner 旋转时钟；Success 接入勾号淡入；Reduced/Off 停止时钟并直接到终态。
- 卸载、模板重载和状态反向切换会停止 Spinner/勾号动画。

程序化回归：ActionButtonTests 3/3 通过；解决方案构建 0 错误。实际窗口仍需在最终构建中观察 Web 对照下的中间帧速度、箭头离场和勾号绘制细节。


## 2026-10-28 Pending 可见性修正

Gallery 示例异步等待从350ms调整为1200ms（保持成功示例900ms），让鼠标点击后的Pending、Spinner旋转和按压反馈有足够观察窗口。


## 2026-10-28 复刻偏差修正

复核来源动效清单后移除 ActionButton 专属 Spinner：Arc 来源要求 Pending 文案 morph、箭头离场和 Success 勾号绘制，Spinner 属于普通 Button loading 语义，不应套用到 ActionButton。新增专项断言 Pending 模板没有 PART_Spinner，ActionButtonTests 4/4 通过。


## 2026-10-28 三段状态链修正

按用户反馈固定为：点击后箭头旋转；异步成功时文字从原文案 morph 到 SuccessLabel 并触发内部宽度收缩；280ms 过渡后停止旋转并绘制勾号。ActionButtonTests 4/4 通过。

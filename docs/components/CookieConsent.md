# CookieConsent

状态：实现完成，真实窗口视觉、焦点和主题验收待执行。

Arc Free 来源采集批次为 `2026-11-03-cookie-consent`。控件提供同意栏、偏好面板和重新打开按钮三种状态；必需类别始终开启，可选类别在面板中使用 Switch 编辑。`Accept all`、`Reject all`、`Save choices`、`Reset` 会通过 `ConsentChanged` 报告动作，并可用 `StorageKey`/`Version` 持久化。

WPF 使用原生 Control 模板和现有 Switch/Button 组件；偏好开关通过 `DraftEnabled` 编辑态绑定，只有保存/接受/拒绝时才写入 `Consent`。偏好类别列表采用最大 300 DIP 的内部 ScrollViewer，避免窄窗口或长列表撑破面板。Position 会分别对齐 bar、panel 和 reopen chip，居中/左侧 chip 靠左，右侧 chip 靠右。状态面现在会根据 `ArcMotionLevel` 对 bar、panel 和 chip 做淡入及轻微缩放进入，打开、关闭和提交路径会安排明确的按钮焦点归还；Arc 的单一 surface 尺寸形变和逐帧 reduced-motion 细节仍待补齐。

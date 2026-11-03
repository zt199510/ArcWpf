# CookieConsent

状态：按 Arc 源码重写，离屏对比通过；真实窗口、焦点和深色主题验收待执行。

Arc Free 来源采集批次为 `2026-11-03-cookie-consent`。控件提供同意栏、偏好面板和重新打开按钮三种状态；必需类别始终开启，可选类别在面板中使用 Switch 编辑。`Accept all`、`Reject all`、`Save choices`、`Reset` 会通过 `ConsentChanged` 报告动作，并可用 `StorageKey`/`Version` 持久化。

WPF 使用原生 Control 模板和现有 Switch/Button 组件；偏好开关通过 `DraftEnabled` 编辑态绑定，只有保存/接受/拒绝时才写入 `Consent`。偏好类别列表采用最大 300 DIP 的内部 ScrollViewer，避免窄窗口或长列表撑破面板。Position 会分别对齐 bar、panel 和 reopen chip，居中/左侧 chip 靠左，右侧 chip 靠右。状态面现在会根据 `ArcMotionLevel` 对 bar、panel 和 chip 做淡入及轻微缩放进入，打开、关闭和提交路径会安排明确的按钮焦点归还；Arc 的单一 surface 尺寸形变和逐帧 reduced-motion 细节仍待补齐。

## 2026-11-03 复刻审计后的实现

本节取代上文与它冲突的旧说明。此前的实现距 Arc 很远，测试通过并不代表复刻达标，详见 [验证记录](../verification/2026-11-03-replica-batch-audit.md)。

一个表面在条、面板、按钮三形态间用 GROW physical(.46)/SHRINK physical(.38) 变形，内容面入场带位移、透明和模糊。断点：条宽低于 560 堆叠，低于等于 300 时 Customize 另起一行；面板内容宽不足 340 时 Save 独占一行。模板只保留 `PART_Layer`；旧的 `PART_Bar`、`PART_Panel`、`PART_Chip`、`PART_CategoryList` 已移除。未实现 Arc 的 policy 链接与受控 preferencesOpen。

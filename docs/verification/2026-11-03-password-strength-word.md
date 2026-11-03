# PasswordStrength 强度词与强度条修复（2026-11-03）

问题层级：控件内部布局。用户反馈：长标签与强度条重叠。

## 根因

1. 词槽偏窄：`.word` 为 `flex 0 0 4.5rem`，另有 `.meterRow` 的 12 间距（源码确认）。WPF 把 12 边距算进 72 列，词槽实测只剩 60。
2. 旧词不移除：`SetWord` 用子元素的 `IsHitTestVisible` 查找当前词，但 `PART_Word` 为 `IsHitTestVisible="False"`，WPF 会把它强制到子元素，查找恒为空。每次变化都新增一个词，旧词留在原位。

## 修改

- `PART_Word` 固定宽 72，列改为 Auto，边距 12 不再占用词槽。强度条因此比修改前窄 12，与来源一致。
- 用 `_currentWord` 字段记录当前词；重新应用模板时清空。

## 结果

环境：Linux + Wine + Xvfb，改名 Liberation 字体，96 DPI 布局，360 宽。字体与 Segoe UI 不同，文字宽度为近似值。

| 项 | 修改前 | 修改后 |
|---|---|---|
| 词槽宽度 | 60 | 72 |
| 强度条右端 | 288 | 276 |
| “Too short” 宽度 / 与强度条的距离 | 57.59 / 14.41 | 57.59 / 26.41 |
| 依次输入至 Strong 后词槽内容（Full 动效） | Too short、Weak、Fair、Good、Strong 叠放 | 仅 Strong |

PasswordStrengthTests：8 项，7 通过，1 失败。

- 新增 `WordSlotKeepsTheLongestWordClearOfTheMeter`、`OnlyTheCurrentWordRemains`：修改前均失败，修改后均通过。
- `EditorsUseTheBareTemplate` 失败（`ApplyTemplate()` 返回 false）：修改前后相同，未处理。

未运行全量测试（按用户要求只测改动的控件）。

## 未验证与限制

- 修改前的测量中，“Too short” 未在像素上压到强度条（距离 14.41），因此未复现词盖住强度条本身，只复现了词槽偏窄和词叠放。Windows 上 Segoe UI 的实际宽度未测量。
- 强度词文案目前不可配置，长度上限就是 “Too short”。来源的 `messages.levels` 翻译未实现；更长的译文仍可能超出 72 宽词槽。
- 实际窗口：未运行/待验证（需 Windows）。

# ModelPicker

状态：待验收。模型选择胶囊：精选模型、推理强度和按厂商分组的其余模型；下方总览显示上下文、价格和强度。事实见[参考卡](../references/components/ModelPicker.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`PickerModel`（`Id`、`Name`、`Provider`、`Short`、`Blurb`、`Description`、`Context`、`InputPrice`、`OutputPrice`、`Mark`、`Efforts`、`DefaultEffort`）、`PickerProvider`、`ModelEffort`；`Models`、`Providers`、`Featured`、`Value`、`Effort`、`IsOpen`、`ShowSummary`、`IsCompact`、`OpensUpward`、`Note`；事件 `ModelChosen`、`EffortChanged`。

## 复用情况

共用 `Motion/AiKit.cs`（`AiMark`、`AiGlyphs`、`FadeSwap`、`SpringWidthBox`、`TurningChevron`、`AiSpinner`、`CopyAction`、`ChunkFade`）。库不带品牌标记，`Mark` 为空时显示首字母圆片；Gallery 的标记属于样例数据。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 菜单打开 | .12 淡入、.16 缩放；显示结果，参数为近似 | `OnOpen` | 真实输入 model-open；`TheMenuOpens...`（间距 8） | — |
| 高亮滑动 | 一块高亮在 snappy 弹簧上滑；键盘直接跳；文档描述 | `MoveHighlight` | 真实悬停 model-effort | — |
| 子菜单 | 悬停 120ms 打开，移到侧边不关；窄于 640 改分页并带 Back | `Pend`、`ShowSub`、`Page` | 真实输入 sub=Effort/More；`pages=True` | 子菜单出现方式为 WPF适配 |
| 胶囊与总览 | 名称淡入交换，宽度弹簧；总览数值交叉淡入 | `Refresh`、`Summary` | 真实输入 model-high、model-sonnet | — |

## 已知差异

- 强度吸附到最近可用档：`ModelEffortText.Snap`，同距时取较低档；Arc 同距规则未确认（待核对）。
- 价格和描述是样例值。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch11-replication.md)。

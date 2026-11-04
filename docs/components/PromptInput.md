# PromptInput

状态：待验收。AI 提示输入框：自增高输入、`/` 命令与 `@` 引用的原子令牌、附件、工具开关、模型胶囊、提交与停止、字数环和错误行。事实见[参考卡](../references/components/PromptInput.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Text`、`Placeholder`、`MaxLength`（默认 4000）、`LongPasteLength`（默认 2000）、`Commands`、`Mentions`、`AttachActions`、`Tools`、`Models`、`Providers`、`FeaturedModels`、`Model`、`Effort`、`IsGenerating`、`Progress`、`ErrorText`、`ShowDictation`、`IsDictating`、`Attachments`、`Tokens`；方法 `SetPrompt`、`AddFiles`；事件 `Submitted`、`StopRequested`、`AttachRequested`、`AttachmentAdded`、`AttachmentRemoved`、`DictationRequested`。上传、听写和外部来源由宿主实现，控件只反映状态。

## 复用情况

共用 `Motion/AiKit.cs`（`AiMark`、`AiGlyphs`、`FadeSwap`、`SpringWidthBox`、`TurningChevron`、`AiSpinner`、`CopyAction`、`ChunkFade`）。库不带品牌标记，`Mark` 为空时显示首字母圆片；Gallery 的标记属于样例数据。 内嵌 `ModelPicker`（紧凑模式）；令牌对账思路同 `MentionInput`，但绘制层独立。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 列表打开 | .12 淡入、.16 缩放 .97→1；显示结果，缩放参数为近似 | `BuildList` | 真实输入：comp-slash、comp-at；`SlashOpens...` | 弹簧未逐帧对比 |
| 输入框增高 | 约八行后滚动；文档描述 | `_fieldClip`、`MaxHeight` | `TheFieldGrows...`；真实 Shift+Enter 64 高 | — |
| 发送/停止变形 | 箭头上移淡出，方块在 morph 弹簧上缩入；文档描述 | `SendFace` | 真实输入：comp-run、comp-stopped | 曲线未逐帧对比 |
| 回复进度弧 | 44 环随回复填满，结束淡出；文档描述 + 显示几何 | `ReplyRing`、`AiRing` | `TheReplyRing...` | — |
| 附件进入与完成 | 缩入，环填充，勾弹入后交给移除键；文档描述 | `AddChip`、`AttachmentChip` | `ChipsFollowUploads...` | 勾的停留 .7s 为 WPF适配，待核对 |

## 已知差异

- 问候语、建议按钮、线程从中间滑到底部属于 Arc 示例舞台，这里由 Gallery 页面或宿主负责。Gallery 建议按钮未带图标。
- 听写波形未实现：`IsDictating` 只改变麦克风状态，Esc 取消听写走 `DictationRequested`。
- 长粘贴阈值 2000 字符是 WPF 适配值，文档未给数字（待核对）。
- 上传中禁止发送，失败的附件不随消息发送；后者为 WPF 适配。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch11-replication.md)。

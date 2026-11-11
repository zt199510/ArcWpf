# AttachmentTray

状态：本轮用户验收通过（2026-11-11）；专项未测项见验证记录。随消息发送的文件：缩略图预览、上传进度、失败重试、移除。事实见[参考卡](../references/components/AttachmentTray.md)。

行布局参考免费 file-upload（MIT，快照已入库），代码为 WPF 独立重写；芯片布局依据 Arc Pro composer 的公开文档和显示结果，没有使用 Pro 源码。

## 范围与 API

`AttachmentTray`：`Layout`（`List`、`Chips`）、`Items`（`ObservableCollection<PromptAttachment>`）；事件 `Removed`、`RetryRequested`、`PreviewRequested`。调用方负责上传并更新条目的 `Status`、`Progress`、`Error`。`PromptAttachment` 新增 `Preview`；未提供时从 `FilePath` 读取图片，读取失败退回图标。

## 复用情况

复用 `PromptAttachment`、`AiRing`、`SpringHeightBox`、`SpringDriver`、`Announcer`。PromptInput 仍用自己的私有芯片，未改为复用本控件。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 行进出 | 高度弹簧 smooth；源码确认 | `Entry.Enter`、`Leave` | 真实输入 tray-upload、tray-removed；移除后焦点到相邻按钮 | — |
| 进度 | 一根弹簧同时驱动条和百分比；源码确认 | `Sync`、`Apply` | 真实输入 `Uploading 10%` | — |
| 落地 | 读到 100% 换文字，条折叠，对勾出现；源码确认 | `Chrome`、`PopIn` | Windows Gallery 重试至 Uploaded；`FinishedUpload...` | 对勾描线已实现，逐帧差异待核对 |
| 重试 | 回到 0 重新开始，不沿条回退；WPF适配 | `RetryItem`、`Sync` | 真实输入 retry→Done；`FailureShows...` | — |
| 芯片 | 宽度弹簧开合；WPF适配 | `SpringWidthFold` | 截图 tray-chips；滚轮横移 60 | — |

## 已知差异

- 没有拖放投放区：投放区由 FileDropzone 负责，本控件只管已选文件。
- Arc 的状态提示行未显示，同样内容走无障碍播报。
- 旧批次使用 Wine；本轮已做部分 Windows Gallery 目视与鼠标检查，范围及未测项见[验证记录](../verification/2026-11-11-ai-controls-acceptance.md)。

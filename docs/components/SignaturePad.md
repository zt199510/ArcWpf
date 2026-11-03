# SignaturePad

状态：待验收。自动化测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/SignaturePad.md)，验证见[记录](../verification/2026-11-03-batch4-replication.md)。

## 范围与 API

API：`Strokes`、`InkColor` 等属性，`Undo`、`Redo`、`Clear`，事件 `StrokesChanged`、`Exporting`（未处理时弹出保存对话框）。笔迹算法公开在 `SignatureInk`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 绘制后取得焦点 | 2px 轮廓，偏移 2 | `SignaturePad` 焦点环 | 截图 sig-drawn | 无 |
| 清除 | 擦除 inOut KeySpline(.65,0,.35,1) | `Clear` | 截图 sig-wipe、sig-cleared | 中间帧与 Arc 差约 3.82% |
| 回放 | 按笔迹时间重绘 | `PaintReplay` | 未单独对比 | 未逐帧核对 |

## 已知差异

- 触控与压感未在真实设备上运行。

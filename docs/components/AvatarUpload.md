# AvatarUpload

状态：WPF 基础视觉、圆形裁剪工作区、拖拽定位、键盘移动、缩放、按 `OutputSize` 生成裁剪图片和保存/取消流程已完成，已完成构建与 Gallery 启动检查；真实窗口拖放、主题和 DPI 仍待人工验收。

像素对照：2026-11-02 已按线上页面重做，结果与已知差异见 [记录](../verification/2026-11-02-next-controls-pixel.md)；离屏对比已完成，真实窗口未验证。

来源：Arc Pro 页面公开资料；Registry 返回 404，无 Pro 源码复用。当前为独立 WPF 实现，提供圆形头像/首字母回退、悬停相机层、图片选择、拖放、Enter/Space 触发、大小限制、圆形裁剪工作区、拖拽定位、方向键移动、缩放滑块、保存/取消、上传进度环/错误提示和 `FileSelected` 事件。新增 `Hint`、`Accept`、`MaxSize`、`Size`、`IsCropping`、`Zoom`、`CropOffsetX`、`CropOffsetY` 属性。

与 Arc Pro 的差异：WPF 裁剪阶段仍以圆形遮罩和变换预览呈现；保存时会生成 `OutputSize` 的方形 `CroppedImage`，并将 PNG 保持为 PNG、其他格式按 92% JPEG 写入临时文件后通过 `FileSelected` 返回。捏合缩放、上传取消/重试和成功收口动效仍待后续扩展。

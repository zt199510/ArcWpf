# AvatarUpload

状态：WPF 基础视觉、圆形裁剪工作区、拖拽定位、键盘移动、缩放和保存/取消流程已完成，已完成构建与 Gallery 启动检查；真实窗口拖放、主题和 DPI 仍待人工验收。

来源：Arc Pro 页面公开资料；Registry 返回 404，无 Pro 源码复用。当前为独立 WPF 实现，提供圆形头像/首字母回退、悬停相机层、图片选择、拖放、Enter/Space 触发、大小限制、圆形裁剪工作区、拖拽定位、方向键移动、缩放滑块、保存/取消、上传进度环/错误提示和 `FileSelected` 事件。新增 `Hint`、`Accept`、`MaxSize`、`Size`、`IsCropping`、`Zoom`、`CropOffsetX`、`CropOffsetY` 属性。

与 Arc Pro 的差异：WPF 当前保留原始 `ImageSource`，裁剪阶段以圆形遮罩和变换预览呈现，尚未将裁剪结果重新编码为 `outputSize` 的 PNG/JPEG；捏合缩放、上传取消/重试和成功收口动效仍待后续扩展。

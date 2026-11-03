# ConfirmPopover 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/confirm-popover（Arc Pro）。`/r/confirm-popover.json` 2026-11-03 返回 404。
- 证据等级上限为**运行观察**：只用公开预览的 DOM 几何、计算样式、截图和可见行为。未使用 Pro 源码，未反编译页面 JS，Pro 文档未入库。
- 批次：2026-11-03，项目采集标识，非官方版本。用户图片：无。当前页面是否仍同版：未知。
- **许可风险待用户决定**：Arc 许可把 Pro 文档也算作 Pro 源码，并禁止免费分发组件库。本控件依公开预览独立实现，但 API 形状与采集时读到的公开文档有重合。见 `THIRD-PARTY-NOTICES.md`。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | Atlas settings；Remove、Delete 两行及各自确认文案 | 运行观察 | 一致；头像改姓名缩写 |
| 初始值与状态 | 关闭；Log 为 Nothing has changed yet | 运行观察 | 一致 |
| 控件本体尺寸/圆角 | 触发器高 40 圆角 14；卡片 320 宽，圆角 22；Remove 卡高 166.4 | DOM 几何 | 一致 |
| 字体、颜色、边框 | 标题 16/17.6；说明 14/19.6；危险色 oklch(.53 .18 10) | 计算样式 | 一致 |
| 内部对齐锚点 | 内边距 16；操作行高 40，间距 8；卡片右缘贴触发器右缘 | DOM 几何 | 一致 |
| 外部间距 | 演示卡片顶部留 120；页脚距卡片 14 | DOM 几何 | 一致 |

## 状态与动效索引

打开、输入、错误短语、进行中、成功、失败都有截图和 DOM。弹簧值（卡片形变 .42、Cancel 收起 .4、填充 .26）和抖动关键帧是**推测**，只能看到结果，看不到 Arc 实现。

## WPF差异和回归入口

- 卡片放在最外层 AdornerLayer（WPF适配）：不被 ScrollViewer 裁切，范围受窗口约束，与 Arc 一致；不用 Popup。
- 阴影放在独立底板上，避免 Effect 把卡片文字栅格化。
- Gallery 的 Switch 是项目现有的 42×24，Arc 演示是 32×20。
- HoldToConfirm 的按住动作 Arc 这个控件没有，未加入；只借用“填充+反色文字”。Dialog 未使用，因为 Arc 这里是非模态的就地卡片。
- 回归：`tests/ArcWpf.Tests/ConfirmPopoverTests.cs`。

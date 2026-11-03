# LiquidTabBar 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/liquid-tab-bar（Arc Pro）。`/r/*.json` 2026-11-03 返回 404。
- 证据等级上限为**运行观察**：只用公开预览的 DOM 几何、计算样式、截图和可见行为。未使用 Pro 源码，未反编译页面 JS，未阅读 Pro 文档正文（只读了页面元数据的前 12 行）。Pro 文档未冻结入库。
- 批次：2026-11-03，项目采集标识，非官方版本。
- **许可风险待用户决定**：Arc 许可把 Pro 文档也算作 Pro 源码，并禁止免费分发组件库。本控件依公开预览独立实现，API 自行命名，但外观和交互有意相同。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 尺寸 | 条高 48，内边距 4，间隙 2，透镜高 40，图标槽 44 | DOM 几何 | 一致 |
| 宽度 | Home 91，Inbox 96，All 模式整条 392 | DOM 几何 | 公式一致；数值受替身字体影响（387） |
| 内容 | Home、Saved、Inbox（2）、Profile；说明文字 | 运行观察 | 一致 |
| 悬停 | 透镜单侧伸出 6.44 | DOM 几何 | 一致 |

## WPF差异和回归入口

- 透镜是整行第二份副本加裁剪，图标填充、文字反色在通过处发生。
- 自写透镜，未组合 Tabs、Carousel；复用 OwnedMotion、SpringDriver 和主题工具。
- Xvfb 下 `CompositionTarget.Rendering` 帧很稀疏，大步长可能让截图停在动画中途，截图需分段等待；真实窗口帧率未验证。
- 回归：`tests/ArcWpf.Tests/LiquidTabBarTests.cs`。

# Pagination 参考基准

[原页](https://uiarc.dev/components/pagination)、[Markdown 合约](https://uiarc.dev/components/pagination/markdown)、[Registry](https://uiarc.dev/r/pagination.json)、[许可证](https://uiarc.dev/license)、[冻结清单](../../../tests/Baselines/References/2026-10-26-pagination/manifest.json)。

`2026-10-26-pagination` 是项目采集批次，不是上游发行版本。当前快照包含页面、Markdown、Registry JSON、TSX、CSS、共享动效令牌和许可页。来源页面标记该组件为 Free；许可证页面说明 Free 组件采用 MIT，当前只作行为与视觉参考，未直接复制 React、Motion 或 lucide 运行时代码。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | `Pagination`，上一页、最多五个页码、下一页 | Markdown API 与 Registry TSX | Gallery使用12页示例，PageCount默认0 |
| 初始值与状态 | `page=1`、`pageCount=12`；当前页为1 | Markdown Usage、TSX `currentPage` | 已实现，见规格与验证记录 |
| 控件本体尺寸/圆角 | 页码/箭头按钮 `control-height-sm`，圆角 `radius-control` | CSS `.nav button` | 36 DIP，已测量并观察窗口 |
| 字体、颜色、边框 | 小字号、数字等宽；当前页表面、1px border、resting shadow | CSS `.nav button`、`.mark` | 使用 ArcWpf 语义资源 |
| 内部对齐锚点 | 行内 flex，按钮居中；共享 mark 对齐当前页按钮左上角 | CSS、TSX `offsetLeft/offsetTop` | WPF Canvas共享Border标记 |
| 外部间距 | `gap: space-1`；允许换行 | CSS `.nav` | PaginationPanel使用4 DIP间距换行 |
| 展示容器宽度/背景 | 组件自身透明，无固定外壳宽度；窄容器换行 | CSS `.nav`、Markdown Responsive | Gallery 由页面容器控制 |

## 状态与动效索引

- 页码窗口最多五项，当前页尽量处于中间；页码窗口移动时数字像传送带一样按槽位差滑动。
- 共享当前页 mark 在页码按钮之间以 `morph` spring 移动；首次测量前当前按钮自行绘制边框和表面，避免闪烁。
- Reduced motion 直接交换页码和 mark 位置；箭头按下缩放取消。
- 只有五个页码按钮常驻，不随 `pageCount` 生成全部页码；原版使用 ResizeObserver 重新定位 mark。

## WPF 差异和回归入口

- WPF 使用受控 `Page` / `PageCount` 依赖属性与 `PageChanged` 路由事件，不复制 React `onPageChange` 函数或 `aria-current` 属性；AutomationPeer 提供当前页和按钮名称。
- WPF 使用 `Button`/`ControlTemplate` 与共享动效调度器，不引入 `motion`、`lucide-react` 或 React Portal。
- 源码只明确 Tab、Enter、Space；方向键是否扩展为桌面分页器体验列为 WPF 设计选择，必须在规格和测试中单独标注。
- 实际窗口焦点、DPI、窄容器换行和动画观感尚未建立回归基准。


## 实现补充与证据（2026-10-26）

箭头释放CSS依赖补充自同日已冻结文件 `tests/Baselines/References/2026-10-26-dialog/b717584d841b7030.css`，SHA-256 `bf1d670eca7ffcd0770067c76b5a71fd98eff12de4f3658c36a701898c64d619`；--duration-spring=.58s，--ease-spring为0至1的37点线性采样，与现有OwnedMotion spring采样一致。该文件属于补充批次，未覆盖Pagination原始快照。

实际窗口浅色/暗色、鼠标和Enter/空格/Shift+Tab、窄宽换行已观察；来源网页未做同环境逐帧对比。完整证据及剩余边界见[验证记录](../../verification/2026-10-26-pagination.md)。

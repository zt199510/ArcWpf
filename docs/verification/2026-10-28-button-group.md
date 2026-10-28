# ButtonGroup 验证记录（2026-10-28）

按新流程执行：先冻结 `tests/Baselines/References/2026-10-28-button-group`（Registry tsx/css、页面、markdown、motion tokens、license，manifest 含 SHA-256），再写参考卡，后实现。

## 复用与改动范围

- 复用：`ControlSpring`（高亮）、`OwnedMotion`（ease-spring 采样即 Arc `--ease-spring`）、`Menu`（弹出层、键盘、条目动效）。
- `Menu`：新增内部对齐钩子；菜单项样式与模板抽为共享键，Menu 自身行为不变。
- `ActionButton`：BezierEase/CriticalSpringEase 移到 `Motion/ArcEasing.cs` 共享，行为不变。
- 主题：ArcThemeResources 新增 5 个 ButtonGroup 混合色（OKLab 无彩混合按 L 线性计算）。

## 验证

- 构建：Linux `dotnet build ArcWpf.slnx -p:EnableWindowsTargeting=true` 0 错误，无新增警告。
- ButtonGroupTests（9 项：几何、reserve 定宽、交叉淡入启动、content/Off 不动画、方向键、折叠/恢复、chevron 菜单、禁用/Peer、Gallery 页加载）：未运行，WPF 测试需 Windows。
- MenuTests、ActionButtonTests 回归：未运行，同上。
- 实际窗口：未运行。需在 Windows 对照网页预览检查：高亮段间滑动与出现方式、分隔线淡出、按压下沉、Share→Copied 交叉淡入、窄容器折叠、chevron 旋转与菜单对齐、明暗主题与 Solid 配色。

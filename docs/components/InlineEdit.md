# InlineEdit 组件规格

状态：已按冻结源码重做（2026-10-31），Linux 环境仅完成编译；InlineEditTests 6 项未运行，实际窗口待人工验收。来源冻结于 `tests/Baselines/References/2026-10-29-inline-edit/`（2026-10-31 复核与线上 registry 一致）。

## API

`InlineEdit : Control`，编辑宿主为原生 TextBox（保留 IME、撤销、选区）：

- `Value`（双向）：已保存值；非编辑、非 saving 时外部新值以文字动效替换显示。保存成功后才写回 `Value`（乐观显示由内部 shown 承担）。
- `Label`、`Placeholder`、`Variant`（`Title` 22px/500/行高 28，`Body` 14px/400/行高 21、TextSecondary）、`Multiline`。
- `SaveAsync`：返回 `Task`；faulted/cancelled 视为失败并回滚。`Validate`：返回错误文本阻止保存。
- `ReadOnly`：WPF 扩展（源码无），只关闭进入编辑。
- `IsEditing`、`Phase`、`Error`：只读；`Saved`、`SaveFailed`、`EditCancelled` 事件。
- `InkBrush` 等主题输入属性：样式以 DynamicResource 提供，供内部可动画画刷在主题切换时重算。

## 布局契约（InlineEditBox）

只有展示文字决定盒子尺寸：编辑框与展示文字同一矩形；frame 在其后，单行时宽度独立弹簧，多行时盒子高度弹簧；动作槽贴在 frame 右缘外 6px（多行 8px）。行尾预留 66px（多行 38px），进入编辑不重排。根节点左移 9px（padding 8 + 边框 1）使文字与周边内容对齐。单行编辑时展示文字 min-width 5em、右侧额外 16px。

## 范围矩阵

| 交付项 | 范围 | 实现状态 | 证据 / 剩余差异 |
|---|---|---|---|
| 展示/编辑同度量原位切换，frame 贴合文字（非整行） | 必做 | 已实现 | 旧版 frame 撑满整行 |
| 点击处落光标；键盘激活全选；铅笔点击光标在末尾 | 必做 | 已实现 | 旧版一律全选（曾记暂缓） |
| Enter 保存、Shift+Enter 换行（多行）、Escape 取消、离开组件保存、切窗口不保存 | 必做 | 已实现 | WPF 额外：点击窗口空白处视为离开 |
| 保存：乐观显示、文字 .5 透明度、spinner；成功画勾 1.8s；失败回滚（文字下落/旧值上方落入）、danger 闪烁 1.4s 后 .48s 淡出 | 必做 | 已实现 | 旧版失败显示异常消息、无回滚动效 |
| 失败消息 “Couldn’t save “X”, so the last saved … is back. Try again”，重试回到编辑并恢复失败文本 | 必做 | 已实现 | 旧版缺失 |
| 验证错误：danger 边框 + 3px 24% danger 环，消息行 | 必做 | 已实现 | |
| 宽度 morph（.42）、输入时 typing 弹簧（.16）、多行高度 smooth（.4）、字体/容器变化瞬跟 | 必做 | 已实现 | 旧版无 |
| 文字层 popLayout 交叉（.3em、blur 4/2） | 必做 | 已实现 | 旧版无 |
| 动作槽：pencil（仅 hover/键盘焦点显示）/ Check+X / spinner / 画勾 / alert，scale .6 snappy + blur 交叉 | 必做 | 已实现 | 旧版用字符 ✓ × ! |
| 消息行高度从 0 弹簧展开，文字上浮淡入 | 必做 | 已实现 | |
| Save/Cancel hover、按压 .96 | 必做 | 已实现 | color-mix 以 sRGB 近似 |
| 编辑态焦点环 | 不适用 | — | 来源 --focus-ring 为 transparent；旧版额外阴影已删除 |
| `as` 语义标签 | 不适用 | — | WPF 无 HTML 标题语义 |
| Reduced：文字/槽位仅淡入淡出、尺寸瞬跳、spinner 静止；Off：直接终态 | 必做 | 已实现 | |

## 已知 WPF 差异

- TextBox 是否采用 `TextBlock.LineHeight` 待在 Windows 上测量；若忽略，编辑态文字可能与展示文字有亚像素纵向偏移。
- 编辑框沿用 Input 的 -2 DIP TextBoxView 内缩补偿（同一宿主类型），起点差待实测。
- 弹簧打断不保留速度（OwnedMotion 从当前值重新起算）。

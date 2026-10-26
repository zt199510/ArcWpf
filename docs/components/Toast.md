# Toast：M2规格

状态：实现及程序化验证完成，待用户实际窗口验收。见[验证记录](../verification/2026-10-26-toast.md)。

## 公开契约

Toast继承ContentControl，Title/Description/IsOpen/Duration及只读IsPresented；Close(reason)触发Closing/Closed。默认Duration=4500，0或负数关闭自动计时。ToastHost继承ContentControl，Items为ObservableCollection<Toast>，Show(title,description,duration)加入队列，DismissAll清空。ToastHost位置由使用方决定；Gallery将其放在页面右下区域。

IsOpen双向绑定；关闭来源区分Programmatic/Timeout/CloseButton/Swipe/Unloaded/Replaced。关闭按钮不抢焦点，默认模板仅呈现成功提示；Content保留原生扩展空间但首期不提供动作槽。Host卸载时关闭并清理计时器、时钟和事件。

## 视觉与动效

默认416宽、最大416，26圆角、边框1、16/14/14/14内边距；30px成功图标、12列间距、标题14/500单行省略、说明14/19.6换行、32关闭按钮。使用Surface、Foreground、TextSecondary、Border、Floating及新增ToastSuccess/ToastSuccessBorder/ToastSuccessGlyph资源。

Full入场.4 spring，y16/scale.96到终态、透明度入场；退出.16秒下沉8/scale.98淡出。Reduced只.12秒淡化，Off立即。自动关闭不因悬停暂停。WPF首期保留完成后关闭清理，暂不实现原版拖拽/投掷手势。

## 验收矩阵

| 交付项 | 范围 | 状态 | 证据/剩余差异 |
|---|---|---|---|
| API/Host/队列/绑定 | 必做 | 已实现 | 4项Toast专项及全量回归 |
| 外形/图标/无描述 | 必做 | 已实现 | 布局测试；实际视觉待验收 |
| 自动关闭/手动关闭/清理 | 必做 | 已实现 | Dispatcher测试；实际窗口待验收 |
| 主题/自动化/Gallery | 必做 | 已实现 | 构造与主题测试；读屏待验收 |
| 滑动/动作按钮/类型变体 | 暂缓 | 未实现 | 后续ToastStack/手势需求 |
| 实际窗口/DPI/截图/性能 | 延期 | 未运行 | 不以程序化测试替代 |

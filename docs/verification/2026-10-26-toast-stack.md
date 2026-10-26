# ToastStack 验证记录（2026-10-26）

## 已完成

- Arc ToastStack Registry、CSS、动效依赖和许可快照已冻结到 `tests/Baselines/References/2026-10-26-toast-stack`。
- 实现原生 `ToastStack`、`ToastStackItem` 和 `ToastStackPanel`，支持队列、ID更新、上限、折叠堆叠、展开暂停、五种类型、动作按钮和批量关闭。
- Gallery 和 Consumer 示例已接入。
- 新增 8 项 ToastStack 程序化测试。

## 验证结果

- `dotnet build tests/ArcWpf.Tests/ArcWpf.Tests.csproj -c Debug --no-restore`：通过；产品代码无错误。
- ToastStack 专项：8 项通过，结果：`tests/ArcWpf.Tests/TestResults/toast-stack.trx`。全量回归：168 项通过，结果：`tests/ArcWpf.Tests/TestResults/toast-stack-full.trx`。
- 实际窗口交互、截图、DPI、右滑手势、读屏和性能：用户此前明确暂不做，保持未测。


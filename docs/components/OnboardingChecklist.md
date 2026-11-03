# OnboardingChecklist

状态：待验收。自动化测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/OnboardingChecklist.md)，验证见[记录](../verification/2026-11-03-batch4-replication.md)。

## 范围与 API

API：`Steps`、`DefaultCompleted`、`Title`、`DoneTitle`、`IsHidden`，方法 `Check`、`Uncheck`、`Reset`，事件 `CompletedChanged`。`OnboardingStep.Action` 返回 Task，抛异常时步骤保持打开并显示错误。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 勾选 | 划线、环形进度、计数滚动；720ms 后折叠 | `Check`、`Settle` | 测试与截图 check-done | Wine 下动画明显变慢，只核对终态 |
| 完成组展开 | 折叠弹簧 | `Fold` | 截图 check-group | 无 |
| 隐藏 | .34 折叠 | `IsHidden` | 截图 check-hidden | 无 |

## 已知差异

- `RollingNumber` 的位宽弹簧未实现，只在 10 步及以上进位时可见。

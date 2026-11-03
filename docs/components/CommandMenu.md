# CommandMenu

状态：待验收。自动化测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/CommandMenu.md)，验证见[记录](../verification/2026-11-03-batch4-replication.md)。

## 范围与 API

API：`Items`、`Query`、`Placeholder`、`Label`、`ShowsClose`、`AutoFocusSearch`，方法 `FocusSearch`，事件 `ItemSelected`、`Closed`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 输入出现清除按钮 | opacity、scale .6→1 snappy、blur 2 | `SetClear` | 测试 `QueryFiltersAndShowsClear` | 无 |
| 清空 | popLayout，opacity、scale .6、blur，instant standard | `SetClear` | 未截帧 | 未逐帧核对 |
| 结果变化 | 框高 smooth，行淡入 | `Rebuild` | 截图 cmd-in、cmd-empty | 列表行的 layout 位移未逐帧核对 |

## 已知差异

- `⌘`、`⇧` 在 Wine 替身字体下显示为方块，Windows 上依赖系统字体回退，待真实窗口确认。
- 滚动条是 WPF 悬浮样式，不是浏览器原生滚动条。

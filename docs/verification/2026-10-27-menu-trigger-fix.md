# Menu 触发器无法打开修复（2026-10-27）

## 现象

用户反馈：点击 Menu 触发器不弹出菜单项；ButtonGroup 的 chevron 菜单同样不弹出。

## 根因（运行复现确认）

- `Menu.cs` 位于 `namespace ArcWpf.Controls`，其中 `GetTemplateChild("PART_Trigger") as Button` 的 `Button` 解析为项目自己的 `ArcWpf.Controls.Button`，而模板里的触发器是 WPF 原生 `Button`，转换结果恒为 null。
- `_trigger` 为 null 时 Click 处理与 `PlacementTarget` 都没有挂上，点击触发器什么也不发生。初始提交就存在；原测试只直接设置 `IsOpen`，从未经过真实点击路径，因此没有发现。
- 同一处修复后发现第二个问题：`PlacePopup` 的偏移相对触发器左上角，y 写成 6 使菜单盖在触发器上；改为 `targetSize.Height + 6`（来源 sideOffset 6）。
- TreeView `FindButton` 有同类遮蔽（行按钮为 WPF Button），键盘操作后的焦点恢复一直找不到按钮，一并改为完整类型名。

## 验证

- 构建：`dotnet build ArcWpf.slnx -p:EnableWindowsTargeting=true` 0 错误。
- 实际窗口（Wine 9 + Xvfb 运行自包含 Gallery，非 Windows 原生）：
  - 修复前：点击 Actions 只获得焦点，无 Popup 窗口；插桩确认 `_trigger` 分支从未执行。
  - 修复后：Menu 页 Actions 点击弹出 Rename/Duplicate/Delete，位于触发器下方 6、右边缘对齐；点击 Duplicate 显示“已选择：Duplicate”并关闭。
  - ButtonGroup 页 chevron 点击弹出 Pin to sidebar/Delete，chevron 旋转、高亮停在 chevron、菜单与组外边对齐；ArrowDown 移动选中项正常。
  - 未完成：ButtonGroup 菜单项调用后 Wine 在显示 ToolTip 窗口时崩溃（X RENDER BadMatch，任何带 ToolTip 的元素悬停都会触发，与本修复无关），该步骤以 Menu 页相同代码路径的结果为准。Wine 下圆角外黑角是分层窗口透明不受支持，非控件问题。
- 测试：`PopupRemainsPresentedAfterTriggerClick` 改为对 `PART_Trigger` 触发真实 Click，断言打开、`PlacementTarget`、Popup 位于触发器下方及再次点击关闭；ButtonGroup `MenuAddsChevronSegmentAtTheEnd` 改为点击 chevron 打开。测试未运行（WPF 测试需 Windows）。
- 待 Windows 验收：原生窗口下点击、外部点击关闭、翻转到上方与 DPI 缩放下的偏移。

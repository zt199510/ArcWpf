# BottomSheet 组件规格

状态：Arc Free Registry 已冻结；WPF 已实现来源默认档位、位置/遮罩联动、抓手键盘与拖拽、内容滚动和 Drawer 模态关闭链。鼠标/触摸物理操作、多 DPI 与逐帧外观待实际验收。来源见[参考卡](../references/components/BottomSheet.md)。

`BottomSheet : Drawer` 继续使用 `DrawerHost`、`Show`/`Close`、`IsOpen` 双向绑定、遮罩与 Escape 关闭、关闭命令、焦点范围和归还。新增 `Detents : ObservableCollection<double>`，默认 `[.45,.92]`；正有限值排序去重，超过 1 按 1 处理，空集合回退 .92。`InitialDetent` 默认 0，`CurrentDetent` 双向绑定，`MoveToDetent(index)` 与 `CurrentDetentChanged` 用于程序控制；`IsExpanded` 为只读状态。旧 `SnapHeight` 保留为 WPF 兼容属性，默认 NaN；设置为非负有限 DIP 高度时改为单档固定高度，并受宿主高度约束。

模板部件：`PART_Surface` 为最大 576 DIP、宿主宽度内居中的底部表面；`PART_Header` 含 56×20 DIP 抓手、标题/说明和 32 DIP 关闭按钮；`PART_Body` 是有限高度的 ScrollViewer，最低档禁用滚动，最高档启用。来源 CSS 的 18 DIP 圆角、20 DIP 横向内边距与语义画刷已映射。桌面无浏览器 safe-area，未引入底部 160px 延伸。

Full 模式用保速弹簧从底部外 40 DIP 进入当前档位；拖动跟随指针，上拉超出最大档使用来源 120 DIP 橡皮筋，释放按最近 80ms 速度与 .2s 投影选档，超过 320 DIP/s 至少移动一档；遮罩强度随位置变化。关闭由 Drawer 统一完成，形变落地后归还焦点。Reduced 档位即时落点，表面淡入/淡出；Off 即时落点且不保留渲染订阅。宿主高度改变时重新计算档位，卸载、换模板和关闭停止自有动画。

抓手 Up/Down/Home/End 与 Enter/Space 切档；在表面/内容上向下读键或在折叠内容内向下滚轮先展开。标题栏支持鼠标和触摸拖动；折叠内容支持触摸拖动，展开后可滚动，顶端下拉可回到拖动。物理触摸捕获、焦点进入遮挡内容、屏幕缩放和视觉节奏仍需窗口观察；程序化事件和测量不能替代这些验收。

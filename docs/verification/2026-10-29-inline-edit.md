# InlineEdit 验证记录（2026-10-29）

## 实现

- 新增 InlineEdit 控件、主题、Gallery 页面和来源冻结批次。
- 覆盖展示/编辑、提交/取消、验证、异步保存、失败回滚、禁用/只读和多行模式。

## 程序化结果

- Controls 构建：成功，0 警告/错误。
- Gallery 构建：成功，4 个既有未相关警告。
- InlineEdit 专项：3/3 通过。

## 待运行



- 实际窗口的原位尺寸、中文 IME、焦点归还、长文案、多行高度、明暗主题和 Reduced/Off。

未使用 computer-use；程序化结果与真实窗口验收分开记录。



## 2026-10-31 Arc 对齐修正

- 根因：旧模板 idle 直接显示边框，hover 不呈现 muted surface；动作槽覆盖文本，saving/saved 没有来源对应的状态槽。
- 修复：透明 idle frame、hover muted surface、66px 右侧预留、saving spinner、saved/failed 状态图标和失败边框。
- InlineEdit 专项复验：3/3 通过。
- 实际窗口：仍待人工复核原位尺寸、动作槽位置、长文案和多行高度。


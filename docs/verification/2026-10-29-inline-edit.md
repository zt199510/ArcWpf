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

## 2026-10-31 按冻结源码重做

- 复核：冻结源码与线上 `https://uiarc.dev/r/inline-edit.json` 逐文件一致。
- 旧实现与源码的差异：frame 撑满整行而非贴合文字；标题字号 20（源 text-xl=22）且行高未取整；进入编辑一律全选（源码按点击位置落光标）；无 pencil；动作按钮放在 frame 内部且用字符 ✓ ×；spinner 不旋转；saved/failed 用字符；失败只显示异常文本，无回滚动效、闪烁和 “Try again”；编辑态额外阴影（源 focus-ring 为 transparent）；宽度/高度/文字/消息动效均缺失却被记为已实现。
- 修复：新增 InlineEditBox/InlineEditReveal 布局部件，按源码状态机重写，详见 [组件规格](../components/InlineEdit.md)。Gallery 改为源站 Project settings 面板（Atlas web app / 描述 / Fail the next save）。
- 新增测试：FrameHugsTextAndFieldCoversDisplay、FailedSaveRollsBackAndOffersRetry、ExternalValueIsIgnoredWhileEditing；保留原 3 项。

### 程序化结果

- Linux（EnableWindowsTargeting）构建 ArcWpf.slnx：成功，0 错误；警告均为既有 CS0067。
- 专项测试：**未运行**（同 HoldToConfirm 记录，wine 下 WPF 文本排版 FailFast）。
- 实际窗口：未运行，待在 Windows 上验收原位尺寸、光标落点、中文 IME、宽度 morph、多行高度、失败闪烁与重试、明暗主题和 Reduced/Off。

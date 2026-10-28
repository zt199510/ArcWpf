# NumberInput 组件规格

- 状态：首版已实现，程序化验证通过，实际窗口视觉验收待做。
- 来源：[Arc Number field](https://uiarc.dev/components/number-field/markdown)。
- 范围：受边界约束的数值编辑、步进按钮、方向键、Enter/Escape、前后缀和说明文本。

## API

`NumberInput` 公开 `Value`、`Minimum`、`Maximum`、`Step`、`Label`、`Description`、`Prefix` 和 `Suffix`。`Value` 为双向绑定 DP；输入值在提交、按钮和键盘操作时夹取到边界。

## 外形对齐\n\n- 中号默认宽度 196 DIP；外壳使用 1 DIP 边框、控制圆角和 3 DIP 内缩。\n- 左右步进按钮各 34 DIP，数值、前缀和后缀作为同一居中组布局；后缀不能放到右侧步进按钮外。\n- 标签行与外壳保持同宽，说明文本位于外壳下方。\n\n## 键盘与状态

上下方向键按 `Step` 调整，Shift/ PageUp/PageDown 使用十倍步长，Home/End 跳到边界，Enter 提交输入，Escape 恢复当前值。到达边界时按钮禁用；禁用控件不接受编辑。

首版暂缓拖拽标签、极限回弹、数字滚轮动画和 limitHint 文案。




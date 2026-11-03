# TextColoring 文本框分段着色规格

状态：已实现，离屏与程序化验证通过；真实窗口未运行，Windows 上的 Segoe UI Emoji 未验证。证据见 [验证记录](../verification/2026-11-02-text-coloring.md)。

`TextColoring` 是附加到 `TextBox` 及其派生控件（`Input`、`Textarea`、`MentionInput` 等）的能力，不是独立控件。Arc 没有对应组件；这里的目标是让 WPF 文本框也能像网页 `textarea` 那样显示彩色表情，并支持按范围给文字着色。

## API

- `TextSegment(int Start, int Length, Brush Foreground)`：一段 UTF-16 字符范围及其画刷。
- `TextColoring.Segmenter`（附加属性，`Func<string, IEnumerable<TextSegment>>`）：每次文本变化时在 UI 线程调用。范围重叠时后者生效，越界自动截断。未覆盖的文字使用控件的 `Foreground`。
- `TextColoring.ColorEmoji`（附加属性，`bool`）：用彩色层绘制表情。
- `TextColoring.EmojiFontFamily`（附加属性，默认 Segoe UI Emoji）：表情字体。
- `TextColoring.Refresh(box)`：不改文本时重新分段，例如切换主题后刷新分段画刷。

```xml
<arc:Textarea x:Name="Notes" arc:TextColoring.ColorEmoji="True" />
```

```csharp
TextColoring.SetSegmenter(Notes, text => Regex.Matches(text, @"#\w+").Select(m => new TextSegment(m.Index, m.Length, tagBrush)));
```

## 机制

保留原生 TextBox 负责编辑、光标、选区、撤销和输入法，只替换字形绘制：

1. 把内部文本视图的 `Opacity` 设为 0。不改 `Foreground`，所以样式触发器和绑定继续有效。光标和选区由独立的装饰元素绘制，不受影响。
2. 在 TextBox 自身滚动呈现器的 AdornerLayer 中加一层，只绘制可见行。这一层随视口裁剪，也不会盖住窗口内覆盖在 TextBox 上方的 Drawer、Dialog，并且 Popup 内同样可用。
3. 每段文字用 `GetRectFromCharacterIndex` 取原生位置后绘制。表情按字素簇切分，在原生测量的宽度内居中绘制，放不下时等比缩小。

## 回退与限制

- 文本含从右到左字符，或 `FlowDirection` 为从右到左时，暂时恢复原生绘制，分段和彩色都不生效，避免画错位置。
- 模板没有 `PART_ContentHost` 滚动容器时保持原生绘制。
- 原生绘制在文本视图内的内容会被隐藏：输入法合成下划线和拼写检查波浪线不显示。合成中的文字本身仍会绘制。
- 光标位置和表情之间的间距取决于原生布局。原生把组合序列拆成多个字形时，表情两侧会有空隙，光标也可能停在序列内部。
- 彩色字体的限制与 [EmojiPicker](EmojiPicker.md) 相同。字体不含彩色层时，表情按普通文字绘制。
- `Segmenter` 每次文本变化都会运行，超长文本需要调用方控制开销。绘制本身只处理可见行。

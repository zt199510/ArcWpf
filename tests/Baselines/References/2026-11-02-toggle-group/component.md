# Toggle group

> A row of icon or text toggles, single or multiple: one pressed fill glides between options, or each fades in on its own and neighbours join, as in a formatting toolbar.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/toggle-group
- Markdown: https://uiarc.dev/components/toggle-group/markdown
- Registry item: https://uiarc.dev/r/toggle-group.json
- Source file: `registry/components/toggle-group/toggle-group.tsx`
- Dependencies: motion
- Keywords: input, toggle, toolbar, new, toggle group, toggle buttons, formatting, bold italic underline, text alignment, icon buttons, pressed, multi select, button group

## When to use

- Formatting toolbars: bold, italic and underline as multiple, alignment as single.
- Compact view options on a canvas, map or editor, such as grid or list, or which layers to show.
- Any short row of icon toggles that apply immediately.

## When not to use

- Use segmented-control for switching between views, ranges or tabs of content.
- Use checkbox or switch for settings in a form that are saved together.
- Use chip-group for filters with long text labels or many options that wrap.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/toggle-group
pnpm dlx shadcn@latest add @uiarc/toggle-group
yarn dlx shadcn@latest add @uiarc/toggle-group
bunx --bun shadcn@latest add @uiarc/toggle-group
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/toggle-group.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/toggle-group/toggle-group.tsx`

   The source is in the registry item: https://uiarc.dev/r/toggle-group.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ToggleGroup, ToggleGroupItem } from "@/components/arc/toggle-group/toggle-group";
import { AlignCenter, AlignLeft, AlignRight, Bold, Italic, Underline } from "lucide-react";

export function FormatBar() {
  const [marks, setMarks] = useState<string[]>([]);
  const [align, setAlign] = useState("left");
  return (
    <div style={{ display: "flex", gap: 8 }}>
      <ToggleGroup type="multiple" label="Text style" value={marks} onValueChange={setMarks}>
        <ToggleGroupItem value="bold" label="Bold"><Bold /></ToggleGroupItem>
        <ToggleGroupItem value="italic" label="Italic"><Italic /></ToggleGroupItem>
        <ToggleGroupItem value="underline" label="Underline"><Underline /></ToggleGroupItem>
      </ToggleGroup>
      <ToggleGroup label="Alignment" value={align} onValueChange={setAlign}>
        <ToggleGroupItem value="left" label="Align left"><AlignLeft /></ToggleGroupItem>
        <ToggleGroupItem value="center" label="Align center"><AlignCenter /></ToggleGroupItem>
        <ToggleGroupItem value="right" label="Align right"><AlignRight /></ToggleGroupItem>
      </ToggleGroup>
    </div>
  );
}
```

## API reference

### ToggleGroup

A row of toggle buttons on a flat bordered strip. In single mode one tinted fill glides from option to option; in multiple mode each option's fill fades and settles in place, and neighbours that are both on join into one shape. Also exported as the default export.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `type` | `"single" \| "multiple"` | `"single"` | single keeps at most one option pressed, like a radio group; choosing the pressed option again keeps it. multiple lets each option toggle on its own. |
| `value` | `string \| null (single) \| readonly string[] (multiple)` | – | Pressed value or values, for a controlled group. |
| `defaultValue` | `string \| null (single) \| readonly string[] (multiple)` | – | Starting value for an uncontrolled group. |
| `onValueChange` | `(value: string) => void (single) \| (value: string[]) => void (multiple)` | – | Called with the new value or the new list of values. |
| `label` (required) | `string` | – | Accessible name of the group, such as Text style or Alignment. |
| `size` | `"sm" \| "md"` | `"md"` | Items 28 or 36px tall; the strip is 36 or 44px. |
| `disabled` | `boolean` | `false` | Disables every item. |
| `children` (required) | `ReactNode` | – | ToggleGroupItem elements, as direct children so neighbouring fills can join. |
| `className` | `string` | – | Class on the group element. |

### ToggleGroupItem

One toggle. Holds an icon, text or both; icons are sized to 16px (14px at sm). Also available as ToggleGroup.Item.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `string` | – | Value this item adds or selects. |
| `label` | `string` | – | Accessible name and native tooltip. Required when the item shows only an icon. |
| `disabled` | `boolean` | – | Disables this item and removes it from arrow key movement. |
| `children` (required) | `ReactNode` | – | Icon, text or both. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves into the group on one item (the last focused, else the first pressed, else the first) and out again. |
| Arrow keys | Move focus to the previous or next item, wrapping at the ends. Focus moves without pressing, as in a toolbar. |
| Home / End | Focus the first or last item. |
| Space / Enter | Toggle the focused item, or select it in single mode. |

## Accessibility

- Single mode renders role="radiogroup" with role="radio" items and aria-checked; multiple mode renders role="group" with aria-pressed buttons. Both are labelled by label.
- Roving tabindex: only one item is a tab stop, so a toolbar with several groups stays quick to cross with Tab.
- Icon only items take their accessible name and tooltip from label. Pressed state is shown by fill and text contrast, and exposed through aria-checked or aria-pressed.
- The fills are aria-hidden decoration.

## Motion

- Single: one fill with a shared layoutId moves to the chosen item on the morph spring, scoped per group by a LayoutGroup; its radius is scale corrected so it keeps its shape between items of different widths.
- Multiple: every item owns a fill that fades in over 180ms while it settles from 86% scale on the spring curve. When two neighbours are on, the fills close the gap and square off their inner corners, so a run of pressed items reads as one shape.
- Pressing an item gives its icon and label a small 92% press and spring back; the item box never scales, so neighbours never shift.
- Reduced motion moves the single fill instantly and replaces fades and presses with instant color changes.

## Responsive behavior

- The strip stays on one line and sizes to its items; place groups in a wrapping flex row so they stack at 320px.
- Items are at least 36px square at md; use md on touch screens and sm in dense desktop toolbars.

## Performance

- Single mode runs one Motion layout animation per change; multiple mode is pure CSS transitions on opacity, transform and inset.
- Item order for the roving tab stop is read from the DOM once per render of the group.

## Notes for AI

- Choose ToggleGroup for tool-like options that act on something else, often icon only: text formatting, view options, filters on a canvas. Choose segmented-control for a tab-like choice between views or ranges.
- Use type="multiple" for independent on/off options (bold, italic) and the default single type for mutually exclusive ones (alignment).
- Give every icon only item a label. Keep items as direct children; wrapping each in another element stops neighbouring fills from joining.
- Place several groups side by side with an 8px gap to build a toolbar; each group stays one tab stop.

## Related

- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.
- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Chip group](https://uiarc.dev/components/chip-group/markdown): Filter by a few facets with chips that morph as you pick them.
- [Rich text editor](https://uiarc.dev/components/rich-text-editor/markdown): A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.

## Guidance for AI tools

Toggle group: A row of icon or text toggles, single or multiple: one pressed fill glides between options, or each fades in on its own and neighbours join, as in a formatting toolbar. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

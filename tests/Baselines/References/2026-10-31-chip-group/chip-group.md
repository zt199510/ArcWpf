# Chip group

> Filter by a few facets with chips that morph as you pick them.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/chip-group
- Markdown: https://uiarc.dev/components/chip-group/markdown
- Registry item: https://uiarc.dev/r/chip-group.json
- Source file: `registry/components/chip-group/chip-group.tsx`
- Dependencies: motion, lucide-react
- Keywords: chips, filter, toggle, react chip group, filter chips, toggle chips, tag filter, selectable chips, chip select

## When to use

- Facet filters people toggle often, like topics or categories.
- Single clearable choices shown as chips, via multiple={false}.
- Long option sets folded behind a +N more chip with maxVisible.

## When not to use

- Use multi-select when space is tight.
- Use checkbox in forms.
- Use segmented-control for one choice among a few views.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/chip-group
pnpm dlx shadcn@latest add @uiarc/chip-group
yarn dlx shadcn@latest add @uiarc/chip-group
bunx --bun shadcn@latest add @uiarc/chip-group
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/chip-group.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/chip-group/chip-group.tsx`

   The source is in the registry item: https://uiarc.dev/r/chip-group.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ChipGroup } from "@/components/arc/chip-group/chip-group";

export function TopicFilter() {
  const [topics, setTopics] = useState<string[]>([]);
  return (
    <ChipGroup
      label="Topics"
      value={topics}
      onValueChange={setTopics}
      maxVisible={4}
      options={["Design", "Motion", "Code", "Research", "Writing"].map((t) => ({ value: t.toLowerCase(), label: t }))}
    />
  );
}
```

## API reference

### ChipGroup

Toggleable filter chips that morph when selected and fold long sets behind a +N more chip.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `options` (required) | `{ value: string; label: string }[]` | – | Chips in order. |
| `value` (required) | `string[]` | – | Selected values. |
| `onValueChange` (required) | `(value: string[]) => void` | – | Called with the new selection, in option order. |
| `label` (required) | `string` | – | Accessible name of the group, such as "Topics". |
| `multiple` | `boolean` | `true` | Allow several chips. In single mode the selected chip can still be cleared. |
| `maxVisible` | `number` | `Infinity` | Chips shown before the rest fold. Selected chips always stay in view. |
| `className` | `string` | – | Added to the group. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Arrow keys | Move focus between chips and the more chip, wrapping. |
| Home / End | Focus the first or last chip. |
| Enter / Space | Toggles the focused chip or opens the overflow. |

## Accessibility

- The group has role="group" and aria-label; each chip is a button with aria-pressed.
- Roving tabindex keeps one chip in the tab order; the more chip uses aria-expanded.
- Check icons and surfaces are aria-hidden.

## Motion

- Selecting grows a check in, slides the label over, and springs the chip edge while neighbours glide to new places, even across lines. Revealed chips stagger in and the group height morphs.
- Reduced motion replaces the morphs with short fades.

## Responsive behavior

- Chips wrap onto new lines and the frame height follows on a spring.
- Selected chips always stay visible, even beyond maxVisible, so folding never hides active filters.

## Performance

- Every chip has layout position animation and its own ResizeObserver; keep sets to a few dozen and fold the rest.
- Revealed chips stagger in, capped at 0.3s total.

## Notes for AI

- Use for facet filters people toggle often. Use multi-select when space is tight and checkbox in forms.
- Always controlled. Set multiple={false} for a single, clearable choice.
- Exported as both named and default.

## Related

- [Multi-select](https://uiarc.dev/components/multi-select/markdown): Select several values while keeping the field readable.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Tag input](https://uiarc.dev/components/tag-input/markdown): Turn short text values into removable tags.

## Also in selects

- [Select](https://uiarc.dev/components/select/markdown): A compact choice field with a keyboard friendly menu.
- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Morph select](https://uiarc.dev/components/morph-select/markdown): A select whose trigger grows into the list, with a gliding highlight and type-ahead.

## Guidance for AI tools

Chip group: Filter by a few facets with chips that morph as you pick them. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

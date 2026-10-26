# Select

> A compact choice field with a keyboard friendly menu.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/select
- Markdown: https://uiarc.dev/components/select/markdown
- Registry item: https://uiarc.dev/r/select.json
- Source file: `registry/components/select/select.tsx`
- Dependencies: @radix-ui/react-select, motion, lucide-react
- Keywords: field, form, react select, dropdown select, radix select, animated select, select menu, form select field

## When to use

- A short fixed list where typing is not needed, such as region or sort order.
- Form fields that should submit natively through Radix's hidden select via name.

## When not to use

- Use combobox for long or searchable lists.
- Use multi-select when several values can be chosen.
- Use segmented-control for two to four choices that should stay visible.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/select
pnpm dlx shadcn@latest add @uiarc/select
yarn dlx shadcn@latest add @uiarc/select
bunx --bun shadcn@latest add @uiarc/select
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/select.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-select motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/select/select.tsx`

   The source is in the registry item: https://uiarc.dev/r/select.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Select } from "@/components/arc/select/select";

export function RegionSelect() {
  const [region, setRegion] = useState("eu");
  return (
    <Select
      label="Region"
      value={region}
      onValueChange={setRegion}
      options={[
        { value: "us", label: "United States" },
        { value: "eu", label: "Europe" },
        { value: "ap", label: "Asia Pacific", disabled: true },
      ]}
    />
  );
}
```

## API reference

### Select

A labelled Radix select whose shown value rolls in the direction of the list.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label for the trigger. |
| `options` (required) | `{ value: string; label: string; disabled?: boolean }[]` | – | Items in list order. |
| `placeholder` | `string` | `"Select an option"` | Shown when no value is selected. |
| `description` | `string` | – | Helper copy under the trigger, linked through aria-describedby. |
| `id` | `string` | – | Trigger id. Generated when omitted. |
| `className` | `string` | – | Added to the trigger. |
| `...props` | `Omit<SelectPrimitive.SelectProps, "children">` | – | Radix Select root props: value, defaultValue, onValueChange, open, onOpenChange, name, required, disabled. ref goes to the trigger button. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space / ArrowDown | Opens the list from the trigger. |
| ArrowUp / ArrowDown | Moves between enabled items. |
| Enter / Space | Selects the highlighted item and closes. |
| Escape | Closes without changing the value. |
| Type a letter | Jumps to the next item starting with it (Radix typeahead). |

## Accessibility

- Built on Radix Select, so the trigger, listbox, and options get the right roles and focus handling.
- The real value renders in a visually hidden Radix Value; the animated copy is aria-hidden.
- Label is linked with htmlFor, and description through aria-describedby.

## Motion

- A later option rises in from below and an earlier one drops from above, with a soft blur.
- Reduced motion swaps the value with an instant crossfade.

## Responsive behavior

- The menu matches the trigger width, caps at min(24rem, 100vw minus 20px), and at 320px or the available height, then scrolls.
- Long labels ellipsize in the trigger instead of widening it.
- The trigger never scales on press, so Radix measures a stable anchor on touch.

## Performance

- The menu renders in a Radix portal only while open; items are not virtualized, so keep the list short.
- The translucent menu drops its backdrop blur under prefers-reduced-transparency.

## Notes for AI

- Pick for a short fixed list where typing is not needed. Use combobox for long or searchable lists, multi-select for several values, and segmented-control for two to four visible choices.
- Controlled with value and onValueChange, or uncontrolled with defaultValue; pass name to submit with a form.

## Related

- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Multi-select](https://uiarc.dev/components/multi-select/markdown): Select several values while keeping the field readable.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.

## Also in selects

- [Morph select](https://uiarc.dev/components/morph-select/markdown): A select whose trigger grows into the list, with a gliding highlight and type-ahead.
- [Chip group](https://uiarc.dev/components/chip-group/markdown): Filter by a few facets with chips that morph as you pick them.

## Guidance for AI tools

Select: A compact choice field with a keyboard friendly menu. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

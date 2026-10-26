# Combobox

> Search and select from a list without leaving the field.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/combobox
- Markdown: https://uiarc.dev/components/combobox/markdown
- Registry item: https://uiarc.dev/r/combobox.json
- Source file: `registry/components/combobox/combobox.tsx`
- Dependencies: motion, lucide-react
- Keywords: field, search, react combobox, autocomplete, searchable select, typeahead dropdown, filterable select, autocomplete input

## When to use

- Single-choice fields with long lists, such as time zones or countries.
- Lists where people know the name and want to type, including aliases via keywords.

## When not to use

- Use select for a short list where typing adds nothing.
- Use multi-select when several values can be chosen.
- Use expanding-search when the search navigates instead of setting a value.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/combobox
pnpm dlx shadcn@latest add @uiarc/combobox
yarn dlx shadcn@latest add @uiarc/combobox
bunx --bun shadcn@latest add @uiarc/combobox
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/combobox.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/combobox/combobox.tsx`

   The source is in the registry item: https://uiarc.dev/r/combobox.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Combobox } from "@/components/arc/combobox/combobox";

const timezones = [
  { value: "utc", label: "UTC" },
  { value: "cet", label: "Central European", keywords: ["zurich", "berlin"] },
  { value: "pst", label: "Pacific", keywords: ["san francisco"] },
];

export function TimezoneField() {
  const [zone, setZone] = useState("");
  return <Combobox label="Time zone" options={timezones} value={zone} onValueChange={setZone} />;
}
```

## API reference

### Combobox

A searchable single-select field: type to filter, pick from a listbox that follows its height on a spring.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label, also used to name the listbox. |
| `options` (required) | `ComboboxOption[]` | – | { value, label, disabled?, keywords? }. Keywords also match the query. |
| `value` | `string` | – | Controlled selected value. Empty string means none. |
| `defaultValue` | `string` | `""` | Initial value when uncontrolled. |
| `onValueChange` | `(value: string) => void` | – | Called with the new value, or an empty string when cleared. |
| `description` | `string` | – | Helper copy under the field, linked through aria-describedby. |
| `placeholder` | `string` | `"Search or select…"` | Shown when nothing is selected. While searching, the chosen label shows here instead. |
| `emptyMessage` | `string` | `"No matches found"` | Shown in a status row when the filter matches nothing. |
| `className` | `string` | – | Added to the control wrapper. |
| `...props` | `Omit<InputHTMLAttributes<HTMLInputElement>, "value" \| "defaultValue" \| "onChange" \| "placeholder">` | – | Forwarded to the input, including ref, id, name, disabled, and onFocus. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowDown / ArrowUp | Opens the list, then moves through enabled options, wrapping at the ends. |
| Enter | Selects the active option. |
| Escape | Closes the list and discards the query. |

## Accessibility

- Input has role="combobox" with aria-expanded, aria-controls, aria-autocomplete="list", and aria-activedescendant.
- Options use role="option" with aria-selected and aria-disabled; the list is labelled "<label> options".
- The empty state is a role="status" row; the clear button is labelled "Clear selection".

## Motion

- The popover springs in from slightly above and the listbox height follows filtering on a smooth spring.
- A chosen label rises into the field with a soft blur; the clear button scales in.
- Reduced motion keeps opacity-only fades and instant height changes.

## Responsive behavior

- The popover spans the field width and the listbox caps at min(300px, 40vh), scrolling with contained overscroll.
- It closes on any pointerdown outside, so a tap elsewhere dismisses it on touch.

## Performance

- Filtering is memoized and runs on every keystroke across all options; results are not virtualized, so pre-filter very large lists.
- A ResizeObserver drives the listbox height spring while filtering.

## Notes for AI

- Use for long or searchable single-choice lists. Use select for short lists, multi-select for several values, and expanding-search for search that navigates.
- Controlled with value and onValueChange, or uncontrolled with defaultValue. The input shows the label, not the value, so pair name with a hidden input if you post a form.
- Add keywords to options for synonyms and aliases.

## Related

- [Select](https://uiarc.dev/components/select/markdown): A compact choice field with a keyboard friendly menu.
- [Multi-select](https://uiarc.dev/components/multi-select/markdown): Select several values while keeping the field readable.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.

## Also in selects

- [Morph select](https://uiarc.dev/components/morph-select/markdown): A select whose trigger grows into the list, with a gliding highlight and type-ahead.
- [Chip group](https://uiarc.dev/components/chip-group/markdown): Filter by a few facets with chips that morph as you pick them.

## Guidance for AI tools

Combobox: Search and select from a list without leaving the field. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

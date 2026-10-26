# Search field

> A recognizable search entry point with clear affordances.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/search-field
- Markdown: https://uiarc.dev/components/search-field/markdown
- Registry item: https://uiarc.dev/r/search-field.json
- Source file: `registry/components/search-field/search-field.tsx`
- Dependencies: motion, lucide-react
- Keywords: field, search, react search input, search field, search bar with clear button, filter input, list filter search

## When to use

- Filtering a visible list or table in place.
- Toolbar search where the query should be clearable with one click.

## When not to use

- Use expanding-search for compact header search with results.
- Use combobox when the search sets a form value.
- Use filter-toolbar when search sits with other filters and sort.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/search-field
pnpm dlx shadcn@latest add @uiarc/search-field
yarn dlx shadcn@latest add @uiarc/search-field
bunx --bun shadcn@latest add @uiarc/search-field
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/search-field.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/search-field/search-field.tsx`

   The source is in the registry item: https://uiarc.dev/r/search-field.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { SearchField } from "@/components/arc/search-field/search-field";

export function MemberFilter() {
  const [query, setQuery] = useState("");
  return (
    <SearchField
      label="Search members"
      placeholder="Name or email"
      value={query}
      onValueChange={setQuery}
    />
  );
}
```

## API reference

### SearchField

A controlled search input with a clear button that scales in inside a reserved slot.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label tied to the input. |
| `value` (required) | `string` | – | Current query. |
| `onValueChange` (required) | `(value: string) => void` | – | Called on every keystroke and with an empty string when cleared. |
| `...props` | `Omit<InputHTMLAttributes<HTMLInputElement>, "type">` | – | Forwarded to the input, including ref, placeholder, and name. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Escape | Clears the field (native type="search" behavior in most browsers). |

## Accessibility

- Native input type="search" with a real label.
- Clear button is labelled "Clear search" and returns focus to the input.
- The search icon is aria-hidden.

## Motion

- The clear button scales in from 0.8 with a slight blur on a snappy spring and presses to 0.96.
- Reduced motion fades it in and out without scale or blur; the reserved slot keeps the field width fixed either way.

## Responsive behavior

- The clear button has a reserved slot, so the field width never shifts when it appears.
- It fills its column with min-width 0 and shrinks inside narrow toolbars.

## Performance

- onValueChange fires on every keystroke; debounce expensive filtering or fetching yourself.

## Notes for AI

- Use to filter a visible list in place. Use expanding-search for a compact header search with results, and combobox to choose a value.
- Always controlled: pass value and onValueChange, and debounce expensive filtering yourself.

## Related

- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.
- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.

## Also in text fields

- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Password strength](https://uiarc.dev/components/password-strength/markdown): Show how strong a new password is while it is typed.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Guidance for AI tools

Search field: A recognizable search entry point with clear affordances. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

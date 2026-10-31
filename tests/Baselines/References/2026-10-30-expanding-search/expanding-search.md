# Expanding search

> An icon that morphs into a search field with results beneath it.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/expanding-search
- Markdown: https://uiarc.dev/components/expanding-search/markdown
- Registry item: https://uiarc.dev/r/expanding-search.json
- Source file: `registry/components/expanding-search/expanding-search.tsx`
- Dependencies: motion, lucide-react
- Keywords: search, field, motion, react expanding search, search icon expand, animated search bar, header search, search with results dropdown, collapsible search

## When to use

- Header or toolbar search where a full field would crowd the layout.
- Quick navigation across local items with grouped results and recent suggestions.

## When not to use

- Use search-field to filter a visible list in place.
- Use combobox to set a form value.
- Use command-palette for searching commands across the app.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/expanding-search
pnpm dlx shadcn@latest add @uiarc/expanding-search
yarn dlx shadcn@latest add @uiarc/expanding-search
bunx --bun shadcn@latest add @uiarc/expanding-search
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/expanding-search.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/expanding-search/expanding-search.tsx`

   The source is in the registry item: https://uiarc.dev/r/expanding-search.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ExpandingSearch } from "@/components/arc/expanding-search/expanding-search";

export function HeaderSearch() {
  const router = useRouter();
  return (
    <ExpandingSearch
      label="Search projects and docs"
      items={[
        { id: "p1", title: "Arc website", group: "Projects", meta: "Updated today" },
        { id: "d1", title: "Motion tokens", group: "Docs", keywords: ["spring"] },
      ]}
      onSelect={(item) => router.push(`/items/${item.id}`)}
    />
  );
}
```

## API reference

### ExpandingSearch

An icon button that morphs into a search field with grouped, highlighted results beneath it.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Names the button, field, and results, such as "Search projects and docs". |
| `items` (required) | `ExpandingSearchItem[]` | – | { id, title, meta?, group?, icon?, keywords? }. Filtered client-side by title and keywords. |
| `suggestions` | `ExpandingSearchItem[]` | `[]` | Shown before anything is typed, such as recent searches. |
| `suggestionsLabel` | `string` | `"Recent"` | Heading for suggestions. |
| `placeholder` | `string` | – | Field placeholder. Defaults to label. |
| `onSelect` | `(item: ExpandingSearchItem) => void` | – | Called when a result is chosen; the field then folds back. |
| `onExpandedChange` | `(expanded: boolean) => void` | – | Called when the field opens or folds. |
| `expandedWidth` | `number` | `360` | Widest the field grows, never past its container. |
| `maxResults` | `number` | `6` | Most results shown. |
| `anchor` | `"start" \| "end"` | `"end"` | Edge the button sits on; the field grows away from it. |
| `emptyHint` | `string` | `"Try a shorter word or check the spelling."` | Tip under the empty state. |
| `className` | `string` | – | Added to the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | On the icon button, expands the field. |
| ArrowDown / ArrowUp | Moves through results, wrapping. |
| Enter | Chooses the active result. |
| Escape | Folds the field back into the icon. |

## Accessibility

- The field is role="combobox" with aria-expanded, aria-controls, aria-autocomplete="list", and aria-activedescendant.
- Results are a role="listbox" of role="option" items inside labelled role="group" sections.
- Result counts and empty states are announced through a polite live region; the collapsed field is inert.

## Motion

- The button morphs into the field on a spring, results unfold beneath with a panel that tracks the field width, and a highlight travels between options.
- Reduced motion swaps states with fades and no width or position travel.

## Responsive behavior

- The field grows to the smaller of expandedWidth and its container, and a ResizeObserver follows container resizes.
- The results panel matches the field width and caps at min(22rem, 60vh), scrolling itself.
- The idle lane takes no pointer events, so it never blocks what sits under it on small screens.

## Performance

- Ranking is memoized and runs over all items per query; pass pre-filtered or server results for large sets.
- Result rows use layout position animation, capped by maxResults at 6 by default.

## Notes for AI

- Use in headers and toolbars where a full field would crowd the layout. Use search-field to filter a list in place and combobox to set a form value.
- Place it in the space it may grow into; it fills that space up to expandedWidth. Filtering is local, so pass pre-fetched items or update items as the query changes.
- Exported as both named and default.

## Related

- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Morph nav](https://uiarc.dev/components/morph-nav/markdown): A navigation bar that morphs into rich menus, search, and a compact state as one surface.

## Also in text fields

- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Password strength](https://uiarc.dev/components/password-strength/markdown): Show how strong a new password is while it is typed.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Guidance for AI tools

Expanding search: An icon that morphs into a search field with results beneath it. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

# Pagination

> Move through a long collection with clear bounds.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/pagination
- Markdown: https://uiarc.dev/components/pagination/markdown
- Registry item: https://uiarc.dev/r/pagination.json
- Source file: `registry/components/pagination/pagination.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, navigation, react pagination, pagination component, page numbers, table pagination, animated pagination, next previous pages

## When to use

- Paged server results and long tables.
- Lists where the page should live in the URL or state and fetch on change.

## When not to use

- Use carousel for browsing slides.
- Use timeline with maxHeight for a scrolling feed instead of pages.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/pagination
pnpm dlx shadcn@latest add @uiarc/pagination
yarn dlx shadcn@latest add @uiarc/pagination
bunx --bun shadcn@latest add @uiarc/pagination
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/pagination.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/pagination/pagination.tsx`

   The source is in the registry item: https://uiarc.dev/r/pagination.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { Pagination } from "@/components/arc/pagination/pagination";

export function Results() {
  const [page, setPage] = useState(1);
  return <Pagination page={page} pageCount={12} onPageChange={setPage} />;
}
```

## API reference

### Pagination

Previous and next buttons around a sliding five page window with a traveling current page mark.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `page` (required) | `number` | – | Current page, 1-based. Clamped to the range. |
| `pageCount` (required) | `number` | – | Total pages. Zero renders an empty nav. |
| `onPageChange` (required) | `(page: number) => void` | – | Called with the requested page. |
| `label` | `string` | `"Pagination"` | Accessible name for the nav landmark. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves between the previous, page, and next buttons. |
| Enter / Space | Goes to that page. |

## Accessibility

- Renders a nav landmark with the label as its name.
- Page buttons are labelled "Page 3" and the current one has aria-current="page".
- Previous and next are disabled at the ends and carry aria-labels.

## Motion

- The current page mark springs to the selected button.
- When the window shifts, numbers slide like a belt by the number of slots moved.
- Reduced motion jumps the mark and swaps numbers without sliding.

## Responsive behavior

- It always shows a five page window plus arrows, so it stays the same width on any screen.
- The row wraps if its container is narrower than the buttons, and the current mark re-measures on resize.

## Performance

- Only five page buttons render regardless of pageCount; one ResizeObserver repositions the current mark.

## Notes for AI

- Use for paged server results and long tables. Use carousel for browsing slides.
- It is fully controlled: keep page in state or the URL and fetch on onPageChange.

## Related

- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.
- [Carousel](https://uiarc.dev/components/carousel/markdown): Browse a row of slides by dragging, flicking, or arrowing through them.

## Also in navigation

- [Tabs](https://uiarc.dev/components/tabs/markdown): Switch between related content in the same context.
- [Breadcrumb](https://uiarc.dev/components/breadcrumb/markdown): Show where a page sits in a hierarchy.
- [Mega menu morph](https://uiarc.dev/components/mega-menu-morph/markdown): A site header whose menus share one floating panel. Moving between sections morphs its size, slides the next menu in from that side and glides the caret under the new trigger, with hover intent, a safe triangle and an accordion sheet on narrow screens.

## Guidance for AI tools

Pagination: Move through a long collection with clear bounds. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt


# Empty state

> A useful next step when there is nothing to show yet.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/empty-state
- Markdown: https://uiarc.dev/components/empty-state/markdown
- Registry item: https://uiarc.dev/r/empty-state.json
- Source file: `registry/components/empty-state/empty-state.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, guidance, react empty state, no results, empty list placeholder, zero state, blank slate, first run empty view

## When to use

- Empty lists, zero search results, and first-run views.
- A view that should morph between states, such as empty and success, by changing props.

## When not to use

- Use skeleton while data is still loading.
- Use alert for errors inside a page that still has content.
- Use onboarding-checklist when first-run needs several steps.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/empty-state
pnpm dlx shadcn@latest add @uiarc/empty-state
yarn dlx shadcn@latest add @uiarc/empty-state
bunx --bun shadcn@latest add @uiarc/empty-state
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/empty-state.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/empty-state/empty-state.tsx`

   The source is in the registry item: https://uiarc.dev/r/empty-state.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Search } from "lucide-react";
import { Button } from "@/components/arc/button/button";
import { EmptyState } from "@/components/arc/empty-state/empty-state";

export function NoResults({ onClear }: { onClear: () => void }) {
  return (
    <EmptyState
      icon={<Search size={24} />}
      title="No matches"
      description="Try a shorter search or clear the filters."
      action={<Button variant="secondary" onClick={onClear}>Clear filters</Button>}
    />
  );
}
```

## API reference

### EmptyState

A centered icon, title, description, and optional action for empty views.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Headline. A new title rises in while the old one leaves. |
| `description` (required) | `string` | – | One or two sentences on why it is empty and what to do. |
| `action` | `ReactNode` | – | Call to action, usually a button. |
| `icon` | `ReactNode` | `<Folder />` | Leading icon. A different icon component crossfades in. |
| `className` | `string` | – | Class for the root section. |
| `label` | `string` | – | Accessible name for the section region. |

## Accessibility

- Renders a section with an h3 title; pass label to name the region.
- The icon is aria-hidden.
- Outgoing copy is hidden from assistive tech while it fades.

## Motion

- Changing title or description rolls the copy in place while the block height springs to fit.
- A new icon pops in with a short blur.
- Reduced motion swaps copy with a fade and snaps height; the icon's idle animation stops.

## Responsive behavior

- Padding scales with the viewport between fixed bounds, and the description caps at 18rem so lines stay short.
- Actions wrap and center, so two buttons stack on narrow screens.

## Performance

- One ResizeObserver drives the height spring; the icon's idle animation is CSS and stops under reduced motion.

## Notes for AI

- Use for empty lists, zero search results, and first-run views. Use skeleton while data is loading.
- Keep it mounted and change its props to morph between states such as empty and success.

## Related

- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Alert](https://uiarc.dev/components/alert/markdown): A persistent message that helps people recover or continue.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.
- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.

## Also in cards

- [Card](https://uiarc.dev/components/card/markdown): A contained group of related content and actions.
- [Metric card](https://uiarc.dev/components/metric-card/markdown): A compact summary for a number that needs context.
- [Metric ticker](https://uiarc.dev/components/metric-ticker/markdown): A strip of live KPIs that roll to new values digit by digit and flash their change; rest on one to pause the strip and open its recent trend as a sparkline.

## Guidance for AI tools

Empty state: A useful next step when there is nothing to show yet. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

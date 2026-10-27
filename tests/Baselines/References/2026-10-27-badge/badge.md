# Badge

> A small label for status, category, or metadata.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/badge
- Markdown: https://uiarc.dev/components/badge/markdown
- Registry item: https://uiarc.dev/r/badge.json
- Source file: `registry/components/badge/badge.tsx`
- Dependencies: motion
- Keywords: label, status, react badge, status badge, status pill, animated badge, tag component, label pill, badge with icon

## When to use

- Short statuses next to titles or in table cells, such as Live, Draft, or Failed.
- Counts or states that change in place and should morph instead of jump.
- Tagging a row with one tone plus an optional icon.

## When not to use

- Use alert or toast when the message needs a full sentence.
- Use chip-group when people toggle the values.
- Use stat-card for a headline number with a trend.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/badge
pnpm dlx shadcn@latest add @uiarc/badge
yarn dlx shadcn@latest add @uiarc/badge
bunx --bun shadcn@latest add @uiarc/badge
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/badge.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/badge/badge.tsx`

   The source is in the registry item: https://uiarc.dev/r/badge.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Check } from "lucide-react";
import { Badge } from "@/components/arc/badge/badge";

export function DeployStatus({ live }: { live: boolean }) {
  return (
    <Badge tone={live ? "success" : "neutral"} icon={live ? <Check size={12} /> : undefined}>
      {live ? "Live" : "Draft"}
    </Badge>
  );
}
```

## API reference

### Badge

A small status pill whose label and icon morph in place when they change.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `tone` | `"neutral" \| "success" \| "info" \| "warning" \| "danger"` | `"neutral"` | Color of the pill. |
| `size` | `"sm" \| "md"` | `"md"` | Height and text size. |
| `icon` | `ReactNode` | – | Leading icon. A different icon component crossfades in. |
| `children` | `ReactNode` | – | Label. String or number children get the rolling text swap; other nodes render as is. |
| `...props` | `HTMLAttributes<HTMLSpanElement>` | – | Forwarded to the root span. |

## Accessibility

- Renders a plain span, so it is read inline with surrounding text.
- The icon is aria-hidden; the label must state the status on its own, not rely on tone color.
- Outgoing labels are hidden from assistive tech while they fade, so only the current text is read.
- It does not announce changes. Put it inside a live region if a status update must be spoken.

## Motion

- A new label rises in with a short blur while the old one lifts away, and the pill width springs to fit.
- Passive reflows such as font swaps resize instantly; only a content change springs.
- Reduced motion swaps the label with a quick fade and snaps the width.

## Responsive behavior

- The pill sizes to its label and never wraps, so keep labels to a word or two in narrow cells.
- Hover styles apply only on hover-capable fine pointers.

## Performance

- Each badge has a ResizeObserver for the width spring; fine per row, but avoid thousands in one table.
- Font swaps and passive reflows resize instantly; only content changes animate.

## Notes for AI

- Use for short statuses and counts next to titles or in table cells. Use alert or toast for messages with sentences.
- Keep the badge mounted and change its children to get the morph; remounting with a new key loses it.

## Related

- [Alert](https://uiarc.dev/components/alert/markdown): A persistent message that helps people recover or continue.
- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.

## Also in avatars

- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Avatar group](https://uiarc.dev/components/avatar-group/markdown): Show a team or set of contributors in a small space.
- [Presence stack](https://uiarc.dev/components/presence-stack/markdown): Avatars of the people here now: joins pop in as the stack reflows, a counter rolls past the limit, dots show typing and idle, and hover fans it out with a gliding name label.

## Guidance for AI tools

Badge: A small label for status, category, or metadata. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

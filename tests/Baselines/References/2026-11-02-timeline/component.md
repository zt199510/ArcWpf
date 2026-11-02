# Timeline

> Follow what happened, newest first, grouped by day.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/timeline
- Markdown: https://uiarc.dev/components/timeline/markdown
- Registry item: https://uiarc.dev/r/timeline.json
- Source file: `registry/components/timeline/timeline.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, feed, activity, react timeline, activity feed, audit log, vertical timeline, event feed, changelog timeline

## When to use

- Project history, audit logs, and deploy streams where recency matters.
- Live feeds where new updates slide in at the top.
- Rows that expand in place to show logs or detail.

## When not to use

- Use sortable-data-table when people sort or compare.
- Use stepper for progress through fixed steps.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/timeline
pnpm dlx shadcn@latest add @uiarc/timeline
yarn dlx shadcn@latest add @uiarc/timeline
bunx --bun shadcn@latest add @uiarc/timeline
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/timeline.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/timeline/timeline.tsx`

   The source is in the registry item: https://uiarc.dev/r/timeline.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Timeline } from "@/components/arc/timeline/timeline";

const events = [
  { id: "e1", at: "2026-09-22T09:12:00Z", actor: "Maya", title: "merged Checkout redesign into main", meta: "PR #482" },
  { id: "e2", at: "2026-09-22T08:40:00Z", title: "Deploy failed", tone: "danger" as const, detail: <pre>Build step exited 1</pre> },
];

export function Activity({ now }: { now: number }) {
  return <Timeline events={events} now={now} label="Project activity" maxHeight={420} />;
}
```

## API reference

### Timeline

A vertical activity feed grouped by day with pinned day labels and rows that expand in place.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `events` (required) | `{ id: string; at: string \| number; actor?: string; title: string; meta?: string; detail?: ReactNode; avatar?: string; icon?: ReactNode; tone?: "neutral" \| "success" \| "danger" }[]` | – | Updates in any order; newest shows first. Rows with detail are expandable. |
| `now` (required) | `number` | – | Reference time in epoch ms for relative labels and day groups. Pass a ticking clock to keep labels fresh. |
| `label` (required) | `string` | – | Accessible name for the feed. |
| `timeZone` | `string` | `"UTC"` | Time zone for day groups and clock times. |
| `locale` | `string` | `"en-US"` | Formatting locale. |
| `maxHeight` | `number \| string` | – | Height of the scrolling area. Without it the feed grows with the page. |
| `scrollToNew` | `boolean` | `true` | Scrolls back to the top when a new update arrives. |
| `defaultExpanded` | `string[]` | `[]` | Event ids expanded on mount. |
| `headingLevel` | `2 \| 3 \| 4 \| 5 \| 6` | `3` | Heading level for day labels. |
| `className` | `string` | – | Class for the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowDown / ArrowUp | Moves between expandable rows. |
| Home / End | Jumps to the first or last expandable row. |
| Enter / Space | Expands or collapses the focused row. |

## Accessibility

- The feed is a labelled role="region"; day labels are headings at headingLevel with a hidden update count.
- Expandable rows are buttons with aria-expanded and aria-controls; rows without detail are not interactive.
- Times use a time element with the full date in hidden text, and new updates are announced in a status region.
- Say the outcome in the title, since tone is color only.

## Motion

- The connecting line draws as rows come into view and markers pop in.
- New updates slide in at the top while the rest glide down; details expand on a spring.
- Reduced motion replaces travel with short fades and instant height changes.

## Responsive behavior

- Day labels stay pinned while updates scroll under them, inside maxHeight or the page.
- Below 420px row padding tightens so text keeps its width.

## Performance

- Events are not virtualized; set maxHeight for long feeds and trim old events.
- Pass a now that ticks about once a minute rather than every second, since it re-renders every row.

## Notes for AI

- Use for project history, audit logs, and deploy streams where order and recency matter. Use sortable-data-table when people sort or compare.
- Pass a ticking now (for example updated every minute) so relative times stay current, and a fixed timeZone to avoid hydration mismatches.

## Related

- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.
- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.
- [Stepper](https://uiarc.dev/components/stepper/markdown): Show where a person is in a multi-step flow and what is done.

## Also in activity

- [Comment thread](https://uiarc.dev/components/comment-thread/markdown): Threaded comments with replies, reactions, mentions, inline edit, and resolve.
- [Chat thread](https://uiarc.dev/components/chat-thread/markdown): A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.

## Guidance for AI tools

Timeline: Follow what happened, newest first, grouped by day. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

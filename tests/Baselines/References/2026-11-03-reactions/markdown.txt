# Reactions

> Acknowledge a post in one tap, with counts that roll.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/reactions
- Markdown: https://uiarc.dev/components/reactions/markdown
- Registry item: https://uiarc.dev/r/reactions.json
- Source file: `registry/components/reactions/reactions.tsx`
- Dependencies: @radix-ui/react-popover, motion, lucide-react
- Keywords: reactions, social, comments, react reactions, emoji reactions, reaction picker, like button with count, slack reactions, comment reactions

## When to use

- Lightweight acknowledgement on comments, posts, or release notes.
- Slack-style reaction pills with counts and a picker for new reactions.

## When not to use

- Use a reply field such as ai-composer when people need to say something specific.
- Use action-swap for a single like or follow toggle without counts.
- Use badge for static counts that people cannot change.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/reactions
pnpm dlx shadcn@latest add @uiarc/reactions
yarn dlx shadcn@latest add @uiarc/reactions
bunx --bun shadcn@latest add @uiarc/reactions
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/reactions.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-popover motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/reactions/reactions.tsx`

   The source is in the registry item: https://uiarc.dev/r/reactions.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Reactions } from "@/components/arc/reactions/reactions";
import { Heart, PartyPopper, ThumbsUp } from "lucide-react";

const options = [
  { id: "like", label: "Like", icon: ThumbsUp },
  { id: "love", label: "Love", icon: Heart },
  { id: "party", label: "Celebrate", icon: PartyPopper },
];

export function CommentReactions() {
  return <Reactions options={options} defaultValue={[{ id: "like", count: 3, reacted: false }]} onValueChange={(_, change) => save(change)} />;
}
```

## API reference

### Reactions

A bar of reaction pills with counts and an add button that opens a picker tray.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `options` (required) | `ReactionOption[]` | – | Every reaction the picker offers: { id, label, icon: LucideIcon }. |
| `value` | `Reaction[]` | – | Controlled reactions: { id, count, reacted }. Pills keep this order; a count of zero leaves the bar. |
| `defaultValue` | `Reaction[]` | `[]` | Initial reactions when uncontrolled. |
| `onValueChange` | `(next: Reaction[], change: { id: string; reacted: boolean }) => void` | – | Called with the next list and the change that produced it. |
| `label` | `string` | `"Reactions"` | Accessible name for the bar. |
| `className` | `string` | – | Extra class on the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Toggles your reaction on a pill, or opens the picker from the add button. |
| ArrowLeft / ArrowRight / ArrowUp / ArrowDown | Moves between options in the picker, wrapping at the ends. |
| Home / End | Jumps to the first or last option. |
| Escape | Closes the picker and returns focus to the add button. |

## Accessibility

- The bar is role="group"; each pill is a button with aria-pressed and a label like "Love, 4 reactions, including yours".
- The picker is a role="toolbar" with roving tabindex and labelled options; the add button is labelled "Add reaction".
- A role="status" region announces each change; a pill that leaves while focused hands focus to its neighbour or the add button.

## Motion

- Pills scale in from a blur and reflow on a morph spring; the icon gets a velocity kick that stacks on quick taps, and digits roll in the direction of the count.
- The picker grows out of the add button while a shared highlight glides between options.
- Reduced motion, read only after hydration, swaps everything with short fades and no layout springs.

## Responsive behavior

- The bar wraps onto new lines as pills are added, and its height springs to fit.
- The picker opens in a Radix popover, so it repositions near viewport edges.
- Hover highlights in the picker follow the mouse only; touch taps select directly.

## Performance

- Pills use layout position animation and count digits roll individually; fine for a handful of reactions per item.
- In long comment threads each bar runs its own ResizeObserver, so avoid rendering hundreds at once.

## Notes for AI

- Use for lightweight acknowledgement on comments, posts, or release notes. When people need to say something specific, add a reply field instead.
- Icons must be Lucide component types, not elements. Persist the change from onValueChange and control value when counts come from the server.

## Related

- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Avatar group](https://uiarc.dev/components/avatar-group/markdown): Show a team or set of contributors in a small space.
- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.

## Also in gestures

- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.
- [Swipe actions](https://uiarc.dev/components/swipe-actions/markdown): Reveal row actions with a swipe, or from the same actions in a menu.

## Guidance for AI tools

Reactions: Acknowledge a post in one tap, with counts that roll. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

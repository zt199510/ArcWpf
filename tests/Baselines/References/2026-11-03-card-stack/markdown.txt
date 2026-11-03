# Card stack

> Review a deck one card at a time, with a throw and an undo.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/card-stack
- Markdown: https://uiarc.dev/components/card-stack/markdown
- Registry item: https://uiarc.dev/r/card-stack.json
- Source file: `registry/components/card-stack/card-stack.tsx`
- Dependencies: motion, lucide-react
- Keywords: gesture, motion, triage, react card stack, swipe cards, tinder swipe, swipeable card deck, card triage, swipe left right

## When to use

- One-by-one triage such as shortlisting, review queues, or onboarding picks.
- Decks where swiping left or right should be undoable.

## When not to use

- Use a list or sortable-data-table when people compare items side by side.
- Use carousel to browse items without deciding.
- Use swipe-actions for quick actions on rows in a list.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/card-stack
pnpm dlx shadcn@latest add @uiarc/card-stack
yarn dlx shadcn@latest add @uiarc/card-stack
bunx --bun shadcn@latest add @uiarc/card-stack
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/card-stack.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/card-stack/card-stack.tsx`

   The source is in the registry item: https://uiarc.dev/r/card-stack.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { CardStack } from "@/components/arc/card-stack/card-stack";

const cities = [{ id: "lis", name: "Lisbon" }, { id: "osl", name: "Oslo" }, { id: "kyo", name: "Kyoto" }];

export function Shortlist() {
  return (
    <CardStack
      items={cities}
      getKey={city => city.id}
      getLabel={city => city.name}
      renderCard={city => <h3>{city.name}</h3>}
      labels={{ left: "Skip", right: "Shortlist" }}
      onDecide={(city, decision) => save(city.id, decision)}
    />
  );
}
```

## API reference

### CardStack

A deck reviewed one card at a time by swiping left or right, with buttons, undo, and reset.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `T[]` | – | Cards in order. |
| `getKey` (required) | `(item: T) => string` | – | Stable key per item. |
| `getLabel` (required) | `(item: T) => string` | – | Short name for labels and announcements, such as "Lisbon". |
| `renderCard` (required) | `(item: T) => ReactNode` | – | Card content. |
| `onDecide` | `(item: T, decision: "left" \| "right") => void` | – | Called when a card is swiped or decided by button or key. |
| `onUndo` | `(item: T, decision: "left" \| "right") => void` | – | Called when the last decision is undone. |
| `onReset` | `() => void` | – | Called when all cards come back. |
| `labels` | `{ left: string; right: string }` | `{ left: "Pass", right: "Keep" }` | Action names on the buttons and drag stamps. |
| `outcomes` | `{ left: string; right: string }` | – | Past tense of each action for announcements, such as "skipped". |
| `label` | `string` | `"Cards"` | Accessible name for the stack. |
| `renderEmpty` | `(reset: () => void) => ReactNode` | – | Shown once every card is decided. Defaults to a Start over button. |
| `className` | `string` | – | Class for the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Decides the top card left or right when the stack is focused. |
| Backspace / Cmd+Z / Ctrl+Z | Undoes the last decision. |

## Accessibility

- The stage is a focusable role="group" with aria-roledescription="card stack" and a hidden hint describing drag and keys.
- Each card is labelled like "Lisbon, 1 of 3".
- Decisions, undo, and reset are announced in a polite status region.
- Buttons mirror every gesture, so swiping is never required.

## Motion

- The top card follows the drag with tilt and stamps; a flick or far drag throws it off with its velocity.
- Cards beneath rise into place, and undo flies a card back from where it left.
- Reduced motion fades cards out and in at home without travel.

## Responsive behavior

- The stage is min(100%, 20rem) wide; below 26rem the cards get shorter, and below 22.5rem Undo becomes icon-only.
- Cards use touch-action pan-y, so vertical swipes still scroll the page.

## Performance

- Only the top four cards render in the stage, however long the deck.
- Drag and throw run on motion values without per-frame React updates.

## Notes for AI

- Use for one-by-one triage: shortlisting, review queues, onboarding picks. Use a list or sortable-data-table when people compare items side by side.
- Persist decisions from onDecide and reverse them in onUndo.

## Related

- [Swipe actions](https://uiarc.dev/components/swipe-actions/markdown): Reveal row actions with a swipe, or from the same actions in a menu.
- [Carousel](https://uiarc.dev/components/carousel/markdown): Browse a row of slides by dragging, flicking, or arrowing through them.
- [Wallet stack](https://uiarc.dev/components/wallet-stack/markdown): Fan a stack of cards and lift one out to see its activity.

## Also in media

- [Image compare](https://uiarc.dev/components/image-compare/markdown): Drag a divider across two images to see what changed.

## Guidance for AI tools

Card stack: Review a deck one card at a time, with a throw and an undo. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

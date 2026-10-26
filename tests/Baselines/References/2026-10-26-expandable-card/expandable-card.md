# Expandable card

> Give a dense card more room when requested.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/expandable-card
- Markdown: https://uiarc.dev/components/expandable-card/markdown
- Registry item: https://uiarc.dev/r/expandable-card.json
- Source file: `registry/components/expandable-card/expandable-card.tsx`
- Dependencies: motion, lucide-react
- Keywords: disclosure, layout, react expandable card, collapsible card, expand card animation, disclosure card, show more card, card with details

## When to use

- A single summary card with optional details, like a plan, order, or log entry.
- Cards whose summary line updates and should roll changed words in place.

## When not to use

- Use accordion for a list of mutually exclusive sections.
- Use card when there is nothing to reveal.
- Use card-stack for browsing several cards in one spot.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/expandable-card
pnpm dlx shadcn@latest add @uiarc/expandable-card
yarn dlx shadcn@latest add @uiarc/expandable-card
bunx --bun shadcn@latest add @uiarc/expandable-card
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/expandable-card.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/expandable-card/expandable-card.tsx`

   The source is in the registry item: https://uiarc.dev/r/expandable-card.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ExpandableCard } from "@/components/arc/expandable-card/expandable-card";

export function PlanDetails() {
  return (
    <ExpandableCard title="Pro plan" description="Billed yearly, renews March 4" width={400} expandedWidth={600}>
      <ul>
        <li>Unlimited projects</li>
        <li>Priority support</li>
      </ul>
    </ExpandableCard>
  );
}
```

## API reference

### ExpandableCard

A card whose header toggles a details panel beneath it.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Card heading, shown in the toggle. |
| `description` | `string` | – | Summary line. Changed words roll in when it updates. |
| `children` (required) | `ReactNode` | – | Details revealed when expanded. |
| `defaultExpanded` | `boolean` | `false` | Starts expanded. |
| `width` | `number` | – | Collapsed width cap in px. The card is centered and never wider than its container. Fills the container when omitted. |
| `expandedWidth` | `number` | – | Expanded width cap in px, so the card grows sideways into more room when opened. Defaults to width. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Toggles the card from its header button. |
| Escape | Closes the card while focus is inside it and returns focus to the header. |

## Accessibility

- The header is a native button with aria-expanded and aria-controls pointing at the panel.
- The rolling description keeps a screen-reader-only copy of the full text; animated words are aria-hidden.
- The collapsed panel is inert, so its content is not reachable by keyboard or screen readers while closed.

## Motion

- Open and close are one morph: width and height move together on the smooth spring, which never overshoots, and the arrow turns on the same spring.
- Details fade in once the box has made room and fade out before it shrinks. They are laid out at their open width throughout, so text never reflows or stretches.
- Clicking again mid-animation reverses from the current size with its velocity. Changed words in the description rise in; unchanged words hold still.
- Reduced motion applies end states instantly and swaps the description without animation.

## Responsive behavior

- Without width caps the card fills its container. With width and expandedWidth it stays centered and grows sideways to the open cap, never wider than its container, so on a phone it simply uses the full width.
- The whole header is the toggle with a 64px minimum height, so it is an easy touch target.

## Performance

- The panel stays mounted but inert at zero height when collapsed, so a second click mid-animation reverses smoothly.
- Width and height are animated for real on one spring rather than scaled, so text and corners never stretch; keep the details short.
- The description splits into one span per word for the roll; keep it to a short line.

## Notes for AI

- Use for a single standalone expandable summary such as a plan, order, or log entry. Use accordion for a list of mutually exclusive sections.
- State is internal; there is no controlled prop.

## Related

- [Accordion](https://uiarc.dev/components/accordion/markdown): Progressively reveal supporting information in place.
- [Card](https://uiarc.dev/components/card/markdown): A contained group of related content and actions.
- [Card stack](https://uiarc.dev/components/card-stack/markdown): Review a deck one card at a time, with a throw and an undo.

## Also in expand

- [Scroll area](https://uiarc.dev/components/scroll-area/markdown): A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.
- [Resizable panels](https://uiarc.dev/components/resizable-panels/markdown): Trade space between panes by dragging the divider between them.
- [Expandable text](https://uiarc.dev/components/expandable-text/markdown): Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.

## Guidance for AI tools

Expandable card: Give a dense card more room when requested. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt


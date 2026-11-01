# Hover card

> Preview a person or link on hover or focus without leaving the page.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/hover-card
- Markdown: https://uiarc.dev/components/hover-card/markdown
- Registry item: https://uiarc.dev/r/hover-card.json
- Source file: `registry/components/hover-card/hover-card.tsx`
- Dependencies: @radix-ui/react-popover, motion
- Keywords: preview, people, popover, react hover card, profile preview, user hover card, mention preview, link preview popup, radix hover card

## When to use

- Previews of people behind mentions, avatars, or author names.
- Link or reference previews where the click must stay free for navigation.
- Dense lists where moving between triggers should open cards almost instantly.

## When not to use

- Use tooltip for a plain text label.
- Use popover when the content has buttons or inputs.
- Use user-menu for the account menu on your own avatar.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/hover-card
pnpm dlx shadcn@latest add @uiarc/hover-card
yarn dlx shadcn@latest add @uiarc/hover-card
bunx --bun shadcn@latest add @uiarc/hover-card
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/hover-card.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-popover motion
```

2. Copy the source into your project. Main file: `registry/components/hover-card/hover-card.tsx`

   The source is in the registry item: https://uiarc.dev/r/hover-card.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { HoverCard, HoverCardProfile } from "@/components/arc/hover-card/hover-card";

export function Mention() {
  return (
    <HoverCard
      content={<HoverCardProfile name="Maya Chen" role="Product designer, Payments" stats={[{ label: "Projects", value: 12 }]} />}
    >
      <button type="button">@maya</button>
    </HoverCard>
  );
}
```

## API reference

### HoverCard

A read-only preview that opens after a short hover or keyboard focus and stays open while the pointer travels into it.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `children` (required) | `ReactElement` | – | The trigger, such as a mention button or link. Must accept a ref and be focusable. |
| `content` (required) | `ReactNode` | – | The preview. HoverCardProfile covers people. |
| `side` | `"top" \| "bottom" \| "left" \| "right"` | `"bottom"` | Preferred side of the trigger. |
| `align` | `"start" \| "center" \| "end"` | `"start"` | Alignment against the trigger. |
| `openDelay` | `number` | `500` | Milliseconds of hover before the first card opens. |
| `closeDelay` | `number` | `140` | Grace in milliseconds after the pointer leaves. |
| `className` | `string` | – | Class on the card surface. |

### HoverCardProfile

A person preview: portrait, name, role, bio, stats, and a footer line.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `name` (required) | `string` | – | Full name. Initials are derived from it when there is no avatar. |
| `role` | `string` | – | Role and team. |
| `avatar` | `string \| ReactNode` | – | Portrait URL or your own image element such as next/image. |
| `bio` | `string` | – | Short bio paragraph. |
| `stats` | `{ label: string; value: string \| number }[]` | `[]` | Rendered as a definition list. |
| `meta` | `ReactNode` | – | Footer line such as location and local time. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Keyboard focus on the trigger opens the card immediately. |
| Enter | Opens the card without the hover delay. |
| Escape | Closes the card and keeps it closed while the pointer rests on the trigger. |

## Accessibility

- The card has role="tooltip" and the trigger gets aria-describedby while it is open.
- Focus never moves into the card, so keep its content read only.
- On touch, a tap toggles the card instead of relying on hover.

## Motion

- Grows from the trigger edge: scale from 0.96 and a 4px offset on a smooth spring, with a separate fade. Mid-exit returns retarget instead of jumping.
- Within 300ms of another card, the next opens after 80ms with a fade only. Profile rows settle in with a small stagger.
- Reduced motion drops scale and travel and keeps a short fade.

## Responsive behavior

- The card is 18.5rem wide, capped at the space Radix reports as available or the viewport minus 24px.
- On touch, a tap toggles the card instead of waiting for hover.
- Long names and roles ellipsize and stats wrap onto new lines.

## Performance

- Only one hover card is open on the page, and it mounts in a portal only while open.
- Open and close use timers, not pointer tracking loops; the profile avatar decodes async.

## Notes for AI

- Use for previews of people, links, or references where the click must stay free for navigation. Use tooltip for plain labels and popover for interactive content.
- Only one hover card is open at a time on the page. Anything actionable belongs behind the trigger itself.

## Related

- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [User menu](https://uiarc.dev/components/user-menu/markdown): Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.

## Also in overlays

- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Drawer](https://uiarc.dev/components/drawer/markdown): A temporary side surface for focused work.
- [Bottom sheet](https://uiarc.dev/components/bottom-sheet/markdown): A sheet that rests at a peek or full height and follows your finger.
- [Share access](https://uiarc.dev/components/share-access/markdown): A share panel: invite people as validated email chips with suggestions, change roles from a compact menu, remove with undo, switch link access and copy the link, with every change landing in place.
- [Confirm popover](https://uiarc.dev/components/confirm-popover/markdown): A destructive button that grows into a small confirmation card, with the consequence, optional type-to-confirm, progress on the Confirm button, and a check or error before folding back.

## Guidance for AI tools

Hover card: Preview a person or link on hover or focus without leaving the page. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

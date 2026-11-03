# Expanding button group

> Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/expanding-button-group
- Markdown: https://uiarc.dev/components/expanding-button-group/markdown
- Registry item: https://uiarc.dev/r/expanding-button-group.json
- Source file: `registry/components/expanding-button-group/expanding-button-group.tsx`
- Dependencies: motion, lucide-react
- Keywords: action, group, toolbar, new, button group, icon buttons, expanding, reveal label, hover label, action bar, message actions, file actions, confirm in place, morph, pill

## When to use

- A compact action bar on a message, file, or card where space is tight but labels should be one glance away.
- Toolbars where actions finish in place and deserve a confirmation without a toast, such as Archive or Copy link.

## When not to use

- Use toggle-group or segmented-control when the buttons select a value instead of running an action.
- Use plain labelled buttons when there are only one or two actions and space allows; hiding labels then costs clarity.
- Use a dropdown menu for more than about six actions, or for actions that need descriptions.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/expanding-button-group
pnpm dlx shadcn@latest add @uiarc/expanding-button-group
yarn dlx shadcn@latest add @uiarc/expanding-button-group
bunx --bun shadcn@latest add @uiarc/expanding-button-group
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/expanding-button-group.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/expanding-button-group/expanding-button-group.tsx`

   The source is in the registry item: https://uiarc.dev/r/expanding-button-group.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Archive, Clock3, Forward, Reply, Trash2 } from "lucide-react";
import { ExpandingButtonGroup } from "@/components/arc/expanding-button-group/expanding-button-group";

export function MessageActions({ message }: { message: Message }) {
  return (
    <ExpandingButtonGroup
      label="Message actions"
      defaultExpanded="reply"
      items={[
        { id: "reply", label: "Reply", icon: <Reply />, onSelect: () => openReply(message.id) },
        { id: "forward", label: "Forward", icon: <Forward />, onSelect: () => openForward(message.id) },
        { id: "archive", label: "Archive", doneLabel: "Archived", icon: <Archive />, onSelect: () => archive(message.id) },
        { id: "snooze", label: "Snooze", doneLabel: "Snoozed", icon: <Clock3 />, onSelect: () => snooze(message.id, "tomorrow-8am") },
        { id: "delete", label: "Delete", doneLabel: "Deleted", tone: "danger", icon: <Trash2 />, onSelect: () => moveToTrash(message.id) },
      ]}
    />
  );
}
```

## API reference

### ExpandingButtonGroup

Icon buttons in a compact group where one action always shows its label. The one you point at or focus takes the label over while its neighbours slide aside, and an action confirms in place.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `{ id: string; label: string; icon: ReactNode; onSelect?: () => void \| boolean \| Promise<void \| boolean>; doneLabel?: string; tone?: "neutral" \| "danger"; disabled?: boolean }[]` | – | The actions, in order. label is shown when the button expands and is always its accessible name. With doneLabel, a successful onSelect turns the icon into a check and the label into that word, such as "Archived". Return false or reject to skip it; a promise keeps the button busy until it settles. |
| `label` (required) | `string` | – | Accessible name of the toolbar, such as "Message actions". |
| `size` | `"sm" \| "md"` | `"md"` | Button height of 28 or 36px, with 14 or 16px icons. |
| `defaultExpanded` | `string \| null` | `First enabled action` | Id of the action whose label shows at rest, usually the primary one. One action is always expanded: pointing at or focusing another moves the expansion there, and the group returns to this one when the pointer and focus leave. |
| `onAction` | `(id: string) => void` | – | Called with the item id whenever an action runs, before the item's own onSelect. |
| `confirmDuration` | `number` | `1400` | How long the confirmation stays, in milliseconds. |
| `className` | `string` | – | Extra class on the toolbar. |
| `style` | `CSSProperties` | – | Inline styles on the toolbar. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves into the toolbar onto one action (the last focused, else the resting one) and out again. |
| Arrow left / Arrow right | Moves focus to the previous or next action, wrapping at the ends. The focused action expands to show its label. |
| Home / End | Moves focus to the first or last action. |
| Enter / Space | Runs the focused action. |

## Accessibility

- The root is a toolbar named by label, with one tab stop and roving arrow key focus.
- Each button keeps its full label as its accessible name while collapsed; the visible label and icon are hidden from assistive technology so nothing is read twice.
- Disabled actions use aria-disabled instead of disabled, so they stay focusable and still reveal their label, but do nothing when activated.
- A confirmation is announced through a polite live region, such as "Archived". The same word is announced again on a repeat action.
- A busy action (a promise from onSelect) sets aria-busy until it settles.
- The expansion itself is the focus indicator; pointer focus draws no ring and keyboard focus shows Arc's shared focus ring. Only keyboard focus expands, so a mouse click does not leave a label open after the pointer leaves.
- Confirmation is never color alone: the icon becomes a check and the label changes to a past tense word.

## Motion

- Each button's width springs on the morph spring between its icon size and icon plus measured label. Neighbours slide aside in normal flow and the group's own width follows, so nothing resizes in a single frame.
- The label slot always reserves the longer of label and doneLabel, so the confirmation never changes the width. The server render already shows the resting action at that exact width, so hydration moves nothing.
- Anchor the group at its start edge (not centred) and reserve its widest width in the layout, so expansion only moves buttons inside the group.
- The icon keeps the same left inset in both states; the label slides out from behind it with a short fade and blur, and tucks back faster than it came.
- A confirmation draws a check where the icon was and lifts the old word out while the new one rises in, while the width springs between the two words.
- Hover only follows real pointer movement, and moving to a neighbour needs the pointer 4px inside it, so a button that slides under a resting pointer never takes over and a pointer resting on a boundary never flips between two actions.
- Pressing shrinks the icon slightly; the button box never scales, because its width is the animation.
- With reduced motion, labels and widths change at once with a quick fade and the check appears without drawing.

## Responsive behavior

- Five medium actions with one label open take about 260 to 280px, depending on the longest label. Reserve that width in narrow layouts rather than letting the group squeeze content.
- Touch has no hover: the first tap expands an action and the second runs it. A tap outside the group closes a label opened by touch.
- Buttons are 36px tall at md and 28px at sm; use md where touch matters.

## Performance

- Each label is measured from invisible copies in its own slot (and again if its font or text changes) with a ResizeObserver; width is a Motion value, so React renders only when the expanded action changes.
- Width animation causes layout in the group only; keep the group out of large reflowing containers.

## Notes for AI

- Use it for a row of three to six related actions on one object, where icons are recognisable but labels help: message, file and card actions.
- Give every action a short verb label and a matching lucide icon. Use doneLabel only for actions that finish at once (Archive, Snooze, Copy link); leave it out for actions that open something (Reply, Rename).
- Mark at most one destructive action with tone="danger". It needs no confirm dialog when the surrounding UI offers Undo.
- One action is always expanded. Put the primary action first, or set defaultExpanded to it, so it reads as a labelled button at rest.
- This is an action group. For choosing one or several values use toggle-group or segmented-control.
- On touch the first tap reveals the label and a second tap runs it; the resting action runs on the first tap.
- Place it start aligned in a row that reserves its widest width, so the expansion never moves surrounding content.

## Related

- [Button group](https://uiarc.dev/components/button-group/markdown): Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Floating button group](https://uiarc.dev/components/floating-button-group/markdown): Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
- [Toggle group](https://uiarc.dev/components/toggle-group/markdown): A row of icon or text toggles, single or multiple: one pressed fill glides between options, or each fades in on its own and neighbours join, as in a formatting toolbar.
- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.
- [Swipe actions](https://uiarc.dev/components/swipe-actions/markdown): Reveal row actions with a swipe, or from the same actions in a menu.

## Also in buttons

- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Copy button](https://uiarc.dev/components/copy-button/markdown): Copy a value with immediate confirmation.
- [Action swap](https://uiarc.dev/components/action-swap/markdown): Swap an action label while preserving its place and intent.
- [Icon morph](https://uiarc.dev/components/icon-morph/markdown): Icons that morph between states: menu to close, play to pause, copy to check, and more.

## Guidance for AI tools

Expanding button group: Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

# Swipe actions

> Reveal row actions with a swipe, or from the same actions in a menu.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/swipe-actions
- Markdown: https://uiarc.dev/components/swipe-actions/markdown
- Registry item: https://uiarc.dev/r/swipe-actions.json
- Source file: `registry/components/swipe-actions/swipe-actions.tsx`
- Dependencies: @radix-ui/react-dropdown-menu, motion, lucide-react
- Keywords: swipe, gesture, list, react swipe actions, swipe to delete, swipe to archive, ios swipe row, swipeable list item, mobile list actions, swipe gesture list

## When to use

- Touch-first triage lists such as mail, notifications, or tasks.
- Rows with one or two quick actions per side, like Archive and Mark unread.
- Lists where a full swipe should commit the main action and the row should animate away.

## When not to use

- Use context-menu or dropdown-menu for desktop-first tables.
- Use reorderable-list when rows need to be dragged vertically.
- Use hold-to-confirm for a single destructive action outside a list.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/swipe-actions
pnpm dlx shadcn@latest add @uiarc/swipe-actions
yarn dlx shadcn@latest add @uiarc/swipe-actions
bunx --bun shadcn@latest add @uiarc/swipe-actions
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/swipe-actions.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-dropdown-menu motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/swipe-actions/swipe-actions.tsx`

   The source is in the registry item: https://uiarc.dev/r/swipe-actions.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { SwipeActions, SwipeActionsRow } from "@/components/arc/swipe-actions/swipe-actions";
import { Archive, MailOpen } from "lucide-react";

export function Inbox({ messages }: { messages: Message[] }) {
  return (
    <SwipeActions label="Inbox">
      {messages.map(message => (
        <SwipeActionsRow
          key={message.id}
          label={message.subject}
          leading={[{ label: "Unread", icon: <MailOpen size={18} />, tone: "accent", keepRow: true, onSelect: () => markUnread(message.id) }]}
          trailing={[{ label: "Archive", icon: <Archive size={18} />, tone: "danger", onSelect: () => archive(message.id) }]}
        >
          <MessagePreview message={message} />
        </SwipeActionsRow>
      ))}
    </SwipeActions>
  );
}
```

## API reference

### SwipeActions

The list. Keeps one row open at a time and closes it on an outside press or Escape.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Accessible name for the list. |
| `children` (required) | `ReactNode` | – | SwipeActionsRow elements. Removing one animates its height closed. |
| `className` | `string` | – | Extra class on the surface. |

### SwipeActionsRow

One row whose actions reveal under its left and right edges on a horizontal swipe.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Names the row's More actions button, for example the message subject. |
| `leading` | `SwipeAction[]` | `[]` | Actions under the left edge, revealed by swiping right. The first one commits on a full swipe. |
| `trailing` | `SwipeAction[]` | `[]` | Actions under the right edge, revealed by swiping left. The last one commits on a full swipe. |
| `fullSwipe` | `boolean` | `true` | Lets a long swipe or flick commit the outermost action. |
| `children` (required) | `ReactNode` | – | Row content. |
| `className` | `string` | – | Extra class on the row. |

### SwipeAction

One action in leading or trailing.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Shown under the icon and in the menu. |
| `icon` (required) | `ReactNode` | – | Action icon. |
| `onSelect` (required) | `() => void` | – | Runs on full swipe, tap, or menu choice. Remove the item here unless keepRow is set. |
| `tone` | `"neutral" \| "accent" \| "danger"` | `"neutral"` | Fill of the revealed action. |
| `keepRow` | `boolean` | – | Springs the row home after the action instead of sliding it away. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Opens a row's More actions menu from its button. |
| ArrowDown / ArrowUp | Moves between the row's actions in the menu. |
| Escape | Closes the menu or puts away an open row. |

## Accessibility

- The list is a ul with role="list" and aria-label; each row is an li.
- Every row has a "More actions for <label>" menu with the same actions, so the gesture is never required.
- Revealed action buttons are aria-hidden and out of the tab order; after a removing action, focus moves to a neighbouring row.

## Motion

- The row tracks the finger, rubber-bands past its last stop, and settles on a spring that carries the release velocity; a flick projects its travel.
- Past the threshold the outer action stretches across the row, then the row slides out and the list closes the gap.
- Reduced motion jumps the row to its stops and fades rows in and out instead of animating height.

## Responsive behavior

- Swipes lock to the horizontal axis after the first movement, so vertical page scrolling still works on touch.
- A full swipe commits past the larger of the open width plus 48px or 56% of the row, measured with a ResizeObserver.
- Every row also has a More actions menu, so mouse and keyboard users never need the gesture.

## Performance

- Drag updates a motion value directly, so rows do not re-render while following the finger.
- Rows are not virtualized and each has its own ResizeObserver; paginate long lists.

## Notes for AI

- Use for short, triage-style lists on touch (mail, notifications, tasks). For desktop-first tables use context-menu or a row menu.
- Remove the item from state inside onSelect; set keepRow for actions that keep the item, such as mark as unread.
- Rows must be direct children of SwipeActions, keyed by item id, so removal and undo animate.

## Related

- [Context menu](https://uiarc.dev/components/context-menu/markdown): Secondary actions kept close to the selected object.
- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.
- [Reorderable list](https://uiarc.dev/components/reorderable-list/markdown): Organize items with direct drag and drop.
- [Dropdown menu](https://uiarc.dev/components/dropdown-menu/markdown): A focused list of actions anchored to a trigger.

## Also in gestures

- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.

## Guidance for AI tools

Swipe actions: Reveal row actions with a swipe, or from the same actions in a menu. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

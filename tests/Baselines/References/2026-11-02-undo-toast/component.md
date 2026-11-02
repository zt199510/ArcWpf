# Undo toast

> Archive or delete rows with a second chance: the gap closes, a countdown toast merges repeat actions, and Undo or ⌘Z puts every row back exactly where it was.

- Type: Component (feedback)
- Access: Arc Pro
- Page: https://uiarc.dev/components/undo-toast
- Markdown: https://uiarc.dev/components/undo-toast/markdown
- Source file: `registry/components/undo-toast/undo-toast.tsx`
- Dependencies: motion, lucide-react
- Keywords: feedback, toast, undo, new, snackbar, archive, delete, trash, inbox, list, remove, restore, countdown, cmd z, ctrl z, optimistic, batch, gmail undo

## When to use

- An inbox, notification list, task list or file list where archiving or deleting should be one click with a way back.
- Bulk triage, where people remove several items quickly and one toast should summarise them all.
- Anywhere a confirmation dialog would slow down a routine, reversible action.

## When not to use

- Irreversible destruction such as deleting a project or revoking keys; use confirm-popover so the consequence is stated first.
- General app notifications that are not tied to removing list items; use toast-stack.
- Lists of hundreds of visible rows animating at once; virtualise the list and remove through the ref handle instead.

## Installation

Undo toast is part of Arc Pro. The live preview is public; the source and install command need Pro.

### CLI with a Pro token

1. Create a token in your account and set it in the environment (or `.env.local`). Never commit it.

```bash
export ARC_PRO_TOKEN=arc_pro_...
```

2. Add the Pro registry to `components.json`:

```json
{
  "registries": {
    "@uiarc": "https://uiarc.dev/r/{name}.json",
    "@uiarc-pro": {
      "url": "https://uiarc.dev/r/pro/{name}.json",
      "headers": {
        "Authorization": "Bearer ${ARC_PRO_TOKEN}"
      }
    }
  }
}
```

3. Install:

```bash
npx shadcn@latest add @uiarc-pro/undo-toast
```

### Manual

Signed-in Pro members can copy the source from the Manual tab on the docs page.

- Plans: https://uiarc.dev/pricing
- Create a Pro token: https://uiarc.dev/account#pro-access
- Setup guide: https://uiarc.dev/docs/ai#pro-access

## Usage

```tsx
import { Archive, Trash2 } from "lucide-react";
import { UndoToast, UndoToastRow, UndoToastRowAction } from "@/components/arc/undo-toast/undo-toast";

export function Inbox({ threads, setThreads }: { threads: Thread[]; setThreads: (t: Thread[]) => void }) {
  return (
    <UndoToast
      label="Conversations"
      items={threads}
      onItemsChange={setThreads}
      getKey={thread => thread.id}
      noun={["conversation", "conversations"]}
      onCommit={entries => api.apply(entries.map(e => ({ id: e.key, action: e.kind })))}
      style={{ paddingBottom: 76 }}
      renderItem={(thread, { remove }) => (
        <UndoToastRow
          label={`${thread.from}, ${thread.subject}`}
          leading={<Avatar src={thread.avatar} />}
          title={thread.from}
          description={thread.subject}
          meta={thread.time}
          shortcuts={{ e: () => remove("archive"), Backspace: () => remove("delete") }}
          actions={<>
            <UndoToastRowAction label="Archive" icon={<Archive size={16} />} onClick={() => remove("archive")} />
            <UndoToastRowAction label="Delete" icon={<Trash2 size={16} />} onClick={() => remove("delete")} />
          </>}
        />
      )}
    />
  );
}
```

## API reference

### UndoToast

A list whose rows can be archived or deleted with a second chance. The removed row fades and slips aside while its gap closes on a spring, and a toast rises from the bottom with a countdown ring that drains around the seconds left. Removing more rows merges them into the same toast: the count rolls up, the ring refills and the toast gives a small nudge. Undo, or Cmd/Ctrl+Z, opens every row again in its exact original place, top to bottom, with a brief accent glow, and the toast turns into a check with the restored count. Hovering or focusing the toast pauses the countdown. When it runs out the batch is committed; if the commit fails the rows come back and the toast says so.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `getKey` (required) | `(item: T) => string` | – | A stable key for each item. Rows are tracked by key, so an item removed and restored keeps its identity. |
| `renderItem` (required) | `(item: T, ctx: UndoRowContext) => ReactNode` | – | Renders one row. ctx.remove(kind) removes it with an action kind, ctx.removed is true while it is folded away and ctx.restoredAt changes each time it is put back. |
| `label` (required) | `string` | – | Accessible name of the list. |
| `items` | `readonly T[]` | – | Controlled items. Removed rows stay in this array, folded away, until the batch is committed; then onItemsChange receives the list without them. |
| `defaultItems` | `readonly T[]` | – | Starting items when uncontrolled. |
| `onItemsChange` | `(items: T[]) => void` | – | Called with the new list when a batch is committed, never while it can still be undone. |
| `actions` | `Record<string, UndoAction>` | `archive and delete` | The kinds of removal. Each has a label ("Archive", used in failure copy), done ("archived", used in the toast) and an optional icon. The first key is the default kind for ctx.remove(). |
| `noun` | `readonly [string, string]` | `["item", "items"]` | Singular and plural nouns for the toast: "Conversation archived", "3 conversations archived". Mixed batches read "2 archived, 1 deleted". |
| `duration` | `number` | `6000` | Milliseconds the undo window stays open. It refills whenever another row joins the batch and pauses while the toast is hovered or focused or the tab is hidden. |
| `onRemove` | `(entries: UndoEntry<T>[]) => void` | – | Called as soon as a row is removed, for optimistic server updates. Pair it with onUndo to reverse them. |
| `onUndo` | `(entries: UndoEntry<T>[]) => void \| Promise<unknown>` | – | Called on undo. Return a promise to show Restoring with a spinner; a rejection keeps the rows removed, says Couldn't undo, try again, and restarts the countdown. |
| `onCommit` | `(entries: UndoEntry<T>[]) => void \| Promise<unknown>` | – | Called when the undo window closes, with every entry in the batch and its original index. Return a promise; a rejection opens the rows again and shows Couldn't archive conversation. |
| `undoShortcut` | `boolean` | `true` | Cmd+Z on Apple devices and Ctrl+Z elsewhere undo the pending batch, unless focus is in a text field that has its own undo. The Undo button shows the shortcut on devices with a pointer. |
| `pauseOnHover` | `boolean` | `true` | Hovering or focusing the toast pauses the countdown. |
| `empty` | `ReactNode` | – | Shown, fading up, once every row is gone. |
| `toastPlacement` | `"container" \| "viewport"` | `"container"` | container floats the toast over the bottom of the component, so reserve padding at the bottom of the list. viewport fixes it to the bottom of the window. |
| `ref` | `Ref<UndoToastHandle>` | – | remove(key, kind), undo() and commit() for toolbars, bulk actions and tests. |
| `className` | `string` | – | Class on the root element. |
| `style` | `CSSProperties` | – | Inline styles on the root element. |

### UndoToastRow

A ready made row: leading media, a title and description, and metadata that gives way to the action buttons on hover and focus without reflowing. The row is focusable and runs single key shortcuts while focused.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `ReactNode` | – | The first line, truncated with an ellipsis. |
| `leading` | `ReactNode` | – | Media before the text, such as an avatar. |
| `description` | `ReactNode` | – | The second line, truncated with an ellipsis. |
| `meta` | `ReactNode` | – | Right aligned metadata such as a time. It fades out while the actions are shown. |
| `actions` | `ReactNode` | – | Row buttons, usually UndoToastRowAction. Shown on hover and focus, and always on touch screens. |
| `shortcuts` | `Record<string, () => void>` | – | Single key shortcuts on the focused row, such as { e: archive, Backspace: remove }. |
| `label` | `string` | – | Accessible name of the row, such as the sender and subject. |
| `unread` | `boolean` | – | Sets the title in medium weight. |
| `className` | `string` | – | Class on the row. |

### UndoToastRowAction

A round icon button for a row action, named by its label.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Accessible name and tooltip, such as Archive. |
| `icon` (required) | `ReactNode` | – | A 16px lucide icon. |
| `onClick` (required) | `() => void` | – | Usually ctx.remove(kind). The click does not bubble to the row. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves through the rows, their actions and the Undo button. |
| E / Backspace / Delete | With UndoToastRow shortcuts, archive or delete the focused row. Focus moves to the same control on the next row, or the previous one at the end of the list. |
| Cmd+Z / Ctrl+Z | Undoes the pending batch from anywhere on the page, except inside a text field. |
| Enter / Space | On Undo, restores the batch. |
| Escape | With focus in the toast, closes the undo window now and commits the batch. |

## Accessibility

- The list is a labelled ul. A folded row is aria-hidden and inert, so it leaves the tab order and the accessibility tree the moment it is removed and returns when it is restored.
- Removing a focused row hands focus to the same control on the next row, or the previous one; when the list is empty focus moves to Undo. Undoing from the toast moves focus to the first restored row.
- A polite live region announces each change in words: Conversation archived. Press Command Z or the Undo button to undo; 3 conversations restored; Couldn't archive conversation.
- The toast is a labelled group that describes itself with the live text. The Undo button carries aria-keyshortcuts. The countdown ring and seconds are decorative; hovering or focusing the toast pauses them so there is time to act.
- State never relies on color: the toast pairs a countdown, check or alert icon with a sentence.

## Motion

- Remove: the row content fades and slips 28px aside with a small scale and blur in 180ms while the row height closes on the smooth spring, so rows below glide up to fill the gap.
- The toast rises 18px from a slightly smaller, blurred state on the morph spring, from its bottom edge. It leaves by dropping and fading on the exit curve.
- Merge: the message crossfades vertically when its shape changes, the count rolls digit by digit, the ring springs back to full and the toast gets a velocity nudge on its scale.
- The countdown ring is an SVG path whose length follows a linear motion value; the seconds inside roll on each whole second. Hover, focus and a hidden tab pause the same animation, so the ring stops exactly where it was.
- Undo: restored rows reopen top to bottom with a 35ms stagger, their content slides back in on the snappy spring after the height starts to open, and a faint accent glow marks where they returned. The ring turns into a check with a small rotation.
- Everything is interruptible: removing again mid collapse, undoing mid collapse or removing during an undo all retarget from the current values.
- With reduced motion rows open and close at once, the toast only fades, digits swap without rolling and the ring refills instantly; the countdown itself still drains so the time limit stays visible.

## Responsive behavior

- Rows truncate their title and description with an ellipsis at any width; the time and the actions share one cell so nothing reflows on hover.
- On touch screens the row actions are always visible and the shortcut hint on Undo is hidden.
- The toast is at most 372px wide and keeps 12px from the component edges, so it fits a 320px phone; the message truncates before the Undo button shrinks.

## Performance

- Only removed and restored rows animate. Row height is the one layout property that moves, because closing the gap is the information; the content moves with transforms and opacity.
- The countdown is one motion value drained by a linear animation, not a timer tick; React re-renders once per whole second for the digit.
- Rows render through a small child component, so a toast update does not re-render every row's content.

## Notes for AI

- Use UndoToast instead of a confirmation dialog for reversible removals: archive, delete to trash, remove from a list, mark done. It lets people act fast and recover.
- Choose one pattern for the server: deferred (do nothing until onCommit, then send the batch) or optimistic (send in onRemove, reverse in onUndo). Deferred is simpler and needs no reversal endpoint.
- Keep items controlled when the list lives in your store. Removed rows stay in your array until onItemsChange fires on commit, which is what lets undo put them back at the same index.
- Reserve space under the list for the toast in container placement (about 76px), or use toastPlacement="viewport" for full page lists.
- Name the noun so the toast reads naturally: noun={["file", "files"]}. Give each action a label and past tense if you add kinds beyond archive and delete.
- Use your own row markup freely; UndoToastRow is a convenience. Call ctx.remove from buttons, swipe actions or keyboard shortcuts.

## Related

- [Toast stack](https://uiarc.dev/components/toast-stack/markdown): Stack short results at the edge until you reach for them.
- [Toast](https://uiarc.dev/components/toast/markdown): Brief confirmation for a completed background action.
- [Swipe actions](https://uiarc.dev/components/swipe-actions/markdown): Reveal row actions with a swipe, or from the same actions in a menu.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.
- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.

## Also in messages

- [Alert](https://uiarc.dev/components/alert/markdown): A persistent message that helps people recover or continue.
- [Announcement bar](https://uiarc.dev/components/announcement-bar/markdown): A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
- [Cookie consent](https://uiarc.dev/components/cookie-consent/markdown): A consent bar that morphs into preferences and folds into a reopen button.

## Guidance for AI tools

Undo toast: Archive or delete rows with a second chance: the gap closes, a countdown toast merges repeat actions, and Undo or ⌘Z puts every row back exactly where it was. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

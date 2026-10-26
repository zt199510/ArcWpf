# Toast

> Brief confirmation for a completed background action.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/toast
- Markdown: https://uiarc.dev/components/toast/markdown
- Registry item: https://uiarc.dev/r/toast.json
- Source file: `registry/components/toast/toast.tsx`
- Dependencies: motion, lucide-react
- Keywords: status, message, react toast, toast notification, success toast, swipe to dismiss toast, animated toast, snackbar

## When to use

- A single success confirmation, like Changes saved, controlled with local state.
- Demos or small apps that do not need a toast queue.

## When not to use

- Use toast-stack for app-wide notifications with types, actions, updates, and stacking.
- Use alert for persistent messages that should stay in the page flow.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/toast
pnpm dlx shadcn@latest add @uiarc/toast
yarn dlx shadcn@latest add @uiarc/toast
bunx --bun shadcn@latest add @uiarc/toast
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/toast.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/toast/toast.tsx`

   The source is in the registry item: https://uiarc.dev/r/toast.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import Toast from "@/components/arc/toast/toast";

export function SavedToast() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>Save</button>
      <Toast open={open} onOpenChange={setOpen} title="Changes saved" description="Synced to all devices." />
    </>
  );
}
```

## API reference

### Toast

A single swipeable notification with a drawn check, auto-dismiss, and morphing copy. Default export.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Headline. Changes crossfade while shown. |
| `description` | `string` | – | Supporting line. |
| `open` | `boolean` | `true` | Whether the toast is shown. Setting it true again after a dismiss re-raises it. |
| `onOpenChange` | `(open: boolean) => void` | – | Called with false on close, swipe, or timeout. Auto-dismiss only runs when this is provided. |
| `duration` | `number` | `4500` | Milliseconds before auto-dismiss. 0 or less disables it. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab then Enter | Reaches and activates the dismiss button. |

## Accessibility

- Renders role="status" with aria-live="polite" and aria-atomic, so new content is announced without stealing focus.
- The dismiss button is labelled "Dismiss notification"; the check icon is aria-hidden.
- Positioning is up to the caller; the auto-dismiss timer does not pause on hover, so keep messages short.

## Motion

- Enters rising 16px from 0.96 scale on a morph spring while the check draws in; a swipe past 80px or 480px/s throws it off with its release velocity.
- Closing sinks it 8px and fades. Copy changes rise in while the height springs to fit.
- Reduced motion uses opacity fades and disables dragging.

## Responsive behavior

- The toast is min(100%, 26rem) wide and the title ellipsizes on one line.
- Positioning is up to you; fix it to the bottom center on phones so it clears the thumb zone edges.
- It swipes horizontally on touch and mouse; drag is disabled under reduced motion.

## Performance

- Drag and throw run on motion values, and a ResizeObserver springs the height when copy changes.
- The auto-dismiss timer does not pause on hover, so keep messages short.

## Notes for AI

- Use for a single success confirmation you control with local state. Use toast-stack for an app-wide queue with types, actions, updates, and stacking.
- Import as a default export. Position it yourself, for example fixed to the bottom of the viewport.

## Related

- [Toast stack](https://uiarc.dev/components/toast-stack/markdown): Stack short results at the edge until you reach for them.
- [Alert](https://uiarc.dev/components/alert/markdown): A persistent message that helps people recover or continue.

## Also in messages

- [Announcement bar](https://uiarc.dev/components/announcement-bar/markdown): A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
- [Cookie consent](https://uiarc.dev/components/cookie-consent/markdown): A consent bar that morphs into preferences and folds into a reopen button.

## Guidance for AI tools

Toast: Brief confirmation for a completed background action. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

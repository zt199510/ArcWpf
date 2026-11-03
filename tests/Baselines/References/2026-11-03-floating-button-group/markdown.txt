# Floating button group

> Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/floating-button-group
- Markdown: https://uiarc.dev/components/floating-button-group/markdown
- Registry item: https://uiarc.dev/r/floating-button-group.json
- Source file: `registry/components/floating-button-group/floating-button-group.tsx`
- Dependencies: motion, lucide-react, @radix-ui/react-tooltip
- Keywords: action, group, toolbar, new, button group, floating toolbar, action bar, editor toolbar, selection toolbar, tools rail, hover highlight, shared highlight, icon buttons

## When to use

- A floating editor or canvas toolbar with undo, redo, share and present.
- A selection toolbar that appears over text or objects.
- A vertical tools rail with zoom and insert actions.

## When not to use

- Use toggle-group for formatting toggles or alignment where the choice stays pressed.
- Use segmented-control to switch between views.
- Use split-button when there is one main action with a few alternatives.
- Use a dropdown menu when there are more actions than fit in one row.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/floating-button-group
pnpm dlx shadcn@latest add @uiarc/floating-button-group
yarn dlx shadcn@latest add @uiarc/floating-button-group
bunx --bun shadcn@latest add @uiarc/floating-button-group
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/floating-button-group.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react @radix-ui/react-tooltip
```

2. Copy the source into your project. Main file: `registry/components/floating-button-group/floating-button-group.tsx`

   The source is in the registry item: https://uiarc.dev/r/floating-button-group.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Link2, MessageCircle, Play, Redo2, Undo2 } from "lucide-react";
import { FloatingButtonGroup } from "@/components/arc/floating-button-group/floating-button-group";

export function BoardToolbar({ history, copied, copyLink, presenting, onPresent }: BoardToolbarProps) {
  return (
    <FloatingButtonGroup
      label="Board actions"
      variant="floating"
      items={[
        { id: "undo", label: "Undo", icon: <Undo2 />, iconOnly: true, shortcut: "⌘Z", disabled: !history.canUndo, onSelect: history.undo },
        { id: "redo", label: "Redo", icon: <Redo2 />, iconOnly: true, shortcut: "⇧⌘Z", disabled: !history.canRedo, onSelect: history.redo },
        { type: "separator" },
        { id: "comment", label: "Comment", icon: <MessageCircle />, shortcut: "C" },
        { id: "share", label: copied ? "Copied" : "Share", reserveLabels: ["Share", "Copied"], icon: <Link2 />, onSelect: copyLink },
        { type: "separator" },
        { id: "present", label: presenting ? "Stop" : "Present", reserveLabels: ["Present", "Stop"], icon: <Play />, pressed: presenting, onSelect: onPresent },
      ]}
      onAction={id => track("toolbar", id)}
    />
  );
}
```

## API reference

### FloatingButtonGroup

Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place. An action toolbar: each button does something; nothing stays selected. For a set of options where one or more stay chosen, use toggle-group.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `({ id: string; label: string; icon?: ReactNode; iconOnly?: boolean; onSelect?: () => void; disabled?: boolean; pressed?: boolean; shortcut?: string; reserveLabels?: string[] } \| { type: "separator"; id?: string })[]` | – | The buttons in order. A separator draws a short rule between logical clusters. label is the visible text and the accessible name; iconOnly moves it into a tooltip. pressed marks an action that switches a mode on, such as Present, with aria-pressed and a quiet tint. shortcut is shown in the tooltip only; the component does not bind the key. reserveLabels lists the other labels an item can switch to, such as ["Share", "Copied"]: the button always has the width of the widest, so a label change never resizes the button, its neighbours or the tray. |
| `label` (required) | `string` | – | Accessible name of the toolbar, such as "Board actions". |
| `variant` | `"muted" \| "floating"` | `"muted"` | muted sits quietly on a page on the muted surface. floating raises the tray with a hairline edge and the floating shadow, for overlays such as a selection or canvas toolbar. |
| `size` | `"sm" \| "md"` | `"md"` | Button height of 30 or 36px, with matching tray padding, gaps and corner radius. |
| `orientation` | `"horizontal" \| "vertical"` | `"horizontal"` | A row or a column, such as a tools rail. Arrow keys follow the orientation. |
| `iconOnly` | `boolean` | `false` | Shows every item with an icon as an icon only. An item's own iconOnly wins, so a toolbar can mix icon buttons with labelled ones. |
| `tooltipSide` | `"top" \| "bottom" \| "left" \| "right"` | `"top" for a row, "right" for a column` | Where item tooltips open. Top and bottom use the library Tooltip; left and right open beside a column without covering its neighbours. |
| `onAction` | `(id: string) => void` | – | Called with the item id after the item's own onSelect, for handling every action in one place. |
| `className` | `string` | – | Extra class on the tray. |
| `style` | `CSSProperties` | – | Inline styles on the tray. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves into the toolbar on one button, the last one focused, and out again. The highlight appears on the focused button. |
| Arrow left / Arrow right | In a row: moves focus to the previous or next enabled button, wrapping at the ends. The highlight follows. |
| Arrow up / Arrow down | In a column: moves focus to the previous or next enabled button, wrapping at the ends. |
| Home / End | Moves focus to the first or last enabled button. |
| Enter / Space | Runs the focused action. The highlight darkens while the key is held. |

## Accessibility

- The tray is a toolbar with aria-label and aria-orientation, and a single tab stop that roves between buttons.
- Every button has an accessible name from its label, also when it shows only an icon. Icon only buttons and buttons with a shortcut get a tooltip with the label and shortcut, which screen readers hear as the description.
- Mode actions set aria-pressed. Disabled actions use aria-disabled, stay readable with their tooltip, and are skipped by the arrow keys.
- Separators have the separator role with the orientation across the toolbar.
- Pointer focus draws no ring; keyboard focus shows Arc's shared focus ring. The shared highlight is the keyboard indicator, shown only for keyboard focus so a mouse click never leaves it behind.
- When a label changes, such as Share to Copied, the accessible name changes with it and the outgoing label is hidden from assistive technology while it fades. Reserved label ghosts are hidden from assistive technology.

## Motion

- One highlight per tray, driven by motion values so moving it never re-renders the buttons. It travels in position and size to the hovered or focused button on the morph spring, and stays put while the pointer crosses a gap or a separator.
- Appearing from nothing it lands in place and grows in from 94 percent on the snappy spring, instead of sliding from wherever it last was. It fades when the pointer leaves the tray.
- Pressing darkens the highlight and settles it to 96.5 percent in place on the snappy spring; the buttons themselves never scale, so neighbours and tooltip anchors stay still.
- A new label crossfades in place with a short rise and blur. With reserveLabels the button already has the widest label's width, so nothing around it moves; without it, the highlight re-measures and follows the new width.
- Nothing but the highlight ever moves: hover, focus, press and label changes leave the tray and every button at the same box. The highlight is measured exactly in tray coordinates, snapped to device pixels and corrected for ancestor transforms.
- The pointer leaving the tray is detected with a native listener, because React bubbles pointer events through portals and a tooltip would otherwise count as inside the tray.
- With reduced motion the highlight jumps to its button and fades briefly, and labels crossfade without travel.

## Responsive behavior

- The tray keeps its natural size and never wraps or clips a button. At narrow widths make secondary items icon only; to switch without a layout shift after hydration, render both densities and pick one with a container query, as the demo does below 360px.
- Touch shows the highlight under the finger while pressed and fades it on release; hover only tracks a mouse or pen.
- The highlight re-measures when the tray or any button resizes, so it stays aligned through font loading, label changes and container reflow.

## Performance

- The highlight moves on transform and size through motion values without React renders; only hover and focus changes re-render the buttons.
- One ResizeObserver per tray, observing the tray and its buttons. Reserved labels are plain CSS grid ghosts, measured by layout, not by script.

## Notes for AI

- Use it for actions, not selection. When one or more options stay chosen, use toggle-group or segmented-control.
- Group related actions with separators and keep a toolbar to about seven buttons. Make secondary actions icon only before dropping labels from the main one.
- Give every icon only item a specific label, such as "Zoom to fit", not "Fit".
- shortcut is a hint only. Bind the key yourself, scoped so it does not fight the page.
- Pass reserveLabels whenever an item's label changes, so the toolbar never shifts.
- Use variant floating when the toolbar sits over content such as a canvas or a text selection; use muted in a page or a panel.

## Related

- [Toggle group](https://uiarc.dev/components/toggle-group/markdown): A row of icon or text toggles, single or multiple: one pressed fill glides between options, or each fades in on its own and neighbours join, as in a formatting toolbar.
- [Button group](https://uiarc.dev/components/button-group/markdown): Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Expanding button group](https://uiarc.dev/components/expanding-button-group/markdown): Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.

## Also in buttons

- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.
- [Copy button](https://uiarc.dev/components/copy-button/markdown): Copy a value with immediate confirmation.
- [Action swap](https://uiarc.dev/components/action-swap/markdown): Swap an action label while preserving its place and intent.
- [Icon morph](https://uiarc.dev/components/icon-morph/markdown): Icons that morph between states: menu to close, play to pause, copy to check, and more.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.

## Guidance for AI tools

Floating button group: Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

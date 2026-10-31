# Time picker

> Choose a time with sensible keyboard behavior.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/time-picker
- Markdown: https://uiarc.dev/components/time-picker/markdown
- Registry item: https://uiarc.dev/r/time-picker.json
- Source file: `registry/components/time-picker/time-picker.tsx`
- Dependencies: motion, lucide-react
- Keywords: time, field, react time picker, time select, time input, meeting time picker, 12 hour time picker, time dropdown

## When to use

- Picking a time of day at a fixed interval, such as meetings or reminders.
- Fields that show 12-hour times but store 24-hour HH:mm values.

## When not to use

- Use date-picker for dates, pairing the two for a timestamp.
- Use time-dial for a more tactile, showcase time selection.
- Use select when only a few named times are allowed.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/time-picker
pnpm dlx shadcn@latest add @uiarc/time-picker
yarn dlx shadcn@latest add @uiarc/time-picker
bunx --bun shadcn@latest add @uiarc/time-picker
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/time-picker.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/time-picker/time-picker.tsx`

   The source is in the registry item: https://uiarc.dev/r/time-picker.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { TimePicker } from "@/components/arc/time-picker/time-picker";

export function MeetingTime() {
  const [time, setTime] = useState("14:30");
  return <TimePicker label="Start time" value={time} onChange={setTime} minuteStep={30} />;
}
```

## API reference

### TimePicker

A trigger with a scrolling listbox of times at a fixed minute step; the shown time rolls like a clock.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label. |
| `value` | `string` | – | Controlled time as "HH:mm" (24-hour). |
| `defaultValue` | `string` | `"09:00"` | Initial time when uncontrolled. |
| `onChange` | `(value: string) => void` | – | Called with the new "HH:mm" value. |
| `description` | `string` | – | Helper copy under the field. |
| `placeholder` | `string` | `"Select a time"` | Shown when the value is empty. |
| `minuteStep` | `1 \| 5 \| 10 \| 15 \| 30` | `15` | Spacing between options. |
| `format` | `"12h" \| "24h"` | `"12h"` | Display format. Values stay 24-hour. |
| `disabled` | `boolean` | `false` | Disables the trigger. |
| `className` | `string` | – | Added to the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Opens the list, or selects the active time when open. |
| ArrowDown / ArrowUp | Opens the list, then moves through times, wrapping. |
| Escape | Closes the list. |

## Accessibility

- Trigger has aria-haspopup="listbox", aria-expanded, aria-controls, and aria-labelledby combining the label and a hidden value.
- The menu is role="listbox" with role="option" items carrying aria-selected.
- The list opens centered on the selected time and scrolls only itself, never the page.

## Motion

- A later time rises in from below and an earlier one drops from above with a soft blur; the menu springs in from slightly above.
- Reduced motion crossfades the value and opens the menu with opacity only.

## Responsive behavior

- The list spans the field width and caps at 250px, scrolling itself with contained overscroll.
- It closes on any pointerdown outside the field.

## Performance

- All options at the chosen step render at once; minuteStep 1 means 1,440 rows, so prefer 5 or more.

## Notes for AI

- Use for picking a time of day at a fixed interval. Pair with date-picker for a full timestamp.
- Controlled or uncontrolled; values are always 24-hour "HH:mm" strings regardless of display format.

## Related

- [Date picker](https://uiarc.dev/components/date-picker/markdown): Choose a date without losing context.
- [Calendar](https://uiarc.dev/components/calendar/markdown): Browse dates in a clear, compact month view.
- [Select](https://uiarc.dev/components/select/markdown): A compact choice field with a keyboard friendly menu.
- [Time dial](https://uiarc.dev/components/time-dial/markdown): Spin a dial to pick an hour and see which team cities are at work.

## Also in pickers

- [Date range picker](https://uiarc.dev/components/date-range-picker/markdown): A range picker that grows from its trigger into two months with presets and a stretching range highlight.
- [Color picker](https://uiarc.dev/components/color-picker/markdown): A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.
- [Emoji picker](https://uiarc.dev/components/emoji-picker/markdown): A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid.

## Guidance for AI tools

Time picker: Choose a time with sensible keyboard behavior. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

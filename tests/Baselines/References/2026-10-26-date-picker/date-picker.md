# Date picker

> Choose a date without losing context.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/date-picker
- Markdown: https://uiarc.dev/components/date-picker/markdown
- Registry item: https://uiarc.dev/r/date-picker.json
- Source file: `registry/components/date-picker/date-picker.tsx`
- Dependencies: motion, lucide-react
- Keywords: date, field, react date picker, datepicker, date input with calendar, calendar popover, form date field, animated date picker

## When to use

- A date field in a form, such as a start or due date.
- Dates with limits or blocked days, shown in a locale format.

## When not to use

- Use calendar inline when the grid should stay visible.
- Use time-picker for times, and pair it with this for a full timestamp.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/date-picker
pnpm dlx shadcn@latest add @uiarc/date-picker
yarn dlx shadcn@latest add @uiarc/date-picker
bunx --bun shadcn@latest add @uiarc/date-picker
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/date-picker.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/date-picker/date-picker.tsx`

   The source is in the registry item: https://uiarc.dev/r/date-picker.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { DatePicker } from "@/components/arc/date-picker/date-picker";

export function StartDateField() {
  const [start, setStart] = useState<Date>();
  return (
    <DatePicker
      label="Start date"
      value={start}
      onChange={setStart}
      minDate={new Date()}
      showToday
    />
  );
}
```

## API reference

### DatePicker

A field button that opens Calendar in a popover and shows the date with per-part rolling.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label tied to the trigger. |
| `value` | `Date` | – | Selected date. |
| `onChange` | `(date: Date \| undefined) => void` | – | Called on pick and with undefined from the Clear button. |
| `description` | `string` | – | Helper copy under the field. |
| `placeholder` | `string` | `"Select a date"` | Shown with no value. |
| `minDate` | `Date` | – | Earliest selectable day. |
| `maxDate` | `Date` | – | Latest selectable day. |
| `disabledDates` | `(date: Date) => boolean` | – | Return true to block a day. |
| `locale` | `string` | `"en-US"` | Formatting and calendar locale. |
| `format` | `Intl.DateTimeFormatOptions` | `{ month: "short", day: "numeric", year: "numeric" }` | How the value is shown. |
| `showToday` | `boolean` | – | Adds a Today button to the calendar header. |
| `...props` | `Omit<ButtonHTMLAttributes<HTMLButtonElement>, "value" \| "onChange">` | – | Forwarded to the trigger button, including id, disabled, and className (applied to the root). |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space / ArrowDown | Opens the calendar and focuses the selected or current day. |
| Escape | Closes the popover and returns focus to the trigger. |
| Calendar keys | Arrows, Home, End, PageUp, and PageDown navigate days as in Calendar. |

## Accessibility

- Trigger has aria-haspopup="dialog" and aria-expanded; the popover is role="dialog" labelled "<label> calendar".
- The formatted value lives in a visually hidden span; the rolling text is aria-hidden.
- Tabbing out or clicking outside closes it, and focus returns to the trigger when it was inside.

## Motion

- Changed date parts roll up for a later date and down for an earlier one; the popover springs in and closes about 240 ms after a pick so the highlight can land.
- Reduced motion crossfades the value and closes immediately after a pick.

## Responsive behavior

- The popover is min(320px, 100vw minus 32px) wide, left-aligned to the field.
- Below 360px of viewport the popover stretches to both field edges.
- It closes on any pointerdown outside, so a tap elsewhere dismisses it.

## Performance

- The calendar mounts only while open, and changed date parts roll with layout position animation.

## Notes for AI

- Use for a date field in forms. Use calendar inline when the grid should stay visible, and time-picker for times.
- value is controlled only. There is no name prop; serialize the Date into a hidden input for plain forms.

## Related

- [Calendar](https://uiarc.dev/components/calendar/markdown): Browse dates in a clear, compact month view.
- [Time picker](https://uiarc.dev/components/time-picker/markdown): Choose a time with sensible keyboard behavior.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.

## Also in pickers

- [Date range picker](https://uiarc.dev/components/date-range-picker/markdown): A range picker that grows from its trigger into two months with presets and a stretching range highlight.
- [Color picker](https://uiarc.dev/components/color-picker/markdown): A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.
- [Emoji picker](https://uiarc.dev/components/emoji-picker/markdown): A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid.

## Guidance for AI tools

Date picker: Choose a date without losing context. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

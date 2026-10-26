# Calendar

> Browse dates in a clear, compact month view.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/calendar
- Markdown: https://uiarc.dev/components/calendar/markdown
- Registry item: https://uiarc.dev/r/calendar.json
- Source file: `registry/components/calendar/calendar.tsx`
- Dependencies: motion, lucide-react
- Keywords: date, field, react calendar, date calendar, month calendar, inline date picker, booking calendar, animated calendar

## When to use

- Inline date picking where the month should stay visible, such as booking pages.
- Picking one date with blocked days via minDate, maxDate, and disabledDates.

## When not to use

- Use date-picker for a form field that opens a calendar popover.
- Use time-picker for times of day.
- Use booking-pill for a full compact booking flow.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/calendar
pnpm dlx shadcn@latest add @uiarc/calendar
yarn dlx shadcn@latest add @uiarc/calendar
bunx --bun shadcn@latest add @uiarc/calendar
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/calendar.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/calendar/calendar.tsx`

   The source is in the registry item: https://uiarc.dev/r/calendar.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Calendar } from "@/components/arc/calendar/calendar";

export function DeliveryDate() {
  const [date, setDate] = useState<Date>();
  return (
    <Calendar
      value={date}
      onChange={setDate}
      minDate={new Date()}
      disabledDates={(day) => day.getDay() === 0}
      showToday
    />
  );
}
```

## API reference

### Calendar

A month grid for picking one date, with sliding month changes and a gliding selection.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `Date` | – | Selected date. |
| `onChange` | `(date: Date) => void` | – | Called when a day is picked. |
| `month` | `Date` | – | Controlled visible month. |
| `onMonthChange` | `(month: Date) => void` | – | Called with the first of the new month when navigating. |
| `minDate` | `Date` | – | Earliest selectable day. |
| `maxDate` | `Date` | – | Latest selectable day. |
| `disabledDates` | `(date: Date) => boolean` | – | Return true to block a day, such as weekends. |
| `locale` | `string` | `"en-US"` | Month, weekday, and day names. |
| `className` | `string` | – | Added to the root section. |
| `showToday` | `boolean` | `false` | Adds a Today button that returns to the current month and selects today when allowed. |

### useToday

Hook returning the viewer's local date, undefined during SSR and hydration, and updating across midnight.

No props.

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Arrow keys | Move by one day or one week, skipping blocked days and crossing months. |
| Home / End | Move to the start or end of the week. |
| PageUp / PageDown | Move by one month; with Shift, by one year. |
| Enter / Space | Selects the focused day. |

## Accessibility

- Days sit in a role="grid" with role="row" and role="gridcell" buttons carrying full date aria-labels, aria-selected, and aria-current="date" for today.
- Roving tabindex keeps one day in the tab order; month changes are announced through a polite live region.
- Previous and next buttons are labelled and use aria-disabled at the min or max month.

## Motion

- Months slide in the direction of travel while the title letters and year digits roll; the selection highlight glides between days on the morph spring.
- Reduced motion, applied after hydration, swaps months and moves the highlight instantly.

## Responsive behavior

- The calendar is min(100%, 328px) wide and days are square cells that scale with it.
- The header is a container query: below 260px the title takes its own row and the controls move under it.

## Performance

- Only the incoming month is observed with a ResizeObserver, to morph between four, five, and six rows.
- Each day is a button, so a month is about 42 cells; render one calendar rather than many side by side.

## Notes for AI

- Use inline where the calendar is always visible, such as booking pages. Use date-picker for a form field that opens a calendar popover.
- value is controlled only; month can be controlled or left internal.
- Also exports date helpers addDays, addMonths, sameDay, and startOfDay, and the CalendarDateMatcher type.

## Related

- [Date picker](https://uiarc.dev/components/date-picker/markdown): Choose a date without losing context.
- [Time picker](https://uiarc.dev/components/time-picker/markdown): Choose a time with sensible keyboard behavior.
- [Booking pill](https://uiarc.dev/components/booking-pill/markdown): One pill that reshapes through party size, date, time, and a ticket.

## Also in pickers

- [Date range picker](https://uiarc.dev/components/date-range-picker/markdown): A range picker that grows from its trigger into two months with presets and a stretching range highlight.
- [Color picker](https://uiarc.dev/components/color-picker/markdown): A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.
- [Emoji picker](https://uiarc.dev/components/emoji-picker/markdown): A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid.

## Guidance for AI tools

Calendar: Browse dates in a clear, compact month view. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

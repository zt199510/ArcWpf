# Date range picker

> A range picker that grows from its trigger into two months with presets and a stretching range highlight.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/date-range-picker
- Markdown: https://uiarc.dev/components/date-range-picker/markdown
- Registry item: https://uiarc.dev/r/date-range-picker.json
- Source file: `registry/components/date-range-picker/date-range-picker.tsx`
- Dependencies: motion, lucide-react
- Keywords: inputs, new, react date range picker, date range, calendar range selection, analytics date filter, start and end date, date presets, booking date picker

## When to use

- Analytics and reporting filters that need presets like Last 7 days or This quarter.
- Booking or leave requests where a start and end day are picked together.
- Toolbars where the picker must stay compact until opened.

## When not to use

- Use date-picker for a single date.
- Use calendar when the month should stay visible on the page.
- Use time-picker when the user chooses a time of day rather than days.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/date-range-picker
pnpm dlx shadcn@latest add @uiarc/date-range-picker
yarn dlx shadcn@latest add @uiarc/date-range-picker
bunx --bun shadcn@latest add @uiarc/date-range-picker
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/date-range-picker.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/date-range-picker/date-range-picker.tsx`

   The source is in the registry item: https://uiarc.dev/r/date-range-picker.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { DateRangePicker } from "@/components/arc/date-range-picker/date-range-picker";
import type { DateRange } from "@/components/arc/date-range-picker/date-range-picker";

export function ReportRange() {
  const [range, setRange] = useState<DateRange | null>(null);
  return (
    <DateRangePicker
      label="Report period"
      value={range}
      onChange={setRange}
      weekStartsOn={1}
      maxDate={new Date()}
    />
  );
}
```

## API reference

### DateRangePicker

A trigger that grows into a panel with one or two months and a preset rail, then shrinks back with the applied range.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `DateRange \| null` | – | Controlled value, { start, end } as inclusive whole days. Pass null for no selection. |
| `defaultValue` | `DateRange \| null` | `null` | Uncontrolled starting value. |
| `onChange` | `(range: DateRange) => void` | – | Called with the range on Apply. |
| `label` | `string` | `"Date range"` | Accessible name of the trigger and the dialog. |
| `placeholder` | `string` | `"Select dates"` | Trigger text with no selection. |
| `presets` | `DateRangePreset[]` | `defaultDateRangePresets` | Shortcuts: { label, range: (today) => DateRange }. Defaults to Today through Year to date. |
| `minDate` | `Date` | – | Earliest selectable day. |
| `maxDate` | `Date` | – | Latest selectable day. |
| `weekStartsOn` | `0 \| 1` | `0` | 0 is Sunday, 1 is Monday. |
| `locale` | `string` | `"en-US"` | Locale for Intl.DateTimeFormat labels and month titles. |
| `months` | `"auto" \| 1 \| 2` | `"auto"` | Force one or two months. auto shows two when the boundary is at least 712px wide. |
| `boundary` | `() => HTMLElement \| null` | – | Element the panel should stay inside. Defaults to the viewport. |
| `className` | `string` | – | Extra class on the root. |

### useToday

Hook returning the viewer's local date, or undefined during server render and hydration. Updates at local midnight and when the tab becomes visible.

No props.

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Opens the panel from the trigger; picks a start or end day in the grid. |
| Arrow keys | Move by day or week in the grid, extending the range preview once a start is picked. |
| Home / End | Jump to the start or end of the week. |
| PageUp / PageDown | Move by month; add Shift to move by year. |
| ArrowUp / ArrowDown | Move between presets in the rail; ArrowLeft and ArrowRight in the compact row. |
| Escape | Closes without applying and returns focus to the trigger. |

## Accessibility

- The trigger has aria-haspopup="dialog", aria-expanded, and a label that includes the current range.
- Each month is a role="grid" labelled by its title, with columnheaders and gridcells using aria-selected for days in range and aria-current="date" for today.
- Days have full date labels and a roving tabindex; months that are leaving turn inert so focus only finds the current set.
- A polite live region announces the start day, then the range and its length.

## Motion

- The trigger surface grows into the panel on a physical spring and shrinks back on Apply while the formatted label flies into the trigger with a shared layoutId.
- The range highlight is one bar per week that stretches as you hover, and the two ends glide between days.
- Months slide in the direction of travel, and label words roll up or down with time's direction.
- Reduced motion jumps the surface size, drops the shared layout and slide, and uses short opacity fades.

## Responsive behavior

- months="auto" shows two months beside a vertical preset rail when the boundary is at least 712px wide, otherwise one month under a scrolling preset row.
- In compact mode the panel width is min(352px, available) and day cells size between 32 and 42px to fit.
- The panel shifts horizontally to stay 8px inside the boundary, and recomputes on window resize.

## Performance

- ResizeObservers on the trigger and panel feed the size springs; updates are batched through a microtask.
- Each week's highlight and each end is a motion value animation, so hovering does not re-render the grid beyond the range change.
- Intl.DateTimeFormat instances are memoized per locale.

## Notes for AI

- Use when a start and end date are chosen together: report filters, analytics periods, bookings.
- Selection is draft until Apply; onChange fires only on Apply. Cancel, Escape, or an outside click discards the draft.
- Pass custom presets as { label, range: today => ({ start, end }) }; today is the viewer's local date.
- The trigger is disabled until hydration because today is unknown on the server.
- For a single date use date-picker; for an always-visible month use calendar.

## Related

- [Date picker](https://uiarc.dev/components/date-picker/markdown): Choose a date without losing context.
- [Calendar](https://uiarc.dev/components/calendar/markdown): Browse dates in a clear, compact month view.
- [Time picker](https://uiarc.dev/components/time-picker/markdown): Choose a time with sensible keyboard behavior.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.

## Also in pickers

- [Color picker](https://uiarc.dev/components/color-picker/markdown): A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.
- [Emoji picker](https://uiarc.dev/components/emoji-picker/markdown): A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid.

## Guidance for AI tools

Date range picker: A range picker that grows from its trigger into two months with presets and a stretching range highlight. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

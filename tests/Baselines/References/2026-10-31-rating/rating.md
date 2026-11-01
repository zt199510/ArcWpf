# Rating

> A simple, accessible way to capture a numeric opinion.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/rating
- Markdown: https://uiarc.dev/components/rating/markdown
- Registry item: https://uiarc.dev/r/rating.json
- Source file: `registry/components/rating/rating.tsx`
- Dependencies: motion, lucide-react
- Keywords: input, feedback, react star rating, rating component, half star rating, review stars, accessible rating, 5 star rating

## When to use

- Collecting a 1 to N score in reviews or feedback forms.
- Displaying average scores, with readOnly and allowHalf.
- Forms that submit natively, via name.

## When not to use

- Use slider for continuous values.
- Use reactions for emoji feedback on content.
- Use radio-group when options are labelled choices rather than a scale.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/rating
pnpm dlx shadcn@latest add @uiarc/rating
yarn dlx shadcn@latest add @uiarc/rating
bunx --bun shadcn@latest add @uiarc/rating
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/rating.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/rating/rating.tsx`

   The source is in the registry item: https://uiarc.dev/r/rating.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Rating } from "@/components/arc/rating/rating";

export function ReviewForm() {
  const [stars, setStars] = useState(0);
  return (
    <form>
      <Rating label="Overall experience" value={stars} onChange={setStars} name="rating" />
    </form>
  );
}
```

## API reference

### Rating

A star rating built as a radio group, with half stars, scrubbing, clearing, and a rolling value label.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label and accessible name of the group. |
| `value` | `number` | – | Controlled value. 0 means not rated. |
| `defaultValue` | `number` | `0` | Initial value when uncontrolled. |
| `onChange` | `(value: number) => void` | – | Receives the committed value; 0 means cleared. |
| `max` | `number` | `5` | Number of stars. |
| `readOnly` | `boolean` | `false` | Renders a static image with an aria-label instead of radios. |
| `disabled` | `boolean` | `false` | Disables interaction. |
| `description` | `string` | – | Hint under the stars, linked with aria-describedby. |
| `size` | `"sm" \| "md" \| "lg"` | `"md"` | Star size. |
| `allowHalf` | `boolean` | `false` | Pointer, touch, and arrow keys move in half stars. |
| `allowClear` | `boolean` | `true` | Choosing the current value again clears it; Backspace and Delete clear from the keyboard. |
| `showValue` | `boolean` | `true` | Shows the word or number for the previewed value beside the stars. |
| `valueLabels` | `readonly string[]` | – | One word per whole star. Five whole stars default to Poor through Excellent. |
| `emptyLabel` | `string` | `"Not rated"` | Shown when the value is 0. |
| `name` | `string` | – | Submits the value with a surrounding form via a hidden input. |
| `className` | `string` | – | Class on the field wrapper. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowRight / ArrowUp | Increases by one step (half a star with allowHalf). |
| ArrowLeft / ArrowDown | Decreases by one step. |
| Home / End | Jumps to the lowest or highest value. |
| 1-9 | Sets that many stars. |
| Backspace / Delete | Clears the rating when allowClear is on. |

## Accessibility

- Interactive ratings are role="radiogroup" with one role="radio" per step and a roving tab stop on the checked value.
- Each radio is labelled like "Great, 4 of 5 stars"; commits are announced in a polite live region.
- readOnly renders role="img" with an aria-label such as "Quality: 4 out of 5".

## Motion

- The fill sweeps star by star with a small stagger in the direction the value moved; a commit pops the stars in a wave.
- Pressed stars dip to 0.86 scale and the value word rolls in the direction of change.
- Reduced motion sets fills instantly and drops the pop, press scale, and word roll.

## Responsive behavior

- Stars sit in 36px cells for easy touch targets, and the value label wraps under the stars when space runs out.
- Touch presses scrub across stars with pointer capture; touch-action pan-y keeps vertical scrolling working.

## Performance

- Fills and pops are short motion animations per star; fine for a form, but use readOnly for long lists of scores.

## Notes for AI

- Use for collecting or showing a 1 to N score. Use slider for continuous values and reactions for emoji feedback.
- Pass name to submit with a native form. Use readOnly to display averages; combine with allowHalf for fractional values.

## Related

- [Slider](https://uiarc.dev/components/slider/markdown): Pick a value or a range on a track that follows your finger.
- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.
- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.

## Guidance for AI tools

Rating: A simple, accessible way to capture a numeric opinion. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

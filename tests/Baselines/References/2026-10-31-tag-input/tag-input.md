# Tag input

> Turn short text values into removable tags.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/tag-input
- Markdown: https://uiarc.dev/components/tag-input/markdown
- Registry item: https://uiarc.dev/r/tag-input.json
- Source file: `registry/components/tag-input/tag-input.tsx`
- Dependencies: motion, lucide-react
- Keywords: field, tags, react tag input, tags input, chip input, multi value input, email chips input, keyword input

## When to use

- Free-form lists of short values like tags, keywords, or emails.
- Fields where people add values by pressing Enter or comma.

## When not to use

- Use multi-select or chip-group when values come from a fixed list.
- Use input for a single value.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/tag-input
pnpm dlx shadcn@latest add @uiarc/tag-input
yarn dlx shadcn@latest add @uiarc/tag-input
bunx --bun shadcn@latest add @uiarc/tag-input
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/tag-input.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/tag-input/tag-input.tsx`

   The source is in the registry item: https://uiarc.dev/r/tag-input.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { TagInput } from "@/components/arc/tag-input/tag-input";

export function TopicsField() {
  const [topics, setTopics] = useState(["design", "motion"]);
  return (
    <TagInput
      label="Topics"
      value={topics}
      onValueChange={setTopics}
      description="Press Enter or comma to add."
    />
  );
}
```

## API reference

### TagInput

A field that turns typed text into removable tags, with keyboard picking and a gliding selection ring.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label tied to the text input. |
| `value` | `string[]` | – | Controlled tags. |
| `defaultValue` | `string[]` | `[]` | Initial tags when uncontrolled. |
| `onValueChange` | `(value: string[]) => void` | – | Called with the full list after each add or remove. |
| `placeholder` | `string` | `"Add a tag"` | Shown while there are no tags. |
| `description` | `string` | – | Helper copy under the field. |
| `id` | `string` | – | Input id. Generated when omitted. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / , | Adds the typed text as a tag. Duplicates (case-insensitive) pulse the existing tag instead. |
| Backspace | At the start of the input, picks the last tag; pressed again, removes the picked tag. |
| Delete | Removes the picked tag. |
| ArrowLeft / ArrowRight | Moves the pick between tags and back to the input. |
| Escape | Clears the pick. |

## Accessibility

- Each tag has a remove button labelled "Remove <tag>".
- Adds, removals, picks, and duplicates are announced in a polite aria-live region.
- Blurring the input commits any typed draft as a tag.

## Motion

- New tags blur and scale in where the text was typed; removed tags leave and the rest glide on the morph spring. The shell follows wrapped rows on a smooth spring.
- A ring glides between picked tags. Reduced motion removes layout travel, scale, and blur, keeping fades.

## Responsive behavior

- Tags wrap onto new rows and the shell height follows on a spring, so the field never scrolls horizontally.
- The text input keeps an 80px minimum, dropping to its own row when the last row is full.

## Performance

- Every tag is a motion element with layout position animation; fine for dozens of tags, not hundreds.
- Two ResizeObservers track the content and message heights.

## Notes for AI

- Use for free-form values like tags, emails, or keywords. Use multi-select or chip-group when values come from a fixed list.
- Controlled or uncontrolled. Tags are plain strings; validate or normalize in onValueChange.

## Related

- [Multi-select](https://uiarc.dev/components/multi-select/markdown): Select several values while keeping the field readable.
- [Chip group](https://uiarc.dev/components/chip-group/markdown): Filter by a few facets with chips that morph as you pick them.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.

## Also in special inputs

- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.
- [Money input](https://uiarc.dev/components/money-input/markdown): A currency field with live grouping, stable width, rolling digits, and minor-unit output.
- [Phone input](https://uiarc.dev/components/phone-input/markdown): A phone field with a country picker, formatting as you type, and E.164 output.
- [Scrub input](https://uiarc.dev/components/scrub-input/markdown): A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
- [Shortcut recorder](https://uiarc.dev/components/shortcut-recorder/markdown): Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.

## Guidance for AI tools

Tag input: Turn short text values into removable tags. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

# Inline edit

> Rename in place: the text becomes a field without moving.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/inline-edit
- Markdown: https://uiarc.dev/components/inline-edit/markdown
- Registry item: https://uiarc.dev/r/inline-edit.json
- Source file: `registry/components/inline-edit/inline-edit.tsx`
- Dependencies: motion, lucide-react
- Keywords: inline edit, rename, click to edit, react inline edit, editable text, inline editing, rename in place, optimistic save field

## When to use

- Titles and descriptions read far more often than edited, like a project name.
- Single fields that save on their own with optimistic updates and rollback.

## When not to use

- Use input or textarea in a form when several fields save together.
- Use input when the field should always look editable.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/inline-edit
pnpm dlx shadcn@latest add @uiarc/inline-edit
yarn dlx shadcn@latest add @uiarc/inline-edit
bunx --bun shadcn@latest add @uiarc/inline-edit
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/inline-edit.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/inline-edit/inline-edit.tsx`

   The source is in the registry item: https://uiarc.dev/r/inline-edit.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { InlineEdit } from "@/components/arc/inline-edit/inline-edit";

export function ProjectTitle({ project }: { project: { id: string; name: string } }) {
  return (
    <InlineEdit
      as="h1"
      label="Project name"
      value={project.name}
      validate={(next) => (next.trim() ? null : "Name can’t be empty")}
      onSave={(next) => renameProject(project.id, next)}
    />
  );
}
```

## API reference

### InlineEdit

Click-to-edit text that becomes a field in place with the same metrics, saves optimistically, and rolls back on failure.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `string` | – | The saved value. Outside changes replace the text while not editing. |
| `onSave` (required) | `(next: string) => void \| Promise<unknown>` | – | Persists the value. Return a promise to show saving; reject it to roll back with a retry. |
| `label` (required) | `string` | – | Accessible name, for example "Project name". |
| `validate` | `(next: string) => string \| null \| undefined` | – | Returns an error message to block saving. |
| `placeholder` | `string` | `""` | Shown when the value is empty. |
| `multiline` | `boolean` | `false` | Wraps and grows in height; Shift+Enter adds a line break. |
| `variant` | `"title" \| "body"` | `"title"` | Type style: title for names and headings, body for descriptions. |
| `as` | `"span" \| "p" \| "h1" \| "h2" \| "h3"` | `"span"` | Element that holds the text, so a title can stay a heading. |
| `className` | `string` | – | Added to the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | On the text, starts editing. |
| Enter | Saves the draft. In multiline mode, Shift+Enter inserts a line break. |
| Escape | Cancels and rolls the text back. |
| Tab | Leaving the component saves, the way a rename does. |

## Accessibility

- The resting text is a native button labelled "<label>: <value>", so it is focusable and announced as editable.
- The field has aria-label and aria-invalid; validation and save errors render in a polite live region linked through aria-describedby.
- Save progress and results are announced through a role="status" region; save and cancel buttons are labelled.

## Motion

- Text rolls between values, a check draws once a save lands, and multiline boxes follow their height; failed saves roll back with motion.
- Reduced motion crossfades text, draws the check instantly, and replaces the spinner with a static mark.

## Responsive behavior

- Text wraps within the column and reserves 66px at the end for the save and cancel buttons.
- With multiline the box grows in height as the text wraps.
- Hover hints apply only on fine pointers; on touch a tap starts editing.

## Performance

- Two ResizeObservers size the frame; sizes are measured in a layout effect, so no frame shows a wrong width.

## Notes for AI

- Use for single fields read far more often than edited, like titles and descriptions. Use a regular form with input or textarea when several fields save together.
- value is controlled by the saved data; update it after onSave resolves. Return a promise from onSave to get saving, saved, and failed states.
- Exported as both named and default.

## Related

- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Toast](https://uiarc.dev/components/toast/markdown): Brief confirmation for a completed background action.

## Also in text fields

- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Password strength](https://uiarc.dev/components/password-strength/markdown): Show how strong a new password is while it is typed.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.

## Guidance for AI tools

Inline edit: Rename in place: the text becomes a field without moving. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

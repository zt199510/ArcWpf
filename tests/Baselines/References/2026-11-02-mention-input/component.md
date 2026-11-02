# Mention input

> A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/mention-input
- Markdown: https://uiarc.dev/components/mention-input/markdown
- Registry item: https://uiarc.dev/r/mention-input.json
- Source file: `registry/components/mention-input/mention-input.tsx`
- Dependencies: motion, lucide-react
- Keywords: inputs, new, react mention input, at mention textarea, mentions autocomplete, tag people in comment, channel mention, slack style mentions, mention suggestions, textarea with mentions

## When to use

- Comment and reply boxes where people are tagged with @ and should be notified by id.
- Chat or task composers that link #channels or projects inline.
- Any text field whose stored value needs structured references alongside the plain text.

## When not to use

- Use textarea for plain notes with no references.
- Use tag-input when the tokens are a separate list rather than part of a sentence.
- Use rich-text-editor when the text also needs headings, lists, or links.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/mention-input
pnpm dlx shadcn@latest add @uiarc/mention-input
yarn dlx shadcn@latest add @uiarc/mention-input
bunx --bun shadcn@latest add @uiarc/mention-input
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/mention-input.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/mention-input/mention-input.tsx`

   The source is in the registry item: https://uiarc.dev/r/mention-input.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { MentionInput, serializeMentions, type MentionValue } from "@/components/arc/mention-input/mention-input";

const people = [
  { id: "u1", name: "Maya Chen", role: "Design lead" },
  { id: "u2", name: "Theo Park", role: "Engineer" },
];
const channels = [{ id: "c1", name: "launch", description: "Release planning", members: 14 }];

export function CommentBox({ onPost }: { onPost: (body: string) => void }) {
  const [value, setValue] = useState<MentionValue>({ text: "", mentions: [] });
  return (
    <MentionInput
      aria-label="Comment"
      placeholder="Write a comment, @ to mention"
      people={people}
      channels={channels}
      value={value}
      onChange={setValue}
      submitOnEnter
      onSubmit={next => {
        onPost(serializeMentions(next));
        setValue({ text: "", mentions: [] });
      }}
    />
  );
}
```

## API reference

### MentionInput

An autosizing textarea with @ people and # channel mentions. Mentions are atomic tokens, and the value carries plain text plus a structured mentions array with offsets.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `MentionValue` | – | Controlled value: { text, mentions }. Each mention has kind, id, label, start, and end offsets into text. |
| `defaultValue` | `MentionValue` | – | Initial value when uncontrolled. Defaults to empty text and no mentions. |
| `onChange` | `(value: MentionValue) => void` | – | Called on every edit with the new text and the mentions moved or dropped to match it. |
| `people` | `MentionPerson[]` | – | People offered after @. Each has id, name, and optional role and avatar. Leave it out to turn person mentions off. |
| `channels` | `MentionChannel[]` | – | Channels offered after #. Each has id, name, and optional description and members count. Leave it out to turn channel mentions off. |
| `onMentionAdd` | `(mention: Mention) => void` | – | Called when a suggestion is chosen and becomes a mention. |
| `onSubmit` | `(value: MentionValue) => void` | – | Called on Enter when submitOnEnter is set and the text is not blank. |
| `submitOnEnter` | `boolean` | `false` | Enter submits and Shift+Enter adds a line. Enter during IME composition never submits. |
| `placeholder` | `string` | – | Placeholder text for the empty field. |
| `minRows` | `number` | `1` | Starting height in rows. |
| `maxRows` | `number` | `8` | The field grows to this many rows, then scrolls. |
| `placement` | `"auto" \| "top" \| "bottom"` | `"auto"` | Where suggestions open. auto flips above the caret when there is less than 300px below it and more room above. |
| `maxSuggestions` | `number` | `6` | Most suggestions shown at once, best matches first. |
| `disabled` | `boolean` | – | Disables the textarea and dims the field. |
| `name` | `string` | – | Forwarded to the textarea. The form value is the plain text; use serializeMentions to keep ids. |
| `id` | `string` | – | Forwarded to the textarea, for an external label. |
| `aria-label` | `string` | – | Accessible name when there is no visible label. |
| `aria-describedby` | `string` | – | Id of help or error text for the textarea. |
| `className` | `string` | – | Extra class on the root. |

### MentionInputHandle

Methods on the ref.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `focus` | `() => void` | – | Focuses the textarea. |
| `insert` | `(text: string) => void` | – | Inserts text at the caret as if typed, so it joins the native undo stack. |
| `openSuggestions` | `(kind: MentionKind) => void` | – | Types @ or # at the caret so suggestions open, adding a space first when needed. Useful for a mention button. |
| `clear` | `() => void` | – | Empties the text and mentions. |
| `textarea` | `HTMLTextAreaElement \| null` | – | The underlying textarea. |

### serializeMentions

Turns a value into storage friendly text, <@id> for people and <#id> for channels by default.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `MentionValue` | – | The value to serialize. |
| `format` | `(mention: Mention) => string` | – | Custom token for each mention, such as a markdown link. |

### mentionText

Returns the visible text of a mention, @label or #label.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `kind` (required) | `MentionKind` | – | "person" or "channel". |
| `label` (required) | `string` | – | The mention label. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| @ / # | Opens people or channel suggestions after a space, line start, or opening bracket or quote. |
| Arrow Down / Arrow Up | Moves through suggestions, wrapping at the ends. |
| Enter / Tab | Turns the highlighted suggestion into a mention. |
| Escape | Dismisses suggestions for the current trigger without leaving the field. |
| Backspace / Delete | Next to a mention, one press removes the whole token. |
| Arrow Left / Arrow Right | Steps over a mention as one unit. |
| Enter / Shift+Enter | With submitOnEnter, Enter submits and Shift+Enter adds a line. |

## Accessibility

- The textarea is a combobox with aria-autocomplete="list", aria-expanded, aria-controls, and aria-activedescendant pointing at the highlighted option.
- Suggestions are a listbox labelled People or Channels, with options marked aria-selected.
- A polite live region announces the result count, such as 3 people or No matches.
- Mention tints come from an aria-hidden mirror behind a transparent textarea, so assistive tech reads the plain text.
- Choosing a suggestion types through execCommand insertText, so native undo keeps working.

## Motion

- The field height springs to its content between minRows and maxRows.
- Suggestions pop in from the caret with a small scale and a snappy spring; the highlight glides between rows and the panel height springs as results change.
- Reduced motion jumps the height and highlight and fades the panel without scale or offset.

## Responsive behavior

- Fills its container width. The suggestion panel is 272px wide, capped to the field width, and clamped so it never runs past the right edge.
- With placement auto the panel flips above the caret near the bottom of the viewport, which suits composers pinned to the bottom of a mobile screen.
- Options use pointer events and click, so touch taps choose a suggestion without a hover state.

## Performance

- Each edit rebuilds a mirror of the text to draw token tints and reconciles mention offsets with a prefix and suffix diff; fine for messages, not for pages of text.
- Two ResizeObservers drive the field and panel height springs; ranking runs over the full people and channels arrays each keystroke, so pass a pre-filtered list past a few thousand entries.

## Notes for AI

- Choose it for comment boxes, message composers, and task descriptions that tag people or channels. Use textarea for plain multi-line text and tag-input for a list of chips outside the text.
- Store serializeMentions(value) or the mentions array, not only value.text, so renamed people still resolve by id.
- Filter people on the server by passing a fresh people array per keystroke; ranking is prefix, then word start, then substring, then role or description.
- Hook a toolbar @ button to ref.openSuggestions("person").

## Related

- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Tag input](https://uiarc.dev/components/tag-input/markdown): Turn short text values into removable tags.
- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Chat thread](https://uiarc.dev/components/chat-thread/markdown): A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.
- [Rich text editor](https://uiarc.dev/components/rich-text-editor/markdown): A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.

## Also in special inputs

- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.
- [Money input](https://uiarc.dev/components/money-input/markdown): A currency field with live grouping, stable width, rolling digits, and minor-unit output.
- [Phone input](https://uiarc.dev/components/phone-input/markdown): A phone field with a country picker, formatting as you type, and E.164 output.
- [Scrub input](https://uiarc.dev/components/scrub-input/markdown): A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
- [Shortcut recorder](https://uiarc.dev/components/shortcut-recorder/markdown): Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.

## Guidance for AI tools

Mention input: A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

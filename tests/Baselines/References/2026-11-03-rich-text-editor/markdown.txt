# Rich text editor

> A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/rich-text-editor
- Markdown: https://uiarc.dev/components/rich-text-editor/markdown
- Registry item: https://uiarc.dev/r/rich-text-editor.json
- Source file: `registry/components/rich-text-editor/rich-text-editor.tsx`
- Dependencies: motion, lucide-react
- Keywords: inputs, new, react rich text editor, markdown editor, wysiwyg editor, slash command menu, floating formatting toolbar, contenteditable editor, notion style editor, lightweight text editor

## When to use

- Comments, notes, and posts where people expect markdown shortcuts and a selection toolbar.
- Forms that need Markdown or clean HTML out without pulling in a full editor framework.
- Short to medium documents with headings, lists, quotes, and code blocks.

## When not to use

- Use textarea for plain text, or mention-input when the only rich part is tagging people.
- Use inline-edit for a single line edited in place.
- Use code-block to display code, not to write prose.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/rich-text-editor
pnpm dlx shadcn@latest add @uiarc/rich-text-editor
yarn dlx shadcn@latest add @uiarc/rich-text-editor
bunx --bun shadcn@latest add @uiarc/rich-text-editor
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/rich-text-editor.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/rich-text-editor/rich-text-editor.tsx`

   The source is in the registry item: https://uiarc.dev/r/rich-text-editor.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { RichTextEditor, type RichTextValue } from "@/components/arc/rich-text-editor/rich-text-editor";

export function PostEditor() {
  const [doc, setDoc] = useState<RichTextValue | null>(null);
  return (
    <>
      <RichTextEditor
        aria-label="Post body"
        placeholder="Write your update"
        defaultMarkdown={"# Release notes\n\n- Faster search\n- New **dark** theme"}
        onChange={setDoc}
      />
      <button type="button" disabled={!doc || doc.empty} onClick={() => save(doc!.markdown)}>
        Publish
      </button>
    </>
  );
}

declare function save(markdown: string): void;
```

## Examples

### Controlled HTML with custom undo buttons

```tsx
const editor = useRef<RichTextEditorHandle>(null);
const [html, setHtml] = useState("<p>Draft</p>");
const [history, setHistory] = useState({ canUndo: false, canRedo: false });

<>
  <button type="button" disabled={!history.canUndo} onClick={() => editor.current?.undo()}>Undo</button>
  <button type="button" disabled={!history.canRedo} onClick={() => editor.current?.redo()}>Redo</button>
  <RichTextEditor ref={editor} value={html} onChange={value => setHtml(value.html)} onHistoryChange={setHistory} />
</>
```

## API reference

### RichTextEditor

A light rich text editor on contentEditable with markdown shortcuts, a floating selection toolbar that morphs into a link field, a slash menu for blocks, its own undo history, and HTML plus Markdown output.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `string` | – | Controlled HTML. The editor only rewrites its content when this differs from what it last emitted. |
| `defaultValue` | `string` | – | Initial HTML when uncontrolled. Sanitized on load. |
| `defaultMarkdown` | `string` | – | Initial content as Markdown, used when no HTML value is given. |
| `onChange` | `(value: RichTextValue) => void` | – | Called after each edit with { html, markdown, text, empty }. HTML is clean: canonical tags and only href attributes. |
| `onHistoryChange` | `(state: { canUndo: boolean; canRedo: boolean }) => void` | – | Reports whether undo and redo are available, for your own toolbar. |
| `placeholder` | `string` | `"Start writing"` | Shown when the editor is empty. |
| `blockHint` | `string` | `"Type / for blocks"` | The hint on an empty line while the caret is in it. |
| `readOnly` | `boolean` | `false` | Turns off editing, the toolbar, and shortcuts. |
| `autoFocus` | `boolean` | `false` | Focuses the editor on mount with the caret at the end. |
| `aria-label` | `string` | `"Editor"` | Accessible name of the textbox. |
| `className` | `string` | – | Extra class on the root. |
| `style` | `CSSProperties` | – | Inline style on the root. |

### RichTextEditorHandle

Methods on the ref.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `focus` | `() => void` | – | Focuses the editor. |
| `getHTML` | `() => string` | – | Clean HTML of the current content. |
| `getMarkdown` | `() => string` | – | Current content as Markdown. |
| `getText` | `() => string` | – | Plain text, one block per line. |
| `setHTML` | `(html: string) => void` | – | Replaces the content with sanitized HTML as one undo step. |
| `setMarkdown` | `(markdown: string) => void` | – | Replaces the content from Markdown as one undo step. |
| `clear` | `() => void` | – | Empties the editor as one undo step. |
| `undo` | `() => void` | – | Steps back in the editor's own history. |
| `redo` | `() => void` | – | Steps forward in the editor's own history. |
| `element` | `HTMLDivElement \| null` | – | The contentEditable element. |

### markdownToHtml

Converts Markdown to the editor's HTML: headings (levels four to six become h3), lists with nesting, quotes, fenced code, rules, and inline bold, italic, strike, code, and links.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `markdown` (required) | `string` | – | Markdown source. |

### htmlToMarkdown

Converts editor HTML, or an element, to Markdown. Browser only.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `input` (required) | `string \| HTMLElement` | – | HTML string or element. |

### sanitizeHtml

Keeps only the tags the editor understands and drops every attribute except safe link hrefs (http, https, mailto, tel, relative, hash). Bold and italic spans from other editors become marks. Browser only.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `html` (required) | `string` | – | Untrusted HTML. |

### normalizeUrl

Adds a scheme to bare input: example.com/docs becomes https://example.com/docs and an email becomes a mailto link.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `input` (required) | `string` | – | What the person typed. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Cmd/Ctrl+B | Toggles bold. |
| Cmd/Ctrl+I | Toggles italic. |
| Cmd/Ctrl+Shift+X | Toggles strikethrough. |
| Cmd/Ctrl+E | Toggles inline code within one block. |
| Cmd/Ctrl+K | Opens the link field for the selection or the link under the caret. |
| Cmd/Ctrl+Z / Cmd/Ctrl+Shift+Z / Cmd/Ctrl+Y | Undo and redo in the editor's own history. Underline (Cmd/Ctrl+U) is blocked. |
| / | After a space or at line start, opens the block menu. Type to filter; Arrow Up and Down move, Enter or Tab chooses, Escape closes. |
| Tab / Shift+Tab | Indents or outdents a list item; in a code block Tab inserts two spaces. |
| Enter / Shift+Enter | New block or a line break. Enter on an empty list item or quote leaves it; two Enters at the end of a code block exit it. |
| Backspace | At the start of a heading, quote, code block, or list item, turns it back into a paragraph or outdents it; after a divider, removes the divider. |
| Alt+F10 | Moves focus into the floating toolbar while it is open. Arrow Left and Right move between buttons; Escape returns to the text. |

## Accessibility

- The editing surface has role textbox with aria-multiline and aria-label, and aria-readonly when read-only.
- The slash menu is a listbox wired to the textbox with aria-controls and aria-activedescendant.
- The floating toolbar has role toolbar with toggle buttons using aria-pressed; titles show shortcuts.
- A polite live region announces Formatted, Link added, Undone, and similar results.
- Toolbar buttons prevent mousedown so the text selection survives clicks.

## Motion

- The toolbar glides with the selection and its surface springs between the formatting face and the link face, which slide and blur across each other.
- The slash menu pops from the slash, its highlight glides between rows, and its height springs as results filter.
- Reduced motion jumps the toolbar position and size, crossfades the faces, and fades menus without scale or offset.

## Responsive behavior

- The root is an inline-size container; under 330px wide the toolbar hides its heading and quote buttons and keeps the inline marks.
- The toolbar and slash menu flip above or below based on the nearest scrolling or clipping ancestor, and the toolbar is clamped inside the editor width.
- Enter, Backspace, and undo go through beforeinput, so mobile keyboards and IME composition behave the same as hardware keys.

## Performance

- Each selection change is batched to one requestAnimationFrame; each edit clones the DOM once to produce HTML, Markdown, and text for onChange.
- Undo keeps up to 300 HTML snapshots, merging typing within one second; very long documents make each snapshot and the per-edit clone more costly.
- No editor framework: only motion and lucide-react icons ship with it.

## Notes for AI

- Choose it for comments, notes, posts, and descriptions that need headings, lists, quotes, code, and links. Use textarea for plain text and mention-input when the only structure is @ mentions.
- Store onChange markdown or html; both are clean. The value prop is HTML and only rewrites the DOM when it differs from the last emitted HTML, so feeding onChange html back is safe.
- Supported blocks are p, h1 to h3, ul, ol, blockquote, pre, and hr; inline marks are strong, em, s, code, and a. Images, tables, and underline are not supported.
- Paste runs through sanitizeHtml, and markdown-looking plain text is converted, so content from docs and other editors stays tidy.
- Build your own undo buttons with onHistoryChange and ref.undo and ref.redo.

## Related

- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
- [Code block](https://uiarc.dev/components/code-block/markdown): Present code with legible hierarchy and copy access.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Also in editors

- [Signature pad](https://uiarc.dev/components/signature-pad/markdown): Smooth ink that thins with speed, with undo, replay, and PNG or SVG export.
- [File dropzone](https://uiarc.dev/components/file-dropzone/markdown): A generous target for dropping one or more files.
- [Avatar upload](https://uiarc.dev/components/avatar-upload/markdown): Click or drop a photo onto the avatar to crop it in place with drag, pinch and zoom. It uploads through a progress ring and settles, with clear errors and retry.

## Guidance for AI tools

Rich text editor: A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

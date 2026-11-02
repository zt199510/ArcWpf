# Emoji picker

> A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid.

- Type: Component (inputs)
- Access: Arc Pro
- Page: https://uiarc.dev/components/emoji-picker
- Markdown: https://uiarc.dev/components/emoji-picker/markdown
- Source file: `registry/components/emoji-picker/emoji-picker.tsx`
- Dependencies: motion, lucide-react
- Keywords: input, picker, emoji, new, emoji picker, emoji keyboard, smiley, emoticon, skin tone, recent emoji, frequently used, chat, composer, insert emoji, reaction picker

## When to use

- Message composers, comment boxes, and chat inputs.
- Choosing an emoji for a status, a project, a page icon, or a label.
- Any text field where people expect the full emoji set with search.

## When not to use

- Use reactions for a small, fixed set of quick responses on a post or comment.
- Use combobox or a select when the choices are a short, known list.
- Do not use it as an icon picker; emoji render differently on every platform.

## Installation

Emoji picker is part of Arc Pro. The live preview is public; the source and install command need Pro.

### CLI with a Pro token

1. Create a token in your account and set it in the environment (or `.env.local`). Never commit it.

```bash
export ARC_PRO_TOKEN=arc_pro_...
```

2. Add the Pro registry to `components.json`:

```json
{
  "registries": {
    "@uiarc": "https://uiarc.dev/r/{name}.json",
    "@uiarc-pro": {
      "url": "https://uiarc.dev/r/pro/{name}.json",
      "headers": {
        "Authorization": "Bearer ${ARC_PRO_TOKEN}"
      }
    }
  }
}
```

3. Install:

```bash
npx shadcn@latest add @uiarc-pro/emoji-picker
```

### Manual

Signed-in Pro members can copy the source from the Manual tab on the docs page.

- Plans: https://uiarc.dev/pricing
- Create a Pro token: https://uiarc.dev/account#pro-access
- Setup guide: https://uiarc.dev/docs/ai#pro-access

## Usage

```tsx
import { EmojiPicker } from "@/components/arc/emoji-picker/emoji-picker";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/arc/popover/popover";

export function EmojiButton({ onInsert }: { onInsert: (emoji: string) => void }) {
  const [open, setOpen] = useState(false);
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger aria-label="Add emoji"><SmilePlus /></PopoverTrigger>
      <PopoverContent side="top" style={{ padding: 0, width: "min(22rem, calc(100vw - 20px))" }}>
        <EmojiPicker
          onSelect={(selection, event) => {
            onInsert(selection.emoji);
            if (!event.shiftKey) setOpen(false);
          }}
        />
      </PopoverContent>
    </Popover>
  );
}
```

## API reference

### EmojiPicker

A full emoji picker: instant search over names and keywords, category tabs whose indicator follows your scroll, skin tones, recently used emoji remembered on the device, a preview row with the emoji name, and a virtualized grid that is fully usable from the keyboard. Ships its own dataset of every emoji up to Emoji 15.0. It has no chrome of its own, so it drops into a popover, sheet, or card.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `onSelect` | `(selection: EmojiSelection, event: MouseEvent \| KeyboardEvent) => void` | – | Called when an emoji is chosen by click, Enter, or Space. selection has emoji (with the skin tone applied), base, name, and skinTone. Check event.shiftKey to keep a popover open for several picks. |
| `skinTone` | `0 \| 1 \| 2 \| 3 \| 4 \| 5` | – | Controlled skin tone: 0 is the default yellow, 1 to 5 go from light to dark. |
| `defaultSkinTone` | `0 \| 1 \| 2 \| 3 \| 4 \| 5` | – | Skin tone before anything has been stored. |
| `onSkinToneChange` | `(tone: SkinTone) => void` | – | Called when a tone is chosen in the tone menu. |
| `storageKey` | `string \| null` | `"arc-emoji-picker"` | localStorage key for recent emoji and the tone. Pass null to keep them in memory for this page only. Blocked storage falls back to memory automatically. |
| `recentLimit` | `number` | `27` | How many recently used emoji to keep. |
| `defaultRecent` | `string[]` | `18 popular emoji` | Shown under Popular until something has been picked. |
| `columns` | `number` | – | Emoji per row. Defaults to as many 36px cells as fit, at least 6. |
| `autoFocus` | `boolean` | `false` | Focuses the search field on mount. Popovers and dialogs usually focus it already. |
| `searchPlaceholder` | `string` | `"Search emoji"` | Placeholder of the search field. |
| `label` | `string` | `"Emoji picker"` | Accessible name of the picker group. |
| `className` | `string` | – | Class on the root. Set --emoji-picker-width and --emoji-picker-height to size it (352px by 408px by default). |
| `style` | `CSSProperties` | – | Styles on the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Type | Filters every emoji by name and keyword; the best match is ready for Enter. Typing while the grid has focus goes to the search field. |
| Enter | In the search field, picks the highlighted emoji. In the grid, picks the active emoji. |
| Arrow Down | From the search field, moves into the grid. |
| Arrow keys | Move through the grid. Up and down keep the column across sections; Up from the first row returns to search. |
| Home / End | First or last emoji in the row. With Ctrl or Cmd, first or last emoji overall. |
| Page Up / Page Down | Moves a screen of rows. |
| Space | Picks the active emoji in the grid. |
| Arrow Left / Arrow Right | In the category bar, moves between categories; Enter jumps to one. In the tone menu, moves between tones. |
| Escape | Closes the tone menu, then clears the search, then closes the surrounding popover. |

## Accessibility

- The grid is role="grid" with rows and gridcells named after each emoji; section names are column headers. The active cell is exposed with aria-activedescendant from the search field while searching, and from the grid while it has focus, so the active emoji is announced as it moves.
- Only the rows in view are rendered, plus the active row, so aria-activedescendant always points at a real element. Rows carry aria-rowindex and the grid aria-rowcount.
- The number of results is announced in a polite live region while searching, including when nothing matches.
- The category bar is a toolbar with one tab stop; the category in view is marked with aria-current. The tone control is a button with aria-expanded that opens a radiogroup of six named tones.
- Escape is handled on window in the capture phase and marked as handled only when it closes the tone menu or clears the search, so the first Escape never closes a surrounding Radix popover by accident.
- The preview row repeats the active emoji's name for sighted users and is hidden from assistive technology.

## Motion

- The category indicator slides to the category under the top edge on the morph spring as you scroll. When a tab is pressed it goes straight there and waits while the list scrolls smoothly to the category, instead of stepping through every category in between.
- The skin tone menu unrolls leftward out of the tone button on a clip-path inset, and rolls back into it; the inset is a CSS transition, so a quick close reverses mid-way.
- The preview emoji settles in from a slight scale on each change; a pressed emoji dips and springs back.
- Search results appear instantly, with no animation, so typing always feels immediate.
- With reduced motion the indicator and tone menu move at once, scrolling to a category jumps, and the preview does not scale.

## Responsive behavior

- The grid picks its column count from the width, keeping cells at least 36px, so it goes from 9 columns at 352px to 7 in a narrow phone popover.
- The default size is 352 by 408px and it never exceeds its container; set --emoji-picker-width and --emoji-picker-height to change it.
- Hover previews only follow a mouse pointer; on touch a tap picks the emoji directly.

## Performance

- The dataset is about 73 kB of compact strings (1,870 emoji with names, keywords, and skin tone positions) parsed once on first use, then shared by every picker on the page.
- The grid is virtualized: only the rows in view plus a small overscan are rendered, so scrolling the whole set stays light.
- Search scans precomputed lowercase strings and is memoized per query.
- Skin tones are applied when a cell renders, so there is no second dataset for toned variants.

## Notes for AI

- Put it in a popover next to the input it fills. Remember the input's selection and insert at it, then move the caret after the emoji; see the composer demo.
- The picker has no border, background, or padding of its own. Give the popover padding: 0 so the grid runs edge to edge.
- Keep the popover open when Shift is held for several picks in a row, and close it otherwise.
- The dataset covers Emoji 15.0, which renders on current Apple, Google, and Microsoft platforms. Older systems may show a few newer emoji as boxes.
- Use onSkinToneChange with skinTone to sync the tone with a user profile; otherwise it is remembered in localStorage with the recent emoji.

## Related

- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
- [Rich text editor](https://uiarc.dev/components/rich-text-editor/markdown): A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.

## Also in pickers

- [Calendar](https://uiarc.dev/components/calendar/markdown): Browse dates in a clear, compact month view.
- [Date picker](https://uiarc.dev/components/date-picker/markdown): Choose a date without losing context.
- [Date range picker](https://uiarc.dev/components/date-range-picker/markdown): A range picker that grows from its trigger into two months with presets and a stretching range highlight.
- [Time picker](https://uiarc.dev/components/time-picker/markdown): Choose a time with sensible keyboard behavior.
- [Color picker](https://uiarc.dev/components/color-picker/markdown): A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.

## Guidance for AI tools

Emoji picker: A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

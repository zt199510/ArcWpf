# Shortcut recorder

> Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/shortcut-recorder
- Markdown: https://uiarc.dev/components/shortcut-recorder/markdown
- Registry item: https://uiarc.dev/r/shortcut-recorder.json
- Source file: `registry/components/shortcut-recorder/shortcut-recorder.tsx`
- Dependencies: motion, lucide-react
- Keywords: inputs, new, react shortcut recorder, keyboard shortcut input, hotkey recorder, keybinding editor, kbd component, keyboard shortcuts cheatsheet, record hotkey

## When to use

- Settings pages where people remap keyboard shortcuts.
- A keyboard shortcut cheatsheet or help dialog, with ShortcutList.
- Inline key hints in menus, tooltips, and docs, with ShortcutKbd or ShortcutKeys.

## When not to use

- Use ShortcutKbd alone when shortcuts are fixed and only need to be shown.
- Use command-palette to let people run actions by name rather than bind keys.
- Avoid it on touch-only products where no hardware keyboard is expected.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/shortcut-recorder
pnpm dlx shadcn@latest add @uiarc/shortcut-recorder
yarn dlx shadcn@latest add @uiarc/shortcut-recorder
bunx --bun shadcn@latest add @uiarc/shortcut-recorder
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/shortcut-recorder.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/shortcut-recorder/shortcut-recorder.tsx`

   The source is in the registry item: https://uiarc.dev/r/shortcut-recorder.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useEffect, useState } from "react";
import { ShortcutRecorder, matchesShortcut, usePlatform } from "@/components/arc/shortcut-recorder/shortcut-recorder";

export function SearchShortcut() {
  const platform = usePlatform();
  const [shortcut, setShortcut] = useState<string | null>("mod+k");

  useEffect(() => {
    if (!shortcut) return;
    const onKey = (event: KeyboardEvent) => {
      if (matchesShortcut(event, shortcut, platform)) { event.preventDefault(); openSearch(); }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [platform, shortcut]);

  return (
    <ShortcutRecorder
      label="Open search"
      value={shortcut}
      onValueChange={setShortcut}
      resetValue="mod+k"
      bindings={[{ shortcut: "mod+p", label: "Print" }]}
    />
  );
}
```

## API reference

### ShortcutRecorder

A field that records a keyboard shortcut, lights held keys as caps, and asks before taking a combination already in use.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Field label. |
| `hideLabel` | `boolean` | `false` | Keeps the label for screen readers only. |
| `value` | `string \| null` | – | Controlled shortcut, such as "mod+shift+k". mod is Command on Apple platforms and Ctrl elsewhere. |
| `defaultValue` | `string \| null` | `null` | Starting shortcut when uncontrolled. |
| `onValueChange` | `(value: string \| null, details: { replaced?: ShortcutBinding }) => void` | – | Called with the new shortcut; replaced is the binding it was taken from after Use anyway. |
| `resetValue` | `string \| null` | – | What the reset button restores. Defaults to defaultValue. |
| `bindings` | `ShortcutBinding[]` | `[]` | Shortcuts already in use: { shortcut, label }. Recording one asks before taking it. |
| `warnReserved` | `boolean` | `true` | Also warn about combinations the browser or system keeps, such as ⌘W and ⌘C. |
| `requireModifier` | `boolean` | `true` | Require ⌘, Ctrl, or ⌥. Function keys are always allowed. |
| `platform` | `"mac" \| "other"` | – | Overrides the detected platform. |
| `placeholder` | `string` | `"Record shortcut"` | Shown when there is no shortcut. |
| `description` | `string` | – | Hint under the field. |
| `disabled` | `boolean` | `false` | Disables recording. |
| `id` | `string` | – | Id of the recorder button. |
| `className` | `string` | – | Class on the root. |

### ShortcutList

A searchable, grouped shortcut cheatsheet. Holding a modifier lights its caps and fades rows that do not use it.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `groups` (required) | `ShortcutListGroup[]` | – | Groups: { label, items: { label, shortcut, keywords? }[] }. |
| `label` | `string` | `"Keyboard shortcuts"` | Accessible name of the list. |
| `searchable` | `boolean` | `true` | Shows the search field. |
| `searchPlaceholder` | `string` | `"Search shortcuts"` | Search placeholder and label. |
| `highlightPressed` | `boolean` | `true` | Light caps as keys are held and fade rows that do not use the held modifiers. |
| `platform` | `"mac" \| "other"` | – | Overrides the detected platform. |
| `className` | `string` | – | Class on the root. |

### ShortcutKeys

A shortcut drawn as key caps in platform order, with a spoken label.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `shortcut` (required) | `string` | – | Such as "mod+k". |
| `platform` | `"mac" \| "other"` | – | Overrides the detected platform. |
| `pressed` | `ReadonlySet<string>` | – | Held key ids from usePressedKeys; matching caps light up. |
| `size` | `"sm" \| "md"` | `"md"` | Cap size. |
| `className` | `string` | – | Class on the wrapper. |

### ShortcutKbd

A single key cap for inline copy and menus. Also exported as Kbd, a deprecated alias.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `children` (required) | `ReactNode` | – | The key label. |
| `pressed` | `boolean` | `false` | Draws the key pushed down and lit. |
| `size` | `"sm" \| "md"` | `"md"` | Cap size. |
| `...props` | `HTMLAttributes<HTMLElement>` | – | Forwarded to the kbd element. |

### matchesShortcut / formatShortcut / usePlatform / usePressedKeys

Helpers: test a keydown against a stored shortcut, format it as "⌘⇧K" or "Ctrl+Shift+K" with a spoken form, read the platform, and track held keys.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `event, shortcut, platform` (required) | `KeyboardEvent, string, Platform` | – | matchesShortcut returns true when the keydown is this shortcut. |
| `shortcut, platform` (required) | `string, Platform` | – | formatShortcut returns { text, spoken }. |
| `override` | `Platform` | – | usePlatform(override?) returns "mac" or "other"; the server assumes mac. |
| `enabled` | `boolean` | `true` | usePressedKeys(enabled?) returns the set of held key ids. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Starts recording. |
| Any combination (while recording) | Records it; held modifiers show as caps as you build the chord. |
| Escape (while recording) | Cancels recording. |
| Backspace / Delete | Clears the shortcut, while recording or while the field has focus. |
| Tab (while recording) | Stops recording and moves on without recording Tab. |
| Escape (in list search) | Clears the ShortcutList search. |

## Accessibility

- The recorder is a button with aria-pressed while recording, named by the label plus the spoken shortcut, such as "Command Shift K".
- Recording, results, conflicts, and clears are announced through a polite live region; the message row is linked with aria-describedby.
- Key caps are aria-hidden inside the recorder; ShortcutKeys uses role="img" with a spoken aria-label.
- Conflicts offer Use anyway and Cancel as real buttons, and focus returns to the recorder after either.

## Motion

- Caps pop in with a small scale and blur, staggered when a finished shortcut settles, and slide on a snappy layout spring as the chord changes.
- A recording dot pulses; the message row opens on a height spring.
- In ShortcutList, groups and rows collapse on a height spring as search filters them.
- Reduced motion removes the scale, stagger, layout slide, and height springs; caps and messages fade.

## Responsive behavior

- ShortcutList is a container with a query at 520px that puts groups in two columns; below that they stack.
- The recorder keeps a fixed height and caps wrap within it; hover styles apply only on fine pointers.

## Performance

- usePressedKeys adds window keydown and keyup listeners only while enabled; turn off highlightPressed for lists that do not need it.
- ShortcutList filters on every keystroke by building token strings per row; it suits lists of a few hundred shortcuts.

## Notes for AI

- Store shortcuts as normalized strings with mod ("mod+shift+k"); they read as ⌘ on Mac and Ctrl elsewhere without a second binding.
- Run bound actions with matchesShortcut in a keydown listener; it compares physical keys, so ⌥K matches "alt+k" rather than "˚".
- Pass every existing binding in bindings, and use details.replaced from onValueChange to unbind the old owner after Use anyway.
- Use ShortcutList for a help sheet and ShortcutKbd for inline hints such as in menus or command-palette rows.

## Related

- [Command palette](https://uiarc.dev/components/blocks/command-palette/markdown): A complete keyboard driven action surface with search, grouped results, and shortcuts.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.
- [Dropdown menu](https://uiarc.dev/components/dropdown-menu/markdown): A focused list of actions anchored to a trigger.
- [Settings page](https://uiarc.dev/components/blocks/settings-page/markdown): Account settings with a gliding section nav and a save bar that morphs in as you edit.

## Also in special inputs

- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.
- [Money input](https://uiarc.dev/components/money-input/markdown): A currency field with live grouping, stable width, rolling digits, and minor-unit output.
- [Phone input](https://uiarc.dev/components/phone-input/markdown): A phone field with a country picker, formatting as you type, and E.164 output.
- [Scrub input](https://uiarc.dev/components/scrub-input/markdown): A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
- [Tag input](https://uiarc.dev/components/tag-input/markdown): Turn short text values into removable tags.
- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.

## Guidance for AI tools

Shortcut recorder: Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

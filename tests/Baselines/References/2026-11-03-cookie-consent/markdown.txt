# Cookie consent

> A consent bar that morphs into preferences and folds into a reopen button.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/cookie-consent
- Markdown: https://uiarc.dev/components/cookie-consent/markdown
- Registry item: https://uiarc.dev/r/cookie-consent.json
- Source file: `registry/components/cookie-consent/cookie-consent.tsx`
- Dependencies: motion, lucide-react
- Keywords: feedback, new, react cookie consent, cookie banner, gdpr consent, cookie preferences, consent manager, privacy banner, tracking consent

## When to use

- Asking for cookie or tracking consent on a marketing site or app.
- Sites that need per-category choices with a way to reopen them later.

## When not to use

- Use announcement-bar for a site-wide promotion or notice that needs no choice.
- Use toast for a one-off message that dismisses itself.
- Use dialog when the page must block until the visitor decides.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/cookie-consent
pnpm dlx shadcn@latest add @uiarc/cookie-consent
yarn dlx shadcn@latest add @uiarc/cookie-consent
bunx --bun shadcn@latest add @uiarc/cookie-consent
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/cookie-consent.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/cookie-consent/cookie-consent.tsx`

   The source is in the registry item: https://uiarc.dev/r/cookie-consent.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useRef } from "react";
import { CookieConsent, type CookieConsentHandle } from "@/components/arc/cookie-consent/cookie-consent";

export function Consent() {
  const consent = useRef<CookieConsentHandle>(null);
  return (
    <>
      <CookieConsent
        ref={consent}
        storageKey="acme-consent"
        policy={{ label: "Cookie policy", href: "/cookies" }}
        onChange={value => { if (value?.analytics) loadAnalytics(); }}
      />
      <button type="button" onClick={() => consent.current?.openPreferences()}>Cookie settings</button>
    </>
  );
}
```

## API reference

### CookieConsent

A consent bar that morphs into a preferences panel and then folds into a small reopen button, with optional localStorage memory.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `categories` | `CookieCategory[]` | `defaultCookieCategories` | Groups: { id, label, description?, required?, defaultEnabled? }. Required ones are always on. |
| `value` | `CookieConsentValue \| null` | – | Controlled saved choice (category id to allowed). null means the visitor has not decided. |
| `defaultValue` | `CookieConsentValue \| null` | `null` | Starting choice when uncontrolled. |
| `onChange` | `(value: CookieConsentValue \| null, detail: { action: CookieConsentAction; updatedAt: Date }) => void` | – | Called on accept-all, reject-all, save, or reset. Load optional scripts from here. |
| `storageKey` | `string` | – | Remembers the choice in localStorage under this key. Read it elsewhere with readCookieConsent. |
| `version` | `string` | `"1"` | Bump when categories or policy change; a stored choice for another version is ignored and the bar asks again. |
| `preferencesOpen` | `boolean` | – | Controlled open state of the preferences panel. |
| `defaultPreferencesOpen` | `boolean` | `false` | Opens the panel on first render when uncontrolled. |
| `onPreferencesOpenChange` | `(open: boolean) => void` | – | Called when the panel opens or closes. |
| `policy` | `{ label: string; href?: string; onClick?: () => void }` | – | Link to the full policy, shown in the bar and panel. Renders a button when href is missing. |
| `position` | `"bottom" \| "bottom-left" \| "bottom-right"` | `"bottom"` | Where the bar sits along the bottom edge. |
| `strategy` | `"fixed" \| "absolute"` | `"fixed"` | fixed pins to the viewport; absolute fills the nearest positioned ancestor, for previews. |
| `showReopenButton` | `boolean` | `true` | Keeps a 44px cookie button after a choice so visitors can change their mind. |
| `reopenSide` | `"left" \| "right"` | – | Corner for the reopen button. Defaults to the bar's side, or left when centered. |
| `labels` | `Partial<CookieConsentLabels>` | – | Overrides for message, customize, acceptAll, rejectAll, save, preferencesTitle, preferencesDescription, reopen, alwaysOn, and close. |
| `className` | `string` | – | Class on the layer. |
| `ref` | `Ref<CookieConsentHandle>` | – | Imperative handle: openPreferences(), closePreferences(), reset(). |

### readCookieConsent

Reads a saved choice as { value, version, updatedAt }, or null when there is none or it was saved for another version. Safe on the server.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `storageKey` (required) | `string` | – | The same key passed to CookieConsent. |
| `version` | `string` | `"1"` | The version the choice must match. |

### defaultCookieCategories

The default categories: Strictly necessary (required), Preferences, Analytics, and Marketing.

No props.

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves through the bar or panel buttons and switches. |
| Space | Toggles the focused category switch. |
| Escape | Closes the preferences panel without saving and returns focus to the bar or reopen button. |

## Accessibility

- The bar and panel are non-modal dialogs; the bar is described by its message and the panel is labelled by its heading.
- Opening the panel focuses its heading; saving focuses the reopen button, which has aria-haspopup="dialog" and the reopen label.
- Each switch is labelled by its category and described by its hint; required categories show Always on text instead of a switch.
- Save is described by a hidden count such as 2 of 3 optional categories allowed.
- Reject all sits beside Accept all at the same weight, so refusing is as easy as accepting.

## Motion

- One surface springs its position, width, height, and radius between the bar, the panel, and the 44px reopen button; faces rise from a short blur.
- Growing uses a slightly bouncy spring and shrinking a flat one, so the surface never overshoots when it folds away.
- The surface enters rising 24px and scaling from 0.98.
- Reduced motion jumps the surface to each shape and swaps faces with a plain fade.

## Responsive behavior

- The bar is at most 660px and the panel 420px wide, both clamped to the layer, which sits 16px from the viewport edges.
- Below a 560px bar width the message stacks above the actions; panel actions go on one row at a 340px container width.
- The layer ignores pointer input, so only the surface blocks clicks on the page behind it.

## Performance

- Nothing renders until hydration and the stored choice are read, so a decided visitor never sees the bar flash.
- One ResizeObserver tracks the layer width and one tracks the current face; motion runs only when the shape changes.

## Notes for AI

- Mount once near the root layout. Gate optional scripts on onChange or on readCookieConsent(storageKey, version) at startup.
- Bump version whenever categories or the policy change to ask everyone again.
- Wire a footer Cookie settings link to ref.openPreferences(). Set showReopenButton={false} if that link is the only way back.
- Use strategy="absolute" inside a positioned preview frame, not for production.

## Related

- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Announcement bar](https://uiarc.dev/components/announcement-bar/markdown): A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
- [Toast](https://uiarc.dev/components/toast/markdown): Brief confirmation for a completed background action.
- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.

## Also in messages

- [Alert](https://uiarc.dev/components/alert/markdown): A persistent message that helps people recover or continue.
- [Toast stack](https://uiarc.dev/components/toast-stack/markdown): Stack short results at the edge until you reach for them.
- [Undo toast](https://uiarc.dev/components/undo-toast/markdown): Archive or delete rows with a second chance: the gap closes, a countdown toast merges repeat actions, and Undo or ⌘Z puts every row back exactly where it was.

## Guidance for AI tools

Cookie consent: A consent bar that morphs into preferences and folds into a reopen button. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

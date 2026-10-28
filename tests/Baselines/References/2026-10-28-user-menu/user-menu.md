# User menu

> Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/user-menu
- Markdown: https://uiarc.dev/components/user-menu/markdown
- Registry item: https://uiarc.dev/r/user-menu.json
- Source file: `registry/components/user-menu/user-menu.tsx`
- Dependencies: motion, lucide-react
- Keywords: motion, product, react user menu, account menu, avatar dropdown, profile menu, user menu with status, sign out menu, presence indicator

## When to use

- The single account entry point in an app top bar.
- Menus that combine presence status, theme preference, account links, and sign out.
- Apps that want async sign out with a spinner in the menu.

## When not to use

- Use dropdown-menu for generic command lists.
- Use theme-switch for a standalone light and dark toggle.
- Use workspace-sidebar when account and workspace switching live in a side navigation.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/user-menu
pnpm dlx shadcn@latest add @uiarc/user-menu
yarn dlx shadcn@latest add @uiarc/user-menu
bunx --bun shadcn@latest add @uiarc/user-menu
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/user-menu.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/user-menu/user-menu.tsx`

   The source is in the registry item: https://uiarc.dev/r/user-menu.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { UserMenu } from "@/components/arc/user-menu/user-menu";
import { CreditCard, Settings, UserRound } from "lucide-react";

export function TopBarAccount({ user }: { user: { name: string; email: string } }) {
  return (
    <UserMenu
      user={{ ...user, plan: "Pro" }}
      showName
      onThemeChange={setThemePreference}
      items={[
        { label: "Profile", icon: <UserRound size={16} />, onSelect: openProfile },
        { label: "Settings", icon: <Settings size={16} />, keys: ["⌘", ","], onSelect: openSettings },
        { label: "Billing", icon: <CreditCard size={16} />, onSelect: openBilling },
      ]}
      onSignOut={() => signOut()}
    />
  );
}
```

## API reference

### UserMenu

Account menu behind an avatar trigger: a compact identity header, a short group of account links, an inline theme switch, and sign out. Below 640px it opens as a bottom sheet.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `user` (required) | `UserMenuUser` | – | { name, email, plan?, avatarSrc?, avatarSrcSet? }. Falls back to initials without a photo. plan shows as a small accent badge. |
| `items` | `UserMenuItem[]` | `[]` | Account destinations: { label, icon?, keys?, onSelect? }. Keep it to three or four. keys show quiet shortcut hints such as ["⌘", ","]. |
| `showName` | `boolean` | `false` | Shows the name and a chevron beside the avatar from 640px up. Phones always show the avatar alone. |
| `theme` | `"light" \| "dark" \| "system"` | – | Controlled theme preference. |
| `defaultTheme` | `"light" \| "dark" \| "system"` | `"system"` | Initial theme when uncontrolled. |
| `onThemeChange` | `(theme: ThemePreference) => void` | – | Reports the choice. Applying it to the page is up to the app. The menu stays open. |
| `showTheme` | `boolean` | `true` | Shows the inline light, dark, and system switch. |
| `status` | `"available" \| "busy" \| "away"` | – | Controlled presence status. Passing status or onStatusChange adds the presence dot and an inline status switch. |
| `defaultStatus` | `"available" \| "busy" \| "away"` | `"available"` | Initial status when uncontrolled. |
| `onStatusChange` | `(status: UserStatus) => void` | – | Called when a status is chosen. The menu stays open. |
| `onSignOut` | `() => void \| Promise<unknown>` | – | Sign out handler. A returned promise keeps the menu open with a spinner until it settles. |
| `signOutKeys` | `string[]` | – | Shortcut hint for the sign out item. |
| `open` | `boolean` | – | Controlled open state. |
| `defaultOpen` | `boolean` | `false` | Initial open state when uncontrolled. A menu that starts open does not take focus. |
| `onOpenChange` | `(open: boolean) => void` | – | Called when the menu opens or closes. |
| `align` | `"start" \| "center" \| "end"` | `"end"` | Panel alignment against the trigger. |
| `portal` | `boolean` | `true` | Renders the desktop panel on the body. Pass false to keep it inside the trigger wrapper, for example in a contained preview. The mobile sheet always uses the body. |
| `ref` | `Ref<HTMLButtonElement>` | – | The trigger button, for returning focus. |
| `className` | `string` | – | Extra class on the trigger. |

### PresenceDot

The status mark on its own, for avatars elsewhere in the app. Each status has its own shape as well as color.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `status` (required) | `"available" \| "busy" \| "away" \| "offline"` | – | Status to show. |
| `className` | `string` | – | Extra class for positioning. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space / ArrowDown | Opens the menu from the avatar and moves to the first row. |
| ArrowUp | Opens the menu from the avatar and moves to the last row. |
| ArrowDown / ArrowUp | Moves between rows, looping at the ends. The highlight follows. |
| Home / End | Moves to the first or last row. |
| A to Z | Typeahead jumps to the next row starting with the typed letters. |
| ArrowLeft / ArrowRight | Chooses the previous or next option in the theme or status switch. |
| Enter / Space | Activates the row, or chooses a theme or status without closing. |
| Escape / Tab | Closes the menu and returns focus to the avatar. |

## Accessibility

- The trigger has aria-haspopup="menu" and aria-expanded, and is labelled "Account menu, <name>" plus the status when one is shown.
- Rows are menuitem buttons; the theme and status switches are labelled groups of menuitemradio buttons with aria-checked.
- Pointer focus draws no ring; keyboard focus shows Arc's shared focus ring. The highlight marks the keyboard position. Outside press, the scrim, and swipe down close the menu.
- Sign out sets aria-busy while an async handler runs; shortcut hints are aria-hidden.

## Motion

- The panel scales and fades out of the avatar on a spring that never overshoots; rows fade in with a short stagger, and closing is a quick fade.
- One highlight glides between rows for the pointer and the keyboard; the switch thumb slides between options.
- On phones the sheet rises from the bottom edge and follows a downward drag, closing past a short distance or a quick flick.
- Reduced motion opens and closes with a short fade, jumps the highlight, and turns off dragging.

## Responsive behavior

- The menu is 17.5rem wide, capped at the viewport width minus 24px, with 12px collision padding.
- Long names and emails ellipsize instead of widening the menu.
- The trigger is a fixed 40px avatar, and the theme row keeps a 44px minimum height for touch.

## Performance

- Content renders only while open; each opening gets a fresh layout namespace so shared indicators do not animate from stale positions.
- The avatar image decodes async and falls back to initials on error.

## Notes for AI

- Use as the single account entry point in an app top bar. For generic command lists use dropdown-menu; for a standalone light/dark toggle use theme-switch.
- Keep items to three or four account destinations; the theme switch and sign out are built in.
- onThemeChange only reports; apply the theme to the document yourself, optionally with a view transition.
- Return the sign out promise so the item shows progress and the menu closes when it settles.

## Related

- [Dropdown menu](https://uiarc.dev/components/dropdown-menu/markdown): A focused list of actions anchored to a trigger.
- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Theme switcher](https://uiarc.dev/components/theme-switch/markdown): Four smooth ways to move between light and dark appearance.
- [Sidebar: Workspace](https://uiarc.dev/components/blocks/sidebar/markdown): A product sidebar with a workspace switcher, live search, draggable favorites, and inline projects.

## Also in menus

- [Context menu](https://uiarc.dev/components/context-menu/markdown): Secondary actions kept close to the selected object.

## Guidance for AI tools

User menu: Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

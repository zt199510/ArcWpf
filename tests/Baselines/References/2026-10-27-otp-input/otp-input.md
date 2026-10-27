# OTP input

> A six digit verification flow with paste support and keyboard navigation.

- Type: Block
- Page: https://uiarc.dev/components/blocks/otp-input
- Markdown: https://uiarc.dev/components/blocks/otp-input/markdown

- Access: Free, open source
- Registry id: `otp-input`
- Source file: `registry/components/otp-input/otp-input.tsx`
- Built from: Input, Keyboard navigation, Validation
- Keywords: react otp input, verification code input, 2fa code input, pin input, one time code, sms code input

Use it as part of verification after you have sent a code. Validate the code on your server.

## When to use

- Verification codes from email or SMS.
- Two-factor authentication code entry.

## When not to use

- Use password-field for secrets people type from memory.
- Use input for any other text.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/otp-input
pnpm dlx shadcn@latest add @uiarc/otp-input
yarn dlx shadcn@latest add @uiarc/otp-input
bunx --bun shadcn@latest add @uiarc/otp-input
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/otp-input.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/otp-input/otp-input.tsx`

   The source is in the registry item: https://uiarc.dev/r/otp-input.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { OtpInput } from "@/components/arc/otp-input/otp-input";

export function VerifyCode() {
  const [code, setCode] = useState("");
  return (
    <OtpInput
      label="Verification code"
      description="We sent a 6-digit code to your email."
      value={code}
      onChange={next => { setCode(next); if (next.length === 6) verify(next); }}
      autoFocus
    />
  );
}
```

## API reference

### OtpInput

A row of single-character inputs for one-time codes, with paste fill, a gliding focus ring, and animated helper and error copy.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label and group name; each slot is labelled with it plus its position. |
| `length` | `number` | `6` | Number of slots. |
| `value` | `string` | `""` | Current code. Controlled: update it from onChange. |
| `onChange` | `(value: string) => void` | – | Called with the full code after every edit or paste. |
| `description` | `string` | – | Helper text below the slots. |
| `error` | `string` | – | Error text. Sets aria-invalid and shakes the row once when it changes. |
| `inputMode` | `"numeric" \| "text"` | `"numeric"` | numeric strips non-digits. |
| `autoFocus` | `boolean` | `false` | Focuses the first slot on mount. |
| `disabled` | `boolean` | `false` | Disables every slot. |
| `className` | `string` | – | Merged onto the field wrapper. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| 0-9 / characters | Fills the slot and moves to the next one. |
| ArrowLeft / ArrowRight | Moves between slots. |
| Backspace | On an empty slot, clears the previous one and moves back. |
| Delete | Clears the current slot. |
| Cmd/Ctrl + V | Pastes a code across the slots from the focused one. |

## Accessibility

- Slots sit in a labelled role=group, each with an aria-label like Verification code, digit 1 of 6.
- Helper and error text are linked with aria-describedby; the error uses role=alert and sets aria-invalid.
- The first slot uses autocomplete one-time-code so browsers can offer SMS codes.

## Motion

- Typed characters rise into the slot and unblur; a paste lands as a short left-to-right wave.
- One focus ring glides between slots, and a new error nudges the row side to side once.
- Reduced motion removes the glide, shake, and blur and uses instant fades.

## Responsive behavior

- Slots are 42px wide and can shrink with max-width 100%; below 360px gaps tighten and slots drop to 44px tall.
- The first slot uses autocomplete one-time-code, so iOS and Android can offer SMS codes.
- numeric inputMode brings up the number pad on phones.

## Performance

- Only a handful of inputs plus one gliding ring; a ResizeObserver springs the message height.

## Notes for AI

- Use for verification and two-factor codes. Use input or password-field for anything else.
- It is controlled: keep value in state and submit when value.length equals length. There is no default export, so import it by name.

## Related

- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Login and sign up: Email code](https://uiarc.dev/components/blocks/login/markdown): A sign in card that morphs from email to a six digit code to your account.
- [Login and sign up: Sign up](https://uiarc.dev/components/blocks/login/markdown): An account creation flow with field validation, password strength, and a clear completion state.
- [Security settings](https://uiarc.dev/components/blocks/security-settings/markdown): An account security page with guided two-factor setup, password change and active sessions.

## Guidance for AI tools

Blocks are complete, self-contained screens with sample data. Replace the sample data and connect the callbacks described above. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

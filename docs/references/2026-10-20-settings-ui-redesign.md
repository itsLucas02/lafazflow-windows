# Settings UI redesign (20 October 2026)

Reference for the visual system applied to `SettingsWindow.xaml`. Inspired by Handy's Windows app structure (sidebar + grouped cards + strong type hierarchy), with motion/typography principles from the loaded design skills (better-interface, apple-design, emil-design-eng).

## Before

- 860×660 window; 220 px sidebar with a plain list and the version under the app name.
- 12 px body text everywhere; 22 px page titles; 11 px helper text; 5 px input corner radii; 8 px cards; dense 12 px card gaps.
- Content padding 28/24; mixed inline font sizes.

## After (design tokens)

| Token | Value |
| --- | --- |
| Window / app background | `#0F1013` |
| Sidebar | `#131519` (240 px) |
| Card surface | `#1A1D22`, border `#272C34`, radius 12, padding 20, 16 gap |
| Control surface | `#101216`, border `#272C34`, radius 8, min height 34 |
| Accent | `#3DCCC7` on `#123B3D` surfaces |
| Text | primary `#F2F4F7`, secondary `#A3ACB8` |
| Type | Segoe UI Variable; body 13, page title 26/600, field label 13/600, helper 12 |
| Spacing | content 32/28, page header → cards 24 |

## Structural changes

- Sidebar: accent app mark + “LafazFlow / On-device dictation”, nav rows with 8 px radius, hover surface and a 3 px accent bar on the selected item; version moved to a muted footer.
- Content area: consistent 32/28 padding and 24 px header rhythm; cards unified on the shared surface token.
- Rounded inputs: text boxes and combo boxes share the radius/focus treatment (accent border on keyboard focus, subtle hover border).
- Buttons: 8 px radius, 32 px minimum height, hover `#262B33`, pressed accent surface.
- Window enlarged to 960×700 (min 840×600) for the wider sidebar and card breathing room.

## Deliberately not changed

- No navigation animations (frequently used, keyboard-initiated — per the motion guidance).
- Pill/recorder window untouched; only the Settings surface was in scope.
- Diagnostics tables keep their density and column set; only chrome/contrast tokens were aligned.
- No control rewrites: bindings, sections, and command handlers are unchanged.

## Verification

Full suite 843 green (XAML regression assertions updated for the new width/radii). Visual check on the installed build is the remaining acceptance step.

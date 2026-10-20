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

## Revision 2 — neutral palette and spacing pass (same day)

Owner feedback: “spacing is very bad… everything feels smudged… I hate the color.” Applied:

- **Palette:** removed the teal cast. Neutral charcoal: background `#0F0F10`, sidebar `#151517`, cards `#1A1A1D`, borders `#2A2A2F`, controls `#121214`, text `#F4F4F5` / `#A6A6AD`. Accent/emphasis is white (`#EDEDF0`), selection surfaces are white at 8% alpha; green is reserved for the “ready” dot only.
- **Spacing:** content inset `40,32,24,24`; scroll content padded `0,0,28,40` so content never touches the scrollbar and pages end with breathing room; cards padding 24 with 20 gaps; page header → cards 28; nav rows 12/11 with 4 gaps; sidebar padding 18/22 with 28 header gap; window 1000×720.
- **Type rhythm:** default body 13 without a global line-height (global line-height clipped large titles); subtitle/help use explicit 20/18 line heights.
- No binding or behavioral changes.
## Revision 3 — scrollbar, History page, button sizing (same day)

- **Scrollbar:** slimmed to an 8px track with a 1px-inset 6px thumb, neutral `#3A3A40` idle / lighter hover; drag uses the window emphasis tone. Quiet and modern instead of a heavy bar.
- **History page (new section, second in the sidebar):** lists the last 50 dictated texts, newest first, with timestamp and a per-item **Copy** button so text that pasted in the wrong place can be recovered. Stored only on this machine in `%LOCALAPPDATA%\LafazFlow\history.json`; **Clear History** is confirmed first. Empty state explains the page.
- **Button sizing:** every fixed `Width` on buttons became `MinWidth`, so long labels grow instead of clipping; wrap panels reflow when space is tight.
- No binding or behavioral changes outside the new history recording (append on successful paste only).

Verification: history store round-trip/cap/clear/corrupt-file tests; XAML assertions for the History page and scrollbar token; full suite 849 green.
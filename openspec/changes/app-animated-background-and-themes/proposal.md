# Proposal

## Why

Only the login screen has an animated backdrop; every other screen sits on a flat `--tf-bg` colour. The app also has a single light look, with no way for a user to pick a darker one. An animated backdrop on all screens, plus a choice of themes, makes the product feel consistent and lets users work in the look they prefer.

## What Changes

- Add an app-wide animated background (a connected-nodes particle network) that shows in the page area outside the screen content: margins, gutters between panels, and behind popups. It is mounted once at the app root, so every screen and popup gets it.
- Popups, drawers and modals keep a dimmed (and lightly blurred) backdrop so the animation stays visible behind them.
- Add a theme dropdown in the shell header with three themes: **Default** (light), **Professional** (navy blue) and **Dark** (black). Default is the starting theme.
- Remember the chosen theme in the browser and apply it before first paint to avoid a flash of the wrong theme.
- The animated backdrop colours follow the active theme.
- Convert hard-coded colours in shared and feature styles to `--tf-*` tokens where needed so all three themes render correctly.
- The login screen keeps its existing dark animated background and does not get the theme dropdown.
- Respect reduced motion: no animation, static gradient only.

## Capabilities

### New Capabilities
- `app-animated-background`: app-wide decorative animated backdrop behind all authenticated screens and popup backdrops, including motion, accessibility and performance rules.
- `app-theme-selection`: header dropdown to choose Default, Professional or Dark, applied through design tokens, persisted per browser.

### Modified Capabilities
<!-- None. `openspec/specs/` has no existing capabilities; the login background is a separate in-flight change and stays unchanged. -->

## Impact

- Frontend only (Angular 19, plain CSS with `--tf-*` tokens). No backend, API or database change.
- New: shared background component, theme service, header theme dropdown component.
- Touched: `app.component.html`, `shell.component.html/css`, `styles.css` (token sets per theme, overlay/backdrop rules), and component CSS files that hard-code colours (about 187 hex values across component CSS and 52 in `styles.css`).
- Existing specs that assert header contents or colours may need updating.
- No new dependencies.

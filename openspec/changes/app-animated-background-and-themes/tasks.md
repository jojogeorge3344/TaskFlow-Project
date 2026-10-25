# Tasks

## 1. Theme tokens and service

- [x] 1.1 Add `:root[data-theme="professional"]` and `:root[data-theme="dark"]` token overrides (surfaces, text, borders, primary, sidebar, overlay, backdrop colours, `color-scheme: dark`) in `styles.css`; add `--tf-backdrop-*` tokens to the Default set too
- [x] 1.2 Create `ThemeService` (signal of `default | professional | dark`, applies `data-theme` on `<html>`, persists to `tf.theme` with try/catch, falls back to Default on invalid or unavailable storage)
- [x] 1.3 Add the inline pre-bootstrap script in `index.html` that applies the stored theme before first paint
- [x] 1.4 Unit tests for `ThemeService` (default, select, persist, invalid value, storage throws)

## 2. Header theme dropdown

- [x] 2.1 Create standalone `ThemeMenuComponent` (button with swatch and label, listbox with three options, active tick, keyboard and outside-click handling)
- [x] 2.2 Place it in `shell.component.html` before the notification bell and style it with tokens in `shell.component.css`
- [x] 2.3 Component and shell specs: opens, selects, marks active, Arrow/Enter/Space/Escape, outside click, absent on login

## 3. App-wide animated backdrop

- [x] 3.1 Extract the particle network from `LoginBackgroundComponent` into a shared `AppBackdropComponent` that reads colours from `--tf-backdrop-*` tokens and re-reads them when the theme changes
- [x] 3.2 Keep the login screen's look by giving it fixed dark colours, and keep its existing specs passing
- [x] 3.3 Mount `<app-backdrop />` in `app.component.html`, hidden on `/login`
- [x] 3.4 Make shell content and page-level wrappers sit above the backdrop (`z-index: 1`) and drop full-bleed `--tf-bg` fills so the animation shows in margins and gutters
- [x] 3.5 Add `backdrop-filter: blur(2px)` and per-theme overlay opacity to `.tf-backdrop` and `.tf-modal::backdrop`
- [x] 3.6 Specs: canvas present and `aria-hidden`, no loop under reduced motion, loop stops when hidden and on destroy, absent on login, recolours on theme change

## 4. Colour conversion for all themes

- [x] 4.1 Replace hard-coded surface, text and border hex values in `styles.css` with tokens (add new tokens where missing)
- [x] 4.2 Convert hard-coded colours in component CSS, starting with kanban, shell, tfa-header, employee dashboard, activity, security setup and legal pages; leave login CSS unchanged
- [x] 4.3 Check WCAG AA contrast for body text, muted text, primary buttons and status chips in Professional and Dark, and adjust token values

## 5. Verification

- [x] 5.1 Run the frontend unit tests and fix specs affected by the header change
- [ ] 5.2 Walk every screen, tab and popup in all three themes (dashboards, projects, tasks and board, mail, settings, profile, notifications, modals, drawers) and fix visible colour problems
- [ ] 5.3 Check reduced motion, tab hidden, reload with each saved theme (no flash), and a phone-width viewport

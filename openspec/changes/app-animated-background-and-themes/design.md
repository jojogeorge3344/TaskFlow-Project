# Design

## Context

The frontend is Angular 19 (standalone components, plain CSS). `styles.css` defines one light `--tf-*` token set on `:root`; `body` and several layout areas use `--tf-bg`. The login screen already has `app-login-background`, a canvas particle network in `features/auth/login`, used only by `login.component.html`. The app root is just `<router-outlet />`; the authenticated pages render inside `ShellComponent` (sidebar, fixed header, content). Modals use `.tf-modal::backdrop` and `.tf-backdrop` with `--tf-overlay`.

Colour usage: about 1,676 `var(--tf-*)` references, but also about 187 hard-coded hex values in component CSS (largest: legal pages 38, kanban 22, login 22, security setup 21) and 52 in `styles.css`. Theming therefore works mostly by overriding tokens, plus a conversion pass for hard-coded colours. See proposal.md for scope.

## Goals / Non-Goals

**Goals:**
- One backdrop instance for all non-login screens, mounted once so it never restarts on navigation.
- Three themes switched by one attribute, with no per-component theme code.
- No flash of the wrong theme on load.

**Non-Goals:**
- Per-user theme stored on the server or synced across devices.
- Following the OS light/dark setting automatically.
- Changing the login background, adding more than three themes, or user-customisable colours.
- Making content cards translucent.

## Decisions

1. **Shared backdrop component.** Move the particle logic out of `LoginBackgroundComponent` into a reusable `AppBackdropComponent` (`app-backdrop`) under `shared/`, parameterised by colours read from CSS tokens (`--tf-backdrop-from`, `--tf-backdrop-to`, `--tf-backdrop-node`). The login component keeps its own look by passing fixed dark colours, so its spec and behaviour are unchanged. Alternative: copy the component. Rejected to avoid two animation loops to maintain.
2. **Mount at app root, hide on login.** `app.component.html` renders `<app-backdrop />` before `<router-outlet />`, with an `@if` on the current URL so it is absent on `/login` (which has its own). Alternative: mount inside `ShellComponent`. Rejected because account recovery, security setup and legal screens sit outside the shell and should also get it.
3. **Layering.** The backdrop is `position: fixed; inset: 0; z-index: 0; pointer-events: none; aria-hidden`. `body` background stays `--tf-bg`, and the backdrop paints a gradient over it. Shell sidebar, header and panels already have solid surfaces; their wrapper gets `position: relative; z-index: 1`. Layout areas that currently paint `--tf-bg` full-bleed (shell content area and similar) become transparent so the animation shows in margins and gutters. Alternative: translucent cards. Rejected for legibility.
4. **Popups.** Keep `--tf-overlay` on `.tf-backdrop` and `.tf-modal::backdrop`, add `backdrop-filter: blur(2px)`. Native `<dialog>` top layer paints above the fixed backdrop, which then shows through the translucent overlay. Overlay opacity is set per theme.
5. **Theme tokens via attribute.** `ThemeService` (root, signal-based) holds `'default' | 'professional' | 'dark'`, sets `data-theme` on `<html>` (none/`default` for Default), and persists to `localStorage` key `tf.theme` in try/catch. `styles.css` keeps the current tokens as Default and adds `:root[data-theme="professional"]` and `:root[data-theme="dark"]` token overrides plus `color-scheme: dark`. Alternative: CSS classes on `body` or Angular Material theming. Rejected as heavier, and the app is already token-driven.
6. **No flash.** A tiny inline script in `index.html` reads `tf.theme`, validates it against the three names, and sets `data-theme` before the app bootstraps. `ThemeService` reads the same value on construction so state matches.
7. **Palettes (from the approved mockup).** Default: current light tokens. Professional: navy page `#0b1426`, surface `#12203a`, border `#243659`, primary `#60a5fa`. Dark: page `#000000`, surface `#111111`, border `#2a2a2a`, primary `#6ea8fe`. The sidebar token follows each theme. Exact values get a contrast check (WCAG AA) during implementation.
8. **Hard-coded colours.** Replace hex values that represent surfaces, text or borders with tokens (new tokens added where none exists, e.g. soft status colours per theme). Leave brand and semantic colours that read well on all three themes. Login screen CSS is left alone.
9. **Header dropdown.** A standalone `ThemeMenuComponent` in `core/layout`, placed in `shell.component.html` between the header spacer and the notification bell. It is a button plus `role="listbox"` with `role="option"` items, swatches, arrow-key/Enter/Space/Escape handling and outside-click close, as in the mockup. Alternative: native `<select>`. Rejected because swatches cannot be shown in native options.
10. **Reduced motion and cost.** Reuse the existing rules: matchMedia reduced-motion, pause when hidden, capped nodes and DPR, canvas outside the Angular zone. Reduced motion shows the static gradient.

## Risks / Trade-offs

- **Colour regressions in Professional/Dark** (hard-coded hex, inline styles, third-party-like components). Mitigation: conversion pass, then a screen-by-screen visual review in all three themes.
- **Backdrop under dense tables/kanban** may distract. Mitigation: low-alpha nodes and solid card surfaces.
- **Low-end devices.** Mitigation: caps, pause when hidden, reduced-motion fallback.
- **Existing specs** asserting header children or colours may break. Update them as part of the change.
- **Theme on pre-login pages:** the stored theme applies, but there is no dropdown there. Accepted.

## Open Questions

- Theme names are Default / Professional / Dark as agreed on the mockup; no open items that change scope.

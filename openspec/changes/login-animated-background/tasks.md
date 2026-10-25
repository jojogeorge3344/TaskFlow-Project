## 1. Background component

- [x] 1.1 Create standalone `LoginBackgroundComponent` (`app-login-background`) in `features/auth/login/` with a canvas, `aria-hidden`, fixed full-viewport host with `pointer-events: none` and a CSS gradient base
- [x] 1.2 Implement particle network drawing (node count scaled to viewport and capped, link lines by distance, low-alpha slate/blue colours), HiDPI-aware sizing and resize handling
- [x] 1.3 Run the `requestAnimationFrame` loop outside the Angular zone; pause on `visibilitychange` hidden and resume when visible
- [x] 1.4 Honour `prefers-reduced-motion` (no loop, gradient only), reacting to live changes of the media query
- [x] 1.5 Release the frame, resize, media-query and visibility listeners in `ngOnDestroy`

## 2. Login integration

- [x] 2.1 Add `<app-login-background />` as first child of `section.login` and import the component
- [x] 2.2 Update `login.component.css` so the card, MFA step and legal footer layer above the background (`position: relative; z-index: 1`) while keeping the existing layout
- [ ] 2.3 Visually check password step, MFA step, error/notice states, narrow mobile width and footer legibility

## 3. Tests

- [x] 3.1 Add `login-background.component.spec.ts`: canvas rendered and `aria-hidden`, loop starts, no loop under reduced motion, loop cancelled on destroy
- [x] 3.2 Run existing login specs (`login.component.spec.ts`, `login.component.mfa.spec.ts`) and confirm they still pass

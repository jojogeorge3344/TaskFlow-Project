## Context

`login.component` renders a `section.login` (dark slate `#0f172a`, flex-centred) containing one of two `form.login__card` steps plus `app-legal-footer`. The frontend is Angular 19 with standalone components, plain CSS with `--tf-*` tokens, and Karma/Jasmine specs in a real Chrome (so canvas works in specs). No animation library is installed.

## Goals / Non-Goals

**Goals:** full-screen, tasteful, low-cost animation behind the login card; accessible; no change to sign-in behaviour.
**Non-Goals:** animating other screens, theming options, adding Three.js/Lottie, changing the card design.

## Decisions

1. **Canvas particle network, dependency-free.** A standalone `LoginBackgroundComponent` (`app-login-background`) draws about 60 drifting nodes with lines between nodes closer than a threshold, via `requestAnimationFrame`. Chosen over CSS-only blobs for a distinct "connected tasks" feel, and over Three.js/Lottie to avoid bundle weight. Node count scales with viewport area and is capped.
2. **Layering.** `:host` is `position: fixed; inset: 0; z-index: 0; pointer-events: none` with a CSS gradient base (slate to deep blue). The login section content gets `position: relative; z-index: 1`. The canvas has `aria-hidden="true"`; nothing in it is focusable.
3. **Reduced motion.** Read `matchMedia('(prefers-reduced-motion: reduce)')`; when true, skip the loop and show the gradient only. Listen for change events so toggling the OS setting takes effect live.
4. **Lifecycle.** Run the loop outside Angular's zone (`NgZone.runOutsideAngular`) to avoid change detection per frame. Pause on `document.visibilitychange` when hidden; on destroy cancel the frame and remove the resize, media-query and visibility listeners.
5. **HiDPI.** Scale the canvas by `devicePixelRatio` (capped at 2) and resize on window resize.
6. **Placement.** The component lives next to the login component and is used only in `login.component.html`, as the first child of `section.login`, so both steps and the footer share it.

## Risks / Trade-offs

- Low-end devices: mitigated by node cap, DPR cap, pausing when hidden, and the reduced-motion fallback.
- Legal footer contrast over the animated area: nodes and lines use low alpha so the existing `dark` footer variant stays legible; verify visually.
- Test determinism: specs assert DOM and lifecycle (canvas present, aria-hidden, loop cancelled on destroy, no loop under reduced motion) rather than pixels.

## Open Questions

- Style was assumed to be the particle network (the recommended option); revisit if a different look is preferred.

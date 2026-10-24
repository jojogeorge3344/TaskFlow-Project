# Proposal

## Why

The sign-in screen is a plain dark-slate section with a white card, so the first impression of TaskFlow is flat. A subtle full-screen animated background gives the entry point a distinct, on-brand identity without changing how sign-in works.

## What Changes

- Add a full-viewport animated background behind the login card: a canvas particle network (drifting nodes joined by faint lines, evoking connected tasks), drawn in the existing slate/blue palette.
- The background is decorative only: hidden from assistive tech, never intercepts pointer or keyboard input, and sits behind the card and legal footer.
- Respect `prefers-reduced-motion` by showing a static gradient instead of animating.
- Pause animation while the tab is hidden and clean up on destroy so no frame loop leaks after navigating away.
- Applies to both steps of the login screen (password step and MFA step), since they share one component. Other auth screens (account recovery) are unchanged.
- No change to sign-in behaviour, API calls, routing, or the card's look.

## Capabilities

### New Capabilities
- `login-background-animation`: the decorative animated full-screen background on the login screen, including accessibility, reduced-motion and lifecycle behaviour.

### Modified Capabilities
<!-- None: no existing requirement of the sign-in flow changes. -->

## Impact

- Frontend only: new standalone component under `TaskFlow Proj/frontend/src/app/features/auth/login/`, used from `login.component.html`; small CSS changes in `login.component.css`.
- No new dependencies, no backend, API or database changes.
- Existing login specs keep working; a new spec covers the background component.
- Assumption: the particle-network style was the recommended option from exploration and is assumed; swapping style later only touches the new component.

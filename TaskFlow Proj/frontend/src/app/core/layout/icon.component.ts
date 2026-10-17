import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/**
 * Inline stroke icons (24px viewBox, currentColor). Paths follow the Lucide icon set (ISC license).
 * The markup is a fixed, trusted constant; names outside this map render an empty <svg>.
 */
const ICONS = {
  menu: '<path d="M4 6h16M4 12h16M4 18h16"/>',
  'panel-close': '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16M15 10l-2 2 2 2"/>',
  'panel-open': '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16M13 10l2 2-2 2"/>',
  home: '<rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/>',
  users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>',
  building: '<rect x="4" y="2" width="16" height="20" rx="2"/><path d="M9 22v-4h6v4M8 6h.01M12 6h.01M16 6h.01M8 10h.01M12 10h.01M16 10h.01M8 14h.01M12 14h.01M16 14h.01"/>',
  tasks: '<path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>',
  kanban: '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M8 7v7M12 7v4M16 7v9"/>',
  chart: '<path d="M3 3v18h18"/><path d="M7 16v-4M12 16V8M17 16v-7"/>',
  logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  search: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>',
  edit: '<path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z"/>',
  trash: '<path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>',
  power: '<path d="M18.36 6.64a9 9 0 1 1-12.73 0M12 2v10"/>',
  x: '<path d="M18 6L6 18M6 6l12 12"/>',
  check: '<path d="M20 6L9 17l-5-5"/>',
  alert: '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"/>',
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  reset: '<path d="M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5"/>',
  bell: '<path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 0 1-3.46 0"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  mail: '<rect x="2" y="4" width="20" height="16" rx="2"/><path d="M22 7l-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>',
  star: '<path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>',
  flag: '<path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1zM4 22v-7"/>',
  send: '<path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z"/>',
  inbox: '<path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/>',
  paperclip: '<path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>',
  reply: '<path d="M9 17l-5-5 5-5"/><path d="M20 18v-2a4 4 0 0 0-4-4H4"/>',
  'reply-all': '<path d="M7 17l-5-5 5-5"/><path d="M12 17l-5-5 5-5"/><path d="M22 18v-2a4 4 0 0 0-4-4H7"/>',
  forward: '<path d="M15 17l5-5-5-5"/><path d="M4 18v-2a4 4 0 0 1 4-4h12"/>',
  download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/>',
  back: '<path d="M19 12H5M12 19l-7-7 7-7"/>'
} as const;

export type IconName = keyof typeof ICONS;

@Component({
  selector: 'tf-icon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: contents' },
  template: `<svg class="tf-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false" [innerHTML]="markup()"></svg>`
})
export class IconComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly name = input.required<string>();

  readonly markup = computed<SafeHtml>(() => {
    const paths = (ICONS as Record<string, string>)[this.name()] ?? '';
    return this.sanitizer.bypassSecurityTrustHtml(paths);
  });
}

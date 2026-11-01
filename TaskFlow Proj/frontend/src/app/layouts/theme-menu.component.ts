import { Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { THEMES, ThemeService } from '../shared/services/theme.service';
import { ThemeName } from '../shared/services/theme.model';

/** Header dropdown for choosing the colour theme. A listbox (not a native select) so each option can show a swatch. */
@Component({
  selector: 'app-theme-menu',
  standalone: true,
  templateUrl: './theme-menu.component.html',
  styleUrl: './theme-menu.component.css'
})
export class ThemeMenuComponent {
  private readonly themeService = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly themes = THEMES;
  readonly theme = this.themeService.theme;
  readonly open = signal(false);

  label(): string {
    return THEMES.find((entry) => entry.name === this.theme())?.label ?? 'Default';
  }

  toggle(): void {
    const next = !this.open();
    this.open.set(next);
    if (next) {
      queueMicrotask(() => this.optionElements().find((el) => el.getAttribute('aria-selected') === 'true')?.focus());
    }
  }

  choose(name: ThemeName): void {
    this.themeService.select(name);
    this.close();
  }

  onOptionKey(event: KeyboardEvent, name: ThemeName, index: number): void {
    const options = this.optionElements();
    switch (event.key) {
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.choose(name);
        break;
      case 'ArrowDown':
        event.preventDefault();
        options[(index + 1) % options.length]?.focus();
        break;
      case 'ArrowUp':
        event.preventDefault();
        options[(index - 1 + options.length) % options.length]?.focus();
        break;
      case 'Escape':
        event.preventDefault();
        this.close();
        break;
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  private close(): void {
    this.open.set(false);
    this.host.nativeElement.querySelector<HTMLButtonElement>('.theme-menu__button')?.focus();
  }

  private optionElements(): HTMLElement[] {
    return Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>('[role="option"]'));
  }
}

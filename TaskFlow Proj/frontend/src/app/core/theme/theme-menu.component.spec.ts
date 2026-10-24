import { ComponentFixture, TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from './theme.service';
import { ThemeMenuComponent } from './theme-menu.component';

describe('ThemeMenuComponent', () => {
  let fixture: ComponentFixture<ThemeMenuComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    await TestBed.configureTestingModule({ imports: [ThemeMenuComponent] }).compileComponents();
    fixture = TestBed.createComponent(ThemeMenuComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
  });

  const button = () => el.querySelector<HTMLButtonElement>('.theme-menu__button')!;
  const options = () => Array.from(el.querySelectorAll<HTMLElement>('[role="option"]'));

  it('shows the current theme and starts closed', () => {
    expect(button().textContent).toContain('Default');
    expect(button().getAttribute('aria-expanded')).toBe('false');
    expect(options().length).toBe(0);
  });

  it('lists the three themes with the active one selected', () => {
    button().click();
    fixture.detectChanges();

    expect(options().map((o) => o.textContent?.trim())).toEqual(['Default', 'Professional', 'Dark']);
    expect(options().map((o) => o.getAttribute('aria-selected'))).toEqual(['true', 'false', 'false']);
  });

  it('selects a theme on click, applies it and closes', () => {
    button().click();
    fixture.detectChanges();

    options()[2].click();
    fixture.detectChanges();

    expect(TestBed.inject(ThemeService).theme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(button().textContent).toContain('Dark');
    expect(options().length).toBe(0);
  });

  it('supports Arrow, Enter and Escape from the keyboard', () => {
    button().click();
    fixture.detectChanges();

    options()[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(document.activeElement).toBe(options()[1]);

    options()[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();
    expect(TestBed.inject(ThemeService).theme()).toBe('professional');

    button().click();
    fixture.detectChanges();
    options()[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(options().length).toBe(0);
    expect(TestBed.inject(ThemeService).theme()).toBe('professional');
  });

  it('closes on an outside click without changing the theme', () => {
    button().click();
    fixture.detectChanges();

    document.body.click();
    fixture.detectChanges();

    expect(options().length).toBe(0);
    expect(TestBed.inject(ThemeService).theme()).toBe('default');
  });
});

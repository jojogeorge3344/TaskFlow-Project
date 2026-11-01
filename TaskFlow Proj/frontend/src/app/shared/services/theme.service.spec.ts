import { THEME_STORAGE_KEY, ThemeService } from './theme.service';

describe('ThemeService', () => {
  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
  });

  it('uses Default when nothing is stored', () => {
    const service = new ThemeService();

    expect(service.theme()).toBe('default');
    expect(document.documentElement.hasAttribute('data-theme')).toBeFalse();
  });

  it('applies and persists a selected theme', () => {
    const service = new ThemeService();

    service.select('dark');

    expect(service.theme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('removes the attribute when switching back to Default', () => {
    const service = new ThemeService();
    service.select('professional');

    service.select('default');

    expect(document.documentElement.hasAttribute('data-theme')).toBeFalse();
  });

  it('restores the stored theme on creation', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'professional');

    const service = new ThemeService();

    expect(service.theme()).toBe('professional');
    expect(document.documentElement.getAttribute('data-theme')).toBe('professional');
  });

  it('falls back to Default for an invalid stored value', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'neon');

    expect(new ThemeService().theme()).toBe('default');
  });

  it('keeps working when storage throws', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('blocked');
    spyOn(Storage.prototype, 'setItem').and.throwError('blocked');

    const service = new ThemeService();
    service.select('dark');

    expect(service.theme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });
});

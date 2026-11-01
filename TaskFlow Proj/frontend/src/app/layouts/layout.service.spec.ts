import { TestBed } from '@angular/core/testing';
import { LayoutService, SIDEBAR_COLLAPSED_KEY, WIDE_VIEWPORT_QUERY } from './layout.service';

describe('LayoutService', () => {
  let changeListener: ((event: { matches: boolean }) => void) | null;

  function mockViewport(wide: boolean): void {
    changeListener = null;
    spyOn(window, 'matchMedia').and.callFake(
      (query: string) =>
        ({
          matches: query === WIDE_VIEWPORT_QUERY ? wide : false,
          addEventListener: (_: string, listener: (event: { matches: boolean }) => void) => {
            changeListener = listener;
          }
        }) as unknown as MediaQueryList
    );
  }

  function createService(): LayoutService {
    TestBed.configureTestingModule({});
    return TestBed.inject(LayoutService);
  }

  beforeEach(() => {
    localStorage.removeItem(SIDEBAR_COLLAPSED_KEY);
  });

  afterEach(() => {
    localStorage.removeItem(SIDEBAR_COLLAPSED_KEY);
  });

  it('starts expanded when nothing is stored', () => {
    mockViewport(true);
    const service = createService();

    expect(service.collapsed()).toBeFalse();
    expect(service.expanded()).toBeTrue();
  });

  it('persists the collapsed state and restores it in a new session', () => {
    mockViewport(true);
    const service = createService();

    service.toggle();

    expect(service.collapsed()).toBeTrue();
    expect(localStorage.getItem(SIDEBAR_COLLAPSED_KEY)).toBe('true');

    TestBed.resetTestingModule();
    const restored = createService();
    expect(restored.collapsed()).toBeTrue();
    expect(restored.expanded()).toBeFalse();
  });

  it('defaults to expanded and keeps toggling when storage is blocked', () => {
    mockViewport(true);
    spyOn(Storage.prototype, 'getItem').and.throwError('blocked');
    spyOn(Storage.prototype, 'setItem').and.throwError('blocked');
    const service = createService();

    expect(service.collapsed()).toBeFalse();
    expect(() => service.toggle()).not.toThrow();
    expect(service.collapsed()).toBeTrue();
  });

  it('collapses the rail on wide viewports without opening the drawer', () => {
    mockViewport(true);
    const service = createService();

    service.toggle();

    expect(service.collapsed()).toBeTrue();
    expect(service.mobileOpen()).toBeFalse();
  });

  it('opens the drawer on narrow viewports without changing the stored rail state', () => {
    mockViewport(false);
    const service = createService();

    service.toggle();

    expect(service.mobileOpen()).toBeTrue();
    expect(service.expanded()).toBeTrue();
    expect(service.collapsed()).toBeFalse();
    expect(localStorage.getItem(SIDEBAR_COLLAPSED_KEY)).toBeNull();

    service.closeMobile();
    expect(service.mobileOpen()).toBeFalse();
  });

  it('closes the drawer when the viewport becomes wide', () => {
    mockViewport(false);
    const service = createService();
    service.toggle();

    changeListener!({ matches: true });

    expect(service.isWide()).toBeTrue();
    expect(service.mobileOpen()).toBeFalse();
  });
});

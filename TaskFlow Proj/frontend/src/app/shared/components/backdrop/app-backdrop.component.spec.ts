import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppBackdropComponent } from './app-backdrop.component';

describe('AppBackdropComponent', () => {
  let fixture: ComponentFixture<AppBackdropComponent>;
  let reduced: boolean;
  let listeners: Array<() => void>;

  beforeEach(async () => {
    reduced = false;
    listeners = [];
    spyOn(window, 'matchMedia').and.callFake(
      () =>
        ({
          get matches() {
            return reduced;
          },
          addEventListener: (_: string, fn: () => void) => listeners.push(fn),
          removeEventListener: (_: string, fn: () => void) => {
            listeners = listeners.filter((l) => l !== fn);
          }
        }) as unknown as MediaQueryList
    );
    await TestBed.configureTestingModule({ imports: [AppBackdropComponent] }).compileComponents();
  });

  afterEach(() => document.documentElement.removeAttribute('data-theme'));

  function create(): void {
    fixture = TestBed.createComponent(AppBackdropComponent);
    fixture.detectChanges();
  }

  it('renders an aria-hidden canvas', () => {
    create();
    const canvas = (fixture.nativeElement as HTMLElement).querySelector('canvas');
    expect(canvas).toBeTruthy();
    expect(canvas?.getAttribute('aria-hidden')).toBe('true');
  });

  it('animates by default', () => {
    create();
    expect(fixture.componentInstance.running).toBeTrue();
  });

  it('does not animate and hides the canvas under reduced motion', () => {
    reduced = true;
    create();
    expect(fixture.componentInstance.running).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).querySelector('canvas')?.hidden).toBeTrue();
  });

  it('stops the loop and releases listeners on destroy', () => {
    create();
    fixture.destroy();
    expect(fixture.componentInstance.running).toBeFalse();
    expect(listeners.length).toBe(0);
  });

  it('re-reads its colours when the theme changes', async () => {
    create();
    const refresh = spyOn(fixture.componentInstance as unknown as { refreshColors(): void }, 'refreshColors').and.callThrough();

    document.documentElement.setAttribute('data-theme', 'dark');
    await new Promise<void>((resolve) => setTimeout(resolve));

    expect(refresh).toHaveBeenCalled();
  });
});

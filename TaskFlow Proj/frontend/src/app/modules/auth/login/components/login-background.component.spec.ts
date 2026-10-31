import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoginBackgroundComponent } from './login-background.component';

describe('LoginBackgroundComponent', () => {
  let fixture: ComponentFixture<LoginBackgroundComponent>;
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
    await TestBed.configureTestingModule({ imports: [LoginBackgroundComponent] }).compileComponents();
  });

  function create(): void {
    fixture = TestBed.createComponent(LoginBackgroundComponent);
    fixture.detectChanges();
  }

  it('renders an aria-hidden canvas', () => {
    create();
    const canvas = (fixture.nativeElement as HTMLElement).querySelector('canvas');
    expect(canvas).toBeTruthy();
    expect(canvas?.getAttribute('aria-hidden')).toBe('true');
  });

  it('starts the animation loop', () => {
    create();
    expect(fixture.componentInstance.running).toBeTrue();
  });

  it('does not animate and hides the canvas under reduced motion', () => {
    reduced = true;
    create();
    expect(fixture.componentInstance.running).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).querySelector('canvas')?.hidden).toBeTrue();
  });

  it('stops the loop and releases the motion listener on destroy', () => {
    create();
    fixture.destroy();
    expect(fixture.componentInstance.running).toBeFalse();
    expect(listeners.length).toBe(0);
  });
});

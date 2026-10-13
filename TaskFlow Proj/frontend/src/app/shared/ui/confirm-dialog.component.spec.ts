import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmDialogComponent, ConfirmOptions } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  const options: ConfirmOptions = {
    title: 'Deactivate employee?',
    message: 'Rahul Menon will not be able to sign in.',
    confirmLabel: 'Deactivate',
    variant: 'danger'
  };

  let fixture: ComponentFixture<ConfirmDialogComponent>;

  function dialog(): HTMLDialogElement {
    return (fixture.nativeElement as HTMLElement).querySelector('dialog')!;
  }

  function button(action: 'confirm' | 'cancel'): HTMLButtonElement {
    return dialog().querySelector(`[data-action="${action}"]`) as HTMLButtonElement;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ConfirmDialogComponent] }).compileComponents();
    fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    if (dialog().open) {
      dialog().close();
    }
  });

  it('opens a modal with the title, message and confirm label', () => {
    void fixture.componentInstance.open(options);
    fixture.detectChanges();

    expect(dialog().open).toBeTrue();
    expect(dialog().textContent).toContain('Deactivate employee?');
    expect(dialog().textContent).toContain('Rahul Menon will not be able to sign in.');
    expect(button('confirm').textContent?.trim()).toBe('Deactivate');
    expect(button('confirm').classList).toContain('tf-btn--danger');
  });

  it('resolves true and closes when confirmed', async () => {
    const result = fixture.componentInstance.open(options);
    fixture.detectChanges();

    button('confirm').click();

    expect(await result).toBeTrue();
    expect(dialog().open).toBeFalse();
  });

  it('resolves false and closes when cancelled', async () => {
    const result = fixture.componentInstance.open(options);
    fixture.detectChanges();

    button('cancel').click();

    expect(await result).toBeFalse();
    expect(dialog().open).toBeFalse();
  });

  it('resolves false when dismissed with Escape', async () => {
    const result = fixture.componentInstance.open(options);
    fixture.detectChanges();

    dialog().dispatchEvent(new Event('cancel', { cancelable: true }));

    expect(await result).toBeFalse();
    expect(dialog().open).toBeFalse();
  });
});

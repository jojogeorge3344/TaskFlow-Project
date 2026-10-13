import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ToastHostComponent } from './toast-host.component';
import { TOAST_DURATION_MS, ToastService } from './toast.service';

describe('ToastService and ToastHostComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ToastHostComponent] }).compileComponents();
  });

  it('shows a toast in the host and dismisses it automatically', fakeAsync(() => {
    const fixture = TestBed.createComponent(ToastHostComponent);
    const service = TestBed.inject(ToastService);
    const element = fixture.nativeElement as HTMLElement;

    service.success('Employee added');
    fixture.detectChanges();

    expect(element.querySelectorAll('.tf-toast').length).toBe(1);
    expect(element.querySelector('.tf-toast')?.textContent).toContain('Employee added');

    tick(TOAST_DURATION_MS);
    fixture.detectChanges();

    expect(element.querySelectorAll('.tf-toast').length).toBe(0);
  }));

  it('marks error toasts', fakeAsync(() => {
    const fixture = TestBed.createComponent(ToastHostComponent);
    TestBed.inject(ToastService).error('Could not save');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.tf-toast--error')).toBeTruthy();
    tick(TOAST_DURATION_MS);
  }));
});

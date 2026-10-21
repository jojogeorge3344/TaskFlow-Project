import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { LoginComponent, NOTICE_POLL_MS } from './login.component';

describe('LoginComponent notification bell', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let httpMock: HttpTestingController;
  let navigate: jasmine.Spy;

  const el = () => fixture.nativeElement as HTMLElement;
  const bell = () => el().querySelector('[data-action="notice-bell"]') as HTMLButtonElement | null;
  const noticeUrl = (request: { url: string }) => request.url === '/api/v1/auth/password-reset-notice';
  const linkUrl = (request: { url: string }) => request.url === '/api/v1/auth/password-reset-link';

  function create(params: Record<string, string> = {}): void {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}), paramMap: convertToParamMap(params) } }
        }
      ]
    });
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function showBellByTyping(): void {
    component.form.setValue({ emailOrUsername: 'jojo', password: '' });
    component.checkNotice();
    httpMock.expectOne(noticeUrl).flush({ noticeAvailable: true });
    fixture.detectChanges();
  }

  it('shows no bell when there is no notice', () => {
    create();
    expect(bell()).toBeNull();
  });

  it('shows a bell with an unread badge and an accessible name when a notice is waiting', () => {
    create();
    showBellByTyping();

    expect(bell()).toBeTruthy();
    expect(bell()?.getAttribute('aria-label')).toContain('New password set');
    expect(bell()?.textContent?.trim()).toBe('1');
  });

  it('on a page without a link and no password typed the bell points at typing it - no update screen', () => {
    create();
    showBellByTyping();

    bell()!.click();
    fixture.detectChanges();

    expect(navigate).not.toHaveBeenCalled();
    expect(el().querySelector('[data-notice="sign-in-hint"]')).toBeTruthy();
    fixture.destroy();
  });

  it('carries the typed password to the update screen as the proof', () => {
    create();
    showBellByTyping();
    component.form.setValue({ emailOrUsername: 'jojo', password: 'Given-P@ss1' });

    bell()!.click();

    expect(navigate).toHaveBeenCalledOnceWith(['/reset-password'], {
      state: { email: 'jojo', currentPassword: 'Given-P@ss1' }
    });
    fixture.destroy();
  });

  it('opens the update screen with the encrypted token when the page was opened through a login link', () => {
    create({ token: 'enc-token' });
    httpMock.expectOne(linkUrl).flush({ email: 'manager@taskflow.com', noticeAvailable: true });
    fixture.detectChanges();

    bell()!.click();

    expect(navigate).toHaveBeenCalledOnceWith(['/reset-password'], {
      state: { token: 'enc-token', email: 'manager@taskflow.com' }
    });
  });

  it('keeps the bell for as long as it is not used - no time limit', fakeAsync(() => {
    create();
    showBellByTyping();

    for (let seconds = 0; seconds < 10 * 60; seconds += NOTICE_POLL_MS / 1000) {
      tick(NOTICE_POLL_MS);
      httpMock.expectOne(noticeUrl).flush({ noticeAvailable: true });
    }
    fixture.detectChanges();

    expect(bell()).toBeTruthy();
    fixture.destroy();
  }));

  it('shows the bell by itself when the notice appears while the page is open, without a reload', fakeAsync(() => {
    create();
    component.form.setValue({ emailOrUsername: 'jojo', password: '' });
    component.checkNotice();
    httpMock.expectOne(noticeUrl).flush({ noticeAvailable: false });
    fixture.detectChanges();
    expect(bell()).toBeNull();

    // The administrator finishes; the next periodic check picks it up.
    tick(NOTICE_POLL_MS);
    httpMock.expectOne(noticeUrl).flush({ noticeAvailable: true });
    fixture.detectChanges();

    expect(bell()).toBeTruthy();
    fixture.destroy();
  }));

  it('does not check before the page knows an account', fakeAsync(() => {
    create();

    tick(NOTICE_POLL_MS * 3);

    httpMock.expectNone(noticeUrl);
    fixture.destroy();
  }));

  it('does not pile up checks while one is still waiting for its answer', fakeAsync(() => {
    create();
    component.form.setValue({ emailOrUsername: 'jojo', password: '' });

    tick(NOTICE_POLL_MS * 3);

    const pending = httpMock.match(noticeUrl);
    expect(pending.length).toBe(1);
    pending[0].flush({ noticeAvailable: false });
    fixture.destroy();
  }));

  it('keeps showing the bell when a check fails', fakeAsync(() => {
    create();
    showBellByTyping();

    tick(NOTICE_POLL_MS);
    httpMock.expectOne(noticeUrl).flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(bell()).toBeTruthy();
    fixture.destroy();
  }));

  it('stops checking when the page is left', fakeAsync(() => {
    create();
    showBellByTyping();

    fixture.destroy();
    tick(NOTICE_POLL_MS * 3);

    httpMock.expectNone(noticeUrl);
  }));

  it('removes the bell as soon as a lookup stops returning the notice', () => {
    create();
    showBellByTyping();

    component.checkNotice();
    httpMock.expectOne(noticeUrl).flush({ noticeAvailable: false });
    fixture.detectChanges();

    expect(bell()).toBeNull();
  });

  it('has no popover any more: the bell goes straight to the update screen', () => {
    create();
    showBellByTyping();

    bell()!.click();
    fixture.detectChanges();

    expect(el().querySelector('[data-notice="popover"]')).toBeNull();
  });
});

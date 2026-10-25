import { AuthService } from '../../core/auth/auth.service';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MailService } from '../../core/services/mail.service';
import { ToastService } from '../../shared/ui/toast.service';
import { MailReaderComponent } from './mail-reader.component';
import { MailServiceStub, detail, mailServiceStub, message } from './mail-test-data';

describe('MailReaderComponent', () => {
  let mail: MailServiceStub;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    mail = mailServiceStub();
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  function create(): ComponentFixture<MailReaderComponent> {
    TestBed.configureTestingModule({
      imports: [MailReaderComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuthService(() => ({ permissions: systemPermissions('Employee') })) }, { provide: MailService, useValue: mail }, { provide: ToastService, useValue: toast }]
    });
    const fixture = TestBed.createComponent(MailReaderComponent);
    fixture.componentRef.setInput('itemId', 1);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<MailReaderComponent>) => fixture.nativeElement as HTMLElement;
  const click = (fixture: ComponentFixture<MailReaderComponent>, action: string) => {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  it('opens the mail, shows the conversation and refreshes the badge', () => {
    const first = message({ messageId: 9, body: 'Original question' });
    const current = message({ messageId: 10, body: 'The answer', attachments: [{ id: 5, fileName: 'plan.pdf', contentType: 'application/pdf', sizeBytes: 2048 }] });
    mail.open.and.returnValue(of(detail({ message: current, thread: [first, current] })));

    const fixture = create();

    expect(mail.open).toHaveBeenCalledWith(1);
    expect(mail.refreshUnreadCount).toHaveBeenCalled();
    const messages = el(fixture).querySelectorAll('[data-message]');
    expect(messages.length).toBe(2);
    expect(messages[0].textContent).toContain('Original question');
    expect(el(fixture).querySelector('[data-attachment="5"]')?.textContent).toContain('plan.pdf');
    expect(el(fixture).querySelector('[data-attachment="5"]')?.textContent).toContain('2 KB');
  });

  it('shows an address in the body as a clickable link', () => {
    const url = `${window.location.origin}/tasks/42`;
    { const m = message({ body: `Open this request: ${url}\nThanks` }); mail.open.and.returnValue(of(detail({ message: m, thread: [m] }))); }

    const fixture = create();

    const link = el(fixture).querySelector('[data-link]') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe(url);
    expect(link.textContent).toBe(url);
    expect(link.getAttribute('target')).toBeNull();
    expect(el(fixture).querySelector('.message__body')?.textContent).toContain('Open this request:');
    expect(el(fixture).querySelector('.message__body')?.textContent).toContain('Thanks');
  });

  it('opens a link to this application inside the app, without reloading', () => {
    const url = `${window.location.origin}/tasks/42`;
    { const m = message({ body: url }); mail.open.and.returnValue(of(detail({ message: m, thread: [m] }))); }
    const fixture = create();
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);

    const event = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    (el(fixture).querySelector('[data-link]') as HTMLAnchorElement).dispatchEvent(event);

    expect(event.defaultPrevented).toBeTrue();
    expect(navigate).toHaveBeenCalledWith('/tasks/42');
  });

  it('opens another site in a new tab and leaves the click alone', () => {
    { const m = message({ body: 'See https://example.test/page please' }); mail.open.and.returnValue(of(detail({ message: m, thread: [m] }))); }
    const fixture = create();
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);

    const link = el(fixture).querySelector('[data-link]') as HTMLAnchorElement;
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toContain('noopener');
    link.addEventListener('click', (event) => event.preventDefault());
    link.click();

    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not turn markup in a body into HTML', () => {
    { const m = message({ body: '<img src=x onerror=alert(1)> http://x.test/a' }); mail.open.and.returnValue(of(detail({ message: m, thread: [m] }))); }
    const fixture = create();

    expect(el(fixture).querySelector('.message__body img')).toBeNull();
    expect(el(fixture).querySelector('.message__body')?.textContent).toContain('<img src=x');
  });

  it('offers reply, reply all and forward for a person\'s mail', () => {
    const fixture = create();
    const reply = jasmine.createSpy('reply');
    fixture.componentInstance.reply.subscribe(reply);

    click(fixture, 'reply-all');

    expect(reply).toHaveBeenCalledWith({ itemId: 1, mode: 'ReplyAll' });
    expect(el(fixture).querySelector('[data-action="forward"]')).not.toBeNull();
  });

  it('hides reply on mail from TaskFlow but keeps forward and the task link', () => {
    mail.open.and.returnValue(of(detail({}, { isSystem: true, from: { userId: null, name: 'TaskFlow', email: '' }, taskId: 7 })));
    const fixture = create();

    expect(el(fixture).querySelector('[data-action="reply"]')).toBeNull();
    expect(el(fixture).querySelector('[data-action="reply-all"]')).toBeNull();
    expect(el(fixture).querySelector('[data-action="forward"]')).not.toBeNull();
    expect(el(fixture).querySelector('[data-action="open-task"]')?.getAttribute('href')).toBe('/tasks/7');
  });

  it('stars and marks important', () => {
    const fixture = create();
    const changed = jasmine.createSpy('changed');
    fixture.componentInstance.changed.subscribe(changed);

    click(fixture, 'star');
    click(fixture, 'important');

    expect(mail.setFlags).toHaveBeenCalledWith(1, { isStarred: true });
    expect(mail.setFlags).toHaveBeenCalledWith(1, { isImportant: true });
    expect(changed).toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-action="star"]')!.getAttribute('aria-pressed')).toBe('true');
  });

  it('moves to Trash and closes', () => {
    const fixture = create();
    const closed = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(closed);

    click(fixture, 'trash');

    expect(mail.trash).toHaveBeenCalledWith(1);
    expect(toast.success).toHaveBeenCalledWith('Moved to Trash');
    expect(closed).toHaveBeenCalled();
  });

  it('offers restore and delete forever for trashed mail', () => {
    mail.open.and.returnValue(of(detail({ isTrashed: true })));
    const fixture = create();

    expect(el(fixture).querySelector('[data-action="reply"]')).toBeNull();
    click(fixture, 'restore');
    expect(mail.restore).toHaveBeenCalledWith(1);
    click(fixture, 'delete-forever');
    expect(mail.deleteForever).toHaveBeenCalledWith(1);
  });

  it('marks unread and goes back', () => {
    const fixture = create();
    const back = jasmine.createSpy('back');
    fixture.componentInstance.back.subscribe(back);

    click(fixture, 'unread');

    expect(mail.setFlags).toHaveBeenCalledWith(1, { isRead: false });
    expect(back).toHaveBeenCalled();
  });

  it('downloads an attachment', () => {
    mail.open.and.returnValue(of(detail({}, { attachments: [{ id: 5, fileName: 'plan.pdf', contentType: 'application/pdf', sizeBytes: 10 }] })));
    spyOn(URL, 'createObjectURL').and.returnValue('blob:x');
    spyOn(HTMLAnchorElement.prototype, 'click');
    const fixture = create();

    (el(fixture).querySelector('[data-attachment="5"]') as HTMLButtonElement).click();

    expect(mail.download).toHaveBeenCalledWith(5);
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();
  });
});

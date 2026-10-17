import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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
      providers: [provideRouter([]), { provide: MailService, useValue: mail }, { provide: ToastService, useValue: toast }]
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

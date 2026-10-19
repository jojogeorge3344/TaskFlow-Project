import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { MailService } from '../../core/services/mail.service';
import { ToastService } from '../../shared/ui/toast.service';
import { ComposeDialogComponent, ComposeRequest } from './compose-dialog.component';
import { MailServiceStub, detail, mailServiceStub } from './mail-test-data';

describe('ComposeDialogComponent', () => {
  let mail: MailServiceStub;
  let toast: jasmine.SpyObj<ToastService>;
  const priya = { email: 'priya@taskflow.com', name: 'Priya', valid: true };

  beforeEach(() => {
    mail = mailServiceStub();
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  function create(request: ComposeRequest = { kind: 'new' }): ComponentFixture<ComposeDialogComponent> {
    TestBed.configureTestingModule({
      imports: [ComposeDialogComponent],
      providers: [{ provide: MailService, useValue: mail }, { provide: ToastService, useValue: toast }]
    });
    const fixture = TestBed.createComponent(ComposeDialogComponent);
    fixture.componentRef.setInput('request', request);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<ComposeDialogComponent>) => fixture.nativeElement as HTMLElement;
  const click = (fixture: ComponentFixture<ComposeDialogComponent>, action: string) => {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  function write(fixture: ComponentFixture<ComposeDialogComponent>, subject: string, body: string): void {
    fixture.componentInstance.subject.setValue(subject);
    fixture.componentInstance.body.setValue(body);
    fixture.detectChanges();
  }

  it('sends a new mail to TaskFlow users', () => {
    const fixture = create();
    const closed = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(closed);
    fixture.componentInstance.to.set([priya]);
    write(fixture, 'Hello', 'Body text');

    click(fixture, 'send');

    expect(mail.send).toHaveBeenCalledWith({
      to: ['priya@taskflow.com'],
      cc: [],
      subject: 'Hello',
      body: 'Body text',
      attachmentIds: [],
      draftId: null,
      parentItemId: null,
      mode: null
    });
    expect(toast.success).toHaveBeenCalledWith('Mail sent');
    expect(closed).toHaveBeenCalledWith('sent');
  });

  it('blocks sending while a recipient is not a TaskFlow user', () => {
    const fixture = create();
    fixture.componentInstance.to.set([priya, { email: 'someone@gmail.com', name: null, valid: false }]);
    write(fixture, 'Hello', 'Body');

    click(fixture, 'send');

    expect(mail.send).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="To"]')?.textContent).toContain('not TaskFlow users');
  });

  it('needs a recipient and some content', () => {
    const fixture = create();

    click(fixture, 'send');
    expect(el(fixture).querySelector('[data-error="To"]')?.textContent).toContain('at least one recipient');

    fixture.componentInstance.to.set([priya]);
    click(fixture, 'send');
    expect(el(fixture).querySelector('[data-error="Body"]')).not.toBeNull();
    expect(mail.send).not.toHaveBeenCalled();
  });

  it('prefills a reply all and sends it in the conversation', () => {
    mail.compose.and.returnValue(of({
      mode: 'ReplyAll',
      parentItemId: 8,
      to: ['ravi@taskflow.com', 'asha@taskflow.com'],
      cc: ['kiran@taskflow.com'],
      subject: 'Re: Kickoff',
      body: '\n\nOn 17 Oct 2026, Ravi wrote:\n> Agenda',
      forwardedAttachments: []
    }));
    const fixture = create({ kind: 'reply', itemId: 8, mode: 'ReplyAll' });

    expect(mail.compose).toHaveBeenCalledWith(8, 'ReplyAll');
    expect(fixture.componentInstance.to().map((chip) => chip.email)).toEqual(['ravi@taskflow.com', 'asha@taskflow.com']);
    expect(fixture.componentInstance.showCc()).toBeTrue();
    expect(fixture.componentInstance.subject.value).toBe('Re: Kickoff');
    expect(el(fixture).querySelector('#tf-drawer-title')?.textContent).toContain('Reply all');

    click(fixture, 'send');

    expect(mail.send).toHaveBeenCalledWith(jasmine.objectContaining({ parentItemId: 8, mode: 'ReplyAll', cc: ['kiran@taskflow.com'] }));
  });

  it('shows the files a forward carries along', () => {
    mail.compose.and.returnValue(of({
      mode: 'Forward',
      parentItemId: 8,
      to: [],
      cc: [],
      subject: 'Fwd: Specs',
      body: '---------- Forwarded message ----------',
      forwardedAttachments: [{ id: 3, fileName: 'specs.pdf', contentType: 'application/pdf', sizeBytes: 100 }]
    }));
    const fixture = create({ kind: 'reply', itemId: 8, mode: 'Forward' });

    expect(el(fixture).querySelector('[data-forwarded="3"]')?.textContent).toContain('specs.pdf');
    expect(fixture.componentInstance.to()).toEqual([]);
  });

  it('continues a draft and saves it again', () => {
    mail.open.and.returnValue(of(detail(
      { folder: 'Drafts', draftTo: 'priya@taskflow.com, oops', draftCc: null },
      { messageId: 40, subject: 'Plan', body: 'v1', attachments: [{ id: 2, fileName: 'a.txt', contentType: 'text/plain', sizeBytes: 3 }] }
    )));
    mail.contacts.and.callFake((email: string) => of(email === 'priya@taskflow.com' ? [{ id: 3, fullName: 'Priya', email }] : []));
    const fixture = create({ kind: 'draft', itemId: 11 });

    expect(fixture.componentInstance.subject.value).toBe('Plan');
    expect(fixture.componentInstance.attachments().map((file) => file.id)).toEqual([2]);
    expect(fixture.componentInstance.to().map((chip) => [chip.email, chip.valid])).toEqual([
      ['priya@taskflow.com', true],
      ['oops', false]
    ]);

    click(fixture, 'save-draft');

    expect(mail.updateDraft).toHaveBeenCalledWith(40, jasmine.objectContaining({ to: ['priya@taskflow.com', 'oops'], attachmentIds: [2] }));
    expect(toast.success).toHaveBeenCalledWith('Draft saved');
  });

  it('saves a new draft and discards a saved one', () => {
    const fixture = create();
    fixture.componentInstance.to.set([priya]);
    write(fixture, 'Later', '');
    click(fixture, 'save-draft');
    expect(mail.createDraft).toHaveBeenCalledWith(jasmine.objectContaining({ subject: 'Later', to: ['priya@taskflow.com'] }));

    TestBed.resetTestingModule();
    mail.open.and.returnValue(of(detail({ folder: 'Drafts' }, { messageId: 40 })));
    const draft = create({ kind: 'draft', itemId: 11 });
    click(draft, 'discard');
    expect(mail.deleteForever).toHaveBeenCalledWith(11);
  });

  it('refuses files over the limit or of a blocked type before uploading', async () => {
    const fixture = create();
    const big = new File(['x'], 'big.pdf');
    Object.defineProperty(big, 'size', { value: 11 * 1024 * 1024 });
    const exe = new File(['MZ'], 'setup.exe');

    await fixture.componentInstance.onFiles({ files: [big], value: '' } as unknown as HTMLInputElement);
    fixture.detectChanges();
    expect(el(fixture).querySelector('[data-error="File"]')?.textContent).toContain('larger than 10 MB');

    await fixture.componentInstance.onFiles({ files: [exe], value: '' } as unknown as HTMLInputElement);
    fixture.detectChanges();
    expect(el(fixture).querySelector('[data-error="File"]')?.textContent).toContain('.exe');
    expect(mail.uploadAttachment).not.toHaveBeenCalled();
  });

  it('uploads files and removes ones uploaded here', async () => {
    mail.uploadAttachment.and.returnValue(of({ id: 9, fileName: 'notes.txt', contentType: 'text/plain', sizeBytes: 3 }));
    const fixture = create();

    await fixture.componentInstance.onFiles({ files: [new File(['abc'], 'notes.txt')], value: '' } as unknown as HTMLInputElement);
    fixture.detectChanges();
    expect(el(fixture).querySelector('[data-attachment="9"]')?.textContent).toContain('notes.txt');

    (el(fixture).querySelector('[data-attachment="9"] button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(mail.deleteAttachment).toHaveBeenCalledWith(9);
    expect(fixture.componentInstance.attachments()).toEqual([]);
  });

  it('shows server field errors next to their fields', () => {
    mail.send.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400,
      error: { errors: { To: ['Not an active TaskFlow user: gone@taskflow.com.'] } }
    })));
    const fixture = create();
    fixture.componentInstance.to.set([priya]);
    write(fixture, 'Hi', 'x');

    click(fixture, 'send');

    expect(el(fixture).querySelector('[data-error="To"]')?.textContent).toContain('gone@taskflow.com');
  });

  function typeInTo(fixture: ComponentFixture<ComposeDialogComponent>, text: string): void {
    const input = el(fixture).querySelector('#compose-to') as HTMLInputElement;
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('includes an address still being typed when Send is clicked', () => {
    mail.contacts.and.callFake((email: string) => of(email === 'kiran@taskflow.com' ? [{ id: 4, fullName: 'Kiran', email }] : []));
    const fixture = create();
    fixture.componentInstance.to.set([priya]);
    write(fixture, 'Hello', 'Body');
    typeInTo(fixture, 'kiran@taskflow.com');

    click(fixture, 'send');
    fixture.detectChanges();
    fixture.detectChanges();

    expect(mail.send).toHaveBeenCalledTimes(1);
    expect(mail.send.calls.mostRecent().args[0].to).toEqual(['priya@taskflow.com', 'kiran@taskflow.com']);
    expect(fixture.componentInstance.sendWhenChecked()).toBeFalse();
  });

  it('does not send when the typed address turns out not to be a TaskFlow user', () => {
    mail.contacts.and.returnValue(of([]));
    const fixture = create();
    fixture.componentInstance.to.set([priya]);
    write(fixture, 'Hello', 'Body');
    typeInTo(fixture, 'someone@gmail.com');

    click(fixture, 'send');
    fixture.detectChanges();
    fixture.detectChanges();

    expect(mail.send).not.toHaveBeenCalled();
    expect(fixture.componentInstance.to().map((chip) => chip.email)).toContain('someone@gmail.com');
    expect(el(fixture).querySelector('[data-error="To"]')?.textContent).toContain('not TaskFlow users');
  });

  it('keeps a typed address when saving a draft', () => {
    const fixture = create();
    typeInTo(fixture, 'half-typed@taskflow.com');

    click(fixture, 'save-draft');

    expect(mail.createDraft).toHaveBeenCalledWith(jasmine.objectContaining({ to: ['half-typed@taskflow.com'] }));
  });
});

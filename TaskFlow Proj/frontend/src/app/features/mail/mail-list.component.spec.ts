import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { MailService } from '../../core/services/mail.service';
import { MailListComponent } from './mail-list.component';
import { MailServiceStub, listItem, mailServiceStub } from './mail-test-data';

describe('MailListComponent', () => {
  let mail: MailServiceStub;

  beforeEach(() => {
    mail = mailServiceStub();
  });

  function create(folder: 'Inbox' | 'Sent' | 'Drafts' = 'Inbox'): ComponentFixture<MailListComponent> {
    TestBed.configureTestingModule({ imports: [MailListComponent], providers: [{ provide: MailService, useValue: mail }] });
    const fixture = TestBed.createComponent(MailListComponent);
    fixture.componentRef.setInput('folder', folder);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<MailListComponent>) => fixture.nativeElement as HTMLElement;

  it('loads the folder and shows unread rows in bold with their sender', () => {
    const fixture = create();

    expect(mail.list).toHaveBeenCalledWith(jasmine.objectContaining({ folder: 'Inbox', page: 1 }));
    const row = el(fixture).querySelector('[data-item="1"]')!;
    expect(row.classList).toContain('mail-row--unread');
    expect(row.textContent).toContain('Ravi Manager');
    expect(row.textContent).toContain('Weekly report');
  });

  it('shows recipients in Sent and TaskFlow for system mail', () => {
    mail.list.and.returnValue(of({ items: [listItem({ folder: 'Sent', isRead: true })], total: 1, page: 1, pageSize: 25 }));
    const sent = create('Sent');
    expect(el(sent).querySelector('.mail-row__who')?.textContent).toContain('To: Priya');

    TestBed.resetTestingModule();
    mail.list.and.returnValue(of({ items: [listItem({ isSystem: true, from: { userId: null, name: 'TaskFlow', email: '' } })], total: 1, page: 1, pageSize: 25 }));
    const inbox = create();
    expect(el(inbox).querySelector('.mail-row__system')?.textContent).toContain('TaskFlow');
  });

  it('reloads when the folder changes', () => {
    const fixture = create();
    mail.list.calls.reset();

    fixture.componentRef.setInput('folder', 'Sent');
    fixture.detectChanges();

    expect(mail.list).toHaveBeenCalledWith(jasmine.objectContaining({ folder: 'Sent', page: 1 }));
  });

  it('opens a row and marks it read in the list', () => {
    const fixture = create();
    const opened = jasmine.createSpy('opened');
    fixture.componentInstance.opened.subscribe(opened);

    (el(fixture).querySelector('[data-item="1"]') as HTMLElement).click();
    fixture.detectChanges();

    expect(opened).toHaveBeenCalledWith(jasmine.objectContaining({ itemId: 1 }));
    expect(el(fixture).querySelector('[data-item="1"]')!.classList).not.toContain('mail-row--unread');
  });

  it('toggles the star without opening the mail', () => {
    const fixture = create();
    const opened = jasmine.createSpy('opened');
    const changed = jasmine.createSpy('changed');
    fixture.componentInstance.opened.subscribe(opened);
    fixture.componentInstance.changed.subscribe(changed);

    (el(fixture).querySelector('[data-action="star"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(mail.setFlags).toHaveBeenCalledWith(1, { isStarred: true });
    expect(changed).toHaveBeenCalled();
    expect(opened).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-action="star"]')!.classList).toContain('mail-row__star--on');
  });

  it('searches after a pause', fakeAsync(() => {
    const fixture = create();
    mail.list.calls.reset();

    const input = el(fixture).querySelector('[data-field="search"]') as HTMLInputElement;
    input.value = 'invoice';
    input.dispatchEvent(new Event('input'));
    tick(300);
    fixture.detectChanges();

    expect(mail.list).toHaveBeenCalledWith(jasmine.objectContaining({ search: 'invoice', page: 1 }));
  }));

  it('shows an empty state', () => {
    mail.list.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 25 }));
    const fixture = create();

    expect(el(fixture).querySelector('[data-status="empty"]')?.textContent).toContain('Nothing in Inbox');
  });
});

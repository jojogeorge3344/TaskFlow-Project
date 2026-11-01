import { AuthService } from '../../../../shared/auth/auth.service';
import { fakeAuthService, systemPermissions } from '../../../../shared/testing/permissions';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, map } from 'rxjs';
import { MailService } from '../services/mail.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { MailComponent } from './mail.component';
import { MailServiceStub, listItem, mailServiceStub } from '../../../../shared/testing/mail-test-data';

describe('MailComponent', () => {
  let mail: MailServiceStub;
  let params: BehaviorSubject<Record<string, string>>;
  let navigate: jasmine.Spy;

  beforeEach(() => {
    mail = mailServiceStub();
    params = new BehaviorSubject<Record<string, string>>({});
  });

  function create(): ComponentFixture<MailComponent> {
    TestBed.configureTestingModule({
      imports: [MailComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: fakeAuthService(() => ({ permissions: systemPermissions('Employee') })) },
        { provide: MailService, useValue: mail },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']) },
        { provide: ActivatedRoute, useValue: { queryParamMap: params.pipe(map((value) => convertToParamMap(value))) } }
      ]
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(MailComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<MailComponent>) => fixture.nativeElement as HTMLElement;

  it('shows the folders with unread and draft counts', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-count="Inbox"]')?.textContent).toContain('2');
    expect(el(fixture).querySelector('[data-count="Drafts"]')?.textContent).toContain('1');
    expect(el(fixture).querySelector('[data-folder="Inbox"]')?.getAttribute('aria-current')).toBe('page');
    expect(mail.unreadCount.set).toHaveBeenCalledWith(2);
  });

  it('takes the folder and opened mail from the URL', () => {
    params.next({ folder: 'sent', item: '42' });
    const fixture = create();

    expect(fixture.componentInstance.folder()).toBe('Sent');
    expect(fixture.componentInstance.itemId()).toBe(42);
    expect(mail.list).toHaveBeenCalledWith(jasmine.objectContaining({ folder: 'Sent' }));
    expect(mail.open).toHaveBeenCalledWith(42);
  });

  it('switches folders through the URL', () => {
    const fixture = create();

    (el(fixture).querySelector('[data-folder="Starred"]') as HTMLButtonElement).click();

    expect(navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { folder: 'starred', item: null } }));
  });

  it('opens a mail in the reader but a draft in the composer', () => {
    const fixture = create();

    fixture.componentInstance.openItem(listItem({ itemId: 5 }));
    expect(navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { item: 5 } }));

    navigate.calls.reset();
    fixture.componentInstance.openItem(listItem({ itemId: 6, folder: 'Drafts' }));
    expect(navigate).not.toHaveBeenCalled();
    expect(fixture.componentInstance.compose()).toEqual({ kind: 'draft', itemId: 6 });
  });

  it('reloads the list and counts after the composer sends', () => {
    const fixture = create();
    fixture.componentInstance.newMail();
    expect(fixture.componentInstance.compose()).toEqual({ kind: 'new' });
    mail.summary.calls.reset();
    const before = fixture.componentInstance.refreshToken();

    fixture.componentInstance.onComposeClosed('sent');

    expect(fixture.componentInstance.compose()).toBeNull();
    expect(fixture.componentInstance.refreshToken()).toBe(before + 1);
    expect(mail.summary).toHaveBeenCalled();
  });

  it('shows one pane at a time on narrow screens', () => {
    const fixture = create();
    const component = fixture.componentInstance;
    component.narrow.set(true);
    fixture.detectChanges();
    const mailEl = () => el(fixture).querySelector('.mail')!;

    expect(mailEl().getAttribute('data-pane')).toBe('list');
    expect(el(fixture).querySelector('[data-action="show-folders"]')?.textContent).toContain('Inbox');

    (el(fixture).querySelector('[data-action="show-folders"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(mailEl().getAttribute('data-pane')).toBe('folders');

    (el(fixture).querySelector('[data-folder="Sent"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(component.foldersOpen()).toBeFalse();

    params.next({ item: '3' });
    fixture.detectChanges();
    expect(mailEl().getAttribute('data-pane')).toBe('reader');
  });

  it('shows all panes on wide screens', () => {
    const fixture = create();
    fixture.componentInstance.narrow.set(false);
    fixture.detectChanges();

    expect(el(fixture).querySelector('.mail')!.getAttribute('data-pane')).toBe('all');
    expect(el(fixture).querySelector('[data-action="show-folders"]')).toBeNull();
  });
});


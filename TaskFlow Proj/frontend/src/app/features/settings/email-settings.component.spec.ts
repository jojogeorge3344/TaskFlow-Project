import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { EmailDelivery, EmailSettings } from '../../core/models';
import { EmailSettingsService } from '../../core/services/email-settings.service';
import { ToastService } from '../../shared/ui/toast.service';
import { EmailSettingsComponent } from './email-settings.component';

describe('EmailSettingsComponent', () => {
  const stored: EmailSettings = {
    emailEnabled: true,
    smtpHost: 'smtp.example.com',
    smtpPort: 587,
    smtpSecurity: 'StartTls',
    senderAddress: 'taskflow@example.com',
    senderName: 'TaskFlow',
    smtpUsername: 'mailer',
    sendTime: '09:00',
    passwordConfigured: true,
    issues: [],
    updatedAt: '2026-10-17T06:00:00',
    updatedByName: 'Asha Admin'
  };

  const deliveries: EmailDelivery[] = [
    {
      id: 2,
      kind: 'Reminder',
      toAddress: 'ravi@example.com',
      subject: 'Task reminder: Prepare Monthly Report',
      taskId: 7,
      taskTitle: 'Prepare Monthly Report',
      status: 'Failed',
      attempts: 3,
      lastError: 'Could not connect to smtp.example.com:587.',
      createdAt: '2026-10-17T03:30:00',
      lastAttemptAt: '2026-10-17T03:50:00',
      nextAttemptAt: null,
      sentAt: null
    },
    {
      id: 1,
      kind: 'Test',
      toAddress: 'admin@example.com',
      subject: 'TaskFlow test email',
      taskId: null,
      taskTitle: null,
      status: 'Sent',
      attempts: 1,
      lastError: null,
      createdAt: '2026-10-17T03:00:00',
      lastAttemptAt: '2026-10-17T03:00:00',
      nextAttemptAt: null,
      sentAt: '2026-10-17T03:00:01'
    }
  ];

  let getSpy: jasmine.Spy;
  let saveSpy: jasmine.Spy;
  let testSpy: jasmine.Spy;
  let deliveriesSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(stored));
    saveSpy = jasmine.createSpy('save');
    testSpy = jasmine.createSpy('sendTest');
    deliveriesSpy = jasmine.createSpy('deliveries').and.returnValue(of(deliveries));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  function create(): ComponentFixture<EmailSettingsComponent> {
    TestBed.configureTestingModule({
      imports: [EmailSettingsComponent],
      providers: [
        {
          provide: EmailSettingsService,
          useValue: { get: getSpy, save: saveSpy, sendTest: testSpy, deliveries: deliveriesSpy }
        },
        { provide: AuthService, useValue: { currentUser: () => ({ email: 'admin@example.com' }) } },
        { provide: ToastService, useValue: toast }
      ]
    });
    const fixture = TestBed.createComponent(EmailSettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<EmailSettingsComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function field(fixture: ComponentFixture<EmailSettingsComponent>, name: string): HTMLInputElement {
    return el(fixture).querySelector(`[data-field="${name}"]`) as HTMLInputElement;
  }

  function type(fixture: ComponentFixture<EmailSettingsComponent>, name: string, value: string): void {
    const input = field(fixture, name);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function click(fixture: ComponentFixture<EmailSettingsComponent>, action: string): void {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('loads the stored values, password status and last change', () => {
    const fixture = create();

    expect(field(fixture, 'emailEnabled').checked).toBeTrue();
    expect(field(fixture, 'smtpHost').value).toBe('smtp.example.com');
    expect(field(fixture, 'smtpPort').value).toBe('587');
    expect(field(fixture, 'senderAddress').value).toBe('taskflow@example.com');
    expect(field(fixture, 'smtpUsername').value).toBe('mailer');
    expect(field(fixture, 'sendTime').value).toBe('09:00');
    expect(el(fixture).querySelector('[data-status="password-configured"]')).not.toBeNull();
    expect(el(fixture).querySelector('[data-status="updated"]')?.textContent).toContain('Asha Admin');
    expect(el(fixture).querySelector('input[type="password"]')).toBeNull();
  });

  it('shows "Not configured" when the server has no password', () => {
    getSpy.and.returnValue(of({ ...stored, passwordConfigured: false }));
    const fixture = create();

    expect(el(fixture).querySelector('[data-status="password-missing"]')?.textContent).toContain('Not configured');
  });

  it('shows configuration problems only while email is enabled', () => {
    getSpy.and.returnValue(of({ ...stored, issues: ['SMTP server is not set.'] }));
    const enabled = create();
    expect(el(enabled).querySelector('[data-region="issues"]')?.textContent).toContain('SMTP server is not set.');

    TestBed.resetTestingModule();
    getSpy.and.returnValue(of({ ...stored, emailEnabled: false, issues: ['SMTP server is not set.'] }));
    const disabled = create();
    expect(el(disabled).querySelector('[data-region="issues"]')).toBeNull();
  });

  it('saves the form and confirms with a toast', () => {
    saveSpy.and.returnValue(of({ ...stored, smtpPort: 2525 }));
    const fixture = create();

    type(fixture, 'smtpPort', '2525');
    click(fixture, 'save');

    expect(saveSpy).toHaveBeenCalledWith({
      emailEnabled: true,
      smtpHost: 'smtp.example.com',
      smtpPort: 2525,
      smtpSecurity: 'StartTls',
      senderAddress: 'taskflow@example.com',
      senderName: 'TaskFlow',
      smtpUsername: 'mailer',
      sendTime: '09:00'
    });
    expect(toast.success).toHaveBeenCalledWith('Email settings saved');
    expect(field(fixture, 'smtpPort').value).toBe('2525');
  });

  it('blocks saving an enabled configuration without a server', () => {
    const fixture = create();

    type(fixture, 'smtpHost', '');
    click(fixture, 'save');

    expect(saveSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="smtpHost"]')?.textContent).toContain('SMTP server is required');
  });

  it('maps server validation errors onto their fields', () => {
    saveSpy.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { title: 'One or more validation errors occurred.', errors: { SmtpPort: ['Port is not allowed.'] } }
          })
      )
    );
    const fixture = create();

    click(fixture, 'save');

    expect(el(fixture).querySelector('[data-error="smtpPort"]')?.textContent).toContain('Port is not allowed.');
  });

  it('disables the test while the form has unsaved changes', () => {
    const fixture = create();

    type(fixture, 'smtpHost', 'smtp.other.com');

    const button = el(fixture).querySelector('[data-action="test"]') as HTMLButtonElement;
    expect(button.disabled).toBeTrue();
    expect(el(fixture).querySelector('[data-status="save-first"]')?.textContent).toContain('Save changes before testing');
  });

  it('sends a test to the prefilled address and shows the result', () => {
    testSpy.and.returnValue(of({ success: true, message: 'Test email sent to admin@example.com.', deliveryId: 3 }));
    const fixture = create();

    expect(field(fixture, 'testAddress').value).toBe('admin@example.com');
    click(fixture, 'test');

    expect(testSpy).toHaveBeenCalledWith('admin@example.com');
    const result = el(fixture).querySelector('[data-region="test-result"]');
    expect(result?.textContent).toContain('Test email sent to admin@example.com.');
    expect(result?.classList).toContain('email-test__result--ok');
    expect(deliveriesSpy).toHaveBeenCalledTimes(2);
  });

  it('shows a failed test reason', () => {
    testSpy.and.returnValue(
      of({ success: false, message: 'Authentication failed: the SMTP server rejected the username or password.', deliveryId: 4 })
    );
    const fixture = create();

    click(fixture, 'test');

    const result = el(fixture).querySelector('[data-region="test-result"]');
    expect(result?.textContent).toContain('Authentication failed');
    expect(result?.classList).toContain('email-test__result--failed');
  });

  it('lists recent deliveries with status badges and errors', () => {
    const fixture = create();

    const rows = el(fixture).querySelectorAll('tr.delivery-row');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Prepare Monthly Report');
    expect(rows[0].querySelector('.email-status--failed')?.textContent).toContain('Failed');
    expect(rows[0].textContent).toContain('Could not connect');
    expect(rows[1].textContent).toContain('Test email');
    expect(rows[1].querySelector('.email-status--sent')).not.toBeNull();
  });

  it('shows an empty state when nothing has been sent', () => {
    deliveriesSpy.and.returnValue(of([]));
    const fixture = create();

    expect(el(fixture).querySelector('[data-status="no-deliveries"]')).not.toBeNull();
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EmailSettings, UpdateEmailSettings } from '../models';
import { EmailSettingsService } from './email-settings.service';

describe('EmailSettingsService', () => {
  let service: EmailSettingsService;
  let httpMock: HttpTestingController;

  const update: UpdateEmailSettings = {
    emailEnabled: true,
    smtpHost: 'smtp.example.com',
    smtpPort: 587,
    smtpSecurity: 'StartTls',
    senderAddress: 'taskflow@example.com',
    senderName: 'TaskFlow',
    smtpUsername: '',
    sendTime: '09:00'
  };
  const stored: EmailSettings = {
    ...update,
    passwordConfigured: false,
    issues: [],
    updatedAt: '2026-10-17T06:00:00Z',
    updatedByName: 'Asha Admin'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(EmailSettingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the configuration with GET', () => {
    let result: EmailSettings | undefined;
    service.get().subscribe((settings) => (result = settings));

    const request = httpMock.expectOne('/api/v1/settings/email');
    expect(request.request.method).toBe('GET');
    request.flush(stored);

    expect(result).toEqual(stored);
  });

  it('saves the editable fields with PUT and never sends a password', () => {
    service.save(update).subscribe();

    const request = httpMock.expectOne('/api/v1/settings/email');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    expect(Object.keys(request.request.body).some((key) => key.toLowerCase().includes('password'))).toBeFalse();
    request.flush(stored);
  });

  it('posts a test email to the given address', () => {
    let message = '';
    service.sendTest('ops@example.com').subscribe((result) => (message = result.message));

    const request = httpMock.expectOne('/api/v1/settings/email/test');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ toAddress: 'ops@example.com' });
    request.flush({ success: true, message: 'Test email sent to ops@example.com.', deliveryId: 4 });

    expect(message).toContain('ops@example.com');
  });

  it('lists recent deliveries with the requested count', () => {
    service.deliveries(20).subscribe();

    const request = httpMock.expectOne((req) => req.url === '/api/v1/settings/email/deliveries');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('take')).toBe('20');
    request.flush([]);
  });
});

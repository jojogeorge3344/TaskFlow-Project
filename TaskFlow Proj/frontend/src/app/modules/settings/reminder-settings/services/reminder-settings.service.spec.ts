import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReminderSettings, UpdateReminderSettings } from '../../models/settings.model';
import { ReminderSettingsService } from './reminder-settings.service';

describe('ReminderSettingsService', () => {
  let service: ReminderSettingsService;
  let httpMock: HttpTestingController;

  const stored: ReminderSettings = {
    enabled: true,
    deadlineDayEnabled: true,
    defaultFirstDays: 2,
    defaultSecondDays: 1,
    defaultOverdueFrequency: 'Daily',
    mailEnabled: false,
    timezone: 'Asia/Kolkata'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ReminderSettingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the settings with GET', () => {
    let result: ReminderSettings | undefined;
    service.get().subscribe((settings) => (result = settings));

    const request = httpMock.expectOne('/api/v1/settings/reminders');
    expect(request.request.method).toBe('GET');
    request.flush(stored);

    expect(result).toEqual(stored);
  });

  it('saves the editable fields with PUT and returns the stored settings', () => {
    const update: UpdateReminderSettings = {
      enabled: false,
      deadlineDayEnabled: true,
      defaultFirstDays: 3,
      defaultSecondDays: 0,
      defaultOverdueFrequency: 'Weekly',
      mailEnabled: true
    };
    let result: ReminderSettings | undefined;
    service.save(update).subscribe((settings) => (result = settings));

    const request = httpMock.expectOne('/api/v1/settings/reminders');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    expect('timezone' in request.request.body).toBeFalse();
    request.flush({ ...update, timezone: 'Asia/Kolkata' });

    expect(result?.defaultFirstDays).toBe(3);
  });
});

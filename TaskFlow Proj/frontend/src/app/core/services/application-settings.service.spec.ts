import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApplicationSettings, UpdateApplicationSettings } from '../models';
import { ApplicationSettingsService } from './application-settings.service';

describe('ApplicationSettingsService', () => {
  let service: ApplicationSettingsService;
  let httpMock: HttpTestingController;

  const stored: ApplicationSettings = {
    name: 'TaskFlow',
    timezone: 'Asia/Kolkata',
    defaultPriority: 'Medium',
    inAppNotificationsEnabled: true
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ApplicationSettingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the settings with GET', () => {
    let result: ApplicationSettings | undefined;
    service.get().subscribe((settings) => (result = settings));

    const request = httpMock.expectOne('/api/v1/settings/application');
    expect(request.request.method).toBe('GET');
    request.flush(stored);

    expect(result).toEqual(stored);
  });

  it('saves the settings with PUT and returns the stored settings', () => {
    const update: UpdateApplicationSettings = {
      name: 'Acme Tasks',
      timezone: 'UTC',
      defaultPriority: 'High',
      inAppNotificationsEnabled: false
    };
    let result: ApplicationSettings | undefined;
    service.save(update).subscribe((settings) => (result = settings));

    const request = httpMock.expectOne('/api/v1/settings/application');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    request.flush(update);

    expect(result?.name).toBe('Acme Tasks');
  });
});

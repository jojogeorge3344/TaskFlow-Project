import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApplicationSettings, UpdateApplicationSettings } from '../../models/settings.model';
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

  it('defaults the cached brand name to TaskFlow', () => {
    expect(service.appName()).toBe('TaskFlow');
  });

  it('updates the cached brand name when the settings are read', () => {
    service.get().subscribe();
    httpMock.expectOne('/api/v1/settings/application').flush({ ...stored, name: 'Acme Tasks' });

    expect(service.appName()).toBe('Acme Tasks');
  });

  it('updates the cached brand name after a save, so the shell refreshes without a reload', () => {
    service.save({ ...stored, name: 'Renamed Corp' }).subscribe();
    httpMock.expectOne('/api/v1/settings/application').flush({ ...stored, name: 'Renamed Corp' });

    expect(service.appName()).toBe('Renamed Corp');
  });

  it('falls back to TaskFlow when a loaded name is blank', () => {
    service.get().subscribe();
    httpMock.expectOne('/api/v1/settings/application').flush({ ...stored, name: '' });

    expect(service.appName()).toBe('TaskFlow');
  });
});

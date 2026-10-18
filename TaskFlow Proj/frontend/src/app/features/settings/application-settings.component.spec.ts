import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApplicationSettings } from '../../core/models';
import { AuthService } from '../../core/auth/auth.service';
import { AiProviderAdminService } from '../../core/services/ai-provider-admin.service';
import { ApplicationSettingsService } from '../../core/services/application-settings.service';
import { ToastService } from '../../shared/ui/toast.service';
import { ApplicationSettingsComponent } from './application-settings.component';

describe('ApplicationSettingsComponent', () => {
  const stored: ApplicationSettings = {
    name: 'TaskFlow',
    timezone: 'Asia/Kolkata',
    defaultPriority: 'Medium',
    inAppNotificationsEnabled: true
  };

  let getSpy: jasmine.Spy;
  let saveSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(stored));
    saveSpy = jasmine.createSpy('save');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  function create(): ComponentFixture<ApplicationSettingsComponent> {
    TestBed.configureTestingModule({
      imports: [ApplicationSettingsComponent],
      providers: [
        { provide: ApplicationSettingsService, useValue: { get: getSpy, save: saveSpy } },
        { provide: ToastService, useValue: toast },
        // The AI assistant section is covered by its own spec; here it only has to render.
        { provide: AiProviderAdminService, useValue: { getConfig: () => of([]) } },
        { provide: AuthService, useValue: { currentUser: signal({ id: 1, role: 'Administrator' }).asReadonly() } },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ApplicationSettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ApplicationSettingsComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function input(fixture: ComponentFixture<ApplicationSettingsComponent>, field: string): HTMLInputElement {
    return el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;
  }

  function submit(fixture: ComponentFixture<ApplicationSettingsComponent>): void {
    (el(fixture).querySelector('[data-action="save"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('loads and shows the stored values', () => {
    const fixture = create();

    expect(getSpy).toHaveBeenCalled();
    expect(input(fixture, 'name').value).toBe('TaskFlow');
    expect(input(fixture, 'timezone').value).toBe('Asia/Kolkata');
    expect(fixture.componentInstance.form.controls.defaultPriority.value).toBe('Medium');
    const priority = el(fixture).querySelector('[data-field="defaultPriority"]') as HTMLSelectElement;
    expect(priority.selectedOptions[0].textContent?.trim()).toBe('Medium');
    expect(input(fixture, 'inAppNotificationsEnabled').checked).toBeTrue();
  });

  it('saves the edited values and confirms', () => {
    saveSpy.and.callFake((body) => of(body));
    const fixture = create();

    fixture.componentInstance.form.patchValue({ name: 'Acme Tasks', timezone: 'UTC', defaultPriority: 'High' });
    submit(fixture);

    expect(saveSpy).toHaveBeenCalledOnceWith({
      name: 'Acme Tasks',
      timezone: 'UTC',
      defaultPriority: 'High',
      inAppNotificationsEnabled: true
    });
    expect(toast.success).toHaveBeenCalledWith('Application settings saved');
    expect(el(fixture).querySelector('[data-status="saved"]')).not.toBeNull();
  });

  it('blocks a blank application name and shows the error', () => {
    const fixture = create();

    fixture.componentInstance.form.controls.name.setValue('   ');
    fixture.componentInstance.form.controls.name.markAsTouched();
    fixture.detectChanges();
    submit(fixture);

    expect(saveSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="name"]')).not.toBeNull();
  });

  it('shows server field errors from a 400 response', () => {
    saveSpy.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { title: 'One or more validation errors occurred.', errors: { Timezone: ['Unknown timezone.'] } }
          })
      )
    );
    const fixture = create();

    submit(fixture);

    expect(el(fixture).querySelector('[data-error="timezone"]')?.textContent?.trim()).toBe('Unknown timezone.');
    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Unknown timezone.');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('hides the Saved confirmation again once a field is edited', () => {
    saveSpy.and.callFake((body) => of(body));
    const fixture = create();

    submit(fixture);
    expect(el(fixture).querySelector('[data-status="saved"]')).not.toBeNull();

    fixture.componentInstance.form.controls.name.setValue('Other');
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-status="saved"]')).toBeNull();
  });

  it('offers timezone suggestions via a datalist without restricting custom ids', () => {
    const fixture = create();

    const timezone = input(fixture, 'timezone');
    expect(timezone.getAttribute('list')).toBe('app-timezone-options');

    const options = Array.from(el(fixture).querySelectorAll('#app-timezone-options option')).map((option) =>
      option.getAttribute('value')
    );
    expect(options).toContain('Asia/Kolkata');
    expect(options).toContain('UTC');
  });

  it('links to Reminder Settings instead of duplicating interval editing', () => {
    const fixture = create();

    const link = el(fixture).querySelector('a[routerLink="/settings/reminders"]');
    expect(link).not.toBeNull();
    expect(el(fixture).querySelector('[data-field="defaultFirstDays"]')).toBeNull();
  });

  it('shows a retryable error when loading fails', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load the application settings.');

    getSpy.and.returnValue(of(stored));
    (el(fixture).querySelector('[data-action="retry"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(input(fixture, 'name').value).toBe('TaskFlow');
  });
});

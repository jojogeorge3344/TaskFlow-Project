import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { UserRole } from '../../core/models';
import { ReminderSettings } from '../../core/models';
import { ReminderSettingsService } from '../../core/services/reminder-settings.service';
import { ToastService } from '../../shared/ui/toast.service';
import { ReminderSettingsComponent } from './reminder-settings.component';

describe('ReminderSettingsComponent', () => {
  const stored: ReminderSettings = {
    enabled: true,
    deadlineDayEnabled: false,
    defaultFirstDays: 3,
    defaultSecondDays: 1,
    defaultOverdueFrequency: 'Weekly',
    emailEnabled: true,
    timezone: 'Asia/Kolkata'
  };

  let getSpy: jasmine.Spy;
  let saveSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(stored));
    saveSpy = jasmine.createSpy('save');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  function create(role: UserRole = 'Manager'): ComponentFixture<ReminderSettingsComponent> {
    TestBed.configureTestingModule({
      imports: [ReminderSettingsComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser: () => ({ role }) } },
        { provide: ReminderSettingsService, useValue: { get: getSpy, save: saveSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });
    const fixture = TestBed.createComponent(ReminderSettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ReminderSettingsComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function select(fixture: ComponentFixture<ReminderSettingsComponent>, id: string): HTMLSelectElement {
    return el(fixture).querySelector(`#${id}`) as HTMLSelectElement;
  }

  function checkbox(fixture: ComponentFixture<ReminderSettingsComponent>, field: string): HTMLInputElement {
    return el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;
  }

  function submit(fixture: ComponentFixture<ReminderSettingsComponent>): void {
    (el(fixture).querySelector('[data-action="save"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('loads and shows the stored values with a read-only timezone', () => {
    const fixture = create();

    expect(getSpy).toHaveBeenCalled();
    expect(checkbox(fixture, 'enabled').checked).toBeTrue();
    expect(checkbox(fixture, 'deadlineDayEnabled').checked).toBeFalse();
    expect(checkbox(fixture, 'emailEnabled').checked).toBeTrue();
    expect(fixture.componentInstance.form.getRawValue()).toEqual({
      enabled: true,
      deadlineDayEnabled: false,
      defaultFirstDays: 3,
      defaultSecondDays: 1,
      defaultOverdueFrequency: 'Weekly',
      emailEnabled: true
    });
    expect(select(fixture, 'settings-first-reminder').selectedOptions[0].textContent?.trim()).toBe('3 days before the deadline');

    const timezone = checkbox(fixture, 'timezone');
    expect(timezone.value).toBe('Asia/Kolkata');
    expect(timezone.readOnly).toBeTrue();
  });

  it('adds a stored interval that is not a predefined option so the select can show it', () => {
    getSpy.and.returnValue(of({ ...stored, defaultFirstDays: 5 }));
    const fixture = create();

    const options = Array.from(select(fixture, 'settings-first-reminder').options).map((option) => option.textContent?.trim());
    expect(options).toContain('5 days before the deadline');
    expect(fixture.componentInstance.form.controls.defaultFirstDays.value).toBe(5);
  });

  it('saves the edited values without the timezone and confirms', () => {
    saveSpy.and.callFake((body) => of({ ...body, timezone: 'Asia/Kolkata' }));
    const fixture = create();

    fixture.componentInstance.form.patchValue({ enabled: false, defaultSecondDays: 0, defaultOverdueFrequency: 'Daily' });
    submit(fixture);

    expect(saveSpy).toHaveBeenCalledOnceWith({
      enabled: false,
      deadlineDayEnabled: false,
      defaultFirstDays: 3,
      defaultSecondDays: 0,
      defaultOverdueFrequency: 'Daily',
      emailEnabled: true
    });
    expect(toast.success).toHaveBeenCalledWith('Reminder settings saved');
    expect(el(fixture).querySelector('[data-status="saved"]')).not.toBeNull();
    expect(fixture.componentInstance.form.controls.enabled.value).toBeFalse();
  });

  it('hides the Saved confirmation again once a field is edited', () => {
    saveSpy.and.callFake((body) => of({ ...body, timezone: 'Asia/Kolkata' }));
    const fixture = create();

    submit(fixture);
    expect(el(fixture).querySelector('[data-status="saved"]')).not.toBeNull();

    fixture.componentInstance.form.controls.emailEnabled.setValue(false);
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-status="saved"]')).toBeNull();
  });

  it('links a field error to its select for screen readers', () => {
    const fixture = create();

    fixture.componentInstance.form.patchValue({ defaultFirstDays: 1, defaultSecondDays: 1 });
    fixture.detectChanges();

    const second = select(fixture, 'settings-second-reminder');
    expect(second.getAttribute('aria-invalid')).toBe('true');
    expect(second.getAttribute('aria-describedby')).toBe('settings-second-reminder-error');
    expect(el(fixture).querySelector('#settings-second-reminder-error')?.textContent).toContain('The first reminder must come earlier');
    expect(select(fixture, 'settings-first-reminder').getAttribute('aria-describedby')).toBeNull();
  });

  it('blocks a second interval that is not shorter than the first', () => {
    const fixture = create();

    fixture.componentInstance.form.patchValue({ defaultFirstDays: 1, defaultSecondDays: 1 });
    fixture.detectChanges();
    submit(fixture);

    expect(saveSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="defaultSecondDays"]')?.textContent).toContain(
      'The first reminder must come earlier'
    );
  });

  it('shows server field errors from a 400 response and keeps the form values', () => {
    saveSpy.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { title: 'One or more validation errors occurred.', errors: { DefaultFirstDays: ['Must be between 1 and 30.'] } }
          })
      )
    );
    const fixture = create();

    submit(fixture);

    expect(el(fixture).querySelector('[data-error="defaultFirstDays"]')?.textContent?.trim()).toBe('Must be between 1 and 30.');
    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Must be between 1 and 30.');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('shows a retryable error when loading fails', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load the reminder settings.');

    getSpy.and.returnValue(of(stored));
    (el(fixture).querySelector('[data-action="retry"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(checkbox(fixture, 'timezone').value).toBe('Asia/Kolkata');
  });

  it('links Administrators to Email Settings', () => {
    const fixture = create('Administrator');

    const link = el(fixture).querySelector('[data-action="configure-email"]');
    expect(link?.getAttribute('href')).toBe('/settings/email');
  });

  it('does not show the Email Settings link to Managers', () => {
    const fixture = create('Manager');

    expect(el(fixture).querySelector('[data-action="configure-email"]')).toBeNull();
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { TfaSettingsComponent } from './tfa-settings.component';

describe('TfaSettingsComponent', () => {
  const user = signal<AuthUser | null>(null);

  async function create(role: AuthUser['role']) {
    user.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.com', role, employeeCode: null, permissions: systemPermissions(role) });
    await TestBed.configureTestingModule({
      imports: [TfaSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: fakeAuthService(user.asReadonly()) }]
    }).compileComponents();
    const fixture = TestBed.createComponent(TfaSettingsComponent);
    fixture.detectChanges();
    return { fixture, http: TestBed.inject(HttpTestingController), el: fixture.nativeElement as HTMLElement };
  }

  it('is its own screen, titled "TFA settings", with the model and server settings in it', async () => {
    const { http, el } = await create('Administrator');
    http.expectOne('/api/v1/assistant/providers/config').flush([]);

    expect(el.querySelector('[data-page="tfa-settings"] h1')!.textContent).toContain('TFA settings');
    expect(el.querySelector('[data-section="ai-assistant"]')).not.toBeNull();
    http.verify();
  });

  it('shows no model settings to someone without settings.ai.manage, and asks the server for nothing', async () => {
    const { http, el } = await create('Manager');

    expect(el.querySelector('[data-section="ai-assistant"]')).toBeNull();
    http.expectNone('/api/v1/assistant/providers/config');
    http.verify();
  });
});

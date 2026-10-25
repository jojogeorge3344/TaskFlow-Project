import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AppComponent } from './app.component';

@Component({ standalone: true, template: '' })
class StubPageComponent {}

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render a router outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('shows the app-wide backdrop outside the login screen', async () => {
    const router = TestBed.inject(Router);
    router.resetConfig([{ path: 'dashboard', component: StubPageComponent }, { path: 'login', component: StubPageComponent }]);
    const fixture = TestBed.createComponent(AppComponent);

    await router.navigateByUrl('/dashboard');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-backdrop')).toBeTruthy();

    await router.navigateByUrl('/login');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-backdrop')).toBeNull();
  });
});

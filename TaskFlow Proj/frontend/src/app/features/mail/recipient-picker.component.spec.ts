import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { MailService } from '../../core/services/mail.service';
import { MailServiceStub, mailServiceStub } from './mail-test-data';
import { RecipientPickerComponent } from './recipient-picker.component';

describe('RecipientPickerComponent', () => {
  let mail: MailServiceStub;
  const priya = { id: 3, fullName: 'Priya', email: 'priya@taskflow.local' };

  beforeEach(() => {
    mail = mailServiceStub();
  });

  function create(): ComponentFixture<RecipientPickerComponent> {
    TestBed.configureTestingModule({ imports: [RecipientPickerComponent], providers: [{ provide: MailService, useValue: mail }] });
    const fixture = TestBed.createComponent(RecipientPickerComponent);
    fixture.componentRef.setInput('label', 'To');
    fixture.componentRef.setInput('inputId', 'to');
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<RecipientPickerComponent>) => fixture.nativeElement as HTMLElement;
  const input = (fixture: ComponentFixture<RecipientPickerComponent>) => el(fixture).querySelector('input') as HTMLInputElement;

  function type(fixture: ComponentFixture<RecipientPickerComponent>, value: string): void {
    input(fixture).value = value;
    input(fixture).dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function enter(fixture: ComponentFixture<RecipientPickerComponent>): void {
    input(fixture).dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
  }

  it('suggests TaskFlow users while typing and adds the chosen one', fakeAsync(() => {
    mail.contacts.and.returnValue(of([priya]));
    const fixture = create();

    type(fixture, 'pri');
    tick(250);
    fixture.detectChanges();

    expect(mail.contacts).toHaveBeenCalledWith('pri');
    const option = el(fixture).querySelector('[data-contact="priya@taskflow.local"]') as HTMLElement;
    option.dispatchEvent(new MouseEvent('mousedown'));
    fixture.detectChanges();

    expect(fixture.componentInstance.chips()).toEqual([{ email: 'priya@taskflow.local', name: 'Priya', valid: true }]);
    expect(el(fixture).querySelector('[data-chip="priya@taskflow.local"]')?.textContent).toContain('Priya');
  }));

  it('accepts a typed address that is a TaskFlow user', () => {
    mail.contacts.and.returnValue(of([priya]));
    const fixture = create();

    type(fixture, 'Priya@TaskFlow.local');
    enter(fixture);

    expect(fixture.componentInstance.chips()).toEqual([{ email: 'priya@taskflow.local', name: 'Priya', valid: true }]);
    expect(el(fixture).querySelector('[data-error="to"]')).toBeNull();
  });

  it('flags an address that is not a TaskFlow user', () => {
    mail.contacts.and.returnValue(of([]));
    const fixture = create();

    type(fixture, 'someone@gmail.com,');

    const chip = el(fixture).querySelector('[data-chip="someone@gmail.com"]')!;
    expect(chip.classList).toContain('picker__chip--invalid');
    expect(el(fixture).querySelector('[data-error="to"]')?.textContent).toContain('Not a TaskFlow user: someone@gmail.com');
    expect(fixture.componentInstance.invalidChips().length).toBe(1);
  });

  it('checks chips handed in from outside, once each', () => {
    mail.contacts.and.returnValue(of([priya]));
    const fixture = create();

    fixture.componentInstance.chips.set([{ email: 'priya@taskflow.local', name: null, valid: null }]);
    fixture.detectChanges();

    expect(mail.contacts).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.chips()[0].valid).toBeTrue();
  });

  it('removes chips with the button or backspace', () => {
    const fixture = create();
    fixture.componentInstance.chips.set([
      { email: 'a@taskflow.local', name: 'A', valid: true },
      { email: 'b@taskflow.local', name: 'B', valid: true }
    ]);
    fixture.detectChanges();

    (el(fixture).querySelector('[data-chip="a@taskflow.local"] button') as HTMLButtonElement).click();
    input(fixture).dispatchEvent(new KeyboardEvent('keydown', { key: 'Backspace' }));
    fixture.detectChanges();

    expect(fixture.componentInstance.chips()).toEqual([]);
  });
});

import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, flush } from '@angular/core/testing';
import { DrawerComponent } from './drawer.component';

@Component({
  standalone: true,
  imports: [DrawerComponent],
  template: `
    <button type="button" id="opener" (click)="open.set(true)">Open</button>
    <tf-drawer [open]="open()" title="Edit employee" (closed)="open.set(false)">
      <div>
        <input id="first-field" />
        <button type="button" id="last-button">Save</button>
      </div>
    </tf-drawer>
  `
})
class HostComponent {
  readonly open = signal(false);
}

describe('DrawerComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function tab(shiftKey = false): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true, cancelable: true });
    (document.activeElement as HTMLElement).dispatchEvent(event);
    return event;
  }

  function openDrawer(): void {
    const opener = el().querySelector('#opener') as HTMLButtonElement;
    opener.focus();
    opener.click();
    fixture.detectChanges();
    flush();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('moves focus to the first field when it opens', fakeAsync(() => {
    openDrawer();

    expect(document.activeElement?.id).toBe('first-field');
  }));

  it('wraps Tab from the last control back to the first, and Shift+Tab the other way', fakeAsync(() => {
    openDrawer();
    const closeButton = el().querySelector('.tf-drawer__head button') as HTMLButtonElement;

    (el().querySelector('#last-button') as HTMLButtonElement).focus();
    const forward = tab();
    expect(forward.defaultPrevented).toBeTrue();
    expect(document.activeElement).toBe(closeButton);

    const backward = tab(true);
    expect(backward.defaultPrevented).toBeTrue();
    expect(document.activeElement?.id).toBe('last-button');
  }));

  it('returns focus to the opener when it closes', fakeAsync(() => {
    openDrawer();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    flush();

    expect(el().querySelector('.tf-drawer')).toBeNull();
    expect(document.activeElement?.id).toBe('opener');
  }));
});

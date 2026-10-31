import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { IconComponent } from './icon.component';

@Component({
  standalone: true,
  imports: [IconComponent],
  template: `<tf-icon [name]="name()" />`
})
class HostComponent {
  readonly name = signal('users');
}

describe('IconComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
  });

  it('renders the inline SVG for a known icon', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg.tf-icon')!;

    expect(svg.getAttribute('viewBox')).toBe('0 0 24 24');
    expect(svg.getAttribute('aria-hidden')).toBe('true');
    expect(svg.querySelectorAll('path, circle, rect').length).toBeGreaterThan(0);
  });

  it('leaves the SVG empty for an unknown icon name', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.name.set('does-not-exist');
    fixture.detectChanges();
    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg.tf-icon')!;

    expect(svg.childElementCount).toBe(0);
  });
});

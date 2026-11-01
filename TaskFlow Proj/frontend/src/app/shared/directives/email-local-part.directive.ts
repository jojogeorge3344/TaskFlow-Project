import { Directive, ElementRef, HostListener, inject } from '@angular/core';
import { NgControl } from '@angular/forms';
import { sanitizeLocalPart } from '../utils/email-domain';

/** Keeps an email input to the part before the fixed domain: a typed or pasted `@...` is dropped and the text is lowercased. */
@Directive({
  selector: 'input[appEmailLocalPart]',
  standalone: true
})
export class EmailLocalPartDirective {
  private readonly element = inject<ElementRef<HTMLInputElement>>(ElementRef);
  private readonly control = inject(NgControl, { optional: true });

  @HostListener('input')
  onInput(): void {
    const input = this.element.nativeElement;
    const cleaned = sanitizeLocalPart(input.value);
    if (cleaned !== input.value) {
      input.value = cleaned;
    }
    this.control?.control?.setValue(cleaned);
  }
}

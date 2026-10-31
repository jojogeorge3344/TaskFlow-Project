import { Component, effect, inject, input, model, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { IconComponent } from '../../core/layout/icon.component';
import { MailContact } from '../../core/models';
import { MailService } from '../../core/services/mail.service';
import { RecipientChip } from './recipient-picker.model';

/** Chips for To or Cc that only accept TaskFlow users: suggestions come from the user directory, and a typed
 * address that is not an active TaskFlow user is flagged before sending. */
@Component({
  selector: 'app-recipient-picker',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './recipient-picker.component.html',
  styleUrl: './recipient-picker.component.css'
})
export class RecipientPickerComponent {
  private readonly mailService = inject(MailService);
  private readonly queries = new Subject<string>();
  private readonly checking = new Set<string>();

  readonly label = input.required<string>();
  readonly inputId = input.required<string>();
  readonly chips = model<RecipientChip[]>([]);

  readonly text = signal('');
  readonly suggestions = signal<MailContact[]>([]);
  readonly activeIndex = signal(-1);

  constructor() {
    this.queries
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) => (term.length < 1 ? of([]) : this.mailService.contacts(term))),
        takeUntilDestroyed()
      )
      .subscribe({
        next: (contacts) => {
          const taken = new Set(this.chips().map((chip) => chip.email));
          this.suggestions.set(contacts.filter((contact) => !taken.has(contact.email.toLowerCase())));
          this.activeIndex.set(-1);
        },
        error: () => this.suggestions.set([])
      });

    // Chips added from outside (a draft, a reply) are checked once.
    effect(() => {
      const pending = this.chips().filter((chip) => chip.valid === null);
      untracked(() => pending.filter((chip) => !this.checking.has(chip.email)).forEach((chip) => this.verify(chip.email)));
    });
  }

  invalidChips(): RecipientChip[] {
    return this.chips().filter((chip) => chip.valid === false);
  }

  invalidEmails(): string {
    return this.invalidChips()
      .map((chip) => chip.email)
      .join(', ');
  }

  onInput(value: string): void {
    if (/[,;]$/.test(value)) {
      this.addTyped(value.slice(0, -1));
      return;
    }
    this.text.set(value);
    this.queries.next(value.trim());
  }

  onKeydown(event: KeyboardEvent): void {
    const suggestions = this.suggestions();
    if (event.key === 'ArrowDown' && suggestions.length) {
      event.preventDefault();
      this.activeIndex.update((index) => (index + 1) % suggestions.length);
    } else if (event.key === 'ArrowUp' && suggestions.length) {
      event.preventDefault();
      this.activeIndex.update((index) => (index <= 0 ? suggestions.length - 1 : index - 1));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const active = suggestions[this.activeIndex()];
      if (active) {
        this.pick(active);
      } else {
        this.addTyped(this.text());
      }
    } else if (event.key === 'Backspace' && this.text() === '' && this.chips().length) {
      this.chips.update((chips) => chips.slice(0, -1));
    } else if (event.key === 'Escape') {
      this.suggestions.set([]);
    }
  }

  onBlur(): void {
    // Let a suggestion click land before the list disappears.
    setTimeout(() => {
      if (this.text().trim()) {
        this.addTyped(this.text());
      }
      this.suggestions.set([]);
    }, 150);
  }

  /** Turns any typed-but-uncommitted text into a chip right away, so an action like Send never misses an address
   * the user typed but did not confirm. The new chip is checked like any other. */
  commitPending(): void {
    if (this.text().trim()) {
      this.addTyped(this.text());
    }
  }

  pick(contact: MailContact): void {
    this.add({ email: contact.email.toLowerCase(), name: contact.fullName, valid: true });
  }

  remove(chip: RecipientChip): void {
    this.chips.update((chips) => chips.filter((existing) => existing.email !== chip.email));
  }

  private addTyped(raw: string): void {
    const email = raw.trim().toLowerCase();
    if (!email) {
      this.reset();
      return;
    }
    this.add({ email, name: null, valid: null });
  }

  private add(chip: RecipientChip): void {
    if (!this.chips().some((existing) => existing.email === chip.email)) {
      this.chips.update((chips) => [...chips, chip]);
    }
    this.reset();
  }

  private reset(): void {
    this.text.set('');
    this.suggestions.set([]);
    this.activeIndex.set(-1);
    this.queries.next('');
  }

  /** Marks a chip valid when it is exactly an active TaskFlow user's email. */
  private verify(email: string): void {
    this.checking.add(email);
    this.mailService.contacts(email).subscribe({
      next: (contacts) => {
        this.checking.delete(email);
        const match = contacts.find((contact) => contact.email.toLowerCase() === email);
        this.chips.update((chips) =>
          chips.map((chip) => (chip.email === email ? { ...chip, valid: !!match, name: match?.fullName ?? chip.name } : chip))
        );
      },
      error: () => {
        // The server checks again on send; do not block on a failed lookup.
        this.checking.delete(email);
        this.chips.update((chips) => chips.map((chip) => (chip.email === email ? { ...chip, valid: true } : chip)));
      }
    });
  }
}

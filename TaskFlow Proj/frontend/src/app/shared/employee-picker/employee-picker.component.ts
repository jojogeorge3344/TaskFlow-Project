import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, of, switchMap } from 'rxjs';
import { User } from '../../core/models';
import { UserService } from '../../core/services/user.service';

export const EMPLOYEE_SEARCH_DEBOUNCE_MS = 250;

let nextId = 0;

/** A searchable employee dropdown. Typing searches active employees on the server and each option shows how much of the
 * person's time is still free, so an assigner can see who has room before choosing. */
@Component({
  selector: 'app-employee-picker',
  standalone: true,
  template: `
    <div class="employee-picker">
      <input
        class="tf-input"
        type="text"
        role="combobox"
        autocomplete="off"
        placeholder="Search employees"
        aria-autocomplete="list"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="listId"
        [attr.aria-label]="label()"
        [attr.aria-invalid]="invalid()"
        [class.is-invalid]="invalid()"
        [value]="text()"
        (input)="onInput($any($event.target).value)"
        (focus)="onFocus()"
        (blur)="onBlur()"
      />
      @if (open()) {
        <ul class="employee-picker__list" role="listbox" [id]="listId">
          @for (user of options(); track user.id) {
            <li role="option">
              <button type="button" class="employee-picker__option" (mousedown)="$event.preventDefault()" (click)="choose(user)">
                <span>{{ user.fullName }}</span>
                <span class="employee-picker__remaining" [class.is-full]="user.remainingPercent <= 0">
                  {{ user.remainingPercent <= 0 ? 'Fully allocated' : user.remainingPercent + '% remaining' }}
                </span>
              </button>
            </li>
          } @empty {
            <li class="employee-picker__empty">{{ searching() ? 'Searching…' : 'No employees match.' }}</li>
          }
        </ul>
      }
    </div>
  `,
  styles: `
    .employee-picker {
      position: relative;
    }

    .employee-picker__list {
      position: absolute;
      z-index: 20;
      inset-inline: 0;
      margin: 0.25rem 0 0;
      padding: 0.25rem;
      max-height: 14rem;
      overflow: auto;
      list-style: none;
      background: var(--tf-surface);
      border: 1px solid var(--tf-border);
      border-radius: var(--tf-radius);
      box-shadow: 0 12px 28px rgba(15, 23, 42, 0.14);
    }

    .employee-picker__option {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      width: 100%;
      padding: 0.375rem 0.5rem;
      border: 0;
      border-radius: var(--tf-radius-sm);
      background: none;
      color: var(--tf-text);
      text-align: left;
      cursor: pointer;
    }

    .employee-picker__option:hover,
    .employee-picker__option:focus-visible {
      background: var(--tf-primary-soft);
    }

    .employee-picker__remaining {
      color: var(--tf-muted);
      font-size: 0.75rem;
      white-space: nowrap;
    }

    .employee-picker__remaining.is-full {
      color: var(--tf-danger-strong);
    }

    .employee-picker__empty {
      padding: 0.5rem;
      color: var(--tf-muted);
      font-size: 0.8125rem;
    }
  `
})
export class EmployeePickerComponent {
  private readonly userService = inject(UserService);
  private readonly destroyRef = inject(DestroyRef);

  /** The employee already chosen for this row, if any. */
  readonly selected = input<{ id: number; fullName: string } | null>(null);
  /** People who cannot be picked again (already on another row). */
  readonly exclude = input<number[]>([]);
  readonly label = input('Employee');
  readonly invalid = input(false);
  readonly picked = output<User>();

  readonly listId = `employee-options-${nextId++}`;
  readonly open = signal(false);
  readonly searching = signal(false);
  private readonly results = signal<User[]>([]);
  private readonly typed = signal<string | null>(null);
  private readonly search$ = new Subject<string>();

  readonly options = computed(() => this.results().filter((user) => !this.exclude().includes(user.id)));
  readonly text = computed(() => this.typed() ?? this.selected()?.fullName ?? '');

  constructor() {
    this.search$
      .pipe(
        debounceTime(EMPLOYEE_SEARCH_DEBOUNCE_MS),
        switchMap((term) => {
          this.searching.set(true);
          return this.userService
            .getAll({ search: term.trim() || undefined, isActive: true })
            .pipe(catchError(() => of([] as User[])));
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((users) => {
        this.results.set(users);
        this.searching.set(false);
      });
  }

  onFocus(): void {
    this.open.set(true);
    this.search$.next(this.typed() ?? '');
  }

  onBlur(): void {
    this.open.set(false);
    this.typed.set(null);
  }

  onInput(value: string): void {
    this.typed.set(value);
    this.open.set(true);
    this.search$.next(value);
  }

  choose(user: User): void {
    this.open.set(false);
    this.typed.set(null);
    this.picked.emit(user);
  }
}

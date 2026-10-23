import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { User } from '../../core/models';
import { UserService } from '../../core/services/user.service';
import { EMPLOYEE_SEARCH_DEBOUNCE_MS, EmployeePickerComponent } from './employee-picker.component';

function user(id: number, fullName: string, remainingPercent: number): User {
  return {
    id,
    fullName,
    username: fullName.toLowerCase(),
    email: `${fullName.toLowerCase()}@taskflow.com`,
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-09-01T00:00:00Z',
    projectAllocations: [],
    totalAllocatedPercent: 100 - remainingPercent,
    remainingPercent,
    departments: [],
    managers: []
  };
}

describe('EmployeePickerComponent', () => {
  let getAll: jasmine.Spy;
  let fixture: ComponentFixture<EmployeePickerComponent>;

  function input(): HTMLInputElement {
    return (fixture.nativeElement as HTMLElement).querySelector('input') as HTMLInputElement;
  }

  function options(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.employee-picker__option')).map((o) =>
      (o.textContent ?? '').replace(/\s+/g, ' ').trim()
    );
  }

  beforeEach(async () => {
    getAll = jasmine.createSpy('getAll').and.returnValue(of([user(1, 'Priya', 40), user(2, 'Ravi', 0), user(3, 'Asha', 100)]));
    await TestBed.configureTestingModule({
      imports: [EmployeePickerComponent],
      providers: [{ provide: UserService, useValue: { getAll } }]
    }).compileComponents();
    fixture = TestBed.createComponent(EmployeePickerComponent);
    fixture.detectChanges();
  });

  it('searches active employees on the server as the user types and shows the remaining percentage', fakeAsync(() => {
    input().dispatchEvent(new Event('focus'));
    input().value = 'pri';
    input().dispatchEvent(new Event('input'));
    tick(EMPLOYEE_SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();

    expect(getAll).toHaveBeenCalledWith({ search: 'pri', isActive: true });
    expect(options()).toEqual(['Priya 40% remaining', 'Ravi Fully allocated', 'Asha 100% remaining']);
  }));

  it('leaves out people who are already on another row', fakeAsync(() => {
    fixture.componentRef.setInput('exclude', [2]);
    input().dispatchEvent(new Event('focus'));
    tick(EMPLOYEE_SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();

    expect(options()).toEqual(['Priya 40% remaining', 'Asha 100% remaining']);
  }));

  it('emits the chosen employee and shows their name', fakeAsync(() => {
    const picked = jasmine.createSpy('picked');
    fixture.componentInstance.picked.subscribe(picked);
    input().dispatchEvent(new Event('focus'));
    tick(EMPLOYEE_SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();

    ((fixture.nativeElement as HTMLElement).querySelector('.employee-picker__option') as HTMLButtonElement).click();
    fixture.componentRef.setInput('selected', { id: 1, fullName: 'Priya' });
    fixture.detectChanges();

    expect(picked).toHaveBeenCalledWith(jasmine.objectContaining({ id: 1 }));
    expect(input().value).toBe('Priya');
  }));
});

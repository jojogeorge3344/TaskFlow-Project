import { ComponentFixture, TestBed } from '@angular/core/testing';
import { defaultFilter } from '../../../shared/utils/analytics-filter';
import { AnalyticsFilterOptions, AnalyticsFilterState } from '../analytics/models/analytics.model';
import { DashboardFilterBarComponent } from './dashboard-filter-bar.component';

describe('DashboardFilterBarComponent', () => {
  const today = '2026-10-21';
  const options: AnalyticsFilterOptions = {
    today,
    timezone: 'UTC',
    departments: [{ id: 1, name: 'Engineering' }],
    projects: [{ id: 2, name: 'Apollo' }],
    employees: [{ id: 3, name: 'A. Thomas' }],
    managers: [{ id: 4, name: 'R. Nair' }]
  };

  let fixture: ComponentFixture<DashboardFilterBarComponent>;
  let emitted: Partial<AnalyticsFilterState>[];
  let resets: number;

  function create(filter: AnalyticsFilterState = defaultFilter(today)): HTMLElement {
    fixture = TestBed.createComponent(DashboardFilterBarComponent);
    fixture.componentRef.setInput('filter', filter);
    fixture.componentRef.setInput('options', options);
    fixture.componentRef.setInput('today', today);
    emitted = [];
    resets = 0;
    fixture.componentInstance.filterChange.subscribe((patch) => emitted.push(patch));
    fixture.componentInstance.reset.subscribe(() => resets++);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function change(element: HTMLElement, selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement | HTMLSelectElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('change'));
  }

  it('highlights the active preset', () => {
    const element = create();

    expect(element.querySelector('[data-preset="last30"]')?.getAttribute('aria-pressed')).toBe('true');
    expect(element.querySelector('[data-preset="today"]')?.getAttribute('aria-pressed')).toBe('false');
  });

  it('applies a preset as a date range', () => {
    const element = create();

    element.querySelector<HTMLButtonElement>('[data-preset="last7"]')!.click();

    expect(emitted).toEqual([{ from: '2026-10-15', to: '2026-10-21' }]);
  });

  it('treats an edited range as custom', () => {
    const element = create({ ...defaultFilter(today), from: '2026-10-02' });

    expect(element.querySelector('[data-preset="custom"]')?.getAttribute('aria-pressed')).toBe('true');
  });

  it('emits custom dates, the basis and entity filters', () => {
    const element = create();

    change(element, '#filter-from', '2026-10-01');
    change(element, '#filter-to', '2026-10-05');
    change(element, '#filter-basis', 'Deadline');
    change(element, '#filter-department', '1');
    change(element, '#filter-project', '2');
    change(element, '#filter-manager', '4');
    change(element, '#filter-employee', '3');
    change(element, '#filter-employee', '');

    expect(emitted).toEqual([
      { from: '2026-10-01' },
      { to: '2026-10-05' },
      { basis: 'Deadline' },
      { departmentId: 1 },
      { projectId: 2 },
      { managerId: 4 },
      { employeeId: 3 },
      { employeeId: null }
    ]);
  });

  it('shows a validation message and constrains the dates when the end precedes the start', () => {
    const element = create({ ...defaultFilter(today), from: '2026-10-10', to: '2026-10-09' });

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('end date cannot be before the start date');
    expect(element.querySelector<HTMLInputElement>('#filter-to')?.min).toBe('2026-10-10');
    expect(element.querySelector<HTMLInputElement>('#filter-from')?.max).toBe('2026-10-09');
  });

  it('emits reset', () => {
    const element = create();

    element.querySelector<HTMLButtonElement>('[data-action="reset"]')!.click();

    expect(resets).toBe(1);
  });

  it('hides the selectors a role does not need', () => {
    fixture = TestBed.createComponent(DashboardFilterBarComponent);
    fixture.componentRef.setInput('filter', defaultFilter(today));
    fixture.componentRef.setInput('today', today);
    fixture.componentRef.setInput('showManager', false);
    fixture.componentRef.setInput('showEmployee', false);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('#filter-manager')).toBeFalsy();
    expect(element.querySelector('#filter-employee')).toBeFalsy();
    expect(element.querySelector('#filter-department')).toBeTruthy();
  });
});

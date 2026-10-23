import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DrillTarget } from '../../../core/analytics/drill-down';
import {
  AnalyticsTrend,
  BucketCounts,
  DeadlineBuckets,
  DepartmentAnalytics,
  EmployeeAnalyticsSection,
  ManagerAnalytics,
  PriorityCount,
  ProjectAnalytics
} from '../../../core/models';
import { provideDashboardCharts } from '../../../shared/charts/chart-providers';
import { DepartmentAnalyticsComponent } from './department-analytics.component';
import { EmployeeAnalyticsComponent } from './employee-analytics.component';
import { ManagerAnalyticsComponent } from './manager-analytics.component';
import { ProjectAnalyticsComponent } from './project-analytics.component';
import { TaskAnalyticsComponent } from './task-analytics.component';

function counts(pending: number, inProgress: number, completed: number, overdue: number): BucketCounts {
  const total = pending + inProgress + completed + overdue;
  return {
    pending,
    inProgress,
    completed,
    overdue,
    total,
    open: pending + inProgress + overdue,
    completionPercent: total === 0 ? 0 : Math.round((completed / total) * 100)
  };
}

function render<T>(type: new () => T, inputs: Record<string, unknown>): { fixture: ComponentFixture<T>; element: HTMLElement } {
  const fixture = TestBed.createComponent(type);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

function cellsOf(row: Element): string {
  return Array.from(row.children)
    .map((cell) => cell.textContent?.trim())
    .filter((text) => !!text)
    .join(' ');
}

function collect<T extends { drill: { subscribe: (next: (value: DrillTarget) => void) => unknown } }>(component: T): DrillTarget[] {
  const targets: DrillTarget[] = [];
  component.drill.subscribe((target) => targets.push(target));
  return targets;
}

describe('dashboard analytics sections', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideDashboardCharts(), provideRouter([])] });
  });

  describe('DepartmentAnalyticsComponent', () => {
    const departments: DepartmentAnalytics[] = [
      { departmentId: 1, name: 'Engineering', tasks: counts(9, 34, 72, 14) },
      { departmentId: null, name: 'No department', tasks: counts(1, 1, 2, 0) }
    ];

    it('shows each department with its bucket counts and completion percentage', () => {
      const { element } = render(DepartmentAnalyticsComponent, { departments });

      expect(element.querySelector('canvas')).toBeTruthy();
      expect(element.querySelector('tbody tr')?.textContent).toContain('Engineering');
      expect(element.querySelector('tbody tr')?.textContent).toContain('56%');
      expect(element.textContent).toContain('No department');
    });

    it('opens the tasks of a department bucket when a segment is clicked', () => {
      const { fixture, element } = render(DepartmentAnalyticsComponent, { departments });
      const targets = collect(fixture.componentInstance);

      element.querySelectorAll<HTMLButtonElement>('tbody tr:first-child .cell-button')[3].click();
      element.querySelectorAll<HTMLButtonElement>('tbody tr:nth-child(2) .cell-button')[0].click();

      expect(targets).toEqual([
        { departmentId: 1, bucket: 'Overdue' },
        { departmentId: null, bucket: 'Completed' }
      ]);
    });

    it('shows the empty and loading states', () => {
      const empty = render(DepartmentAnalyticsComponent, { departments: [] });
      expect(empty.element.textContent).toContain('No tasks match these filters.');

      const loading = render(DepartmentAnalyticsComponent, { departments, loading: true });
      expect(loading.element.querySelector('[role="status"]')).toBeTruthy();
      expect(loading.element.querySelector('canvas')).toBeFalsy();
    });
  });

  describe('TaskAnalyticsComponent', () => {
    const priorities: PriorityCount[] = [
      { priority: 'Low', count: 2 },
      { priority: 'Medium', count: 3 },
      { priority: 'High', count: 4 },
      { priority: 'Critical', count: 1 }
    ];
    const deadlines: DeadlineBuckets = { overdue: 3, dueToday: 1, next7Days: 5, later: 0 };
    const dayTrend: AnalyticsTrend = {
      granularity: 'day',
      points: [
        { date: '2026-10-19', created: 1, completed: 0 },
        { date: '2026-10-20', created: 0, completed: 2 }
      ]
    };
    const inputs = {
      status: counts(2, 3, 4, 1),
      priorities,
      deadlines,
      trend: dayTrend,
      basis: 'Deadline',
      range: { from: '2026-10-01', to: '2026-10-21' }
    };

    it('shows the status and priority distributions with values in text', () => {
      const { element } = render(TaskAnalyticsComponent, inputs);
      const legends = Array.from(element.querySelectorAll('.donut__legend')).map((legend) => legend.textContent);

      expect(legends[0]).toContain('Completed');
      expect(legends[0]).toContain('4 · 40%');
      expect(legends[1]).toContain('Critical');
      expect(element.querySelectorAll('canvas').length).toBe(3);
    });

    it('says the trend ignores the date basis', () => {
      const { element } = render(TaskAnalyticsComponent, inputs);

      expect(element.textContent).toContain('independent of the date basis');
      expect(element.textContent).toContain('Deadline');
    });

    it('lists the deadline buckets with counts', () => {
      const { element } = render(TaskAnalyticsComponent, inputs);
      const rows = Array.from(element.querySelectorAll('.deadline-row')).map((row) => cellsOf(row));

      expect(rows).toEqual(['Overdue 3', 'Due today 1', 'Next 7 days 5', 'Later 0']);
    });

    it('opens tasks for a status slice, a priority slice, a deadline bucket and a trend point', () => {
      const { fixture, element } = render(TaskAnalyticsComponent, inputs);
      const targets = collect(fixture.componentInstance);

      const legendButtons = element.querySelectorAll<HTMLButtonElement>('.legend-button');
      legendButtons[3].click();
      legendButtons[5].click();
      element.querySelector<HTMLButtonElement>('.deadline-row__fill--overdue')!.click();
      fixture.debugElement.query((debug) => debug.name === 'tf-line-chart').componentInstance.pointClick.emit({
        seriesKey: 'completed',
        index: 1,
        label: '20 Oct',
        value: 2
      });

      expect(targets).toEqual([
        { bucket: 'Overdue' },
        { priority: 'High', bucket: 'All' },
        { deadlineBucket: 'Overdue' },
        { bucket: 'All', range: { from: '2026-10-20', to: '2026-10-20', basis: 'Completed' } }
      ]);
    });

    it('opens a whole clipped week for a weekly trend point', () => {
      const weekly: AnalyticsTrend = {
        granularity: 'week',
        points: [
          { date: '2026-09-28', created: 3, completed: 1 },
          { date: '2026-10-19', created: 2, completed: 2 }
        ]
      };
      const { fixture } = render(TaskAnalyticsComponent, { ...inputs, trend: weekly, range: { from: '2026-10-01', to: '2026-10-21' } });
      const targets = collect(fixture.componentInstance);
      const chart = fixture.debugElement.query((debug) => debug.name === 'tf-line-chart').componentInstance;

      chart.pointClick.emit({ seriesKey: 'created', index: 0, label: '28 Sep', value: 3 });
      chart.pointClick.emit({ seriesKey: 'created', index: 1, label: '19 Oct', value: 2 });

      expect(targets).toEqual([
        { bucket: 'All', range: { from: '2026-10-01', to: '2026-10-04', basis: 'Created' } },
        { bucket: 'All', range: { from: '2026-10-19', to: '2026-10-21', basis: 'Created' } }
      ]);
    });

    it('shows empty messages and loading placeholders', () => {
      const empty = render(TaskAnalyticsComponent, {
        status: counts(0, 0, 0, 0),
        priorities: priorities.map((entry) => ({ ...entry, count: 0 })),
        deadlines: { overdue: 0, dueToday: 0, next7Days: 0, later: 0 },
        trend: { granularity: 'day', points: [{ date: '2026-10-19', created: 0, completed: 0 }] }
      });
      expect(empty.element.textContent).toContain('No tasks match these filters.');
      expect(empty.element.textContent).toContain('No tasks were created or completed in this period.');
      expect(empty.element.querySelector('canvas')).toBeFalsy();

      const loading = render(TaskAnalyticsComponent, { ...inputs, loading: true });
      expect(loading.element.querySelectorAll('[role="status"]').length).toBeGreaterThanOrEqual(4);
    });
  });

  describe('EmployeeAnalyticsComponent', () => {
    const section: EmployeeAnalyticsSection = {
      items: [
        { userId: 5, name: 'A. Thomas', tasks: counts(5, 6, 9, 4) },
        { userId: 6, name: 'B. Joseph', tasks: counts(3, 5, 12, 3) }
      ],
      totalEmployees: 48
    };

    it('shows the top employees, the view-all button and the shared-task note', () => {
      const { element } = render(EmployeeAnalyticsComponent, { section });

      expect(element.querySelector('tbody tr')?.textContent).toContain('A. Thomas');
      expect(element.querySelector('[data-action="view-all"]')?.textContent).toContain('View all 48 employees with tasks');
      expect(element.textContent).toContain('totals can be higher than the number of tasks');
    });

    it('omits the view-all button when everyone is shown', () => {
      const { element } = render(EmployeeAnalyticsComponent, { section: { ...section, totalEmployees: 2 } });

      expect(element.querySelector('[data-action="view-all"]')).toBeFalsy();
    });

    it('asks to show everyone, and offers to go back to the top 10', () => {
      const { fixture, element } = render(EmployeeAnalyticsComponent, { section });
      const requests: boolean[] = [];
      fixture.componentInstance.toggleAll.subscribe((value) => requests.push(value));

      element.querySelector<HTMLButtonElement>('[data-action="view-all"]')!.click();
      fixture.componentRef.setInput('showingAll', true);
      fixture.detectChanges();
      element.querySelector<HTMLButtonElement>('[data-action="show-top"]')!.click();

      expect(requests).toEqual([true, false]);
      expect(element.querySelector('[data-action="view-all"]')).toBeFalsy();
    });

    it('opens the tasks of an employee bucket', () => {
      const { fixture, element } = render(EmployeeAnalyticsComponent, { section });
      const targets = collect(fixture.componentInstance);

      element.querySelectorAll<HTMLButtonElement>('tbody tr:first-child .cell-button')[2].click();

      expect(targets).toEqual([{ employeeId: 5, bucket: 'Pending' }]);
    });

    it('shows the empty state', () => {
      const { element } = render(EmployeeAnalyticsComponent, { section: { items: [], totalEmployees: 0 } });

      expect(element.textContent).toContain('No tasks match these filters.');
    });
  });

  describe('ManagerAnalyticsComponent', () => {
    const managers: ManagerAnalytics[] = [
      { managerId: 1, name: 'R. Nair', departments: 2, employees: 14, projects: 3, tasks: counts(20, 30, 64, 11) },
      { managerId: 2, name: 'L. Paul', departments: 0, employees: 0, projects: 0, tasks: counts(0, 0, 0, 0) }
    ];

    it('shows each manager with team size, tasks and the bucket breakdown', () => {
      const { element } = render(ManagerAnalyticsComponent, { managers });
      const first = cellsOf(element.querySelector('tbody tr')!);

      expect(first).toContain('R. Nair 2 14 3 125 20 30 51% 11');
    });

    it('lists a manager with no team using zeros', () => {
      const { element } = render(ManagerAnalyticsComponent, { managers });
      const last = cellsOf(element.querySelector('tbody tr:last-child')!);

      expect(last).toContain('L. Paul 0 0 0 0 0 0 0% 0');
      expect(element.querySelectorAll<HTMLButtonElement>('tbody tr:last-child .cell-button')[0].disabled).toBeTrue();
    });

    it('opens the team tasks, and only the overdue ones from the overdue count', () => {
      const { fixture, element } = render(ManagerAnalyticsComponent, { managers });
      const targets = collect(fixture.componentInstance);
      const buttons = element.querySelectorAll<HTMLButtonElement>('tbody tr:first-child .cell-button');

      buttons[0].click();
      buttons[1].click();

      expect(targets).toEqual([
        { managerId: 1, bucket: 'All' },
        { managerId: 1, bucket: 'Overdue' }
      ]);
    });

    it('shows the empty and loading states', () => {
      expect(render(ManagerAnalyticsComponent, { managers: [] }).element.textContent).toContain('No managers match');
      expect(render(ManagerAnalyticsComponent, { managers, loading: true }).element.querySelector('[role="status"]')).toBeTruthy();
    });
  });

  describe('ProjectAnalyticsComponent', () => {
    const projects: ProjectAnalytics[] = [
      { projectId: 1, name: 'Apollo', tasks: counts(10, 20, 78, 4), projectedEnd: '2026-11-14T09:00:00Z', outstanding: 34 },
      { projectId: null, name: 'No project', tasks: counts(0, 0, 0, 0), projectedEnd: null, outstanding: 0 }
    ];

    it('shows a progress bar, open and overdue counts and the projected end', () => {
      const { element } = render(ProjectAnalyticsComponent, { projects });

      expect(element.textContent).toContain('Apollo');
      expect(element.textContent).toContain('70% · 34 open · 4 overdue');
      expect(element.textContent).toContain('Projected end');
      expect(element.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow')).toBe('70');
    });

    it('shows 0 percent and no end date for a project without tasks', () => {
      const { element } = render(ProjectAnalyticsComponent, { projects });

      expect(element.textContent).toContain('0% · 0 open · 0 overdue');
      expect(element.textContent).toContain('No end date');
    });

    it('opens the tasks of a project, including the no-project group', () => {
      const { fixture, element } = render(ProjectAnalyticsComponent, { projects });
      const targets = collect(fixture.componentInstance);

      const buttons = element.querySelectorAll<HTMLButtonElement>('.progress-item');
      buttons[0].click();
      buttons[1].click();

      expect(targets).toEqual([
        { projectId: 1, bucket: 'All' },
        { projectId: null, bucket: 'All' }
      ]);
    });

    it('shows the empty and loading states', () => {
      expect(render(ProjectAnalyticsComponent, { projects: [] }).element.textContent).toContain('No projects match');
      expect(render(ProjectAnalyticsComponent, { projects, loading: true }).element.querySelector('[role="status"]')).toBeTruthy();
    });
  });
});

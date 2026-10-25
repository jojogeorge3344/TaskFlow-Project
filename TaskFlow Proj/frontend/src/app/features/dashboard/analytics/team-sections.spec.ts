import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DrillTarget } from '../../../core/analytics/drill-down';
import { BucketCounts, TeamAttention, TeamEmployee } from '../../../core/models';
import { provideDashboardCharts } from '../../../shared/charts/chart-providers';
import { TeamAttentionComponent } from './team-attention.component';
import { TeamEmployeeAnalyticsComponent } from './team-employee-analytics.component';
import { TeamTaskAnalyticsComponent } from './team-task-analytics.component';

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

function collect<T extends { drill: { subscribe: (next: (value: DrillTarget) => void) => unknown } }>(component: T): DrillTarget[] {
  const emitted: DrillTarget[] = [];
  component.drill.subscribe((target) => emitted.push(target));
  return emitted;
}

describe('Manager team sections', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamEmployeeAnalyticsComponent, TeamTaskAnalyticsComponent, TeamAttentionComponent],
      providers: [provideRouter([]), provideDashboardCharts()]
    }).compileComponents();
  });

  describe('TeamEmployeeAnalyticsComponent', () => {
    const employees: TeamEmployee[] = [
      { userId: 1, name: 'B. Joseph', tasks: counts(4, 3, 2, 3), highWorkload: true, hasOverdue: true },
      { userId: 2, name: 'A. Thomas', tasks: counts(1, 1, 5, 0), highWorkload: false, hasOverdue: false },
      { userId: 3, name: 'C. Mathew', tasks: counts(0, 0, 0, 0), highWorkload: false, hasOverdue: false }
    ];

    it('shows each member with their open, done and overdue counts and completion', () => {
      const { element } = render(TeamEmployeeAnalyticsComponent, { employees });

      const row = element.querySelector('[data-employee="1"]')!;
      const cells = Array.from(row.querySelectorAll('th, td')).map((cell) => cell.textContent?.trim());
      expect(cells.slice(0, 5)).toEqual(['B. Joseph', '10', '2', '3', '17%']);
    });

    it('flags High workload and Overdue with text, not colour alone, and shows the overdue count', () => {
      const { element } = render(TeamEmployeeAnalyticsComponent, { employees });

      const flagged = element.querySelector('[data-employee="1"]')!;
      expect(flagged.querySelector('[data-flag="high-workload"]')?.textContent?.trim()).toBe('High workload');
      expect(flagged.querySelector('[data-flag="overdue"]')?.textContent?.trim()).toBe('Overdue: 3');

      const clear = element.querySelector('[data-employee="2"]')!;
      expect(clear.querySelector('[data-flag]')).toBeNull();
      expect(clear.querySelector('.flag-none')?.getAttribute('aria-label')).toBe('No flags');
    });

    it('lists a member with no tasks without a bar', () => {
      const { element } = render(TeamEmployeeAnalyticsComponent, { employees });

      const row = element.querySelector('[data-employee="3"]')!;
      expect(row.textContent).toContain('No tasks');
      expect(row.querySelector('.seg')).toBeNull();
    });

    it('drills into the member and bucket when a segment is clicked', () => {
      const { fixture, element } = render(TeamEmployeeAnalyticsComponent, { employees });
      const emitted = collect(fixture.componentInstance);

      element.querySelector<HTMLButtonElement>('[data-employee="1"] [data-segment="Overdue"]')!.click();

      expect(emitted).toEqual([{ employeeId: 1, bucket: 'Overdue' }]);
    });

    it('sizes the overdue segment to the same value the table shows', () => {
      const { element } = render(TeamEmployeeAnalyticsComponent, { employees });

      const segment = element.querySelector('[data-employee="1"] [data-segment="Overdue"]')!;
      expect(segment.textContent?.trim()).toBe('3');
    });

    it('explains an empty team and shows a placeholder while loading', () => {
      const empty = render(TeamEmployeeAnalyticsComponent, { employees: [] });
      expect(empty.element.textContent).toContain('Ask an administrator to assign departments or direct reports');

      const loading = render(TeamEmployeeAnalyticsComponent, { employees, loading: true });
      expect(loading.element.querySelector('[role="status"]')).toBeTruthy();
      expect(loading.element.querySelector('table')).toBeNull();
    });
  });

  describe('TeamTaskAnalyticsComponent', () => {
    it('shows the team total and a legend that reconciles with the status counts', () => {
      const { element } = render(TeamTaskAnalyticsComponent, {
        status: counts(2, 3, 5, 1),
        priorities: [
          { priority: 'Low', count: 4 },
          { priority: 'High', count: 7 }
        ]
      });

      expect(element.querySelector('.analytics-card__hint')?.textContent).toContain('11 team tasks');
      const legend = Array.from(element.querySelectorAll('.legend-button')).map((button) => button.textContent?.replace(/\s+/g, ' ').trim());
      expect(legend.slice(0, 4)).toEqual(['Completed5 · 45%', 'In progress3 · 27%', 'Pending2 · 18%', 'Overdue1 · 9%']);
    });

    it('drills by bucket and by priority, and has no department or trend content', () => {
      const { fixture, element } = render(TeamTaskAnalyticsComponent, {
        status: counts(2, 3, 5, 1),
        priorities: [{ priority: 'High', count: 7 }]
      });
      const emitted = collect(fixture.componentInstance);

      const buttons = Array.from(element.querySelectorAll<HTMLButtonElement>('.legend-button'));
      buttons[3].click();
      buttons[buttons.length - 1].click();

      expect(emitted).toEqual([{ bucket: 'Overdue' }, { priority: 'High', bucket: 'All' }]);
      expect(element.textContent?.toLowerCase()).not.toContain('department');
      expect(element.textContent?.toLowerCase()).not.toContain('trend');
    });

    it('shows the empty message when there are no tasks', () => {
      const { element } = render(TeamTaskAnalyticsComponent, { status: counts(0, 0, 0, 0), priorities: [] });

      expect(element.textContent).toContain('No tasks match these filters.');
    });
  });

  describe('TeamAttentionComponent', () => {
    const attention: TeamAttention = {
      delayedTasks: [
        { taskId: 231, taskCode: 'TF-231', title: 'Payment API retry', assigneeName: 'B. Joseph', projectName: 'Apollo', daysOverdue: 12 },
        { taskId: 198, taskCode: 'TF-198', title: 'Login audit', assigneeName: 'A. Thomas', projectName: null, daysOverdue: 1 },
        { taskId: 244, taskCode: 'TF-244', title: 'Regression pack', assigneeName: 'C. Mathew', projectName: 'Cirrus', daysOverdue: 0 }
      ],
      projectsNeedingAttention: [{ projectId: 4, name: 'Apollo', overdueTasks: 2 }]
    };

    it('lists the most overdue tasks with assignee, project and days overdue, each linking to the task', () => {
      const { element } = render(TeamAttentionComponent, { attention });

      const items = Array.from(element.querySelectorAll<HTMLAnchorElement>('[data-delayed-task]'));
      expect(items.map((item) => item.getAttribute('href'))).toEqual(['/tasks/231', '/tasks/198', '/tasks/244']);
      expect(items[0].textContent).toContain('TF-231');
      expect(items[0].textContent).toContain('B. Joseph');
      expect(items[0].textContent).toContain('Apollo');
      expect(items.map((item) => item.querySelector('.attention-badge')?.textContent?.trim())).toEqual([
        '12 days overdue',
        '1 day overdue',
        'Overdue today'
      ]);
    });

    it('lists projects needing attention and drills into their overdue tasks', () => {
      const { fixture, element } = render(TeamAttentionComponent, { attention });
      const emitted = collect(fixture.componentInstance);

      const project = element.querySelector<HTMLButtonElement>('[data-attention-project]')!;
      expect(project.textContent).toContain('2 overdue');
      project.click();

      expect(emitted).toEqual([{ projectId: 4, bucket: 'Overdue' }]);
    });

    it('says nothing is overdue when the lists are empty', () => {
      const { element } = render(TeamAttentionComponent, {
        attention: { delayedTasks: [], projectsNeedingAttention: [] }
      });

      expect(element.querySelector('[data-attention-empty]')?.textContent).toContain('Nothing is overdue');
      expect(element.querySelector('[data-delayed-task]')).toBeNull();
    });

    it('shows a placeholder while loading', () => {
      const { element } = render(TeamAttentionComponent, { attention, loading: true });

      expect(element.querySelector('[role="status"]')).toBeTruthy();
      expect(element.querySelector('[data-delayed-task]')).toBeNull();
    });
  });
});

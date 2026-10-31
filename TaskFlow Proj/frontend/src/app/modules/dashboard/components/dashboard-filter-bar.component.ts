import { Component, computed, input, output } from '@angular/core';
import {
  BASIS_LABELS,
  RANGE_PRESETS,
  RangePreset,
  detectPreset,
  presetRange,
  validateRange
} from '../../../shared/utils/analytics-filter';
import { ANALYTICS_BASES, AnalyticsBasis, AnalyticsFilterOptions, AnalyticsFilterState } from '../analytics/models/analytics.model';

@Component({
  selector: 'tf-dashboard-filter-bar',
  standalone: true,
  templateUrl: './dashboard-filter-bar.component.html',
  styleUrl: './dashboard-filter-bar.component.css'
})
export class DashboardFilterBarComponent {
  readonly filter = input.required<AnalyticsFilterState>();
  readonly options = input<AnalyticsFilterOptions | null>(null);
  readonly today = input.required<string>();
  readonly showDepartment = input(true);
  readonly showProject = input(true);
  readonly showManager = input(true);
  readonly showEmployee = input(true);

  readonly filterChange = output<Partial<AnalyticsFilterState>>();
  readonly reset = output<void>();

  readonly presets = RANGE_PRESETS;
  readonly bases = ANALYTICS_BASES;
  readonly basisLabels = BASIS_LABELS;

  readonly activePreset = computed<RangePreset>(() => detectPreset(this.filter(), this.today()));
  readonly rangeError = computed(() => validateRange(this.filter().from, this.filter().to));

  choosePreset(preset: RangePreset): void {
    if (preset === 'custom') {
      return;
    }
    this.filterChange.emit(presetRange(preset, this.today()));
  }

  setFrom(value: string): void {
    this.filterChange.emit({ from: value });
  }

  setTo(value: string): void {
    this.filterChange.emit({ to: value });
  }

  setBasis(value: string): void {
    this.filterChange.emit({ basis: value as AnalyticsBasis });
  }

  setEntity(field: 'departmentId' | 'projectId' | 'employeeId' | 'managerId', value: string): void {
    this.filterChange.emit({ [field]: value ? Number(value) : null });
  }
}

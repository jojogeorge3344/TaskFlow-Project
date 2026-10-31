import { EnvironmentProviders, Provider } from '@angular/core';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';

export function provideDashboardCharts(): (Provider | EnvironmentProviders)[] {
  return [provideCharts(withDefaultRegisterables())];
}

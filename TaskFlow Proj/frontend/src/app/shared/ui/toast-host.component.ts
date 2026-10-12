import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { IconComponent } from '../../core/layout/icon.component';
import { ToastService } from './toast.service';

@Component({
  selector: 'tf-toast-host',
  standalone: true,
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tf-toasts" role="status" aria-live="polite">
      @for (toast of toasts(); track toast.id) {
        <div class="tf-toast" [class.tf-toast--error]="toast.kind === 'error'">
          <tf-icon [name]="toast.kind === 'error' ? 'alert' : 'check'" />
          <span>{{ toast.message }}</span>
        </div>
      }
    </div>
  `
})
export class ToastHostComponent {
  readonly toasts = inject(ToastService).toasts;
}

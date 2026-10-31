import { Injectable, signal } from '@angular/core';
import { ToastKind, Toast } from './toast.model';

export const TOAST_DURATION_MS = 4000;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly toastsSignal = signal<Toast[]>([]);

  readonly toasts = this.toastsSignal.asReadonly();

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  show(message: string, kind: ToastKind = 'success'): void {
    const toast: Toast = { id: this.nextId++, message, kind };
    this.toastsSignal.update((toasts) => [...toasts, toast]);
    setTimeout(() => this.dismiss(toast.id), TOAST_DURATION_MS);
  }

  dismiss(id: number): void {
    this.toastsSignal.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }
}

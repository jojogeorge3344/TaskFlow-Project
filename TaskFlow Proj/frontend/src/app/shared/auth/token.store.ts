import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TokenStore {
  private readonly tokenSignal = signal<string | null>(null);

  get token(): string | null {
    return this.tokenSignal();
  }

  set(token: string | null): void {
    this.tokenSignal.set(token);
  }

  clear(): void {
    this.tokenSignal.set(null);
  }
}

import { Component, inject, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { AppBackdropComponent } from './shared/components/backdrop/app-backdrop.component';
import { LEGAL_APPLICATION, LEGAL_OWNER, legalOwnershipLine } from './modules/legal/utils/legal-owner';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, AppBackdropComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly router = inject(Router);

  /** The login screen draws its own background, so the app-wide one is not shown there. */
  readonly showBackdrop = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => !/^\/login(\/|\?|#|$)/.test(this.router.url))
    ),
    { initialValue: true }
  );

  ngOnInit(): void {
    this.title.setTitle(`${LEGAL_APPLICATION.name} — ${LEGAL_APPLICATION.title}`);
    this.meta.updateTag({ name: 'author', content: LEGAL_OWNER.name });
    this.meta.updateTag({ name: 'description', content: LEGAL_APPLICATION.description });
    this.meta.updateTag({ name: 'copyright', content: legalOwnershipLine() });
  }
}

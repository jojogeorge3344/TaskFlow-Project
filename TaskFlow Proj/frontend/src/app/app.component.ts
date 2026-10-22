import { Component, inject, OnInit } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterOutlet } from '@angular/router';
import { LEGAL_APPLICATION, LEGAL_OWNER, legalOwnershipLine } from './features/legal/legal-owner';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  ngOnInit(): void {
    this.title.setTitle(`${LEGAL_APPLICATION.name} — ${LEGAL_APPLICATION.title}`);
    this.meta.updateTag({ name: 'author', content: LEGAL_OWNER.name });
    this.meta.updateTag({ name: 'description', content: LEGAL_APPLICATION.description });
    this.meta.updateTag({ name: 'copyright', content: legalOwnershipLine() });
  }
}

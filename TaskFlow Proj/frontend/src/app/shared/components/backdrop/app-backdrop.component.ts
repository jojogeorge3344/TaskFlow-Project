import { Component } from '@angular/core';
import { BackdropAnimation } from '../../utils/backdrop-animation';
import { BackdropColors } from '../../utils/backdrop-animation.model';

/** App-wide animated backdrop. Colours come from the active theme's --tf-backdrop-* tokens and follow theme changes. */
@Component({
  selector: 'app-backdrop',
  standalone: true,
  template: '<canvas #canvas class="backdrop__canvas" aria-hidden="true"></canvas>',
  styleUrl: './app-backdrop.component.css'
})
export class AppBackdropComponent extends BackdropAnimation {
  private observer: MutationObserver | null = null;

  protected resolveColors(): BackdropColors {
    const node = getComputedStyle(document.documentElement).getPropertyValue('--tf-backdrop-node').trim() || '37, 99, 235';
    return { link: node, dot: node };
  }

  protected override afterInit(): void {
    this.observer = new MutationObserver(() => this.refreshColors());
    this.observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  }

  protected override beforeDestroy(): void {
    this.observer?.disconnect();
    this.observer = null;
  }
}

import { Component } from '@angular/core';
import { BackdropAnimation } from '../../../../shared/utils/backdrop-animation';
import { BackdropColors } from '../../../../shared/utils/backdrop-animation.model';

/** Full-screen particle network behind the login card, always in its fixed dark blue look (not themed). */
@Component({
  selector: 'app-login-background',
  templateUrl: './login-background.component.html',
  styleUrl: './login-background.component.css'
})
export class LoginBackgroundComponent extends BackdropAnimation {
  protected resolveColors(): BackdropColors {
    return { link: '147, 197, 253', dot: '191, 219, 254' };
  }
}

import { Component, inject, input, signal } from '@angular/core';
import { AssistantActionKind, AssistantActionStatus, AssistantPendingAction } from '../models';
import { AssistantService } from '../services/assistant.service';
import { IconComponent } from './icon.component';

const CONFIRM_LABEL: Record<AssistantActionKind, string> = {
  SendMail: 'Send mail',
  UpdateOwnProfile: 'Save changes',
  ChangeUserRole: 'Change role'
};

const STATUS_LABEL: Record<Exclude<AssistantActionStatus, 'Pending'>, string> = {
  Confirmed: 'Done',
  Cancelled: 'Cancelled',
  Expired: 'Expired. Ask TFA again.',
  Refused: 'Not allowed',
  Failed: 'Not done'
};

/** What TFA has prepared and is waiting for the user to confirm: a mail, a profile change or a role change. It is drawn
 * from the stored payload, never from model text. Confirm, Edit and Cancel only send the user's choice to the server;
 * whether the action is allowed is the server's decision. */
@Component({
  selector: 'app-tfa-action-card',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './tfa-action-card.component.html',
  styleUrl: './tfa-action-card.component.css'
})
export class TfaActionCardComponent {
  private readonly assistant = inject(AssistantService);

  readonly action = input.required<AssistantPendingAction>();

  readonly editing = signal(false);
  readonly editTo = signal('');
  readonly editSubject = signal('');
  readonly editBody = signal('');
  readonly editError = signal<string | null>(null);
  readonly saving = signal(false);

  confirmLabel(): string {
    return CONFIRM_LABEL[this.action().kind];
  }

  statusLabel(): string {
    const status = this.action().status;
    return status === 'Pending' ? '' : STATUS_LABEL[status];
  }

  confirm(): void {
    this.editing.set(false);
    this.assistant.confirm(this.action());
  }

  cancel(): void {
    this.editing.set(false);
    this.assistant.cancel(this.action());
  }

  startEdit(): void {
    const action = this.action();
    const to = action.fields.find((field) => field.label === 'To')?.value ?? '';
    this.editTo.set([...to.matchAll(/<([^>]+)>/g)].map((match) => match[1]).join(', '));
    this.editSubject.set(action.fields.find((field) => field.label === 'Subject')?.value ?? '');
    this.editBody.set(action.body ?? '');
    this.editError.set(null);
    this.editing.set(true);
  }

  stopEdit(): void {
    this.editing.set(false);
    this.editError.set(null);
  }

  saveEdit(): void {
    const to = this.editTo()
      .split(/[,;\s]+/)
      .map((address) => address.trim())
      .filter((address) => address.length > 0);

    this.saving.set(true);
    this.assistant.edit(this.action(), { to, subject: this.editSubject(), body: this.editBody() }, (error) => {
      this.saving.set(false);
      if (error) {
        this.editError.set(error);
      } else {
        this.stopEdit();
      }
    });
  }

  onInput(field: 'to' | 'subject' | 'body', event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    if (field === 'to') {
      this.editTo.set(value);
    } else if (field === 'subject') {
      this.editSubject.set(value);
    } else {
      this.editBody.set(value);
    }
  }
}

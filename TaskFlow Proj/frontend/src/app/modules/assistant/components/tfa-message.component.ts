import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AssistantMessage, AssistantRankedTask } from '../models/assistant.model';

/** The separator the server uses between headings in a section path. */
const HEADING_SEPARATOR = ' › ';

export interface SourceChip {
  key: string;
  label: string;
  title: string;
}
import { AssistantService } from '../services/assistant.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { TfaActionCardComponent } from './tfa-action-card.component';

/** One message in the TFA conversation: the user's own, TFA's answer, a fixed refusal or a notice. Refusals and
 * notices are text from the server, never worded by a model. */
@Component({
  selector: 'app-tfa-message',
  standalone: true,
  imports: [RouterLink, IconComponent, TfaActionCardComponent],
  templateUrl: './tfa-message.component.html',
  styleUrl: './tfa-message.component.css'
})
export class TfaMessageComponent {
  private readonly assistant = inject(AssistantService);

  readonly message = input.required<AssistantMessage>();

  readonly rowClass = computed(() => {
    const message = this.message();
    return message.role === 'User' ? 'tfa-msg tfa-msg--user' : `tfa-msg tfa-msg--bot tfa-msg--${message.kind.toLowerCase()}`;
  });

  readonly icon = computed(() => {
    const message = this.message();
    return message.kind === 'Refusal' ? 'lock' : message.problem ? 'alert' : 'sparkles';
  });

  /** One chip per document section: the document title and the section's own heading, with the full heading path
   * and file as the tooltip. */
  readonly sources = computed<SourceChip[]>(() =>
    (this.message().sources ?? []).map((source) => {
      const heading = source.section.split(HEADING_SEPARATOR).pop() ?? source.section;
      return {
        key: `${source.path}#${source.section}`,
        label: heading && heading !== source.document ? `${source.document}${HEADING_SEPARATOR}${heading}` : source.document,
        title: `${source.document}${HEADING_SEPARATOR}${source.section}\n${source.path}`
      };
    })
  );

  stageLabel(task: AssistantRankedTask): string {
    if (task.isBlocked) {
      return 'Blocked';
    }
    switch (task.stage) {
      case 'Overdue':
        return 'Overdue';
      case 'DueToday':
        return 'Today';
      default:
        return task.daysUntilDue === 1 ? 'Tomorrow' : `${task.daysUntilDue} days`;
    }
  }

  stageClass(task: AssistantRankedTask): string {
    if (task.isBlocked) {
      return 'tfa-tag tfa-tag--block';
    }
    return task.stage === 'Overdue' ? 'tfa-tag tfa-tag--over' : task.stage === 'DueToday' ? 'tfa-tag tfa-tag--soon' : 'tfa-tag tfa-tag--ok';
  }

  taskMeta(task: AssistantRankedTask): string {
    const days = Math.abs(task.daysUntilDue);
    const due = task.stage === 'Overdue' ? `was due ${days} day${days === 1 ? '' : 's'} ago` : `due ${task.dueDate}`;
    return `${task.priority} · ${due}`;
  }

  followLink(): void {
    this.assistant.closeOnPhone();
  }
}

import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, output, signal, untracked, viewChildren } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { IconComponent } from '../../core/layout/icon.component';
import { MAIL_LIMITS, MailAttachment, MailComposeMode } from '../../core/models';
import { MailService } from '../../core/services/mail.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ToastService } from '../../shared/ui/toast.service';
import { formatSize } from './mail-format';
import { RecipientPickerComponent } from './recipient-picker.component';
import { RecipientChip } from './recipient-picker.model';
import { ComposeRequest, ComposeResult, ComposeField } from './compose-dialog.model';

/** Writes a new mail, a reply, reply all or forward, or continues a draft. Recipients must be TaskFlow users. */
@Component({
  selector: 'app-compose-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent, DrawerComponent, RecipientPickerComponent],
  templateUrl: './compose-dialog.component.html',
  styleUrl: './compose-dialog.component.css'
})
export class ComposeDialogComponent {
  private readonly mailService = inject(MailService);
  private readonly toast = inject(ToastService);
  private readonly pickers = viewChildren(RecipientPickerComponent);

  readonly request = input<ComposeRequest | null>(null);
  readonly closed = output<ComposeResult>();

  readonly to = signal<RecipientChip[]>([]);
  readonly cc = signal<RecipientChip[]>([]);
  readonly showCc = signal(false);
  readonly subject = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(MAIL_LIMITS.maxSubjectLength)] });
  readonly body = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(MAIL_LIMITS.maxBodyLength)] });
  readonly attachments = signal<MailAttachment[]>([]);
  /** The original's files on a forward; copied by the server when sent. */
  readonly forwarded = signal<MailAttachment[]>([]);

  readonly loading = signal(false);
  readonly uploading = signal(0);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly fieldErrors = signal<Partial<Record<ComposeField, string>>>({});
  /** Send was pressed while a just-added recipient was still being checked; send once the checks finish. */
  readonly sendWhenChecked = signal(false);

  private draftMessageId: number | null = null;
  private draftItemId: number | null = null;
  private parentItemId: number | null = null;
  private mode: MailComposeMode | null = null;
  /** Uploads made in this dialog, removed again if the mail is discarded. */
  private readonly uploadedHere = new Set<number>();

  readonly open = computed(() => this.request() !== null);
  readonly limits = MAIL_LIMITS;
  readonly formatSize = formatSize;

  readonly title = computed(() => {
    const request = this.request();
    if (request?.kind === 'reply') {
      return request.mode === 'Forward' ? 'Forward' : request.mode === 'ReplyAll' ? 'Reply all' : 'Reply';
    }
    return request?.kind === 'draft' ? 'Draft' : 'New mail';
  });

  constructor() {
    effect(() => {
      const request = this.request();
      untracked(() => this.start(request));
    });

    effect(() => {
      const checking = [...this.to(), ...this.cc()].some((chip) => chip.valid === null);
      if (this.sendWhenChecked() && !checking) {
        untracked(() => {
          this.sendWhenChecked.set(false);
          this.send();
        });
      }
    });
  }

  hasBlockingRecipients(): boolean {
    return [...this.to(), ...this.cc()].some((chip) => chip.valid !== true);
  }

  async onFiles(input: HTMLInputElement): Promise<void> {
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.setFieldError('File', null);
    for (const file of files) {
      const problem = this.checkFile(file);
      if (problem) {
        this.setFieldError('File', problem);
        continue;
      }
      this.uploading.update((count) => count + 1);
      this.mailService.uploadAttachment(file).subscribe({
        next: (attachment) => {
          this.uploadedHere.add(attachment.id);
          this.attachments.update((list) => [...list, attachment]);
          this.uploading.update((count) => count - 1);
        },
        error: (err: unknown) => {
          this.uploading.update((count) => count - 1);
          this.setFieldError('File', this.serverFieldError(err, 'File') ?? apiErrorMessage(err, `Could not upload ${file.name}.`));
        }
      });
    }
  }

  removeAttachment(attachment: MailAttachment): void {
    this.attachments.update((list) => list.filter((existing) => existing.id !== attachment.id));
    if (this.uploadedHere.delete(attachment.id)) {
      this.mailService.deleteAttachment(attachment.id).subscribe({ error: () => undefined });
    }
  }

  send(): void {
    this.error.set(null);
    this.fieldErrors.set({});
    // An address still sitting in a picker's text box is part of the mail, not something to drop.
    this.pickers().forEach((picker) => picker.commitPending());
    if (this.to().length + this.cc().length === 0) {
      this.setFieldError('To', 'Add at least one recipient.');
      return;
    }
    if ([...this.to(), ...this.cc()].some((chip) => chip.valid === null)) {
      this.sendWhenChecked.set(true);
      this.setFieldError('To', 'Checking recipients…');
      return;
    }
    if (this.hasBlockingRecipients()) {
      this.setFieldError('To', 'Remove or fix recipients that are not TaskFlow users.');
      return;
    }
    if (!this.subject.value.trim() && !this.body.value.trim()) {
      this.setFieldError('Body', 'Write a subject or a message before sending.');
      return;
    }
    if (this.subject.invalid || this.body.invalid || this.uploading() > 0 || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.mailService
      .send({
        to: this.to().map((chip) => chip.email),
        cc: this.cc().map((chip) => chip.email),
        subject: this.subject.value,
        body: this.body.value,
        attachmentIds: this.attachments().map((attachment) => attachment.id),
        draftId: this.draftMessageId,
        parentItemId: this.draftMessageId ? null : this.parentItemId,
        mode: this.draftMessageId ? null : this.mode
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.uploadedHere.clear();
          this.toast.success('Mail sent');
          this.closed.emit('sent');
        },
        error: (err: unknown) => this.fail(err, 'Could not send the mail.')
      });
  }

  saveDraft(): void {
    if (this.busy() || this.uploading() > 0) {
      return;
    }
    this.pickers().forEach((picker) => picker.commitPending());
    this.error.set(null);
    this.busy.set(true);
    const draft = {
      to: this.to().map((chip) => chip.email),
      cc: this.cc().map((chip) => chip.email),
      subject: this.subject.value,
      body: this.body.value,
      attachmentIds: this.attachments().map((attachment) => attachment.id),
      parentItemId: this.parentItemId,
      mode: this.mode
    };
    const save = this.draftMessageId ? this.mailService.updateDraft(this.draftMessageId, draft) : this.mailService.createDraft(draft);
    save.subscribe({
      next: () => {
        this.busy.set(false);
        this.uploadedHere.clear();
        this.toast.success('Draft saved');
        this.closed.emit('saved');
      },
      error: (err: unknown) => this.fail(err, 'Could not save the draft.')
    });
  }

  discard(): void {
    for (const id of this.uploadedHere) {
      this.mailService.deleteAttachment(id).subscribe({ error: () => undefined });
    }
    this.uploadedHere.clear();
    if (this.draftItemId) {
      this.mailService.deleteForever(this.draftItemId).subscribe({
        next: () => {
          this.toast.success('Draft discarded');
          this.closed.emit('discarded');
        },
        error: (err: unknown) => this.toast.error(apiErrorMessage(err, 'Could not discard the draft.'))
      });
      return;
    }
    this.closed.emit('discarded');
  }

  cancel(): void {
    this.closed.emit('cancelled');
  }

  fieldError(field: ComposeField): string | null {
    return this.fieldErrors()[field] ?? null;
  }

  private start(request: ComposeRequest | null): void {
    this.reset();
    if (!request || request.kind === 'new') {
      return;
    }

    this.loading.set(true);
    if (request.kind === 'reply') {
      this.parentItemId = request.itemId;
      this.mode = request.mode;
      this.mailService.compose(request.itemId, request.mode).subscribe({
        next: (compose) => {
          this.to.set(compose.to.map((email) => ({ email: email.toLowerCase(), name: null, valid: true })));
          this.cc.set(compose.cc.map((email) => ({ email: email.toLowerCase(), name: null, valid: true })));
          this.showCc.set(compose.cc.length > 0);
          this.subject.setValue(compose.subject);
          this.body.setValue(compose.body);
          this.forwarded.set(compose.forwardedAttachments);
          this.loading.set(false);
        },
        error: (err: unknown) => this.fail(err, 'Could not prepare the reply.')
      });
      return;
    }

    this.draftItemId = request.itemId;
    this.mailService.open(request.itemId).subscribe({
      next: (detail) => {
        this.draftMessageId = detail.message.messageId;
        this.to.set(this.chipsFrom(detail.draftTo));
        this.cc.set(this.chipsFrom(detail.draftCc));
        this.showCc.set(!!detail.draftCc);
        this.subject.setValue(detail.message.subject);
        this.body.setValue(detail.message.body);
        this.attachments.set(detail.message.attachments);
        this.loading.set(false);
      },
      error: (err: unknown) => this.fail(err, 'Could not open the draft.')
    });
  }

  private reset(): void {
    this.to.set([]);
    this.cc.set([]);
    this.showCc.set(false);
    this.subject.reset('');
    this.body.reset('');
    this.attachments.set([]);
    this.forwarded.set([]);
    this.error.set(null);
    this.fieldErrors.set({});
    this.busy.set(false);
    this.loading.set(false);
    this.sendWhenChecked.set(false);
    this.draftMessageId = null;
    this.draftItemId = null;
    this.parentItemId = null;
    this.mode = null;
    this.uploadedHere.clear();
  }

  private chipsFrom(joined: string | null): RecipientChip[] {
    return (joined ?? '')
      .split(/[,;]/)
      .map((email) => email.trim().toLowerCase())
      .filter((email) => email.length > 0)
      .map((email) => ({ email, name: null, valid: null }));
  }

  /** A friendly early check; the server enforces the same limits. */
  private checkFile(file: File): string | null {
    const name = file.name.toLowerCase();
    const extension = name.includes('.') ? name.slice(name.lastIndexOf('.')) : '';
    if ((MAIL_LIMITS.blockedExtensions as readonly string[]).includes(extension)) {
      return `Files of type ${extension} are not allowed.`;
    }
    if (file.size === 0) {
      return `${file.name} is empty.`;
    }
    if (file.size > MAIL_LIMITS.maxAttachmentBytes) {
      return `${file.name} is larger than 10 MB.`;
    }
    const current = [...this.attachments(), ...this.forwarded()];
    if (current.length + this.uploading() >= MAIL_LIMITS.maxAttachments) {
      return `A mail can have at most ${MAIL_LIMITS.maxAttachments} attachments.`;
    }
    const total = current.reduce((sum, attachment) => sum + attachment.sizeBytes, 0) + file.size;
    if (total > MAIL_LIMITS.maxTotalAttachmentBytes) {
      return 'Attachments on one mail can total at most 25 MB.';
    }
    return null;
  }

  private fail(err: unknown, fallback: string): void {
    this.busy.set(false);
    this.loading.set(false);
    const errors = this.serverErrors(err);
    if (errors) {
      const mapped: Partial<Record<ComposeField, string>> = {};
      for (const [key, messages] of Object.entries(errors)) {
        const field = (['To', 'Cc', 'Subject', 'Body', 'AttachmentIds', 'Mode', 'File'] as ComposeField[]).find(
          (candidate) => candidate.toLowerCase() === key.toLowerCase()
        );
        if (field && messages?.length) {
          mapped[field] = messages[0];
        }
      }
      if (Object.keys(mapped).length) {
        this.fieldErrors.set(mapped);
        return;
      }
    }
    this.error.set(apiErrorMessage(err, fallback));
  }

  private serverErrors(err: unknown): Record<string, string[]> | null {
    if (!(err instanceof HttpErrorResponse) || err.status !== 400) {
      return null;
    }
    return (err.error as { errors?: Record<string, string[]> } | null)?.errors ?? null;
  }

  private serverFieldError(err: unknown, field: ComposeField): string | null {
    const errors = this.serverErrors(err);
    const match = errors ? Object.entries(errors).find(([key]) => key.toLowerCase() === field.toLowerCase()) : undefined;
    return match?.[1]?.[0] ?? null;
  }

  private setFieldError(field: ComposeField, message: string | null): void {
    this.fieldErrors.update((errors) => {
      const next = { ...errors };
      if (message) {
        next[field] = message;
      } else {
        delete next[field];
      }
      return next;
    });
  }
}

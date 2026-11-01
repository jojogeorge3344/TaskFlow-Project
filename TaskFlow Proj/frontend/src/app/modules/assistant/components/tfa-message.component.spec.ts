import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AssistantMessage } from '../models/assistant.model';
import { AssistantService } from '../services/assistant.service';
import { TfaMessageComponent } from './tfa-message.component';

function answer(extra: Partial<AssistantMessage> = {}): AssistantMessage {
  return {
    id: 2,
    role: 'Assistant',
    content: 'The assignee and the task creator receive it.',
    kind: 'Text',
    createdAt: '2026-10-31T09:00:00Z',
    providerId: 1,
    provider: 'Ollama',
    links: [],
    rankedTasks: [],
    action: null,
    sources: [],
    ...extra
  };
}

describe('TfaMessageComponent sources', () => {
  let fixture: ComponentFixture<TfaMessageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TfaMessageComponent],
      providers: [provideRouter([]), { provide: AssistantService, useValue: { closeOnPhone: () => undefined } }]
    });
    fixture = TestBed.createComponent(TfaMessageComponent);
  });

  function render(message: AssistantMessage): HTMLElement {
    fixture.componentRef.setInput('message', message);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('does not show the sources of an answer that has them', () => {
    const element = render(
      answer({
        sources: [
          {
            document: 'FRD – Task Management',
            section: '4. Functional Requirements by Module › 4.1.2 Screen: Create Task (and Copy Task)',
            path: 'TF - FRD/02-TaskFlow-Monitoring/02.1-Task-Management/FRD-Task-Management.md'
          },
          { document: 'SCREEN 8: Create Task Screen', section: 'Functional Requirements', path: 'Screens Wise Feature Development Plan/08-create-task-screen.md' }
        ]
      })
    );

    expect(element.querySelector('[data-sources]')).toBeNull();
    expect(element.querySelectorAll('[data-source]').length).toBe(0);
    expect(element.textContent).not.toContain('Sources');
    expect(element.textContent).toContain('The assignee and the task creator receive it.');
  });
});

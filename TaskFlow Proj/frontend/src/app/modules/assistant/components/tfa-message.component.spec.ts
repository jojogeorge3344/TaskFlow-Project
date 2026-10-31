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

  it('shows one chip per source with the document and the last heading, and the full path as the tooltip', () => {
    const element = render(
      answer({
        sources: [
          {
            document: 'FRD – Task Management',
            section: '4. Functional Requirements by Module › 4.1 Module: Task Management (TASK) › 4.1.2 Screen: Create Task (and Copy Task)',
            path: 'TF - FRD/02-TaskFlow-Monitoring/02.1-Task-Management/FRD-Task-Management.md'
          },
          { document: 'SCREEN 8: Create Task Screen', section: 'Functional Requirements', path: 'Screens Wise Feature Development Plan/08-create-task-screen.md' }
        ]
      })
    );

    const chips = Array.from(element.querySelectorAll<HTMLElement>('[data-source]'));
    expect(chips.map((chip) => chip.textContent?.trim())).toEqual([
      'FRD – Task Management › 4.1.2 Screen: Create Task (and Copy Task)',
      'SCREEN 8: Create Task Screen › Functional Requirements'
    ]);
    expect(chips[0].title).toBe(
      'FRD – Task Management › 4. Functional Requirements by Module › 4.1 Module: Task Management (TASK) › 4.1.2 Screen: Create Task (and Copy Task)\n'
        + 'TF - FRD/02-TaskFlow-Monitoring/02.1-Task-Management/FRD-Task-Management.md'
    );
    expect(element.querySelector('[data-sources]')?.textContent).toContain('Sources');
  });

  it('shows only the document when the section is the document itself', () => {
    const element = render(answer({ sources: [{ document: 'TaskFlow TDD', section: 'TaskFlow TDD', path: 'TF - TDD/TDD-TaskFlow.md' }] }));

    expect(element.querySelector('[data-source]')?.textContent?.trim()).toBe('TaskFlow TDD');
  });

  it('shows nothing for an answer without sources', () => {
    const element = render(answer());

    expect(element.querySelector('[data-sources]')).toBeNull();
    expect(element.querySelectorAll('[data-source]').length).toBe(0);
  });

  it('shows nothing for a message that arrived without a sources field', () => {
    const legacy = answer();
    delete (legacy as Partial<AssistantMessage>).sources;

    const element = render(legacy);

    expect(element.querySelector('[data-sources]')).toBeNull();
  });
});

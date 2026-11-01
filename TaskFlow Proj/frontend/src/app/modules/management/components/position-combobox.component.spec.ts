import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Position } from '../project-management/models/project.model';
import { PositionService } from '../project-management/positions/services/position.service';
import { ToastService } from '../../../shared/services/toast.service';
import { PositionComboboxComponent } from './position-combobox.component';

const positions: Position[] = [
  { id: 3, name: 'QA' },
  { id: 4, name: 'Developer' }
];

describe('PositionComboboxComponent', () => {
  let getOrCreate: jasmine.Spy;
  let fixture: ComponentFixture<PositionComboboxComponent>;
  let chosen: jasmine.Spy;
  let created: jasmine.Spy;
  let errorToast: jasmine.Spy;

  function input(): HTMLInputElement {
    return (fixture.nativeElement as HTMLElement).querySelector('input') as HTMLInputElement;
  }

  function commit(value: string): void {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    input().dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    getOrCreate = jasmine.createSpy('getOrCreate').and.callFake((name: string) => of({ id: 99, name }));
    await TestBed.configureTestingModule({
      imports: [PositionComboboxComponent],
      providers: [{ provide: PositionService, useValue: { getOrCreate } }]
    }).compileComponents();

    errorToast = spyOn(TestBed.inject(ToastService), 'error');
    fixture = TestBed.createComponent(PositionComboboxComponent);
    fixture.componentRef.setInput('positions', positions);
    chosen = jasmine.createSpy('chosen');
    created = jasmine.createSpy('created');
    fixture.componentInstance.chosen.subscribe(chosen);
    fixture.componentInstance.created.subscribe(created);
    fixture.detectChanges();
  });

  it('shows the name of the chosen position and lists every position as an option', () => {
    fixture.componentRef.setInput('positionId', 4);
    fixture.detectChanges();

    expect(input().value).toBe('Developer');
    const options = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('datalist option')).map((o) => (o as HTMLOptionElement).value);
    expect(options).toEqual(['QA', 'Developer']);
  });

  it('picks an existing position without creating one', () => {
    commit('QA');

    expect(chosen).toHaveBeenCalledOnceWith(positions[0]);
    expect(getOrCreate).not.toHaveBeenCalled();
  });

  it('uses the existing position when the typed name differs only by case', () => {
    commit('  qa ');

    expect(chosen).toHaveBeenCalledOnceWith(positions[0]);
    expect(getOrCreate).not.toHaveBeenCalled();
  });

  it('creates a position typed by the user and reports it', () => {
    commit('Scrum Master');

    expect(getOrCreate).toHaveBeenCalledOnceWith('Scrum Master');
    expect(created).toHaveBeenCalledOnceWith({ id: 99, name: 'Scrum Master' });
    expect(chosen).toHaveBeenCalledOnceWith({ id: 99, name: 'Scrum Master' });
  });

  it('clears the choice when the field is emptied', () => {
    commit('');

    expect(chosen).toHaveBeenCalledOnceWith(null);
  });

  it('shows the server message and chooses nothing when the position cannot be created', () => {
    getOrCreate.and.returnValue(throwError(() => ({ error: { error: 'Position name must be 100 characters or fewer.' } })));
    commit('x'.repeat(10));

    expect(errorToast).toHaveBeenCalledWith('Position name must be 100 characters or fewer.');
    expect(chosen).not.toHaveBeenCalled();
  });
});

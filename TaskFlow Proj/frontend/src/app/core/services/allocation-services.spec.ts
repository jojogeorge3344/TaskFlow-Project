import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PositionService } from './position.service';
import { ProjectService } from './project.service';
import { UserService } from './user.service';

describe('allocation services', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('PositionService', () => {
    it('lists the positions', () => {
      TestBed.inject(PositionService).getAll().subscribe((positions) => expect(positions).toEqual([{ id: 1, name: 'Member' }]));

      const request = http.expectOne('/api/v1/positions');
      expect(request.request.method).toBe('GET');
      request.flush([{ id: 1, name: 'Member' }]);
    });

    it('creates (or finds) a position by name', () => {
      TestBed.inject(PositionService).getOrCreate('Scrum Master').subscribe((position) => expect(position.id).toBe(9));

      const request = http.expectOne('/api/v1/positions');
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ name: 'Scrum Master' });
      request.flush({ id: 9, name: 'Scrum Master' });
    });
  });

  describe('ProjectService members', () => {
    it('reads the team of a project', () => {
      TestBed.inject(ProjectService).getMembers(4).subscribe();

      const request = http.expectOne('/api/v1/projects/4/members');
      expect(request.request.method).toBe('GET');
      request.flush([]);
    });

    it('replaces the team with the members wrapped in a request body', () => {
      const members = [{ userId: 2, positionId: 3, percent: 60, isManager: true }];
      TestBed.inject(ProjectService).replaceMembers(4, members).subscribe();

      const request = http.expectOne('/api/v1/projects/4/members');
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ members });
      request.flush([]);
    });
  });

  describe('UserService filters', () => {
    it('sends the project and department filters so they match any membership', () => {
      TestBed.inject(UserService).getAll({ projectId: 2, departmentId: 5, isActive: true }).subscribe();

      const request = http.expectOne((r) => r.url === '/api/v1/users');
      expect(request.request.params.get('projectId')).toBe('2');
      expect(request.request.params.get('departmentId')).toBe('5');
      expect(request.request.params.get('isActive')).toBe('true');
      request.flush([]);
    });
  });
});

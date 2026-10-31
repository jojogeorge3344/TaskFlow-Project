import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChangePassword, UpdateOwnProfile, User } from '../../management/employee-management/models/user.model';
import { ProfileService } from './profile.service';
import { NO_ALLOCATIONS, withProject } from '../../../shared/testing/users';

describe('ProfileService', () => {
  let service: ProfileService;
  let httpMock: HttpTestingController;

  const me: User = {
    id: 3,
    fullName: 'Jo Jo',
    username: 'jojo',
    email: 'jojo@taskflow.com',
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    ...NO_ALLOCATIONS
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ProfileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the own profile with GET /users/me', () => {
    let result: User | undefined;
    service.get().subscribe((user) => (result = user));

    const request = httpMock.expectOne('/api/v1/users/me');
    expect(request.request.method).toBe('GET');
    request.flush(me);

    expect(result).toEqual(me);
  });

  it('updates name and email with PUT /users/me', () => {
    const update: UpdateOwnProfile = { fullName: 'Joanna Jo', email: 'joanna@taskflow.com' };
    let result: User | undefined;
    service.update(update).subscribe((user) => (result = user));

    const request = httpMock.expectOne('/api/v1/users/me');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    // The contract carries nothing but the two editable fields.
    expect(Object.keys(request.request.body).sort()).toEqual(['email', 'fullName']);
    request.flush({ ...me, ...update });

    expect(result?.fullName).toBe('Joanna Jo');
  });

  it('changes the password with POST /users/me/password', () => {
    const change: ChangePassword = { newPassword: 'N3wP@ss!' };
    let completed = false;
    service.changePassword(change).subscribe(() => (completed = true));

    const request = httpMock.expectOne('/api/v1/users/me/password');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(change);
    request.flush(null);

    expect(completed).toBeTrue();
  });

});

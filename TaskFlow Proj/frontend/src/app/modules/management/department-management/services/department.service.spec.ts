import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Department, DepartmentMembers, DepartmentWithCount, MemberRef } from '../models/department.model';
import { DepartmentService } from './department.service';

describe('DepartmentService', () => {
  let service: DepartmentService;
  let httpMock: HttpTestingController;

  const department: Department = { id: 1, name: 'Engineering', description: 'Builds the platform', createdAt: '2026-09-01' };

  const departmentWithCount: DepartmentWithCount = { ...department, managerCount: 2, employeeCount: 12 };

  const manager: MemberRef = {
    id: 5,
    fullName: 'Mary Manager',
    username: 'mary',
    email: 'mary@taskflow.com',
    isActive: true
  };

  const employee: MemberRef = {
    id: 7,
    fullName: 'Evan Employee',
    username: 'evan',
    email: 'evan@taskflow.com',
    isActive: false
  };

  const members: DepartmentMembers = { departmentId: 1, managers: [manager], employees: [employee] };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(DepartmentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads every department with its counts from the collection endpoint', () => {
    let received: DepartmentWithCount[] | undefined;
    service.getAll().subscribe((departments) => (received = departments));

    const request = httpMock.expectOne('/api/v1/departments');
    expect(request.request.method).toBe('GET');
    request.flush([departmentWithCount]);

    expect(received).toEqual([departmentWithCount]);
  });

  it('loads one department from the item endpoint', () => {
    let received: Department | undefined;
    service.getById(1).subscribe((result) => (received = result));

    const request = httpMock.expectOne('/api/v1/departments/1');
    expect(request.request.method).toBe('GET');
    request.flush(department);

    expect(received).toEqual(department);
  });

  it('creates a department with the name and description', () => {
    let received: Department | undefined;
    service.create({ name: 'Engineering', description: null }).subscribe((result) => (received = result));

    const request = httpMock.expectOne('/api/v1/departments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Engineering', description: null });
    request.flush(department);

    expect(received).toEqual(department);
  });

  it('updates a department by id', () => {
    let received: Department | undefined;
    service.update(1, { name: 'Platform', description: 'Still builds' }).subscribe((result) => (received = result));

    const request = httpMock.expectOne('/api/v1/departments/1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Platform', description: 'Still builds' });
    request.flush({ ...department, name: 'Platform' });

    expect(received).toEqual({ ...department, name: 'Platform' });
  });

  it('deletes a department by id', () => {
    let completed = false;
    service.remove(1).subscribe(() => (completed = true));

    const request = httpMock.expectOne('/api/v1/departments/1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBeTrue();
  });

  it('loads the managers and employees of a department', () => {
    let received: DepartmentMembers | undefined;
    service.getMembers(1).subscribe((result) => (received = result));

    const request = httpMock.expectOne('/api/v1/departments/1/members');
    expect(request.request.method).toBe('GET');
    request.flush(members);

    expect(received).toEqual(members);
  });

  it('replaces the members with a PUT of the full id sets', () => {
    let received: DepartmentMembers | undefined;
    service.replaceMembers(1, { managerIds: [5], employeeIds: [7] }).subscribe((result) => (received = result));

    const request = httpMock.expectOne('/api/v1/departments/1/members');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ managerIds: [5], employeeIds: [7] });
    request.flush(members);

    expect(received).toEqual(members);
  });

  it('propagates API errors', () => {
    let failed = false;
    service.getAll().subscribe({ error: () => (failed = true) });

    httpMock.expectOne('/api/v1/departments').flush({}, { status: 500, statusText: 'Server Error' });

    expect(failed).toBeTrue();
  });
});

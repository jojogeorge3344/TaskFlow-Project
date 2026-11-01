import { DEFAULT_RETURN_URL, backTarget, safeReturnUrl } from './return-url';

describe('return-url', () => {
  it('accepts in-app paths, with query strings', () => {
    expect(safeReturnUrl('/projects/7')).toBe('/projects/7');
    expect(safeReturnUrl('/tasks?status=ToDo')).toBe('/tasks?status=ToDo');
  });

  it('rejects anything that could leave the app', () => {
    for (const bad of ['//evil.com', 'http://evil.com', 'https://evil.com/x', 'javascript:alert(1)', '/\\evil.com', 'projects/7', '', null, undefined]) {
      expect(safeReturnUrl(bad)).withContext(String(bad)).toBeNull();
    }
  });

  it('labels the destination', () => {
    expect(backTarget('/projects/7')).toEqual({ url: '/projects/7', label: 'Back to project' });
    expect(backTarget('/board')).toEqual({ url: '/board', label: 'Back to board' });
    expect(backTarget('/tasks')).toEqual({ url: '/tasks', label: 'Back to tasks' });
  });

  it('falls back to the task list', () => {
    expect(backTarget(null)).toEqual({ url: DEFAULT_RETURN_URL, label: 'Back to tasks' });
    expect(backTarget('//evil.com')).toEqual({ url: DEFAULT_RETURN_URL, label: 'Back to tasks' });
  });
});

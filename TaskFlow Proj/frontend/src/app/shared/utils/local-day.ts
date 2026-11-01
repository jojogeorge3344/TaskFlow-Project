/** UTC instant of local midnight at the start of a `yyyy-MM-dd` day. */
export function startOfLocalDay(day: string): string {
  return new Date(`${day}T00:00`).toISOString();
}

/** UTC instant of the last millisecond of a local `yyyy-MM-dd` day. */
export function endOfLocalDay(day: string): string {
  const next = new Date(`${day}T00:00`);
  next.setDate(next.getDate() + 1);
  return new Date(next.getTime() - 1).toISOString();
}

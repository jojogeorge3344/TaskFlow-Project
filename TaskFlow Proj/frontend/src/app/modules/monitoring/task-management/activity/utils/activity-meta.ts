/** A readable name, colour tone and icon for each kind of task activity event. */
export interface ActivityEventMeta {
  label: string;
  tone: string;
  icon: string;
}

const KNOWN: Record<string, ActivityEventMeta> = {
  Created: { label: 'Created', tone: 'green', icon: 'plus' },
  Assigned: { label: 'Assigned', tone: 'blue', icon: 'user' },
  Reassigned: { label: 'Reassigned', tone: 'violet', icon: 'users' },
  StatusChanged: { label: 'Status changed', tone: 'amber', icon: 'flag' },
  DeadlineChanged: { label: 'Deadline changed', tone: 'amber', icon: 'clock' },
  PriorityChanged: { label: 'Priority changed', tone: 'amber', icon: 'flag' },
  Completed: { label: 'Completed', tone: 'green', icon: 'check' },
  CommentAdded: { label: 'Comment added', tone: 'blue', icon: 'reply' },
  CommentEdited: { label: 'Comment edited', tone: 'blue', icon: 'edit' },
  CommentDeleted: { label: 'Comment deleted', tone: 'red', icon: 'trash' },
  HoursLogged: { label: 'Hours logged', tone: 'green', icon: 'clock' },
  TimeLogged: { label: 'Time logged', tone: 'green', icon: 'clock' },
  Administrative: { label: 'Administrative', tone: 'gray', icon: 'settings' },
  Deleted: { label: 'Deleted', tone: 'red', icon: 'trash' }
};

export function eventMeta(type: string): ActivityEventMeta {
  return KNOWN[type] ?? { label: type, tone: 'gray', icon: 'chart' };
}

import { WorkstationStatus, LiveWorkstationEvent, TaskItem, TimesheetSummary, User } from './types';

const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

export async function fetchStations(): Promise<WorkstationStatus[]> {
  try {
    const res = await fetch(`${API_BASE}/api/v1/telemetry/stations`, { cache: 'no-store' });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchStations error:', err);
    return [];
  }
}

export async function fetchRecentEvents(limit = 40): Promise<LiveWorkstationEvent[]> {
  try {
    const res = await fetch(`${API_BASE}/api/v1/telemetry/events?limit=${limit}`, { cache: 'no-store' });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchRecentEvents error:', err);
    return [];
  }
}

export async function fetchTasks(status?: string, assignedTo?: string, priority?: string): Promise<TaskItem[]> {
  try {
    const params = new URLSearchParams();
    if (status) params.append('status', status);
    if (assignedTo) params.append('assignedTo', assignedTo);
    if (priority) params.append('priority', priority);

    const url = `${API_BASE}/api/v1/tasks${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await fetch(url, { cache: 'no-store' });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchTasks error:', err);
    return [];
  }
}

export async function fetchUsers(): Promise<User[]> {
  try {
    const res = await fetch(`${API_BASE}/api/v1/users`, { cache: 'no-store' });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchUsers error:', err);
    return [];
  }
}

export async function createTask(payload: {
  title: string;
  description?: string;
  assignedTo?: string;
  priority: string;
  dueDate?: string;
  deliverableRequired: boolean;
}): Promise<TaskItem | null> {
  try {
    const res = await fetch(`${API_BASE}/api/v1/tasks`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!res.ok) return null;
    return await res.json();
  } catch (err) {
    console.error('createTask error:', err);
    return null;
  }
}

export async function updateTaskStatus(
  taskId: string,
  status: string,
  deliverableUrl?: string,
  deliverableFileName?: string
): Promise<{ success: boolean; data?: TaskItem; error?: string }> {
  try {
    const res = await fetch(`${API_BASE}/api/v1/tasks/${taskId}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status, deliverableUrl, deliverableFileName }),
    });

    const data = await res.json();
    if (!res.ok) {
      return { success: false, error: data.message || 'Failed to update task status.' };
    }
    return { success: true, data };
  } catch (err: any) {
    return { success: false, error: err.message };
  }
}

export async function fetchTimesheetSummary(date?: string): Promise<TimesheetSummary | null> {
  try {
    const url = `${API_BASE}/api/v1/timesheet/summary${date ? `?date=${date}` : ''}`;
    const res = await fetch(url, { cache: 'no-store' });
    if (!res.ok) return null;
    return await res.json();
  } catch (err) {
    console.error('fetchTimesheetSummary error:', err);
    return null;
  }
}

export function getTimesheetCsvDownloadUrl(startDate?: string, endDate?: string): string {
  const params = new URLSearchParams();
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);
  return `${API_BASE}/api/v1/timesheet/export/csv${params.toString() ? `?${params.toString()}` : ''}`;
}

import { WorkstationStatus, LiveWorkstationEvent, TaskItem, TimesheetSummary, User } from './types';

const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5020';

let cachedManagerToken: string | null = null;

export async function getAuthHeader(): Promise<Record<string, string>> {
  if (!cachedManagerToken) {
    try {
      const res = await fetch(`${API_BASE}/api/v1/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: 'manager@punnyaheart.org', password: 'Password@123' }),
      });
      if (res.ok) {
        const data = await res.json();
        cachedManagerToken = data.token;
      }
    } catch (err) {
      console.error('Auto-login manager error:', err);
    }
  }

  return cachedManagerToken ? { Authorization: `Bearer ${cachedManagerToken}` } : {};
}

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
}): Promise<{ success: boolean; data?: TaskItem; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/tasks`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify(payload),
    });

    if (!res.ok) {
      const errBody = await res.text();
      return { success: false, error: `API Error ${res.status}: ${errBody || res.statusText}` };
    }

    const data = await res.json();
    return { success: true, data };
  } catch (err: any) {
    console.error('createTask error:', err);
    return { success: false, error: err.message || 'Network error connecting to API' };
  }
}

export async function updateTaskStatus(
  taskId: string,
  status: string,
  deliverableUrl?: string,
  deliverableFileName?: string
): Promise<{ success: boolean; data?: TaskItem; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/tasks/${taskId}/status`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
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

import { WorkstationStatus, LiveWorkstationEvent, TaskItem, TimesheetSummary, User, LeaveRequestItem, StaffMember, CreateStaffPayload, UpdateStaffPayload } from './types';

export const API_BASE = (process.env.NEXT_PUBLIC_API_URL && process.env.NEXT_PUBLIC_API_URL.trim() !== '')
  ? process.env.NEXT_PUBLIC_API_URL.trim()
  : 'https://punnya-api.onrender.com';


const AUTH_TOKEN_KEY = 'phf_admin_token';
const AUTH_USER_KEY = 'phf_admin_user';

export function getStoredAuthToken(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem(AUTH_TOKEN_KEY);
}

export function getStoredUser(): User | null {
  if (typeof window === 'undefined') return null;
  const raw = localStorage.getItem(AUTH_USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

export function setStoredAuth(token: string, user: User) {
  if (typeof window === 'undefined') return;
  localStorage.setItem(AUTH_TOKEN_KEY, token);
  localStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

export function clearStoredAuth() {
  if (typeof window === 'undefined') return;
  localStorage.removeItem(AUTH_TOKEN_KEY);
  localStorage.removeItem(AUTH_USER_KEY);
}

export async function login(
  identifier: string,
  password: string
): Promise<{ success: boolean; user?: User; token?: string; error?: string }> {
  try {
    const res = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: identifier.trim(), password: password.trim() }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => null);
      return { success: false, error: err?.message || 'Invalid username/email or password.' };
    }

    const data = await res.json();
    setStoredAuth(data.token, data.user);
    return { success: true, user: data.user, token: data.token };
  } catch (err: any) {
    return { success: false, error: err.message || 'Unable to reach server. Please check your network connection.' };
  }
}

export function logout() {
  clearStoredAuth();
}

export async function getAuthHeader(): Promise<Record<string, string>> {
  const token = getStoredAuthToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}


export async function fetchStations(): Promise<WorkstationStatus[]> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/telemetry/stations`, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchStations error:', err);
    return [];
  }
}

export async function fetchRecentEvents(limit = 40): Promise<LiveWorkstationEvent[]> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/telemetry/events?limit=${limit}`, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchRecentEvents error:', err);
    return [];
  }
}

export async function fetchTasks(status?: string, assignedTo?: string, priority?: string): Promise<TaskItem[]> {
  try {
    const authHeaders = await getAuthHeader();
    const params = new URLSearchParams();
    if (status) params.append('status', status);
    if (assignedTo) params.append('assignedTo', assignedTo);
    if (priority) params.append('priority', priority);

    const url = `${API_BASE}/api/v1/tasks${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await fetch(url, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchTasks error:', err);
    return [];
  }
}

export async function fetchUsers(): Promise<User[]> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/users`, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
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

export async function fetchTimesheetSummary(
  period?: string,
  startDate?: string,
  endDate?: string
): Promise<TimesheetSummary | null> {
  try {
    const authHeaders = await getAuthHeader();
    const params = new URLSearchParams();
    if (period) params.append('period', period);
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);

    const url = `${API_BASE}/api/v1/timesheet/summary${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await fetch(url, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
    if (!res.ok) return null;
    return await res.json();
  } catch (err) {
    console.error('fetchTimesheetSummary error:', err);
    return null;
  }
}

export function getTimesheetCsvDownloadUrl(period?: string, startDate?: string, endDate?: string, viewType?: string): string {
  const params = new URLSearchParams();
  if (period) params.append('period', period);
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);
  if (viewType) params.append('viewType', viewType);
  return `${API_BASE}/api/v1/timesheet/export/csv${params.toString() ? `?${params.toString()}` : ''}`;
}

export async function downloadTimesheetCsv(
  period?: string,
  startDate?: string, 
  endDate?: string,
  viewType?: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const params = new URLSearchParams();
    if (period) params.append('period', period);
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (viewType) params.append('viewType', viewType);
    const url = `${API_BASE}/api/v1/timesheet/export/csv${params.toString() ? `?${params.toString()}` : ''}`;
    
    const res = await fetch(url, {
      headers: { ...authHeaders }
    });

    if (!res.ok) {
      return { success: false, error: `Failed to download CSV: HTTP ${res.status}` };
    }

    const blob = await res.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    const periodLabel = period || (startDate && endDate ? `${startDate}_to_${endDate}` : (startDate || 'latest'));
    link.download = `timesheet_${periodLabel}_${viewType || 'daily'}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Download failed' };
  }
}

export async function fetchLeaves(status?: string): Promise<LeaveRequestItem[]> {
  try {
    const authHeaders = await getAuthHeader();
    const url = status && status !== 'ALL'
      ? `${API_BASE}/api/v1/leaves?status=${status}`
      : `${API_BASE}/api/v1/leaves`;
    const res = await fetch(url, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchLeaves error:', err);
    return [];
  }
}

export async function reviewLeave(
  leaveId: string,
  action: 'approve' | 'reject',
  comments?: string
): Promise<{ success: boolean; data?: LeaveRequestItem; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/leaves/${leaveId}/${action}`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify({ comments: comments || undefined }),
    });

    if (!res.ok) {
      const err = await res.text();
      return { success: false, error: `API Error ${res.status}: ${err}` };
    }

    const data = await res.json();
    return { success: true, data };
  } catch (err: any) {
    return { success: false, error: err.message || 'Failed to review leave' };
  }
}

export async function fetchStaff(): Promise<StaffMember[]> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/staff`, { 
      cache: 'no-store',
      headers: { ...authHeaders }
    });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchStaff error:', err);
    return [];
  }
}

export async function createStaff(
  payload: CreateStaffPayload
): Promise<{ success: boolean; data?: StaffMember; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/staff`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify(payload),
    });

    if (!res.ok) {
      const errBody = await res.json().catch(() => null);
      return { success: false, error: errBody?.message || `Error ${res.status}: ${res.statusText}` };
    }

    const data = await res.json();
    return { success: true, data };
  } catch (err: any) {
    return { success: false, error: err.message || 'Failed to create staff member' };
  }
}

export async function toggleStaffStatus(
  id: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/staff/${id}/toggle-status`, {
      method: 'PATCH',
      headers: { ...authHeaders },
    });
    if (!res.ok) {
      return { success: false, error: `Error ${res.status}: ${res.statusText}` };
    }
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Failed to toggle status' };
  }
}

export async function resetStaffPassword(
  id: string,
  newPassword: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/staff/${id}/reset-password`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify({ newPassword }),
    });

    if (!res.ok) {
      const errBody = await res.json().catch(() => null);
      return { success: false, error: errBody?.message || `Error ${res.status}` };
    }
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Failed to reset password' };
  }
}

export async function updateStaffWorkMode(
  id: string,
  workMode: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/staff/${id}/work-mode`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify({ workMode }),
    });
    if (!res.ok) {
      return { success: false, error: `Error ${res.status}` };
    }
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Failed to update work mode' };
  }
}

export async function updateStaff(
  id: string,
  payload: UpdateStaffPayload
): Promise<{ success: boolean; data?: StaffMember; error?: string }> {
  try {
    const authHeaders = await getAuthHeader();
    const res = await fetch(`${API_BASE}/api/v1/staff/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify(payload),
    });

    if (!res.ok) {
      const errBody = await res.json().catch(() => null);
      return { success: false, error: errBody?.message || `Error ${res.status}: ${res.statusText}` };
    }

    const data = await res.json();
    return { success: true, data };
  } catch (err: any) {
    return { success: false, error: err.message || 'Failed to update staff member' };
  }
}


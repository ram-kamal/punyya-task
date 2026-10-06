import { WorkstationStatus, LiveWorkstationEvent, TaskItem, TimesheetSummary, User, LeaveRequestItem, StaffMember, CreateStaffPayload, UpdateStaffPayload, TaskBackupDto, BulkDeleteTasksResponse, DeleteAllTasksResponse, RestoreBackupResponse, MonthEndAttendanceReport, StaffPerformanceReport, LeaveReport } from './types';

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

type AuthExpiredHandler = () => void;
let authExpiredHandler: AuthExpiredHandler | null = null;

export function onAuthExpired(handler: AuthExpiredHandler) {
  authExpiredHandler = handler;
}

export function notifyAuthExpired() {
  clearStoredAuth();
  if (authExpiredHandler) {
    authExpiredHandler();
  }
}

export function handleAuthStatus(status: number) {
  if (status === 401) {
    notifyAuthExpired();
  }
}

export async function verifyCurrentSession(): Promise<{ valid: boolean; user?: User }> {
  try {
    const authHeaders = await getAuthHeader();
    if (!authHeaders.Authorization) return { valid: false };

    const res = await fetch(`${API_BASE}/api/v1/auth/me`, {
      cache: 'no-store',
      headers: { ...authHeaders }
    });

    if (res.ok) {
      const user = await res.json();
      return { valid: true, user };
    } else {
      if (res.status === 401) {
        notifyAuthExpired();
      }
      return { valid: false };
    }
  } catch (err) {
    console.error('verifyCurrentSession error:', err);
    return { valid: false };
  }
}

export async function getAuthHeader(): Promise<Record<string, string>> {
  const token = getStoredAuthToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function authFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const authHeaders = await getAuthHeader();
  const headers = {
    ...authHeaders,
    ...(options.headers || {}),
  };
  const res = await fetch(url, {
    ...options,
    headers,
  });
  handleAuthStatus(res.status);
  return res;
}


export async function fetchStations(): Promise<WorkstationStatus[]> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/telemetry/stations`, { cache: 'no-store' });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchStations error:', err);
    return [];
  }
}

export async function fetchRecentEvents(limit = 40): Promise<LiveWorkstationEvent[]> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/telemetry/events?limit=${limit}`, { cache: 'no-store' });
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
    const res = await authFetch(url, { cache: 'no-store' });
    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchTasks error:', err);
    return [];
  }
}

export async function fetchUsers(): Promise<User[]> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/users`, { cache: 'no-store' });
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
    const res = await authFetch(`${API_BASE}/api/v1/tasks`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
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
    const res = await authFetch(`${API_BASE}/api/v1/tasks/${taskId}/status`, {
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

export async function attachDeliverableLink(
  taskId: string,
  url: string,
  fileName?: string
): Promise<{ success: boolean; data?: any; error?: string }> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/${taskId}/attachments/link`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url, fileName }),
    });

    const data = await res.json();
    if (!res.ok) {
      return { success: false, error: data.message || 'Failed to attach link.' };
    }
    return { success: true, data };
  } catch (err: any) {
    return { success: false, error: err.message };
  }
}

export async function updateTask(
  taskId: string,
  payload: {
    title?: string;
    description?: string;
    assignedTo?: string;
    priority?: string;
    dueDate?: string;
    deliverableRequired?: boolean;
    status?: string;
  }
): Promise<{ success: boolean; data?: TaskItem; error?: string }> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/${taskId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!res.ok) {
      const err = await res.text();
      return { success: false, error: err || 'Failed to update task.' };
    }
    const data = await res.json();
    return { success: true, data };
  } catch (err: any) {
    return { success: false, error: err.message };
  }
}

export async function deleteTask(taskId: string): Promise<boolean> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/${taskId}`, {
      method: 'DELETE',
    });
    return res.ok;
  } catch (err) {
    console.error('deleteTask error:', err);
    return false;
  }
}

export async function fetchTimesheetSummary(
  period?: string,
  startDate?: string,
  endDate?: string
): Promise<TimesheetSummary | null> {
  try {
    const params = new URLSearchParams();
    if (period) params.append('period', period);
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);

    const url = `${API_BASE}/api/v1/timesheet/summary${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url, { cache: 'no-store' });
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
    
    const res = await authFetch(url);

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
    const url = status && status !== 'ALL'
      ? `${API_BASE}/api/v1/leaves?status=${status}`
      : `${API_BASE}/api/v1/leaves`;
    const res = await authFetch(url, { cache: 'no-store' });
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
    const res = await authFetch(`${API_BASE}/api/v1/leaves/${leaveId}/${action}`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
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
    const res = await authFetch(`${API_BASE}/api/v1/staff`, { cache: 'no-store' });
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
    const res = await authFetch(`${API_BASE}/api/v1/staff`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
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
    const res = await authFetch(`${API_BASE}/api/v1/staff/${id}/toggle-status`, {
      method: 'PATCH',
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
    const res = await authFetch(`${API_BASE}/api/v1/staff/${id}/reset-password`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
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
    const res = await authFetch(`${API_BASE}/api/v1/staff/${id}/work-mode`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
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
    const res = await authFetch(`${API_BASE}/api/v1/staff/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
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

export async function bulkDeleteTasks(taskIds: string[]): Promise<{ success: boolean; deletedCount?: number; message?: string; error?: string }> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/bulk-delete`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ taskIds }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => null);
      return { success: false, error: err?.message || `Failed to delete selected tasks (HTTP ${res.status})` };
    }

    const data: BulkDeleteTasksResponse = await res.json();
    return { success: true, deletedCount: data.deletedCount, message: data.message };
  } catch (err: any) {
    return { success: false, error: err.message || 'Network error deleting tasks' };
  }
}

export async function deleteAllTasks(): Promise<{ success: boolean; deletedCount?: number; backupId?: string; expiresAt?: string; message?: string; error?: string }> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/delete-all`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (!res.ok) {
      const err = await res.json().catch(() => null);
      return { success: false, error: err?.message || `Failed to delete all tasks (HTTP ${res.status})` };
    }

    const data: DeleteAllTasksResponse = await res.json();
    return { success: true, deletedCount: data.deletedCount, backupId: data.backupId, expiresAt: data.expiresAt, message: data.message };
  } catch (err: any) {
    return { success: false, error: err.message || 'Network error deleting all tasks' };
  }
}

export async function fetchTaskBackups(): Promise<TaskBackupDto[]> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/backups`, {
      cache: 'no-store',
    });

    if (!res.ok) return [];
    return await res.json();
  } catch (err) {
    console.error('fetchTaskBackups error:', err);
    return [];
  }
}

export async function restoreTaskBackup(backupId: string): Promise<{ success: boolean; restoredCount?: number; message?: string; error?: string }> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/backups/${backupId}/restore`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (!res.ok) {
      const err = await res.json().catch(() => null);
      return { success: false, error: err?.message || `Failed to restore backup (HTTP ${res.status})` };
    }

    const data: RestoreBackupResponse = await res.json();
    return { success: true, restoredCount: data.restoredCount, message: data.message };
  } catch (err: any) {
    return { success: false, error: err.message || 'Network error restoring backup' };
  }
}

export async function dismissBackupReminder(backupId: string, milestoneDay: number): Promise<{ success: boolean; error?: string }> {
  try {
    const res = await authFetch(`${API_BASE}/api/v1/tasks/backups/${backupId}/dismiss-reminder`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ milestoneDay }),
    });

    return { success: res.ok };
  } catch (err: any) {
    return { success: false, error: err.message };
  }
}

// ==========================================
// Enterprise Executive Reports Endpoints
// ==========================================

export async function fetchAttendanceReport(
  year?: number,
  month?: number,
  employeeId?: string
): Promise<MonthEndAttendanceReport | null> {
  try {
    const params = new URLSearchParams();
    if (year) params.append('year', year.toString());
    if (month) params.append('month', month.toString());
    if (employeeId) params.append('employeeId', employeeId);

    const url = `${API_BASE}/api/v1/reports/attendance${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url, { cache: 'no-store' });
    if (!res.ok) return null;
    return await res.json();
  } catch (err) {
    console.error('fetchAttendanceReport error:', err);
    return null;
  }
}

export async function downloadAttendanceReportCsv(
  year?: number,
  month?: number,
  employeeId?: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const params = new URLSearchParams();
    if (year) params.append('year', year.toString());
    if (month) params.append('month', month.toString());
    if (employeeId) params.append('employeeId', employeeId);

    const url = `${API_BASE}/api/v1/reports/attendance/csv${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url);

    if (!res.ok) {
      return { success: false, error: `Failed to download Attendance CSV: HTTP ${res.status}` };
    }

    const blob = await res.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = `attendance_report_${year || new Date().getFullYear()}_${month || (new Date().getMonth() + 1)}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Download failed' };
  }
}

export async function fetchPerformanceReport(
  startDate?: string,
  endDate?: string,
  period?: string,
  employeeId?: string
): Promise<StaffPerformanceReport | null> {
  try {
    const params = new URLSearchParams();
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (period) params.append('period', period);
    if (employeeId) params.append('employeeId', employeeId);

    const url = `${API_BASE}/api/v1/reports/performance${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url, { cache: 'no-store' });
    if (!res.ok) return null;
    return await res.json();
  } catch (err) {
    console.error('fetchPerformanceReport error:', err);
    return null;
  }
}

export async function downloadPerformanceReportCsv(
  startDate?: string,
  endDate?: string,
  period?: string,
  employeeId?: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const params = new URLSearchParams();
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (period) params.append('period', period);
    if (employeeId) params.append('employeeId', employeeId);

    const url = `${API_BASE}/api/v1/reports/performance/csv${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url);

    if (!res.ok) {
      return { success: false, error: `Failed to download Performance CSV: HTTP ${res.status}` };
    }

    const blob = await res.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = `staff_performance_report_${period || 'period'}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Download failed' };
  }
}

export async function fetchLeaveReport(
  startDate?: string,
  endDate?: string,
  period?: string,
  employeeId?: string
): Promise<LeaveReport | null> {
  try {
    const params = new URLSearchParams();
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (period) params.append('period', period);
    if (employeeId) params.append('employeeId', employeeId);

    const url = `${API_BASE}/api/v1/reports/leaves${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url, { cache: 'no-store' });
    if (!res.ok) return null;
    return await res.json();
  } catch (err) {
    console.error('fetchLeaveReport error:', err);
    return null;
  }
}

export async function downloadLeaveReportCsv(
  startDate?: string,
  endDate?: string,
  period?: string,
  viewType?: string,
  employeeId?: string
): Promise<{ success: boolean; error?: string }> {
  try {
    const params = new URLSearchParams();
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (period) params.append('period', period);
    if (viewType) params.append('viewType', viewType);
    if (employeeId) params.append('employeeId', employeeId);

    const url = `${API_BASE}/api/v1/reports/leaves/csv${params.toString() ? `?${params.toString()}` : ''}`;
    const res = await authFetch(url);

    if (!res.ok) {
      return { success: false, error: `Failed to download Leaves CSV: HTTP ${res.status}` };
    }

    const blob = await res.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = `leave_report_${period || 'period'}_${viewType || 'summary'}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
    return { success: true };
  } catch (err: any) {
    return { success: false, error: err.message || 'Download failed' };
  }
}



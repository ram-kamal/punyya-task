export interface WorkstationStatus {
  stationId: string;
  userId?: string;
  fullName?: string;
  currentTaskCode?: string;
  currentTaskTitle?: string;
  focusedApp?: string;
  windowTitle?: string;
  state: 'ACTIVE' | 'IDLE' | 'OFFLINE';
  lastSeen: string;
  activeMinutesToday: number;
  idleMinutesToday: number;
}

export interface LiveWorkstationEvent {
  id: string;
  timestamp: string;
  stationId: string;
  userName?: string;
  eventType: string;
  details: string;
  processName?: string;
  taskCode?: string;
}

export interface TaskAttachment {
  id: string;
  fileName: string;
  storagePath: string;
  fileSizeBytes: number;
  mimeType?: string;
  uploadedAt: string;
}

export interface TaskItem {
  id: string;
  taskCode: string;
  title: string;
  description?: string;
  assignedTo?: string;
  assignedToName?: string;
  assignedToStation?: string;
  assignedBy?: string;
  assignedByName?: string;
  status: 'ASSIGNED' | 'IN_PROGRESS' | 'UNDER_REVIEW' | 'COMPLETED';
  priority: 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
  deliverableRequired: boolean;
  deliverableUrl?: string;
  deliverableFileName?: string;
  dueDate?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
  totalTrackedSeconds: number;
  attachments: TaskAttachment[];
}

export interface TimesheetSummary {
  period?: string;
  startDate?: string;
  endDate?: string;
  activeStationsCount: number;
  totalStationsCount: number;
  tasksInProgressCount: number;
  averageEfficiencyPercentage: number;
  totalHoursLoggedToday: number;
  rows: TimesheetRow[];
  dailyBreakdown?: TimesheetRow[];
}

export interface TimesheetRow {
  date: string;
  stationId: string;
  employeeName: string;
  email: string;
  clockIn: string;
  clockOut: string;
  totalHours: number;
  activeHours: number;
  idleHours: number;
  totalTimeFormatted?: string;
  activeTimeFormatted?: string;
  idleTimeFormatted?: string;
  efficiencyPercentage: number;
  tasksCompleted: number;
  daysWorked?: number;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: 'ADMIN' | 'MANAGER' | 'MONITOR' | 'EMPLOYEE';
  stationId?: string;
  username?: string;
}

export interface LeaveRequestItem {
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  stationId?: string;
  leaveType: 'CASUAL' | 'SICK' | 'MEDICAL' | 'ANNUAL' | 'EMERGENCY' | string;
  startDate: string;
  endDate: string;
  totalDays: number;
  reason: string;
  status: 'PENDING' | 'APPROVED' | 'REJECTED' | 'CANCELLED';
  reviewedBy?: string;
  reviewerName?: string;
  reviewedAt?: string;
  reviewComments?: string;
  createdAt: string;
}

export interface StaffMember {
  id: string;
  email: string;
  username?: string;
  fullName: string;
  role: 'ADMIN' | 'MANAGER' | 'MONITOR' | 'EMPLOYEE';
  workMode: 'IN_OFFICE' | 'REMOTE_WFH' | string;
  stationId?: string;
  isActive: boolean;
  createdAt: string;
  activeTasksCount: number;
  shiftStatus?: string;
}

export interface CreateStaffPayload {
  fullName: string;
  username?: string;
  email: string;
  role?: string;
  workMode?: string;
  stationId?: string;
  password?: string;
}

export interface UpdateStaffPayload {
  fullName: string;
  username?: string;
  email: string;
  role: string;
  workMode: string;
  stationId?: string;
  isActive: boolean;
  newPassword?: string;
}



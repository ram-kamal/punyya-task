export interface WorkstationStatus {
  stationId: string;
  userId?: string;
  email?: string;
  username?: string;
  role?: string;
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
  userEmail?: string;
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
  assignedToEmail?: string;
  assignedToStation?: string;
  assignedBy?: string;
  assignedByName?: string;
  assignedByEmail?: string;
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

export interface TaskBackupDto {
  id: string;
  createdAt: string;
  expiresAt: string;
  taskCount: number;
  deletedByName?: string;
  isRestored: boolean;
  restoredAt?: string;
  isExpired: boolean;
  remainingDays: number;
  activeReminderMilestone?: number;
}

export interface BulkDeleteTasksResponse {
  deletedCount: number;
  message: string;
}

export interface DeleteAllTasksResponse {
  deletedCount: number;
  backupId: string;
  expiresAt: string;
  message: string;
}

export interface RestoreBackupResponse {
  backupId: string;
  restoredCount: number;
  message: string;
}

// Enterprise Executive Reports DTOs
export interface AttendanceStaffSummary {
  userId: string;
  employeeName: string;
  email: string;
  stationId: string;
  workMode: string;
  totalWorkingDays: number;
  daysPresent: number;
  daysAbsent: number;
  approvedLeaveDays: number;
  attendanceRatePercentage: number;
  totalHours: number;
  totalTimeFormatted: string;
  activeHours: number;
  idleHours: number;
  averageDailyHours: number;
  efficiencyPercentage: number;
}

export interface AttendanceDailyLog {
  date: string;
  dayOfWeek: string;
  userId: string;
  employeeName: string;
  email: string;
  stationId: string;
  status: 'PRESENT' | 'LEAVE' | 'ABSENT' | 'WEEKEND' | string;
  clockIn: string;
  clockOut: string;
  hoursWorked: number;
  timeFormatted: string;
}

export interface MonthEndAttendanceReport {
  period: string;
  year: number;
  month: number;
  startDate: string;
  endDate: string;
  totalCalendarDays: number;
  totalWorkingDays: number;
  workingDaysElapsed: number;
  averageAttendanceRate: number;
  totalHoursLogged: number;
  staffSummaries: AttendanceStaffSummary[];
  dailyAttendanceLogs: AttendanceDailyLog[];
}

export interface StaffPerformanceRow {
  userId: string;
  employeeName: string;
  email: string;
  role: string;
  stationId: string;
  tasksAssigned: number;
  tasksCompleted: number;
  tasksInProgress: number;
  tasksPending: number;
  taskCompletionRate: number;
  totalHoursWorked: number;
  totalTimeFormatted: string;
  activeHours: number;
  idleHours: number;
  focusEfficiencyPercentage: number;
  deliverablesSubmitted: number;
  deliverablesRequired: number;
  deliverableComplianceRate: number;
  performanceRating: 'Outstanding' | 'High Performer' | 'Good Standing' | 'Needs Attention' | string;
}

export interface StaffPerformanceReport {
  period: string;
  startDate: string;
  endDate: string;
  totalTasksAssigned: number;
  totalTasksCompleted: number;
  teamCompletionRate: number;
  averageFocusScore: number;
  staffPerformance: StaffPerformanceRow[];
}

export interface LeaveStaffSummary {
  userId: string;
  employeeName: string;
  email: string;
  stationId: string;
  totalRequests: number;
  totalApprovedDays: number;
  casualLeaveDays: number;
  sickLeaveDays: number;
  medicalLeaveDays: number;
  annualLeaveDays: number;
  emergencyLeaveDays: number;
  pendingRequests: number;
  rejectedRequests: number;
}

export interface LeaveDetailedRow {
  id: string;
  userId: string;
  employeeName: string;
  email: string;
  leaveType: string;
  startDate: string;
  endDate: string;
  totalDays: number;
  reason: string;
  status: string;
  reviewerName?: string;
  reviewComments?: string;
  appliedAt: string;
  reviewedAt?: string;
}

export interface LeaveReport {
  period: string;
  startDate: string;
  endDate: string;
  totalApplications: number;
  totalApprovedDays: number;
  totalPendingRequests: number;
  totalRejectedRequests: number;
  staffSummaries: LeaveStaffSummary[];
  detailedRecords: LeaveDetailedRow[];
}



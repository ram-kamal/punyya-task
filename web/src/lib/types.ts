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
  activeStationsCount: number;
  totalStationsCount: number;
  tasksInProgressCount: number;
  averageEfficiencyPercentage: number;
  totalHoursLoggedToday: number;
  rows: TimesheetRow[];
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
  efficiencyPercentage: number;
  tasksCompleted: number;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: 'ADMIN' | 'MANAGER' | 'EMPLOYEE';
  stationId?: string;
}

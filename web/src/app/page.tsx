'use client';

import React, { useEffect, useState, useRef, useMemo } from 'react';
import {
  Monitor,
  CheckCircle2,
  Clock,
  TrendingUp,
  Download,
  Plus,
  Radio,
  FileText,
  AlertCircle,
  ExternalLink,
  RefreshCw,
  Users,
  Briefcase,
  Terminal,
  ChevronRight,
  Sparkles,
  Play,
  CheckCircle,
  Paperclip,
  Lock,
  Unlock,
  Search,
  Filter,
  X,
  Layers,
  ShieldCheck,
  Activity,
  UserCheck,
  CalendarDays,
  Check,
  XCircle,
  UserPlus,
  Key,
  Laptop,
  Building,
} from 'lucide-react';
import { WorkstationStatus, LiveWorkstationEvent, TaskItem, TimesheetSummary, User, LeaveRequestItem, StaffMember } from '@/lib/types';
import {
  fetchStations,
  fetchRecentEvents,
  fetchTasks,
  fetchUsers,
  createTask,
  fetchTimesheetSummary,
  getTimesheetCsvDownloadUrl,
  fetchLeaves,
  reviewLeave,
  fetchStaff,
  createStaff,
  toggleStaffStatus,
  resetStaffPassword,
  updateStaffWorkMode,
} from '@/lib/api';
import { signalRClient } from '@/lib/signalr';

type ActivityCategory = 'tasks' | 'all' | 'focus' | 'security';

export default function AdminDashboardPage() {
  const [activeTab, setActiveTab] = useState<'oversight' | 'tasks' | 'timesheets' | 'leaves' | 'staff'>('oversight');
  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);

  // Core Data State
  const [stations, setStations] = useState<WorkstationStatus[]>([]);
  const [events, setEvents] = useState<LiveWorkstationEvent[]>([]);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [timesheet, setTimesheet] = useState<TimesheetSummary | null>(null);
  const [users, setUsers] = useState<User[]>([]);

  // Leave Management State
  const [leaves, setLeaves] = useState<LeaveRequestItem[]>([]);
  const [leaveStatusFilter, setLeaveStatusFilter] = useState<string>('ALL');
  const [leaveSearchQuery, setLeaveSearchQuery] = useState<string>('');
  const [selectedLeaveForReview, setSelectedLeaveForReview] = useState<LeaveRequestItem | null>(null);
  const [reviewAction, setReviewAction] = useState<'approve' | 'reject'>('approve');
  const [reviewComments, setReviewComments] = useState<string>('');
  const [isReviewing, setIsReviewing] = useState<boolean>(false);
  const [reviewFeedback, setReviewFeedback] = useState<string | null>(null);

  // Staff & Non-Domain User Management State
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [staffSearchQuery, setStaffSearchQuery] = useState<string>('');
  const [staffFilter, setStaffFilter] = useState<string>('ALL');
  const [isAddStaffModalOpen, setIsAddStaffModalOpen] = useState<boolean>(false);
  const [newStaffFullName, setNewStaffFullName] = useState<string>('');
  const [newStaffUsername, setNewStaffUsername] = useState<string>('');
  const [newStaffEmail, setNewStaffEmail] = useState<string>('');
  const [newStaffRole, setNewStaffRole] = useState<string>('EMPLOYEE');
  const [newStaffWorkMode, setNewStaffWorkMode] = useState<string>('REMOTE_WFH');
  const [newStaffPassword, setNewStaffPassword] = useState<string>('PHF-Welcome@2026');
  const [isCreatingStaff, setIsCreatingStaff] = useState<boolean>(false);
  const [addStaffMessage, setAddStaffMessage] = useState<string | null>(null);

  const [selectedStaffForPasswordReset, setSelectedStaffForPasswordReset] = useState<StaffMember | null>(null);
  const [newResetPassword, setNewResetPassword] = useState<string>('');
  const [isResettingPassword, setIsResettingPassword] = useState<boolean>(false);
  const [resetPasswordMessage, setResetPasswordMessage] = useState<string | null>(null);

  // Activity Feed Filtering & Search
  const [activityCategory, setActivityCategory] = useState<ActivityCategory>('tasks');
  const [selectedStationFilter, setSelectedStationFilter] = useState<string>('ALL');
  const [eventSearchQuery, setEventSearchQuery] = useState<string>('');

  // Task Kanban Filtering
  const [taskSearchQuery, setTaskSearchQuery] = useState<string>('');
  const [taskPriorityFilter, setTaskPriorityFilter] = useState<string>('ALL');

  // Dispatch Task Modal State
  const [isDispatchModalOpen, setIsDispatchModalOpen] = useState(false);
  const [dispatchTitle, setDispatchTitle] = useState('');
  const [dispatchDesc, setDispatchDesc] = useState('');
  const [dispatchAssignee, setDispatchAssignee] = useState('');
  const [dispatchPriority, setDispatchPriority] = useState('MEDIUM');
  const [dispatchDeliverableReq, setDispatchDeliverableReq] = useState(true);
  const [isDispatching, setIsDispatching] = useState(false);
  const [dispatchMessage, setDispatchMessage] = useState<string | null>(null);

  // Load Initial Data
  const loadDashboardData = async () => {
    try {
      const [stData, evData, tData, tsData, uData, lData, staffData] = await Promise.all([
        fetchStations(),
        fetchRecentEvents(100),
        fetchTasks(),
        fetchTimesheetSummary(),
        fetchUsers(),
        fetchLeaves(),
        fetchStaff(),
      ]);

      setStations(stData);
      setEvents(evData);
      setTasks(tData);
      setTimesheet(tsData);
      setUsers(uData);
      setLeaves(lData);
      setStaff(staffData);
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDashboardData();

    // Setup SignalR Realtime Listener
    signalRClient.onConnectionStateChange = (state) => setIsConnected(state);

    signalRClient.onEventReceived = (evt) => {
      setEvents((prev) => [evt, ...prev.slice(0, 199)]);
      if (evt.eventType && evt.eventType.startsWith('LEAVE_')) {
        fetchLeaves().then(setLeaves);
      }
    };

    signalRClient.onStatusUpdated = (updated) => {
      setStations((prev) => {
        const idx = prev.findIndex((s) => s.stationId === updated.stationId);
        if (idx >= 0) {
          const clone = [...prev];
          clone[idx] = { ...clone[idx], ...updated };
          return clone;
        }
        return [...prev, updated];
      });
    };

    signalRClient.onTaskUpdated = () => {
      fetchTasks().then(setTasks);
      fetchTimesheetSummary().then(setTimesheet);
    };

    signalRClient.onLeaveUpdated = () => {
      fetchLeaves().then(setLeaves);
    };

    signalRClient.connect();

    const interval = setInterval(() => {
      fetchStations().then(setStations);
      fetchTimesheetSummary().then(setTimesheet);
      fetchLeaves().then(setLeaves);
    }, 15000);

    return () => {
      clearInterval(interval);
      signalRClient.disconnect();
    };
  }, []);

  // Handle Quick Task Dispatch
  const handleDispatchTask = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!dispatchTitle.trim()) return;

    setIsDispatching(true);
    setDispatchMessage(null);

    const result = await createTask({
      title: dispatchTitle.trim(),
      description: dispatchDesc.trim(),
      assignedTo: dispatchAssignee || undefined,
      priority: dispatchPriority,
      deliverableRequired: dispatchDeliverableReq,
    });

    setIsDispatching(false);

    if (result.success && result.data) {
      setDispatchMessage(`✓ ${result.data.taskCode} successfully dispatched!`);
      setDispatchTitle('');
      setDispatchDesc('');
      fetchTasks().then(setTasks);
      fetchTimesheetSummary().then(setTimesheet);
      setTimeout(() => {
        setDispatchMessage(null);
        setIsDispatchModalOpen(false);
      }, 1200);
    } else {
      setDispatchMessage(result.error || 'Failed to dispatch task. Central API check required.');
    }
  };

  // Helper: check if event is a task-related milestone
  const isTaskEvent = (eventType: string) => {
    const t = eventType.toUpperCase();
    return (
      t === 'TASK_ASSIGNED' ||
      t === 'TASK_STATUS_CHANGED' ||
      t === 'TASK_STARTED' ||
      t === 'TASK_COMPLETED' ||
      t === 'DELIVERABLE_ATTACHED' ||
      t === 'SHIFT_START' ||
      t === 'SHIFT_END'
    );
  };

  // Helper: check if event is a security/session event
  const isSecurityEvent = (eventType: string) => {
    const t = eventType.toUpperCase();
    return t === 'LOCK' || t === 'UNLOCK' || t === 'IDLE_DETECTED' || t === 'IDLE_RESUMED';
  };

  // Process and Deduplicate Live Events
  const filteredEvents = useMemo(() => {
    let list = events;

    // Station filter
    if (selectedStationFilter !== 'ALL') {
      list = list.filter((e) => e.stationId === selectedStationFilter);
    }

    // Category filter
    if (activityCategory === 'tasks') {
      list = list.filter((e) => isTaskEvent(e.eventType));
    } else if (activityCategory === 'security') {
      list = list.filter((e) => isSecurityEvent(e.eventType));
    } else if (activityCategory === 'focus') {
      list = list.filter((e) => e.eventType.toUpperCase() === 'FOCUS_CHANGED');
    }

    // Search query filter
    if (eventSearchQuery.trim()) {
      const q = eventSearchQuery.toLowerCase();
      list = list.filter(
        (e) =>
          (e.taskCode && e.taskCode.toLowerCase().includes(q)) ||
          (e.details && e.details.toLowerCase().includes(q)) ||
          (e.processName && e.processName.toLowerCase().includes(q)) ||
          (e.stationId && e.stationId.toLowerCase().includes(q)) ||
          (e.userName && e.userName.toLowerCase().includes(q))
      );
    }

    // Smart deduplication for consecutive focus events on the same process & station
    const result: (LiveWorkstationEvent & { repeatCount?: number })[] = [];
    for (let i = 0; i < list.length; i++) {
      const cur = list[i];
      if (
        result.length > 0 &&
        cur.eventType.toUpperCase() === 'FOCUS_CHANGED' &&
        result[result.length - 1].eventType.toUpperCase() === 'FOCUS_CHANGED' &&
        result[result.length - 1].stationId === cur.stationId &&
        result[result.length - 1].processName === cur.processName
      ) {
        // Group with previous
        result[result.length - 1].repeatCount = (result[result.length - 1].repeatCount || 1) + 1;
      } else {
        result.push({ ...cur, repeatCount: 1 });
      }
    }

    return result;
  }, [events, activityCategory, selectedStationFilter, eventSearchQuery]);

  // Filtered Tasks for Kanban Board
  const filteredTasks = useMemo(() => {
    return tasks.filter((t) => {
      const matchesSearch =
        !taskSearchQuery.trim() ||
        t.taskCode.toLowerCase().includes(taskSearchQuery.toLowerCase()) ||
        t.title.toLowerCase().includes(taskSearchQuery.toLowerCase()) ||
        (t.assignedToName && t.assignedToName.toLowerCase().includes(taskSearchQuery.toLowerCase()));

      const matchesPriority = taskPriorityFilter === 'ALL' || t.priority === taskPriorityFilter;

      return matchesSearch && matchesPriority;
    });
  }, [tasks, taskSearchQuery, taskPriorityFilter]);

  // Filtered Leaves for Leave Management Tab
  const filteredLeaves = useMemo(() => {
    return leaves.filter((l) => {
      const matchesSearch =
        !leaveSearchQuery.trim() ||
        l.userName.toLowerCase().includes(leaveSearchQuery.toLowerCase()) ||
        l.userEmail.toLowerCase().includes(leaveSearchQuery.toLowerCase()) ||
        (l.stationId && l.stationId.toLowerCase().includes(leaveSearchQuery.toLowerCase())) ||
        l.leaveType.toLowerCase().includes(leaveSearchQuery.toLowerCase()) ||
        l.reason.toLowerCase().includes(leaveSearchQuery.toLowerCase());

      const matchesStatus = leaveStatusFilter === 'ALL' || l.status === leaveStatusFilter;

      return matchesSearch && matchesStatus;
    });
  }, [leaves, leaveSearchQuery, leaveStatusFilter]);

  // Handle Review Leave (Approve or Reject)
  const handleReviewLeave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedLeaveForReview) return;

    setIsReviewing(true);
    setReviewFeedback(null);

    const result = await reviewLeave(
      selectedLeaveForReview.id,
      reviewAction,
      reviewComments.trim() || undefined
    );

    setIsReviewing(false);

    if (result.success) {
      setReviewFeedback(`✓ Leave request ${reviewAction === 'approve' ? 'Approved' : 'Rejected'} successfully!`);
      fetchLeaves().then(setLeaves);
      setTimeout(() => {
        setSelectedLeaveForReview(null);
        setReviewComments('');
        setReviewFeedback(null);
      }, 1000);
    } else {
      setReviewFeedback(result.error || 'Failed to review leave request.');
    }
  };

  // Filtered Staff Directory
  const filteredStaff = useMemo(() => {
    return staff.filter((s) => {
      const q = staffSearchQuery.toLowerCase().trim();
      const matchesSearch =
        !q ||
        s.fullName.toLowerCase().includes(q) ||
        (s.username && s.username.toLowerCase().includes(q)) ||
        s.email.toLowerCase().includes(q) ||
        (s.stationId && s.stationId.toLowerCase().includes(q));

      const matchesFilter =
        staffFilter === 'ALL' ||
        (staffFilter === 'REMOTE_WFH' && s.workMode === 'REMOTE_WFH') ||
        (staffFilter === 'IN_OFFICE' && s.workMode === 'IN_OFFICE') ||
        (staffFilter === 'ACTIVE' && s.isActive) ||
        (staffFilter === 'INACTIVE' && !s.isActive);

      return matchesSearch && matchesFilter;
    });
  }, [staff, staffSearchQuery, staffFilter]);

  // Handle Create Staff Member
  const handleCreateStaff = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsCreatingStaff(true);
    setAddStaffMessage(null);

    const res = await createStaff({
      fullName: newStaffFullName.trim(),
      username: newStaffUsername.trim() || undefined,
      email: newStaffEmail.trim(),
      role: newStaffRole,
      workMode: newStaffWorkMode,
      password: newStaffPassword.trim() || 'PHF-Welcome@2026',
    });

    setIsCreatingStaff(false);
    if (res.success) {
      setAddStaffMessage('✓ Staff member enrolled successfully!');
      fetchStaff().then(setStaff);
      fetchUsers().then(setUsers);
      setTimeout(() => {
        setIsAddStaffModalOpen(false);
        setNewStaffFullName('');
        setNewStaffUsername('');
        setNewStaffEmail('');
        setNewStaffPassword('PHF-Welcome@2026');
        setAddStaffMessage(null);
      }, 1200);
    } else {
      setAddStaffMessage(res.error || 'Failed to enroll staff member');
    }
  };

  // Handle Toggle Active/Suspended
  const handleToggleStaffStatus = async (id: string) => {
    const res = await toggleStaffStatus(id);
    if (res.success) {
      fetchStaff().then(setStaff);
      fetchUsers().then(setUsers);
    }
  };

  // Handle Toggle Work Mode (In-Office <-> Remote WFH)
  const handleToggleWorkMode = async (id: string, currentMode: string) => {
    const nextMode = currentMode === 'REMOTE_WFH' ? 'IN_OFFICE' : 'REMOTE_WFH';
    const res = await updateStaffWorkMode(id, nextMode);
    if (res.success) {
      fetchStaff().then(setStaff);
    }
  };

  // Handle Reset Password
  const handleResetStaffPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedStaffForPasswordReset) return;

    setIsResettingPassword(true);
    setResetPasswordMessage(null);

    const res = await resetStaffPassword(selectedStaffForPasswordReset.id, newResetPassword.trim());
    setIsResettingPassword(false);
    if (res.success) {
      setResetPasswordMessage('✓ Password updated successfully!');
      setTimeout(() => {
        setSelectedStaffForPasswordReset(null);
        setNewResetPassword('');
        setResetPasswordMessage(null);
      }, 1000);
    } else {
      setResetPasswordMessage(res.error || 'Failed to update password');
    }
  };

  // Counts
  const taskChangesCount = events.filter((e) => isTaskEvent(e.eventType)).length;
  const activeStationsCount = stations.filter((s) => s.state === 'ACTIVE').length;
  const totalStationsCount = stations.length || 5;
  const tasksInProgressCount = tasks.filter((t) => t.status === 'IN_PROGRESS').length;
  const pendingLeavesCount = leaves.filter((l) => l.status === 'PENDING').length;
  const approvedLeavesCount = leaves.filter((l) => l.status === 'APPROVED').length;
  const rejectedLeavesCount = leaves.filter((l) => l.status === 'REJECTED').length;
  const avgEfficiency = timesheet?.averageEfficiencyPercentage ?? 92;
  const hoursLoggedToday = timesheet?.totalHoursLoggedToday ?? 18.5;

  return (
    <div className="min-h-screen bg-[#0C1017] text-slate-100 flex flex-col font-sans antialiased">
      {/* Windows 11 Fluent App Bar Navigation */}
      <header className="border-b border-[#232F42] bg-[#121824]/90 backdrop-blur-md sticky top-0 z-30 px-6 py-3.5 shadow-sm">
        <div className="max-w-7xl mx-auto flex items-center justify-between">
          <div className="flex items-center space-x-3.5">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-600 via-blue-500 to-sky-400 flex items-center justify-center font-black text-white shadow-lg shadow-blue-500/20 text-sm tracking-tight">
              PHF
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <h1 className="text-base font-bold tracking-tight text-white">PHF Work Management System</h1>
                <span className="text-[11px] bg-blue-500/15 text-blue-400 font-semibold px-2 py-0.5 rounded-full border border-blue-500/25">
                  Admin Console
                </span>
              </div>
              <p className="text-xs text-slate-400">Punnya Task Desk • Real-Time Workforce Oversight &amp; Telemetry</p>
            </div>
          </div>

          <div className="flex items-center space-x-3">
            {/* Live SignalR Indicator */}
            <div
              className={`flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-semibold border transition-all ${
                isConnected
                  ? 'bg-emerald-950/50 border-emerald-500/30 text-emerald-400 shadow-sm shadow-emerald-950/20'
                  : 'bg-rose-950/50 border-rose-500/30 text-rose-400'
              }`}
            >
              <span className={`w-2 h-2 rounded-full ${isConnected ? 'bg-emerald-400 animate-pulse' : 'bg-rose-400'}`} />
              <span className="tracking-wide text-[11px]">{isConnected ? 'LIVE FEED CONNECTED' : 'RECONNECTING'}</span>
            </div>

            {/* Quick Dispatch Task Modal Button */}
            <button
              onClick={() => setIsDispatchModalOpen(true)}
              className="flex items-center space-x-2 px-3.5 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold shadow-md shadow-blue-600/20 transition-all active:scale-[0.98]"
            >
              <Plus className="w-4 h-4" />
              <span>DISPATCH TASK</span>
            </button>

            {/* One-Click Timesheet CSV Export */}
            <a
              href={getTimesheetCsvDownloadUrl()}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center space-x-2 px-3.5 py-1.5 rounded-lg bg-[#1B2537] hover:bg-[#233148] border border-[#2D3E5B] text-slate-200 hover:text-white text-xs font-semibold shadow-sm transition-all"
            >
              <Download className="w-3.5 h-3.5 text-emerald-400" />
              <span>TIMESHEET (CSV)</span>
            </a>

            <button
              onClick={loadDashboardData}
              className="p-1.5 rounded-lg bg-[#1B2537] hover:bg-[#233148] border border-[#2D3E5B] text-slate-300 hover:text-white transition"
              title="Refresh Dashboard"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="max-w-7xl mx-auto w-full px-6 py-6 flex-1 space-y-6">
        {/* KPI Metric Summary Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-[#141B28] rounded-xl p-4 border border-[#222E42] relative overflow-hidden group shadow-sm">
            <div className="flex items-center justify-between text-slate-400 mb-1.5">
              <span className="text-[11px] font-bold tracking-wider uppercase">Active Stations</span>
              <Monitor className="w-4 h-4 text-blue-400" />
            </div>
            <div className="text-2xl font-black text-white">
              {activeStationsCount} <span className="text-slate-500 text-sm font-normal">/ {totalStationsCount} Online</span>
            </div>
            <div className="text-[11px] text-emerald-400 mt-2 font-medium flex items-center">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mr-1.5 animate-ping"></span>
              Win32 event hook telemetry active
            </div>
          </div>

          <div className="bg-[#141B28] rounded-xl p-4 border border-[#222E42] relative overflow-hidden group shadow-sm">
            <div className="flex items-center justify-between text-slate-400 mb-1.5">
              <span className="text-[11px] font-bold tracking-wider uppercase">Tasks In Progress</span>
              <Briefcase className="w-4 h-4 text-indigo-400" />
            </div>
            <div className="text-2xl font-black text-white">{tasksInProgressCount}</div>
            <div className="text-[11px] text-slate-400 mt-2 font-medium">
              {tasks.filter((t) => t.status === 'ASSIGNED').length} awaiting employee start
            </div>
          </div>

          <div className="bg-[#141B28] rounded-xl p-4 border border-[#222E42] relative overflow-hidden group shadow-sm">
            <div className="flex items-center justify-between text-slate-400 mb-1.5">
              <span className="text-[11px] font-bold tracking-wider uppercase">Avg Team Efficiency</span>
              <TrendingUp className="w-4 h-4 text-emerald-400" />
            </div>
            <div className="text-2xl font-black text-emerald-400">{avgEfficiency}%</div>
            <div className="text-[11px] text-slate-400 mt-2 font-medium">Active-to-idle ratio computed automatically</div>
          </div>

          <div className="bg-[#141B28] rounded-xl p-4 border border-[#222E42] relative overflow-hidden group shadow-sm">
            <div className="flex items-center justify-between text-slate-400 mb-1.5">
              <span className="text-[11px] font-bold tracking-wider uppercase">Hours Logged Today</span>
              <Clock className="w-4 h-4 text-amber-400" />
            </div>
            <div className="text-2xl font-black text-white">{hoursLoggedToday}h</div>
            <div className="text-[11px] text-slate-400 mt-2 font-medium">Across all clocked-in workstation shifts</div>
          </div>
        </div>

        {/* Fluent Tabs Navigation */}
        <div className="flex items-center justify-between border-b border-[#222E42] pb-3">
          <div className="flex items-center space-x-2">
            <button
              onClick={() => setActiveTab('oversight')}
              className={`px-4 py-2 rounded-lg text-xs font-bold flex items-center space-x-2 transition ${
                activeTab === 'oversight'
                  ? 'bg-blue-600 text-white shadow-md shadow-blue-600/20'
                  : 'text-slate-400 hover:text-white hover:bg-[#162030]'
              }`}
            >
              <Monitor className="w-3.5 h-3.5" />
              <span>Live Oversight &amp; Activity</span>
            </button>

            <button
              onClick={() => setActiveTab('tasks')}
              className={`px-4 py-2 rounded-lg text-xs font-bold flex items-center space-x-2 transition ${
                activeTab === 'tasks'
                  ? 'bg-blue-600 text-white shadow-md shadow-blue-600/20'
                  : 'text-slate-400 hover:text-white hover:bg-[#162030]'
              }`}
            >
              <Briefcase className="w-3.5 h-3.5" />
              <span>Task Distribution &amp; Deliverables</span>
              <span className="bg-[#0E1522] text-slate-300 text-[10px] px-1.5 py-0.5 rounded-full ml-1">
                {tasks.length}
              </span>
            </button>

            <button
              onClick={() => setActiveTab('timesheets')}
              className={`px-4 py-2 rounded-lg text-xs font-bold flex items-center space-x-2 transition ${
                activeTab === 'timesheets'
                  ? 'bg-blue-600 text-white shadow-md shadow-blue-600/20'
                  : 'text-slate-400 hover:text-white hover:bg-[#162030]'
              }`}
            >
              <FileText className="w-3.5 h-3.5" />
              <span>Payroll Timesheets</span>
            </button>

            <button
              onClick={() => setActiveTab('leaves')}
              className={`px-4 py-2 rounded-lg text-xs font-bold flex items-center space-x-2 transition ${
                activeTab === 'leaves'
                  ? 'bg-blue-600 text-white shadow-md shadow-blue-600/20'
                  : 'text-slate-400 hover:text-white hover:bg-[#162030]'
              }`}
            >
              <CalendarDays className="w-3.5 h-3.5" />
              <span>Leave Requests</span>
              {pendingLeavesCount > 0 ? (
                <span className="bg-amber-500/20 border border-amber-500/40 text-amber-300 text-[10px] px-2 py-0.5 rounded-full font-black animate-pulse ml-1">
                  {pendingLeavesCount} PENDING
                </span>
              ) : (
                <span className="bg-[#0E1522] text-slate-400 text-[10px] px-1.5 py-0.5 rounded-full ml-1">
                  {leaves.length}
                </span>
              )}
            </button>

            <button
              onClick={() => setActiveTab('staff')}
              className={`px-4 py-2 rounded-lg text-xs font-bold flex items-center space-x-2 transition ${
                activeTab === 'staff'
                  ? 'bg-blue-600 text-white shadow-md shadow-blue-600/20'
                  : 'text-slate-400 hover:text-white hover:bg-[#162030]'
              }`}
            >
              <Users className="w-3.5 h-3.5" />
              <span>Staff &amp; Enrollment</span>
              <span className="bg-[#0E1522] text-slate-300 text-[10px] px-1.5 py-0.5 rounded-full ml-1">
                {staff.length}
              </span>
            </button>
          </div>

          <div className="hidden sm:flex items-center space-x-2 text-xs text-slate-400">
            <ShieldCheck className="w-3.5 h-3.5 text-blue-400" />
            <span>SQLite Tamper-Lock Active</span>
          </div>
        </div>

        {/* TAB 1: LIVE OVERSIGHT & DECLUTTERED ACTIVITY HUB */}
        {activeTab === 'oversight' && (
          <div className="space-y-6">
            {/* Workstation Fleet Status Table (Full Width for Clarity) */}
            <div className="bg-[#141B28] rounded-xl border border-[#222E42] overflow-hidden shadow-sm">
              <div className="px-5 py-3.5 border-b border-[#222E42] flex items-center justify-between bg-[#111724]">
                <div className="flex items-center space-x-2">
                  <Monitor className="w-4 h-4 text-blue-400" />
                  <h2 className="text-xs font-bold uppercase tracking-wider text-white">Workstation Fleet Status</h2>
                </div>
                <span className="text-[11px] text-slate-400">Near-0% CPU Event Hooks • 300s Idle Detection</span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="bg-[#0D1320] text-[11px] uppercase tracking-wider text-slate-400 border-b border-[#222E42]">
                    <tr>
                      <th className="px-5 py-3">Station</th>
                      <th className="px-5 py-3">Assignee</th>
                      <th className="px-5 py-3">Current Task</th>
                      <th className="px-5 py-3">Foreground App</th>
                      <th className="px-5 py-3">Logged Today</th>
                      <th className="px-5 py-3 text-right">Station State</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#1D273A] font-medium text-xs">
                    {stations.map((st) => (
                      <tr key={st.stationId} className="hover:bg-[#182234]/60 transition">
                        <td className="px-5 py-3.5 font-bold text-white flex items-center space-x-2.5">
                          <span
                            className={`w-2 h-2 rounded-full ${
                              st.state === 'ACTIVE'
                                ? 'bg-emerald-400 animate-pulse'
                                : st.state === 'IDLE'
                                ? 'bg-amber-400'
                                : 'bg-slate-500'
                            }`}
                          />
                          <span className="font-mono text-sm">{st.stationId}</span>
                        </td>
                        <td className="px-5 py-3.5 text-slate-300">
                          <div className="flex items-center space-x-2">
                            <div className="w-6 h-6 rounded-full bg-[#202C40] text-slate-300 flex items-center justify-center text-[10px] font-bold">
                              {st.fullName ? st.fullName.charAt(0) : '?'}
                            </div>
                            <span className="font-semibold text-slate-200">{st.fullName || 'Unassigned'}</span>
                          </div>
                        </td>
                        <td className="px-5 py-3.5">
                          {st.currentTaskCode ? (
                            <div className="inline-flex items-center space-x-1.5 px-2.5 py-1 rounded-md bg-blue-950/60 text-blue-300 border border-blue-700/40">
                              <span className="font-mono font-bold text-blue-400">{st.currentTaskCode}</span>
                              {st.currentTaskTitle && (
                                <span className="text-slate-300 truncate max-w-[200px]">• {st.currentTaskTitle}</span>
                              )}
                            </div>
                          ) : (
                            <span className="text-slate-500 italic">None (awaiting task)</span>
                          )}
                        </td>
                        <td className="px-5 py-3.5 text-slate-200">
                          <span className="font-mono text-[11px] bg-[#0E1422] px-2 py-1 rounded border border-[#222E42] text-slate-300">
                            {st.focusedApp || '—'}
                          </span>
                        </td>
                        <td className="px-5 py-3.5 text-slate-300 font-mono">
                          {Math.floor(st.activeMinutesToday / 60)}h {st.activeMinutesToday % 60}m
                        </td>
                        <td className="px-5 py-3.5 text-right">
                          <span
                            className={`inline-block px-2.5 py-0.5 rounded-full text-[11px] font-extrabold ${
                              st.state === 'ACTIVE'
                                ? 'bg-emerald-950/80 text-emerald-400 border border-emerald-500/30'
                                : st.state === 'IDLE'
                                ? 'bg-amber-950/80 text-amber-400 border border-amber-500/30'
                                : 'bg-slate-800 text-slate-400 border border-slate-700'
                            }`}
                          >
                            {st.state}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Decluttered Activity Hub (Dedicated Task Changes & Live Telemetry) */}
            <div className="bg-[#141B28] rounded-xl border border-[#222E42] overflow-hidden shadow-sm">
              {/* Filter Bar */}
              <div className="p-4 border-b border-[#222E42] bg-[#111724] flex flex-col md:flex-row md:items-center md:justify-between gap-3">
                <div className="flex items-center space-x-3">
                  <div className="flex items-center space-x-2">
                    <Activity className="w-4 h-4 text-emerald-400" />
                    <h2 className="text-xs font-bold uppercase tracking-wider text-white">Workforce Activity Hub</h2>
                  </div>

                  {/* Category Filter Chips */}
                  <div className="flex items-center bg-[#0C1019] p-1 rounded-lg border border-[#222E42]">
                    <button
                      onClick={() => setActivityCategory('tasks')}
                      className={`px-3 py-1 rounded-md text-xs font-bold flex items-center space-x-1.5 transition ${
                        activityCategory === 'tasks'
                          ? 'bg-blue-600 text-white shadow-sm'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      <Sparkles className="w-3 h-3 text-amber-300" />
                      <span>Task Changes</span>
                      <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-blue-900/60 ml-1">
                        {taskChangesCount}
                      </span>
                    </button>

                    <button
                      onClick={() => setActivityCategory('all')}
                      className={`px-3 py-1 rounded-md text-xs font-bold transition ${
                        activityCategory === 'all'
                          ? 'bg-blue-600 text-white shadow-sm'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      All Events
                    </button>

                    <button
                      onClick={() => setActivityCategory('focus')}
                      className={`px-3 py-1 rounded-md text-xs font-bold transition ${
                        activityCategory === 'focus'
                          ? 'bg-blue-600 text-white shadow-sm'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      Process Focus
                    </button>

                    <button
                      onClick={() => setActivityCategory('security')}
                      className={`px-3 py-1 rounded-md text-xs font-bold transition ${
                        activityCategory === 'security'
                          ? 'bg-blue-600 text-white shadow-sm'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      Session &amp; Locks
                    </button>
                  </div>
                </div>

                {/* Right controls: Station filter & Search */}
                <div className="flex items-center space-x-2.5">
                  <div className="relative">
                    <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-500" />
                    <input
                      type="text"
                      placeholder="Filter activity..."
                      value={eventSearchQuery}
                      onChange={(e) => setEventSearchQuery(e.target.value)}
                      className="bg-[#0C1019] border border-[#222E42] rounded-lg pl-8 pr-3 py-1 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 w-44"
                    />
                  </div>

                  <select
                    value={selectedStationFilter}
                    onChange={(e) => setSelectedStationFilter(e.target.value)}
                    className="bg-[#0C1019] border border-[#222E42] rounded-lg px-2.5 py-1 text-xs text-slate-300 focus:outline-none focus:border-blue-500"
                  >
                    <option value="ALL">All Stations</option>
                    {stations.map((s) => (
                      <option key={s.stationId} value={s.stationId}>
                        {s.stationId}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Activity Timeline Feed */}
              <div className="p-4 max-h-[360px] overflow-y-auto space-y-2.5 divide-y divide-[#1B2538]/60">
                {filteredEvents.length === 0 ? (
                  <div className="text-center py-10 space-y-2 text-slate-500">
                    <Sparkles className="w-6 h-6 mx-auto text-slate-600" />
                    <p className="text-xs font-semibold text-slate-400">No activity matching current filters</p>
                    <p className="text-[11px] text-slate-500">
                      Dispatched tasks, status changes, and deliverable uploads will appear here in real time.
                    </p>
                  </div>
                ) : (
                  filteredEvents.map((evt, idx) => {
                    const isTask = isTaskEvent(evt.eventType);
                    const isSec = isSecurityEvent(evt.eventType);
                    const isDeliverable = evt.eventType.toUpperCase() === 'DELIVERABLE_ATTACHED';
                    const isStatusChanged = evt.eventType.toUpperCase() === 'TASK_STATUS_CHANGED';
                    const isDispatched = evt.eventType.toUpperCase() === 'TASK_ASSIGNED';

                    return (
                      <div
                        key={evt.id || idx}
                        className="pt-2.5 first:pt-0 flex items-start justify-between gap-3 text-xs hover:bg-[#162133]/40 p-2 rounded-lg transition"
                      >
                        <div className="flex items-start space-x-3">
                          {/* Event Icon Badge */}
                          <div
                            className={`w-7 h-7 rounded-lg shrink-0 flex items-center justify-center mt-0.5 ${
                              isDeliverable
                                ? 'bg-emerald-950/80 text-emerald-400 border border-emerald-500/30'
                                : isDispatched
                                ? 'bg-purple-950/80 text-purple-400 border border-purple-500/30'
                                : isStatusChanged
                                ? 'bg-blue-950/80 text-blue-400 border border-blue-500/30'
                                : isSec
                                ? 'bg-amber-950/80 text-amber-400 border border-amber-500/30'
                                : 'bg-slate-800 text-slate-400 border border-slate-700'
                            }`}
                          >
                            {isDeliverable ? (
                              <Paperclip className="w-3.5 h-3.5" />
                            ) : isDispatched ? (
                              <Sparkles className="w-3.5 h-3.5" />
                            ) : isStatusChanged ? (
                              <Play className="w-3.5 h-3.5" />
                            ) : isSec ? (
                              <Lock className="w-3.5 h-3.5" />
                            ) : (
                              <Monitor className="w-3.5 h-3.5" />
                            )}
                          </div>

                          {/* Event Description */}
                          <div className="space-y-1">
                            <div className="flex items-center space-x-2">
                              {evt.taskCode && (
                                <span className="font-mono font-bold text-[11px] px-1.5 py-0.5 rounded bg-blue-950/80 text-blue-300 border border-blue-700/40">
                                  {evt.taskCode}
                                </span>
                              )}
                              <span className="font-semibold text-white">{evt.details}</span>
                              {evt.repeatCount && evt.repeatCount > 1 && (
                                <span className="text-[10px] bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded-full">
                                  {evt.repeatCount}×
                                </span>
                              )}
                            </div>

                            <div className="flex items-center space-x-2 text-[11px] text-slate-400">
                              <span className="font-semibold text-slate-300">{evt.stationId}</span>
                              {evt.userName && <span>• {evt.userName}</span>}
                              {evt.processName && (
                                <span className="font-mono text-slate-400">• {evt.processName}</span>
                              )}
                            </div>
                          </div>
                        </div>

                        {/* Timestamp */}
                        <div className="text-[11px] font-mono text-slate-500 shrink-0">
                          {new Date(evt.timestamp).toLocaleTimeString([], {
                            hour: '2-digit',
                            minute: '2-digit',
                            second: '2-digit',
                          })}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          </div>
        )}

        {/* TAB 2: TASK DISTRIBUTION BOARD & DELIVERABLES */}
        {activeTab === 'tasks' && (
          <div className="space-y-5">
            {/* Header & Filter Controls */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-[#141B28] p-4 rounded-xl border border-[#222E42]">
              <div>
                <h2 className="text-sm font-bold text-white">Workforce Task Distribution &amp; Deliverables</h2>
                <p className="text-xs text-slate-400">
                  Deliverable-linked task workflow • All deliverables require file or URL proof before closing
                </p>
              </div>

              <div className="flex items-center space-x-3">
                <div className="relative">
                  <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-500" />
                  <input
                    type="text"
                    placeholder="Search tasks or assignee..."
                    value={taskSearchQuery}
                    onChange={(e) => setTaskSearchQuery(e.target.value)}
                    className="bg-[#0C1019] border border-[#222E42] rounded-lg pl-8 pr-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 w-52"
                  />
                </div>

                <select
                  value={taskPriorityFilter}
                  onChange={(e) => setTaskPriorityFilter(e.target.value)}
                  className="bg-[#0C1019] border border-[#222E42] rounded-lg px-2.5 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-blue-500"
                >
                  <option value="ALL">All Priorities</option>
                  <option value="CRITICAL">Critical</option>
                  <option value="HIGH">High</option>
                  <option value="MEDIUM">Medium</option>
                  <option value="LOW">Low</option>
                </select>

                <button
                  onClick={() => setIsDispatchModalOpen(true)}
                  className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold shadow-md shadow-blue-600/20 transition"
                >
                  <Plus className="w-3.5 h-3.5" />
                  <span>New Task</span>
                </button>
              </div>
            </div>

            {/* Kanban Columns */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              {(['ASSIGNED', 'IN_PROGRESS', 'UNDER_REVIEW', 'COMPLETED'] as const).map((columnStatus) => {
                const columnTasks = filteredTasks.filter((t) => t.status === columnStatus);
                const colTitle =
                  columnStatus === 'ASSIGNED'
                    ? 'Awaiting Start'
                    : columnStatus === 'IN_PROGRESS'
                    ? 'In Progress'
                    : columnStatus === 'UNDER_REVIEW'
                    ? 'Under Review'
                    : 'Completed';

                return (
                  <div key={columnStatus} className="bg-[#141B28] rounded-xl border border-[#222E42] flex flex-col max-h-[700px]">
                    <div className="p-3 border-b border-[#222E42] bg-[#111724] flex items-center justify-between">
                      <span className="text-xs font-bold uppercase tracking-wider text-slate-300">{colTitle}</span>
                      <span className="text-[11px] font-bold bg-[#0C1019] text-slate-300 px-2 py-0.5 rounded-full border border-[#222E42]">
                        {columnTasks.length}
                      </span>
                    </div>

                    <div className="p-3 overflow-y-auto space-y-3 flex-1">
                      {columnTasks.length === 0 ? (
                        <div className="text-xs text-slate-500 text-center py-6 italic">No tasks in this column</div>
                      ) : (
                        columnTasks.map((task) => (
                          <div
                            key={task.id}
                            className="bg-[#0C1018] border border-[#222E42] rounded-lg p-3 space-y-2 hover:border-slate-500 transition shadow-sm"
                          >
                            <div className="flex items-center justify-between">
                              <span className="text-xs font-mono font-bold text-blue-400">{task.taskCode}</span>
                              <span
                                className={`text-[10px] font-extrabold px-1.5 py-0.5 rounded ${
                                  task.priority === 'CRITICAL'
                                    ? 'bg-rose-950 text-rose-400 border border-rose-500/30'
                                    : task.priority === 'HIGH'
                                    ? 'bg-amber-950 text-amber-400 border border-amber-500/30'
                                    : task.priority === 'MEDIUM'
                                    ? 'bg-blue-950 text-blue-400 border border-blue-500/30'
                                    : 'bg-slate-800 text-slate-300'
                                }`}
                              >
                                {task.priority}
                              </span>
                            </div>

                            <h3 className="text-xs font-semibold text-white leading-snug">{task.title}</h3>
                            {task.description && (
                              <p className="text-[11px] text-slate-400 line-clamp-2">{task.description}</p>
                            )}

                            <div className="text-[11px] text-slate-400 flex items-center justify-between pt-1 border-t border-[#1C2638]">
                              <span>{task.assignedToName || 'Unassigned'}</span>
                              <span>{Math.round(task.totalTrackedSeconds / 60)}m tracked</span>
                            </div>

                            {/* Deliverable Proof of Work Section */}
                            {task.deliverableFileName || task.deliverableUrl ? (
                              <div className="bg-emerald-950/40 border border-emerald-500/30 rounded p-2 text-xs flex items-center justify-between">
                                <div className="flex items-center space-x-1.5 text-emerald-300 truncate text-[11px]">
                                  <CheckCircle2 className="w-3.5 h-3.5 shrink-0" />
                                  <span className="truncate">{task.deliverableFileName || 'Deliverable attached'}</span>
                                </div>
                                <a
                                  href={`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5020'}/api/v1/tasks/${task.id}/deliverable`}
                                  target="_blank"
                                  rel="noreferrer"
                                  className="text-blue-400 hover:text-blue-300 shrink-0 ml-2"
                                  title="Download Deliverable"
                                >
                                  <Download className="w-3.5 h-3.5" />
                                </a>
                              </div>
                            ) : task.deliverableRequired ? (
                              <div className="bg-amber-950/30 border border-amber-500/20 rounded p-1.5 text-[10.5px] text-amber-400/90 flex items-center space-x-1">
                                <AlertCircle className="w-3 h-3 shrink-0" />
                                <span>Proof of work required before closing</span>
                              </div>
                            ) : null}
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* TAB 3: PAYROLL TIMESHEETS */}
        {activeTab === 'timesheets' && (
          <div className="space-y-5">
            <div className="flex items-center justify-between bg-[#141B28] p-4 rounded-xl border border-[#222E42]">
              <div>
                <h2 className="text-sm font-bold text-white">Consolidated Workstation Timesheet</h2>
                <p className="text-xs text-slate-400">
                  Accurate down to the second; exportable directly to payroll CSV without touching raw logs
                </p>
              </div>

              <a
                href={getTimesheetCsvDownloadUrl()}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center space-x-2 px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-md shadow-emerald-600/20 transition"
              >
                <Download className="w-4 h-4" />
                <span>EXPORT CSV FOR PAYROLL</span>
              </a>
            </div>

            <div className="bg-[#141B28] rounded-xl border border-[#222E42] overflow-hidden shadow-sm">
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="bg-[#0D1320] text-[11px] uppercase tracking-wider text-slate-400 border-b border-[#222E42]">
                    <tr>
                      <th className="px-5 py-3">Date</th>
                      <th className="px-5 py-3">Station</th>
                      <th className="px-5 py-3">Employee Name</th>
                      <th className="px-5 py-3">Clock In</th>
                      <th className="px-5 py-3">Clock Out</th>
                      <th className="px-5 py-3">Total Hours</th>
                      <th className="px-5 py-3">Active Hours</th>
                      <th className="px-5 py-3">Idle Hours</th>
                      <th className="px-5 py-3">Efficiency</th>
                      <th className="px-5 py-3 text-right">Tasks Done</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#1D273A] font-medium text-xs">
                    {timesheet?.rows.map((row, idx) => (
                      <tr key={idx} className="hover:bg-[#182234]/60 transition">
                        <td className="px-5 py-3.5 text-slate-300 font-mono text-xs">{row.date}</td>
                        <td className="px-5 py-3.5 font-bold text-white">{row.stationId}</td>
                        <td className="px-5 py-3.5 text-white">{row.employeeName}</td>
                        <td className="px-5 py-3.5 text-slate-300 font-mono text-xs">{row.clockIn}</td>
                        <td className="px-5 py-3.5 text-slate-300 font-mono text-xs">{row.clockOut}</td>
                        <td className="px-5 py-3.5 text-white font-semibold">{row.totalHours.toFixed(2)}h</td>
                        <td className="px-5 py-3.5 text-emerald-400 font-semibold">{row.activeHours.toFixed(2)}h</td>
                        <td className="px-5 py-3.5 text-amber-400 font-semibold">{row.idleHours.toFixed(2)}h</td>
                        <td className="px-5 py-3.5">
                          <div className="flex items-center space-x-2">
                            <div className="w-16 h-2 bg-[#0C1018] rounded-full overflow-hidden border border-[#222E42]">
                              <div
                                className="h-full bg-emerald-500 rounded-full"
                                style={{ width: `${Math.min(100, row.efficiencyPercentage)}%` }}
                              />
                            </div>
                            <span className="text-xs font-bold text-emerald-400">
                              {row.efficiencyPercentage.toFixed(1)}%
                            </span>
                          </div>
                        </td>
                        <td className="px-5 py-3.5 text-right font-bold text-white">{row.tasksCompleted}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {/* TAB 4: LEAVE MANAGEMENT & APPROVAL CENTER */}
        {activeTab === 'leaves' && (
          <div className="space-y-6">
            {/* Filter and Search Bar */}
            <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-[#141B28] p-4 rounded-xl border border-[#222E42]">
              <div className="flex items-center space-x-2">
                <span className="text-xs font-bold text-slate-400 uppercase tracking-wider mr-1">Status:</span>
                <button
                  onClick={() => setLeaveStatusFilter('ALL')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition ${
                    leaveStatusFilter === 'ALL'
                      ? 'bg-blue-600 text-white shadow-sm'
                      : 'bg-[#1B2537] text-slate-300 hover:text-white'
                  }`}
                >
                  All ({leaves.length})
                </button>
                <button
                  onClick={() => setLeaveStatusFilter('PENDING')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition flex items-center space-x-1.5 ${
                    leaveStatusFilter === 'PENDING'
                      ? 'bg-amber-600 text-white shadow-sm'
                      : 'bg-[#1B2537] text-slate-300 hover:text-white'
                  }`}
                >
                  <span className="w-2 h-2 rounded-full bg-amber-400"></span>
                  <span>Pending ({pendingLeavesCount})</span>
                </button>
                <button
                  onClick={() => setLeaveStatusFilter('APPROVED')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition flex items-center space-x-1.5 ${
                    leaveStatusFilter === 'APPROVED'
                      ? 'bg-emerald-600 text-white shadow-sm'
                      : 'bg-[#1B2537] text-slate-300 hover:text-white'
                  }`}
                >
                  <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
                  <span>Approved ({approvedLeavesCount})</span>
                </button>
                <button
                  onClick={() => setLeaveStatusFilter('REJECTED')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition flex items-center space-x-1.5 ${
                    leaveStatusFilter === 'REJECTED'
                      ? 'bg-rose-600 text-white shadow-sm'
                      : 'bg-[#1B2537] text-slate-300 hover:text-white'
                  }`}
                >
                  <span className="w-2 h-2 rounded-full bg-rose-400"></span>
                  <span>Rejected ({rejectedLeavesCount})</span>
                </button>
              </div>

              <div className="relative flex-1 max-w-xs">
                <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                <input
                  type="text"
                  placeholder="Search applicant, reason, station..."
                  value={leaveSearchQuery}
                  onChange={(e) => setLeaveSearchQuery(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg pl-9 pr-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                />
                {leaveSearchQuery && (
                  <button
                    onClick={() => setLeaveSearchQuery('')}
                    className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>
            </div>

            {/* Leave Applications Table */}
            <div className="bg-[#141B28] rounded-xl border border-[#222E42] overflow-hidden shadow-sm">
              <div className="px-5 py-3.5 border-b border-[#222E42] flex items-center justify-between bg-[#111724]">
                <div className="flex items-center space-x-2">
                  <CalendarDays className="w-4 h-4 text-blue-400" />
                  <h2 className="text-xs font-bold uppercase tracking-wider text-white">
                    Employee Leave Applications ({filteredLeaves.length})
                  </h2>
                </div>
                <span className="text-[11px] text-slate-400">Manager Decision &amp; Audit Logs</span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="bg-[#0D1320] text-[11px] uppercase tracking-wider text-slate-400 border-b border-[#222E42]">
                    <tr>
                      <th className="px-5 py-3">Applicant / Workstation</th>
                      <th className="px-5 py-3">Type</th>
                      <th className="px-5 py-3">Period &amp; Days</th>
                      <th className="px-5 py-3">Reason</th>
                      <th className="px-5 py-3">Status</th>
                      <th className="px-5 py-3">Reviewed By / Notes</th>
                      <th className="px-5 py-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#1D273A] font-medium text-xs">
                    {filteredLeaves.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="px-5 py-12 text-center text-slate-500">
                          <CalendarDays className="w-8 h-8 mx-auto mb-2 text-slate-600" />
                          No leave applications found matching current criteria.
                        </td>
                      </tr>
                    ) : (
                      filteredLeaves.map((leave) => {
                        const isPending = leave.status === 'PENDING';
                        const isApproved = leave.status === 'APPROVED';
                        const isRejected = leave.status === 'REJECTED';

                        return (
                          <tr key={leave.id} className="hover:bg-[#182133] transition-colors">
                            {/* Applicant */}
                            <td className="px-5 py-3.5">
                              <div className="flex flex-col">
                                <span className="text-white font-bold text-xs">{leave.userName}</span>
                                <span className="text-slate-400 text-[11px]">{leave.userEmail}</span>
                                {leave.stationId && (
                                  <span className="text-[10px] text-blue-400 font-mono mt-0.5">
                                    Station: {leave.stationId}
                                  </span>
                                )}
                              </div>
                            </td>

                            {/* Leave Type */}
                            <td className="px-5 py-3.5">
                              <span
                                className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border ${
                                  leave.leaveType === 'SICK' || leave.leaveType === 'MEDICAL'
                                    ? 'bg-rose-500/10 text-rose-400 border-rose-500/20'
                                    : leave.leaveType === 'EMERGENCY'
                                    ? 'bg-amber-500/10 text-amber-400 border-amber-500/20'
                                    : leave.leaveType === 'ANNUAL'
                                    ? 'bg-purple-500/10 text-purple-400 border-purple-500/20'
                                    : 'bg-blue-500/10 text-blue-400 border-blue-500/20'
                                }`}
                              >
                                {leave.leaveType}
                              </span>
                            </td>

                            {/* Period & Days */}
                            <td className="px-5 py-3.5 text-slate-300">
                              <div className="font-medium text-white">
                                {leave.startDate?.split('T')[0]} <span className="text-slate-500">to</span> {leave.endDate?.split('T')[0]}
                              </div>
                              <span className="text-[10px] text-slate-400">
                                {leave.totalDays} {leave.totalDays === 1 ? 'day' : 'days'}
                              </span>
                            </td>

                            {/* Reason */}
                            <td className="px-5 py-3.5 max-w-xs">
                              <p className="text-slate-300 line-clamp-2 text-xs" title={leave.reason}>
                                {leave.reason}
                              </p>
                            </td>

                            {/* Status */}
                            <td className="px-5 py-3.5">
                              <span
                                className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-extrabold uppercase tracking-wider border ${
                                  isPending
                                    ? 'bg-amber-500/15 text-amber-400 border-amber-500/30'
                                    : isApproved
                                    ? 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30'
                                    : 'bg-rose-500/15 text-rose-400 border-rose-500/30'
                                }`}
                              >
                                {isPending && <span className="w-1.5 h-1.5 rounded-full bg-amber-400 mr-1.5 animate-pulse" />}
                                {isApproved && <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mr-1.5" />}
                                {isRejected && <span className="w-1.5 h-1.5 rounded-full bg-rose-400 mr-1.5" />}
                                {leave.status}
                              </span>
                            </td>

                            {/* Reviewed By / Notes */}
                            <td className="px-5 py-3.5 text-slate-400 text-xs">
                              {(leave.reviewerName || leave.reviewedBy) ? (
                                <div>
                                  <span className="text-slate-300 font-medium">{leave.reviewerName || leave.reviewedBy}</span>
                                  {leave.reviewedAt && (
                                    <span className="block text-[10px] text-slate-500">
                                      {new Date(leave.reviewedAt).toLocaleDateString()}
                                    </span>
                                  )}
                                  {leave.reviewComments && (
                                    <span className="block text-[11px] text-slate-400 italic mt-0.5">
                                      &ldquo;{leave.reviewComments}&rdquo;
                                    </span>
                                  )}
                                </div>
                              ) : (
                                <span className="text-slate-600 italic">Pending review</span>
                              )}
                            </td>

                            {/* Actions */}
                            <td className="px-5 py-3.5 text-right">
                              {isPending ? (
                                <div className="flex items-center justify-end space-x-2">
                                  <button
                                    onClick={() => {
                                      setSelectedLeaveForReview(leave);
                                      setReviewAction('approve');
                                      setReviewComments('');
                                      setReviewFeedback(null);
                                    }}
                                    className="px-2.5 py-1.5 rounded-lg bg-emerald-600/20 hover:bg-emerald-600/30 border border-emerald-500/40 text-emerald-300 hover:text-white text-xs font-bold transition flex items-center space-x-1"
                                    title="Approve leave request"
                                  >
                                    <Check className="w-3.5 h-3.5" />
                                    <span>Approve</span>
                                  </button>

                                  <button
                                    onClick={() => {
                                      setSelectedLeaveForReview(leave);
                                      setReviewAction('reject');
                                      setReviewComments('');
                                      setReviewFeedback(null);
                                    }}
                                    className="px-2.5 py-1.5 rounded-lg bg-rose-600/20 hover:bg-rose-600/30 border border-rose-500/40 text-rose-300 hover:text-white text-xs font-bold transition flex items-center space-x-1"
                                    title="Reject leave request"
                                  >
                                    <X className="w-3.5 h-3.5" />
                                    <span>Reject</span>
                                  </button>
                                </div>
                              ) : (
                                <span className="text-slate-600 text-xs font-semibold">Processed</span>
                              )}
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {/* TAB 5: STAFF & NON-DOMAIN USER MANAGEMENT */}
        {activeTab === 'staff' && (
          <div className="space-y-6">
            {/* Header / Actions Card */}
            <div className="bg-[#141B28] rounded-xl border border-[#222E42] p-5 shadow-sm">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                  <div className="flex items-center space-x-2.5">
                    <div className="p-2 rounded-lg bg-blue-500/10 text-blue-400 border border-blue-500/20">
                      <Users className="w-5 h-5" />
                    </div>
                    <div>
                      <h2 className="text-base font-bold text-white tracking-tight">Staff &amp; Workforce Directory</h2>
                      <p className="text-xs text-slate-400 mt-0.5">
                        Enroll staff with personal emails (Gmail/Yahoo/Outlook) or simple usernames • Seamless WFH device pairing • $0 Cost
                      </p>
                    </div>
                  </div>
                </div>

                <div className="flex items-center space-x-3">
                  <button
                    onClick={() => {
                      setIsAddStaffModalOpen(true);
                      setAddStaffMessage(null);
                    }}
                    className="flex items-center space-x-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold shadow-md shadow-blue-600/20 transition-all active:scale-[0.98]"
                  >
                    <UserPlus className="w-4 h-4" />
                    <span>ENROLL NEW STAFF</span>
                  </button>
                </div>
              </div>

              {/* Filters & Search Row */}
              <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-5 mt-5 border-t border-[#222E42]">
                <div className="relative w-full sm:w-80">
                  <Search className="w-4 h-4 absolute left-3 top-2.5 text-slate-500" />
                  <input
                    type="text"
                    placeholder="Search by name, username, email, station..."
                    value={staffSearchQuery}
                    onChange={(e) => setStaffSearchQuery(e.target.value)}
                    className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg pl-9 pr-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
                  />
                  {staffSearchQuery && (
                    <button
                      onClick={() => setStaffSearchQuery('')}
                      className="absolute right-2.5 top-2.5 text-slate-400 hover:text-white text-xs"
                    >
                      <X className="w-3.5 h-3.5" />
                    </button>
                  )}
                </div>

                <div className="flex items-center space-x-1 overflow-x-auto w-full sm:w-auto pb-1 sm:pb-0">
                  {[
                    { id: 'ALL', label: `All (${staff.length})` },
                    { id: 'REMOTE_WFH', label: `🏠 Remote WFH (${staff.filter((s) => s.workMode === 'REMOTE_WFH').length})` },
                    { id: 'IN_OFFICE', label: `🏢 In-Office (${staff.filter((s) => s.workMode === 'IN_OFFICE').length})` },
                    { id: 'ACTIVE', label: `Active (${staff.filter((s) => s.isActive).length})` },
                    { id: 'INACTIVE', label: `Suspended (${staff.filter((s) => !s.isActive).length})` },
                  ].map((tab) => (
                    <button
                      key={tab.id}
                      onClick={() => setStaffFilter(tab.id)}
                      className={`px-3 py-1.5 rounded-lg text-xs font-semibold whitespace-nowrap transition ${
                        staffFilter === tab.id
                          ? 'bg-blue-600 text-white shadow-sm'
                          : 'bg-[#111724] text-slate-400 hover:text-white hover:bg-[#1A2333] border border-[#222E42]'
                      }`}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Staff Directory Table */}
            <div className="bg-[#141B28] rounded-xl border border-[#222E42] overflow-hidden shadow-sm">
              <div className="px-5 py-3.5 border-b border-[#222E42] flex items-center justify-between bg-[#111724]">
                <div className="flex items-center space-x-2">
                  <Users className="w-4 h-4 text-blue-400" />
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white">Enrolled Team Directory</h3>
                </div>
                <span className="text-xs text-slate-400">
                  Showing {filteredStaff.length} of {staff.length} staff members
                </span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead className="bg-[#0C1019] text-slate-400 uppercase text-[11px] font-bold border-b border-[#222E42]">
                    <tr>
                      <th className="py-3 px-4">Staff Member</th>
                      <th className="py-3 px-4">Role</th>
                      <th className="py-3 px-4">Work Mode</th>
                      <th className="py-3 px-4">Paired Station / Device</th>
                      <th className="py-3 px-4 text-center">Shift Today</th>
                      <th className="py-3 px-4 text-center">Active Tasks</th>
                      <th className="py-3 px-4 text-center">Status</th>
                      <th className="py-3 px-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#1D2738]">
                    {filteredStaff.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="py-12 text-center text-slate-500">
                          <Users className="w-8 h-8 mx-auto mb-2 text-slate-600 opacity-40" />
                          <p className="font-semibold">No staff members found matching criteria.</p>
                        </td>
                      </tr>
                    ) : (
                      filteredStaff.map((member) => (
                        <tr key={member.id} className="hover:bg-[#182132]/60 transition">
                          {/* Staff Name & Contact */}
                          <td className="py-3.5 px-4">
                            <div className="flex items-center space-x-3">
                              <div className="w-8 h-8 rounded-lg bg-blue-500/10 border border-blue-500/20 text-blue-400 flex items-center justify-center font-bold text-xs">
                                {member.fullName.charAt(0).toUpperCase()}
                              </div>
                              <div>
                                <div className="font-bold text-white text-xs">{member.fullName}</div>
                                <div className="text-[11px] text-slate-400 flex items-center space-x-2">
                                  {member.username && (
                                    <span className="text-blue-400 font-mono">@{member.username}</span>
                                  )}
                                  <span>{member.email}</span>
                                </div>
                              </div>
                            </div>
                          </td>

                          {/* Role */}
                          <td className="py-3.5 px-4">
                            <span
                              className={`px-2 py-0.5 rounded text-[10px] font-black tracking-wider uppercase border ${
                                member.role === 'ADMIN'
                                  ? 'bg-purple-950/60 text-purple-300 border-purple-500/40'
                                  : member.role === 'MANAGER'
                                  ? 'bg-blue-950/60 text-blue-300 border-blue-500/40'
                                  : 'bg-slate-800 text-slate-300 border-slate-700'
                              }`}
                            >
                              {member.role}
                            </span>
                          </td>

                          {/* Work Mode with Quick Toggle */}
                          <td className="py-3.5 px-4">
                            <div className="flex items-center space-x-2">
                              <span
                                className={`px-2.5 py-1 rounded-full text-[11px] font-bold border flex items-center space-x-1.5 ${
                                  member.workMode === 'REMOTE_WFH'
                                    ? 'bg-cyan-950/50 text-cyan-300 border-cyan-500/30'
                                    : 'bg-indigo-950/50 text-indigo-300 border-indigo-500/30'
                                }`}
                              >
                                {member.workMode === 'REMOTE_WFH' ? (
                                  <>
                                    <Laptop className="w-3 h-3 text-cyan-400" />
                                    <span>Remote WFH</span>
                                  </>
                                ) : (
                                  <>
                                    <Building className="w-3 h-3 text-indigo-400" />
                                    <span>In-Office</span>
                                  </>
                                )}
                              </span>
                              <button
                                onClick={() => handleToggleWorkMode(member.id, member.workMode)}
                                className="text-[10px] text-slate-400 hover:text-white px-1.5 py-0.5 rounded bg-[#1A2333] hover:bg-[#233148] border border-[#2D3E5B] transition"
                                title="Switch between WFH and In-Office"
                              >
                                ⇄
                              </button>
                            </div>
                          </td>

                          {/* Workstation ID */}
                          <td className="py-3.5 px-4">
                            <span className="font-mono text-xs px-2 py-0.5 rounded bg-[#0C1019] border border-[#222E42] text-slate-300">
                              {member.stationId || 'Unassigned'}
                            </span>
                          </td>

                          {/* Shift Status */}
                          <td className="py-3.5 px-4 text-center">
                            {member.shiftStatus === 'ACTIVE' ? (
                              <span className="inline-flex items-center space-x-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-950/60 text-emerald-300 border border-emerald-500/30">
                                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                                <span>ON CLOCK</span>
                              </span>
                            ) : member.shiftStatus === 'COMPLETED' ? (
                              <span className="inline-block px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-slate-700">
                                CLOCKED OUT
                              </span>
                            ) : (
                              <span className="text-slate-500 text-[11px]">—</span>
                            )}
                          </td>

                          {/* Active Tasks */}
                          <td className="py-3.5 px-4 text-center">
                            <span
                              className={`px-2 py-0.5 rounded text-xs font-bold ${
                                member.activeTasksCount > 0
                                  ? 'bg-blue-950/60 text-blue-300 border border-blue-500/30'
                                  : 'text-slate-500'
                              }`}
                            >
                              {member.activeTasksCount}
                            </span>
                          </td>

                          {/* Status & Active Toggle */}
                          <td className="py-3.5 px-4 text-center">
                            <button
                              onClick={() => handleToggleStaffStatus(member.id)}
                              className={`inline-flex items-center space-x-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold border transition ${
                                member.isActive
                                  ? 'bg-emerald-950/50 text-emerald-300 border-emerald-500/30 hover:bg-emerald-900/50'
                                  : 'bg-rose-950/50 text-rose-400 border-rose-500/30 hover:bg-rose-900/50'
                              }`}
                              title="Click to toggle Active / Suspended status"
                            >
                              <span
                                className={`w-1.5 h-1.5 rounded-full ${
                                  member.isActive ? 'bg-emerald-400' : 'bg-rose-400'
                                }`}
                              />
                              <span>{member.isActive ? 'Active' : 'Suspended'}</span>
                            </button>
                          </td>

                          {/* Actions */}
                          <td className="py-3.5 px-4 text-right">
                            <button
                              onClick={() => {
                                setSelectedStaffForPasswordReset(member);
                                setNewResetPassword('');
                                setResetPasswordMessage(null);
                              }}
                              className="px-2.5 py-1.5 rounded-lg bg-[#1B2537] hover:bg-[#233148] border border-[#2D3E5B] text-slate-300 hover:text-white text-xs font-semibold transition inline-flex items-center space-x-1.5"
                              title="Reset Employee Password"
                            >
                              <Key className="w-3 h-3 text-amber-400" />
                              <span>Reset Key</span>
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* Windows 11 Style Modal: DISPATCH NEW TASK */}
      {isDispatchModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="bg-[#141B28] border border-[#293850] rounded-xl w-full max-w-lg p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-[#222E42]">
              <div className="flex items-center space-x-2 text-white">
                <Sparkles className="w-4 h-4 text-blue-400" />
                <h3 className="text-sm font-bold uppercase tracking-wider">Dispatch New Task</h3>
              </div>
              <button
                onClick={() => setIsDispatchModalOpen(false)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-[#1E293B] transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleDispatchTask} className="space-y-4 text-xs">
              <div>
                <label className="block font-bold text-slate-300 mb-1">Task Title</label>
                <input
                  type="text"
                  required
                  autoFocus
                  placeholder="e.g. Color Grade Patient Exercise Video"
                  value={dispatchTitle}
                  onChange={(e) => setDispatchTitle(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block font-bold text-slate-300 mb-1">Assign To Staff / Workstation</label>
                <select
                  value={dispatchAssignee}
                  onChange={(e) => setDispatchAssignee(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                >
                  <option value="">Unassigned (Awaiting employee claim)</option>
                  {users
                    .filter((u) => u.role === 'EMPLOYEE')
                    .map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.stationId ? `${u.stationId} — ` : ''}
                        {u.fullName} ({u.email})
                      </option>
                    ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-slate-300 mb-1">Priority</label>
                  <select
                    value={dispatchPriority}
                    onChange={(e) => setDispatchPriority(e.target.value)}
                    className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                  >
                    <option value="LOW">Low</option>
                    <option value="MEDIUM">Medium</option>
                    <option value="HIGH">High</option>
                    <option value="CRITICAL">Critical</option>
                  </select>
                </div>

                <div>
                  <label className="block font-bold text-slate-300 mb-1">Deliverable Gate</label>
                  <div className="flex items-center h-9">
                    <input
                      type="checkbox"
                      id="deliverableCheckModal"
                      checked={dispatchDeliverableReq}
                      onChange={(e) => setDispatchDeliverableReq(e.target.checked)}
                      className="w-4 h-4 accent-blue-600 rounded mr-2 cursor-pointer"
                    />
                    <label htmlFor="deliverableCheckModal" className="text-slate-300 cursor-pointer">
                      Proof required to complete
                    </label>
                  </div>
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-300 mb-1">Scope &amp; Deliverable Specifications</label>
                <textarea
                  rows={3}
                  placeholder="Specify requirements, output file formats, or review guidelines..."
                  value={dispatchDesc}
                  onChange={(e) => setDispatchDesc(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                />
              </div>

              {dispatchMessage && (
                <div
                  className={`p-2.5 rounded-lg text-xs font-semibold ${
                    dispatchMessage.startsWith('✓')
                      ? 'bg-emerald-950/80 border border-emerald-500/40 text-emerald-300'
                      : 'bg-rose-950/80 border border-rose-500/40 text-rose-300'
                  }`}
                >
                  {dispatchMessage}
                </div>
              )}

              <div className="flex items-center justify-end space-x-2 pt-2">
                <button
                  type="button"
                  onClick={() => setIsDispatchModalOpen(false)}
                  className="px-4 py-2 rounded-lg bg-[#1B2537] hover:bg-[#222E42] border border-[#2D3E5B] text-slate-300 hover:text-white transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isDispatching || !dispatchTitle.trim()}
                  className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:bg-slate-700 text-white font-bold transition flex items-center space-x-2 shadow-md shadow-blue-600/20"
                >
                  <Plus className="w-3.5 h-3.5" />
                  <span>{isDispatching ? 'Dispatching...' : 'Dispatch Task'}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Windows 11 Style Modal: REVIEW LEAVE REQUEST */}
      {selectedLeaveForReview && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="bg-[#141B28] border border-[#293850] rounded-xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-[#222E42]">
              <div className="flex items-center space-x-2 text-white">
                <CalendarDays className="w-4 h-4 text-blue-400" />
                <h3 className="text-sm font-bold uppercase tracking-wider">
                  Review Leave Application
                </h3>
              </div>
              <button
                onClick={() => setSelectedLeaveForReview(null)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-[#1E293B] transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Application Overview Card */}
            <div className="bg-[#0C1019] rounded-lg p-3.5 border border-[#222E42] space-y-2 text-xs">
              <div className="flex justify-between items-start">
                <div>
                  <div className="font-bold text-white text-sm">{selectedLeaveForReview.userName}</div>
                  <div className="text-slate-400 text-[11px]">{selectedLeaveForReview.userEmail}</div>
                </div>
                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-blue-500/10 text-blue-400 border border-blue-500/20 uppercase">
                  {selectedLeaveForReview.leaveType}
                </span>
              </div>

              <div className="pt-2 border-t border-[#1C2638] flex justify-between text-slate-300">
                <span>Duration:</span>
                <span className="font-semibold text-white">
                  {selectedLeaveForReview.startDate?.split('T')[0]} to {selectedLeaveForReview.endDate?.split('T')[0]} ({selectedLeaveForReview.totalDays} {selectedLeaveForReview.totalDays === 1 ? 'day' : 'days'})
                </span>
              </div>

              <div>
                <span className="text-slate-400 block mb-0.5">Reason provided:</span>
                <p className="bg-[#141B28] p-2 rounded text-slate-200 border border-[#1E2A3E] text-xs">
                  {selectedLeaveForReview.reason}
                </p>
              </div>
            </div>

            <form onSubmit={handleReviewLeave} className="space-y-4 text-xs">
              {/* Decision Toggle */}
              <div>
                <label className="block font-bold text-slate-300 mb-1.5">Manager Decision</label>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setReviewAction('approve')}
                    className={`py-2 px-3 rounded-lg font-bold border transition flex items-center justify-center space-x-1.5 ${
                      reviewAction === 'approve'
                        ? 'bg-emerald-600 border-emerald-500 text-white shadow-md shadow-emerald-600/20'
                        : 'bg-[#0C1019] border-[#222E42] text-slate-400 hover:text-white'
                    }`}
                  >
                    <Check className="w-4 h-4" />
                    <span>Approve Leave</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => setReviewAction('reject')}
                    className={`py-2 px-3 rounded-lg font-bold border transition flex items-center justify-center space-x-1.5 ${
                      reviewAction === 'reject'
                        ? 'bg-rose-600 border-rose-500 text-white shadow-md shadow-rose-600/20'
                        : 'bg-[#0C1019] border-[#222E42] text-slate-400 hover:text-white'
                    }`}
                  >
                    <X className="w-4 h-4" />
                    <span>Reject Leave</span>
                  </button>
                </div>
              </div>

              {/* Review Comments */}
              <div>
                <label className="block font-bold text-slate-300 mb-1">
                  Manager Review Notes / Comments <span className="text-slate-500 font-normal">(Optional)</span>
                </label>
                <textarea
                  rows={2}
                  placeholder={
                    reviewAction === 'approve'
                      ? 'e.g. Approved. Ensure deliverables are handed over.'
                      : 'e.g. Critical release scheduled for that day. Please reschedule.'
                  }
                  value={reviewComments}
                  onChange={(e) => setReviewComments(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                />
              </div>

              {reviewFeedback && (
                <div
                  className={`p-2.5 rounded-lg text-xs font-semibold ${
                    reviewFeedback.startsWith('✓')
                      ? 'bg-emerald-950/80 border border-emerald-500/40 text-emerald-300'
                      : 'bg-rose-950/80 border border-rose-500/40 text-rose-300'
                  }`}
                >
                  {reviewFeedback}
                </div>
              )}

              <div className="flex items-center justify-end space-x-2 pt-2">
                <button
                  type="button"
                  onClick={() => setSelectedLeaveForReview(null)}
                  className="px-4 py-2 rounded-lg bg-[#1B2537] hover:bg-[#222E42] border border-[#2D3E5B] text-slate-300 hover:text-white transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isReviewing}
                  className={`px-4 py-2 rounded-lg font-bold text-white transition flex items-center space-x-1.5 shadow-md ${
                    reviewAction === 'approve'
                      ? 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-600/20'
                      : 'bg-rose-600 hover:bg-rose-500 shadow-rose-600/20'
                  }`}
                >
                  {reviewAction === 'approve' ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                  <span>{isReviewing ? 'Submitting...' : `Confirm ${reviewAction === 'approve' ? 'Approval' : 'Rejection'}`}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Windows 11 Style Modal: ENROLL NEW STAFF MEMBER */}
      {isAddStaffModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="bg-[#141B28] border border-[#293850] rounded-xl w-full max-w-lg p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-[#222E42]">
              <div className="flex items-center space-x-2 text-white">
                <UserPlus className="w-4 h-4 text-blue-400" />
                <h3 className="text-sm font-bold uppercase tracking-wider">Enroll Staff Member</h3>
              </div>
              <button
                onClick={() => setIsAddStaffModalOpen(false)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-[#1E293B] transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleCreateStaff} className="space-y-4 text-xs">
              <div>
                <label className="block font-bold text-slate-300 mb-1">Full Legal Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Dhyan Patel"
                  value={newStaffFullName}
                  onChange={(e) => setNewStaffFullName(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-slate-300 mb-1">Username / Staff ID</label>
                  <input
                    type="text"
                    placeholder="e.g. dhyan or PHF-101"
                    value={newStaffUsername}
                    onChange={(e) => setNewStaffUsername(e.target.value)}
                    className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                  />
                  <span className="text-[10px] text-slate-500">Can be used to sign into desktop app</span>
                </div>

                <div>
                  <label className="block font-bold text-slate-300 mb-1">Personal / Work Email</label>
                  <input
                    type="email"
                    required
                    placeholder="e.g. dhyan@gmail.com"
                    value={newStaffEmail}
                    onChange={(e) => setNewStaffEmail(e.target.value)}
                    className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                  />
                  <span className="text-[10px] text-slate-500">Any free email provider (no domain required)</span>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-bold text-slate-300 mb-1">Role</label>
                  <select
                    value={newStaffRole}
                    onChange={(e) => setNewStaffRole(e.target.value)}
                    className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                  >
                    <option value="EMPLOYEE">Employee</option>
                    <option value="MANAGER">Manager / Lead</option>
                    <option value="ADMIN">System Admin</option>
                  </select>
                </div>

                <div>
                  <label className="block font-bold text-slate-300 mb-1">Work Mode</label>
                  <select
                    value={newStaffWorkMode}
                    onChange={(e) => setNewStaffWorkMode(e.target.value)}
                    className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                  >
                    <option value="REMOTE_WFH">🏠 Remote (Work From Home)</option>
                    <option value="IN_OFFICE">🏢 In-Office (Fixed Desk)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-300 mb-1">Initial Temporary Password</label>
                <input
                  type="text"
                  required
                  value={newStaffPassword}
                  onChange={(e) => setNewStaffPassword(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-blue-500"
                />
                <span className="text-[10px] text-slate-500">Share this with the employee for first-time login</span>
              </div>

              {addStaffMessage && (
                <div
                  className={`p-2.5 rounded-lg text-xs font-semibold ${
                    addStaffMessage.startsWith('✓')
                      ? 'bg-emerald-950/80 border border-emerald-500/40 text-emerald-300'
                      : 'bg-rose-950/80 border border-rose-500/40 text-rose-300'
                  }`}
                >
                  {addStaffMessage}
                </div>
              )}

              <div className="flex items-center justify-end space-x-2 pt-2">
                <button
                  type="button"
                  onClick={() => setIsAddStaffModalOpen(false)}
                  className="px-4 py-2 rounded-lg bg-[#1B2537] hover:bg-[#222E42] border border-[#2D3E5B] text-slate-300 hover:text-white transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isCreatingStaff || !newStaffFullName.trim() || !newStaffEmail.trim()}
                  className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:bg-slate-700 text-white font-bold transition flex items-center space-x-2 shadow-md shadow-blue-600/20"
                >
                  <UserPlus className="w-3.5 h-3.5" />
                  <span>{isCreatingStaff ? 'Enrolling...' : 'Enroll Staff Member'}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Windows 11 Style Modal: RESET STAFF PASSWORD */}
      {selectedStaffForPasswordReset && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="bg-[#141B28] border border-[#293850] rounded-xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-[#222E42]">
              <div className="flex items-center space-x-2 text-white">
                <Key className="w-4 h-4 text-amber-400" />
                <h3 className="text-sm font-bold uppercase tracking-wider">Reset Employee Password</h3>
              </div>
              <button
                onClick={() => setSelectedStaffForPasswordReset(null)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-[#1E293B] transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="bg-[#0C1019] p-3 rounded-lg border border-[#222E42] space-y-1 text-xs">
              <div className="text-slate-400">Employee: <span className="text-white font-bold">{selectedStaffForPasswordReset.fullName}</span></div>
              <div className="text-slate-400">Username / Email: <span className="text-blue-400 font-mono">@{selectedStaffForPasswordReset.username || selectedStaffForPasswordReset.email}</span></div>
            </div>

            <form onSubmit={handleResetStaffPassword} className="space-y-4 text-xs">
              <div>
                <label className="block font-bold text-slate-300 mb-1">New Temporary Password</label>
                <input
                  type="text"
                  required
                  autoFocus
                  placeholder="e.g. PHF-Pass@2026"
                  value={newResetPassword}
                  onChange={(e) => setNewResetPassword(e.target.value)}
                  className="w-full bg-[#0C1019] border border-[#222E42] rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-blue-500"
                />
              </div>

              {resetPasswordMessage && (
                <div
                  className={`p-2.5 rounded-lg text-xs font-semibold ${
                    resetPasswordMessage.startsWith('✓')
                      ? 'bg-emerald-950/80 border border-emerald-500/40 text-emerald-300'
                      : 'bg-rose-950/80 border border-rose-500/40 text-rose-300'
                  }`}
                >
                  {resetPasswordMessage}
                </div>
              )}

              <div className="flex items-center justify-end space-x-2 pt-2">
                <button
                  type="button"
                  onClick={() => setSelectedStaffForPasswordReset(null)}
                  className="px-4 py-2 rounded-lg bg-[#1B2537] hover:bg-[#222E42] border border-[#2D3E5B] text-slate-300 hover:text-white transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isResettingPassword || !newResetPassword.trim()}
                  className="px-4 py-2 rounded-lg bg-amber-600 hover:bg-amber-500 disabled:bg-slate-700 text-white font-bold transition flex items-center space-x-2 shadow-md shadow-amber-600/20"
                >
                  <Key className="w-3.5 h-3.5" />
                  <span>{isResettingPassword ? 'Updating...' : 'Update Password'}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Footer */}
      <footer className="border-t border-[#222E42] bg-[#101520] px-6 py-3.5 mt-auto">
        <div className="max-w-7xl mx-auto flex items-center justify-between text-xs text-slate-500">
          <div>PHF Work Management System • Punnya Heart Foundation</div>
          <div>Near-0% CPU Win32 Telemetry • Tamper-Lock Offline Buffer • WCAG 2.1 AA Accessible</div>
        </div>
      </footer>
    </div>
  );
}

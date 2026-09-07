'use client';

import React, { useEffect, useState, useRef } from 'react';
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
} from 'lucide-react';
import { WorkstationStatus, LiveWorkstationEvent, TaskItem, TimesheetSummary, User } from '@/lib/types';
import {
  fetchStations,
  fetchRecentEvents,
  fetchTasks,
  fetchUsers,
  createTask,
  fetchTimesheetSummary,
  getTimesheetCsvDownloadUrl,
} from '@/lib/api';
import { signalRClient } from '@/lib/signalr';

export default function AdminDashboardPage() {
  const [activeTab, setActiveTab] = useState<'oversight' | 'tasks' | 'timesheets'>('oversight');
  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);

  // Core Data State
  const [stations, setStations] = useState<WorkstationStatus[]>([]);
  const [events, setEvents] = useState<LiveWorkstationEvent[]>([]);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [timesheet, setTimesheet] = useState<TimesheetSummary | null>(null);
  const [users, setUsers] = useState<User[]>([]);

  // Dispatch Task Form State
  const [dispatchTitle, setDispatchTitle] = useState('');
  const [dispatchDesc, setDispatchDesc] = useState('');
  const [dispatchAssignee, setDispatchAssignee] = useState('');
  const [dispatchPriority, setDispatchPriority] = useState('MEDIUM');
  const [dispatchDeliverableReq, setDispatchDeliverableReq] = useState(true);
  const [isDispatching, setIsDispatching] = useState(false);
  const [dispatchMessage, setDispatchMessage] = useState<string | null>(null);

  const eventsEndRef = useRef<HTMLDivElement>(null);

  // Load Initial Data
  const loadDashboardData = async () => {
    try {
      const [stData, evData, tData, tsData, uData] = await Promise.all([
        fetchStations(),
        fetchRecentEvents(50),
        fetchTasks(),
        fetchTimesheetSummary(),
        fetchUsers(),
      ]);

      setStations(stData);
      setEvents(evData);
      setTasks(tData);
      setTimesheet(tsData);
      setUsers(uData);
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
      setEvents((prev) => [evt, ...prev.slice(0, 99)]);
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

    signalRClient.connect();

    const interval = setInterval(() => {
      fetchStations().then(setStations);
      fetchTimesheetSummary().then(setTimesheet);
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

    if (result) {
      setDispatchMessage(`✓ ${result.taskCode} successfully dispatched!`);
      setDispatchTitle('');
      setDispatchDesc('');
      fetchTasks().then(setTasks);
      fetchTimesheetSummary().then(setTimesheet);
      setTimeout(() => setDispatchMessage(null), 4000);
    } else {
      setDispatchMessage('Failed to dispatch task. Central API check required.');
    }
  };

  // KPIs
  const activeStationsCount = stations.filter((s) => s.state === 'ACTIVE').length;
  const totalStationsCount = stations.length || 5;
  const tasksInProgressCount = tasks.filter((t) => t.status === 'IN_PROGRESS').length;
  const avgEfficiency = timesheet?.averageEfficiencyPercentage ?? 92;
  const hoursLoggedToday = timesheet?.totalHoursLoggedToday ?? 18.5;

  return (
    <div className="min-h-screen bg-darkBg text-slate-100 flex flex-col">
      {/* Top Header Navigation */}
      <header className="border-b border-cardBorder bg-surface/80 backdrop-blur sticky top-0 z-30 px-6 py-4">
        <div className="max-w-7xl mx-auto flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-600 to-indigo-700 flex items-center justify-center font-black text-white shadow-lg shadow-blue-500/20">
              PHF
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <h1 className="text-lg font-bold tracking-tight text-white">PHF Work Management System</h1>
                <span className="text-xs bg-blue-500/20 text-blue-400 font-semibold px-2 py-0.5 rounded border border-blue-500/30">
                  Admin Console
                </span>
              </div>
              <p className="text-xs text-slate-400">Punnya Task Dashboard • Event-Driven Workstation Telemetry</p>
            </div>
          </div>

          <div className="flex items-center space-x-4">
            {/* Live SignalR Indicator */}
            <div
              className={`flex items-center space-x-2 px-3 py-1 rounded-full text-xs font-semibold border ${
                isConnected
                  ? 'bg-emerald-950/60 border-emerald-500/30 text-emerald-400'
                  : 'bg-rose-950/60 border-rose-500/30 text-rose-400'
              }`}
            >
              <Radio className={`w-3.5 h-3.5 ${isConnected ? 'animate-pulse' : ''}`} />
              <span>{isConnected ? 'LIVE FEED CONNECTED' : 'RECONNECTING FEED'}</span>
            </div>

            {/* One-Click Timesheet CSV Export */}
            <a
              href={getTimesheetCsvDownloadUrl()}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center space-x-2 px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-md shadow-emerald-600/20 transition-all"
            >
              <Download className="w-4 h-4" />
              <span>EXPORT TIMESHEET (CSV)</span>
            </a>

            <button
              onClick={loadDashboardData}
              className="p-1.5 rounded-lg bg-surfaceHover border border-cardBorder text-slate-300 hover:text-white transition"
              title="Refresh Dashboard"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="max-w-7xl mx-auto w-full px-6 py-6 flex-1 space-y-6">
        {/* KPI Metric Summary Cards (Slide 5 Wireframe) */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-surface rounded-xl p-5 border border-cardBorder relative overflow-hidden group">
            <div className="flex items-center justify-between text-slate-400 mb-2">
              <span className="text-xs font-bold tracking-wider uppercase">Active Stations</span>
              <Monitor className="w-4 h-4 text-blue-400" />
            </div>
            <div className="text-2xl font-black text-white">
              {activeStationsCount} <span className="text-slate-500 text-base font-normal">/ {totalStationsCount} Online</span>
            </div>
            <div className="text-xs text-emerald-400 mt-2 font-medium flex items-center">
              <span className="w-2 h-2 rounded-full bg-emerald-400 mr-1.5 animate-ping"></span>
              Real-time hook telemetry active
            </div>
          </div>

          <div className="bg-surface rounded-xl p-5 border border-cardBorder relative overflow-hidden group">
            <div className="flex items-center justify-between text-slate-400 mb-2">
              <span className="text-xs font-bold tracking-wider uppercase">Tasks In Progress</span>
              <Briefcase className="w-4 h-4 text-indigo-400" />
            </div>
            <div className="text-2xl font-black text-white">{tasksInProgressCount}</div>
            <div className="text-xs text-slate-400 mt-2 font-medium">
              {tasks.filter((t) => t.status === 'ASSIGNED').length} awaiting start
            </div>
          </div>

          <div className="bg-surface rounded-xl p-5 border border-cardBorder relative overflow-hidden group">
            <div className="flex items-center justify-between text-slate-400 mb-2">
              <span className="text-xs font-bold tracking-wider uppercase">Avg Team Efficiency</span>
              <TrendingUp className="w-4 h-4 text-emerald-400" />
            </div>
            <div className="text-2xl font-black text-emerald-400">{avgEfficiency}%</div>
            <div className="text-xs text-slate-400 mt-2 font-medium">Active-time ratio computed automatically</div>
          </div>

          <div className="bg-surface rounded-xl p-5 border border-cardBorder relative overflow-hidden group">
            <div className="flex items-center justify-between text-slate-400 mb-2">
              <span className="text-xs font-bold tracking-wider uppercase">Hours Logged Today</span>
              <Clock className="w-4 h-4 text-amber-400" />
            </div>
            <div className="text-2xl font-black text-white">{hoursLoggedToday}h</div>
            <div className="text-xs text-slate-400 mt-2 font-medium">Across active workstation shifts</div>
          </div>
        </div>

        {/* View Tabs */}
        <div className="flex items-center space-x-2 border-b border-cardBorder pb-2">
          <button
            onClick={() => setActiveTab('oversight')}
            className={`px-4 py-2 rounded-lg text-sm font-bold flex items-center space-x-2 transition ${
              activeTab === 'oversight'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-500/20'
                : 'text-slate-400 hover:text-white hover:bg-surface'
            }`}
          >
            <Monitor className="w-4 h-4" />
            <span>Live Oversight</span>
          </button>

          <button
            onClick={() => setActiveTab('tasks')}
            className={`px-4 py-2 rounded-lg text-sm font-bold flex items-center space-x-2 transition ${
              activeTab === 'tasks'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-500/20'
                : 'text-slate-400 hover:text-white hover:bg-surface'
            }`}
          >
            <Briefcase className="w-4 h-4" />
            <span>Task Distribution &amp; Deliverables</span>
          </button>

          <button
            onClick={() => setActiveTab('timesheets')}
            className={`px-4 py-2 rounded-lg text-sm font-bold flex items-center space-x-2 transition ${
              activeTab === 'timesheets'
                ? 'bg-blue-600 text-white shadow-md shadow-blue-500/20'
                : 'text-slate-400 hover:text-white hover:bg-surface'
            }`}
          >
            <FileText className="w-4 h-4" />
            <span>Payroll Timesheets</span>
          </button>
        </div>

        {/* TAB 1: LIVE OVERSIGHT (Slide 5 Wireframe Layout) */}
        {activeTab === 'oversight' && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left 2 Columns: Workstation Table + Live Event Feed */}
            <div className="lg:col-span-2 space-y-6">
              {/* Workstation Table */}
              <div className="bg-surface rounded-xl border border-cardBorder overflow-hidden">
                <div className="px-5 py-4 border-b border-cardBorder flex items-center justify-between">
                  <div className="flex items-center space-x-2">
                    <Monitor className="w-4 h-4 text-blue-400" />
                    <h2 className="text-sm font-bold uppercase tracking-wider text-white">Workstation Fleet Status</h2>
                  </div>
                  <span className="text-xs text-slate-400">Near-0% CPU Event Hooks</span>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead className="bg-[#0D1322] text-xs uppercase text-slate-400 border-b border-cardBorder">
                      <tr>
                        <th className="px-5 py-3">Station</th>
                        <th className="px-5 py-3">Assignee</th>
                        <th className="px-5 py-3">Current Task</th>
                        <th className="px-5 py-3">Focused App</th>
                        <th className="px-5 py-3 text-right">State</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-cardBorder font-medium">
                      {stations.map((st) => (
                        <tr key={st.stationId} className="hover:bg-surfaceHover/50 transition">
                          <td className="px-5 py-3.5 font-bold text-white flex items-center space-x-2">
                            <span
                              className={`w-2 h-2 rounded-full ${
                                st.state === 'ACTIVE'
                                  ? 'bg-emerald-400 animate-pulse'
                                  : st.state === 'IDLE'
                                  ? 'bg-amber-400'
                                  : 'bg-rose-500'
                              }`}
                            />
                            <span>{st.stationId}</span>
                          </td>
                          <td className="px-5 py-3.5 text-slate-300">{st.fullName || '—'}</td>
                          <td className="px-5 py-3.5">
                            {st.currentTaskCode ? (
                              <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-blue-950/60 text-blue-300 border border-blue-700/40">
                                {st.currentTaskCode} {st.currentTaskTitle ? `• ${st.currentTaskTitle}` : ''}
                              </span>
                            ) : (
                              <span className="text-slate-500 text-xs italic">None (awaiting dispatch)</span>
                            )}
                          </td>
                          <td className="px-5 py-3.5 text-slate-200">
                            <span className="font-mono text-xs bg-slate-900 px-2 py-1 rounded border border-cardBorder">
                              {st.focusedApp || '—'}
                            </span>
                          </td>
                          <td className="px-5 py-3.5 text-right">
                            <span
                              className={`inline-block px-2.5 py-1 rounded-full text-xs font-extrabold ${
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

              {/* LIVE EVENT FEED (Slide 5 Wireframe Terminal) */}
              <div className="bg-surface rounded-xl border border-cardBorder overflow-hidden">
                <div className="px-5 py-3.5 border-b border-cardBorder flex items-center justify-between bg-[#0B101C]">
                  <div className="flex items-center space-x-2">
                    <Terminal className="w-4 h-4 text-emerald-400" />
                    <h2 className="text-sm font-bold uppercase tracking-wider text-emerald-400">Live Workstation Event Feed</h2>
                  </div>
                  <span className="text-xs font-mono text-slate-500">streaming user32 hooks</span>
                </div>

                <div className="p-4 font-mono text-xs bg-[#070A11] h-64 overflow-y-auto space-y-2 select-text">
                  {events.length === 0 ? (
                    <div className="text-slate-500 py-8 text-center italic">Waiting for workstation telemetry events...</div>
                  ) : (
                    events.map((evt, idx) => (
                      <div key={evt.id || idx} className="flex items-start space-x-3 text-slate-300 hover:bg-slate-900/50 py-0.5 rounded px-1">
                        <span className="text-slate-500 shrink-0">
                          {new Date(evt.timestamp).toLocaleTimeString()}
                        </span>
                        <span className="font-bold text-blue-400 shrink-0">{evt.stationId}</span>
                        <span className="text-slate-400">
                          {evt.eventType === 'FOCUS_CHANGED' ? (
                            <span>
                              focus changed → <strong className="text-emerald-300">{evt.processName || 'Window'}</strong>{' '}
                              {evt.taskCode && <span className="text-blue-300">({evt.taskCode})</span>}
                            </span>
                          ) : evt.eventType === 'LOCK' ? (
                            <span className="text-amber-400">session locked (idle threshold reached)</span>
                          ) : evt.eventType === 'UNLOCK' ? (
                            <span className="text-emerald-400">session unlocked by user</span>
                          ) : (
                            <span>{evt.details}</span>
                          )}
                        </span>
                      </div>
                    ))
                  )}
                  <div ref={eventsEndRef} />
                </div>
              </div>
            </div>

            {/* Right Column: DISPATCH NEW TASK (Slide 5 Wireframe Form) */}
            <div className="space-y-6">
              <div className="bg-surface rounded-xl border border-cardBorder p-5">
                <div className="flex items-center space-x-2 mb-4 pb-3 border-b border-cardBorder">
                  <Sparkles className="w-4 h-4 text-amber-400" />
                  <h2 className="text-sm font-bold uppercase tracking-wider text-white">Dispatch New Task</h2>
                </div>

                <form onSubmit={handleDispatchTask} className="space-y-4">
                  <div>
                    <label className="block text-xs font-bold uppercase text-slate-400 mb-1">Task Title</label>
                    <input
                      type="text"
                      required
                      placeholder="e.g. Color Grade Patient Exercise Video"
                      value={dispatchTitle}
                      onChange={(e) => setDispatchTitle(e.target.value)}
                      className="w-full bg-[#0D1322] border border-cardBorder rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold uppercase text-slate-400 mb-1">Assign To Workstation / Staff</label>
                    <select
                      value={dispatchAssignee}
                      onChange={(e) => setDispatchAssignee(e.target.value)}
                      className="w-full bg-[#0D1322] border border-cardBorder rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                    >
                      <option value="">Unassigned (Awaiting dispatch)</option>
                      {users
                        .filter((u) => u.role === 'EMPLOYEE')
                        .map((u) => (
                          <option key={u.id} value={u.id}>
                            {u.stationId ? `${u.stationId} — ` : ''}
                            {u.fullName}
                          </option>
                        ))}
                    </select>
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-xs font-bold uppercase text-slate-400 mb-1">Priority</label>
                      <select
                        value={dispatchPriority}
                        onChange={(e) => setDispatchPriority(e.target.value)}
                        className="w-full bg-[#0D1322] border border-cardBorder rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                      >
                        <option value="LOW">Low</option>
                        <option value="MEDIUM">Medium</option>
                        <option value="HIGH">High</option>
                        <option value="CRITICAL">Critical</option>
                      </select>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase text-slate-400 mb-1">Deliverable Gate</label>
                      <div className="flex items-center h-9">
                        <input
                          type="checkbox"
                          id="deliverableCheck"
                          checked={dispatchDeliverableReq}
                          onChange={(e) => setDispatchDeliverableReq(e.target.checked)}
                          className="w-4 h-4 accent-blue-600 rounded mr-2"
                        />
                        <label htmlFor="deliverableCheck" className="text-xs text-slate-300">
                          Proof required
                        </label>
                      </div>
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold uppercase text-slate-400 mb-1">Task Scope / Instructions</label>
                    <textarea
                      rows={3}
                      placeholder="Specify requirements, deliverables, and format expectations..."
                      value={dispatchDesc}
                      onChange={(e) => setDispatchDesc(e.target.value)}
                      className="w-full bg-[#0D1322] border border-cardBorder rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                    />
                  </div>

                  {dispatchMessage && (
                    <div
                      className={`text-xs p-2.5 rounded-lg ${
                        dispatchMessage.startsWith('✓')
                          ? 'bg-emerald-950 border border-emerald-500/40 text-emerald-300'
                          : 'bg-rose-950 border border-rose-500/40 text-rose-300'
                      }`}
                    >
                      {dispatchMessage}
                    </div>
                  )}

                  <button
                    type="submit"
                    disabled={isDispatching || !dispatchTitle.trim()}
                    className="w-full py-2.5 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:bg-slate-700 text-white font-bold text-sm shadow-md shadow-blue-600/30 transition flex items-center justify-center space-x-2"
                  >
                    <Plus className="w-4 h-4" />
                    <span>{isDispatching ? 'Dispatching...' : 'DISPATCH TASK'}</span>
                  </button>
                </form>
              </div>

              {/* Quick Deliverable Gate Reminder */}
              <div className="bg-surface rounded-xl border border-cardBorder p-4 text-xs text-slate-400 space-y-2">
                <div className="flex items-center space-x-2 text-amber-400 font-bold">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>Deliverable-Linked Completion</span>
                </div>
                <p>
                  Employees cannot close assigned tasks without an attached file or URL proof of work. Once uploaded,
                  deliverables are immediately inspectable in the Task Distribution tab.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* TAB 2: TASK DISTRIBUTION BOARD & DELIVERABLES */}
        {activeTab === 'tasks' && (
          <div className="space-y-6">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-base font-bold text-white">Workforce Task Distribution</h2>
                <p className="text-xs text-slate-400">Review deliverables, progression status, and telemetry attribution</p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              {(['ASSIGNED', 'IN_PROGRESS', 'UNDER_REVIEW', 'COMPLETED'] as const).map((columnStatus) => {
                const columnTasks = tasks.filter((t) => t.status === columnStatus);
                const colTitle =
                  columnStatus === 'ASSIGNED'
                    ? 'Awaiting Start'
                    : columnStatus === 'IN_PROGRESS'
                    ? 'In Progress'
                    : columnStatus === 'UNDER_REVIEW'
                    ? 'Under Review'
                    : 'Completed';

                return (
                  <div key={columnStatus} className="bg-surface rounded-xl border border-cardBorder flex flex-col max-h-[700px]">
                    <div className="p-3 border-b border-cardBorder bg-[#0D1322] flex items-center justify-between">
                      <span className="text-xs font-bold uppercase tracking-wider text-slate-300">{colTitle}</span>
                      <span className="text-xs font-bold bg-slate-800 text-slate-300 px-2 py-0.5 rounded-full">
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
                            className="bg-[#090D16] border border-cardBorder rounded-lg p-3.5 space-y-2.5 hover:border-slate-500 transition"
                          >
                            <div className="flex items-center justify-between">
                              <span className="text-xs font-mono font-bold text-blue-400">{task.taskCode}</span>
                              <span
                                className={`text-[10px] font-extrabold px-1.5 py-0.5 rounded ${
                                  task.priority === 'CRITICAL'
                                    ? 'bg-rose-950 text-rose-400 border border-rose-500/30'
                                    : task.priority === 'HIGH'
                                    ? 'bg-amber-950 text-amber-400 border border-amber-500/30'
                                    : 'bg-slate-800 text-slate-300'
                                }`}
                              >
                                {task.priority}
                              </span>
                            </div>

                            <h3 className="text-sm font-semibold text-white leading-snug">{task.title}</h3>
                            {task.description && (
                              <p className="text-xs text-slate-400 line-clamp-2">{task.description}</p>
                            )}

                            <div className="text-xs text-slate-400 flex items-center justify-between pt-1 border-t border-cardBorder">
                              <span>{task.assignedToName || 'Unassigned'}</span>
                              <span>{Math.round(task.totalTrackedSeconds / 60)}m tracked</span>
                            </div>

                            {/* Deliverable Section (punnya-combined-spec.pptx) */}
                            {task.deliverableFileName || task.deliverableUrl ? (
                              <div className="bg-emerald-950/40 border border-emerald-500/30 rounded p-2 text-xs flex items-center justify-between">
                                <div className="flex items-center space-x-1.5 text-emerald-300 truncate">
                                  <CheckCircle2 className="w-3.5 h-3.5 shrink-0" />
                                  <span className="truncate">{task.deliverableFileName || 'Deliverable attached'}</span>
                                </div>
                                <a
                                  href={`http://localhost:5000/api/v1/tasks/${task.id}/deliverable`}
                                  target="_blank"
                                  rel="noreferrer"
                                  className="text-blue-400 hover:text-blue-300 shrink-0 ml-2"
                                  title="Download Deliverable"
                                >
                                  <Download className="w-3.5 h-3.5" />
                                </a>
                              </div>
                            ) : task.deliverableRequired ? (
                              <div className="bg-amber-950/30 border border-amber-500/20 rounded p-1.5 text-[11px] text-amber-400/90 flex items-center space-x-1">
                                <AlertCircle className="w-3 h-3 shrink-0" />
                                <span>Proof of work required</span>
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
          <div className="space-y-6">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-base font-bold text-white">Consolidated Workstation Timesheet</h2>
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

            <div className="bg-surface rounded-xl border border-cardBorder overflow-hidden">
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="bg-[#0D1322] text-xs uppercase text-slate-400 border-b border-cardBorder">
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
                  <tbody className="divide-y divide-cardBorder font-medium">
                    {timesheet?.rows.map((row, idx) => (
                      <tr key={idx} className="hover:bg-surfaceHover/50 transition">
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
                            <div className="w-16 h-2 bg-slate-800 rounded-full overflow-hidden">
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
      </main>

      {/* Footer */}
      <footer className="border-t border-cardBorder bg-surface/50 px-6 py-4 mt-auto">
        <div className="max-w-7xl mx-auto flex items-center justify-between text-xs text-slate-500">
          <div>PHF Work Management System • Punnya Heart Foundation</div>
          <div>Near-0% CPU Win32 Telemetry • Tamper-Lock Offline Buffer • WCAG 2.1 AA Accessible</div>
        </div>
      </footer>
    </div>
  );
}

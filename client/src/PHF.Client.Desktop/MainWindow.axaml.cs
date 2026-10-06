using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PHF.Client.Core.Http;
using PHF.Client.Core.Realtime;
using PHF.Client.Core.Storage;
using PHF.Client.Core.Sync;
using PHF.Client.Core.Telemetry;
using PHF.Client.Desktop.Telemetry;
using PHF.Client.Desktop.ViewModels;
using PHF.Client.Desktop.Utils;
using PHF.Core.DTOs;
using PHF.Core.Enums;

namespace PHF.Client.Desktop;

public partial class MainWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly TamperProofDatabase _database;
    private readonly ITelemetryEngine _engine;
    private readonly TelemetrySyncWorker _syncWorker;
    private readonly RealtimeClient _realtimeClient;

    private UserProfileDto? _currentUser;
    private string _stationId = "WFH-WORKSTATION";
    private Guid? _activeTaskId = null;
    private string? _activeTaskCode = null;
    private Guid? _modalTargetTaskId = null;
    private string? _stagedFilePath = null;
    private Guid? _reviewTargetLeaveId = null;
    private string _currentTab = "MY_DESK";

    private readonly ObservableCollection<TaskItemViewModel> _tasks = new();
    private readonly ObservableCollection<LeaveItemViewModel> _leaves = new();
    private readonly ObservableCollection<AdminWorkstationItemViewModel> _workforce = new();
    private readonly ObservableCollection<AdminTaskItemViewModel> _allTasks = new();
    private readonly ObservableCollection<AdminTimesheetRowViewModel> _timesheets = new();
    private readonly ObservableCollection<AdminLeaveApprovalViewModel> _adminLeaves = new();
    private readonly ObservableCollection<AdminStaffMemberViewModel> _allStaff = new();
    private readonly ObservableCollection<ParsedCsvTask> _parsedTasks = new();
    private readonly ObservableCollection<TaskBackupDto> _taskBackups = new();
    private Guid? _activeReminderBackupId = null;
    private int? _activeReminderMilestone = null;
    private List<StaffMemberDto> _staffMembers = new();
    private List<TaskResponseDto> _rawAdminTasks = new();
    private string _taskStatusFilter = "ALL";
    private string _taskSearchQuery = string.Empty;
    private string _staffStatusFilter = "ALL";
    private string _staffSearchQuery = string.Empty;
    private string _timesheetPeriod = "today";
    private Guid? _editStaffTargetId = null;
    private Guid? _resetPasswordTargetId = null;

    private DispatcherTimer? _autoRefreshTimer;
    private DispatcherTimer? _toastDismissTimer;
    private DispatcherTimer? _shiftTimer;
    private DateTime? _shiftStartTime;

    private bool IsAdminOrManager => _currentUser != null &&
        (_currentUser.Role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
         _currentUser.Role.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

    public MainWindow()
    {
        InitializeComponent();

        TasksItemsControl.ItemsSource = _tasks;
        LeavesItemsControl.ItemsSource = _leaves;
        WorkforceItemsControl.ItemsSource = _workforce;
        AllTasksItemsControl.ItemsSource = _allTasks;
        TimesheetsItemsControl.ItemsSource = _timesheets;
        LeaveApprovalsItemsControl.ItemsSource = _adminLeaves;
        StaffItemsControl.ItemsSource = _allStaff;
        BulkImportItemsControl.ItemsSource = _parsedTasks;
        TaskBackupsItemsControl.ItemsSource = _taskBackups;

        var defaultUrl = Environment.GetEnvironmentVariable("PHF_API_URL") ?? "https://punnya-api.onrender.com";
        _apiClient = new ApiClient(defaultUrl);
        _database = new TamperProofDatabase();

        // Platform-Adaptive Telemetry Engine: Windows Win32 vs macOS Cocoa/CoreGraphics
        _engine = TelemetryEngineFactory.Create(idleThresholdSeconds: 300);
        _syncWorker = new TelemetrySyncWorker(_database, _apiClient, _engine, syncIntervalSeconds: 30);
        _realtimeClient = new RealtimeClient(defaultUrl, () => _apiClient.AuthToken);

        // Hook engine telemetry updates
        _engine.FocusChanged += OnForegroundFocusChanged;
        _syncWorker.OnBatchSynced += OnBatchSynced;
        _realtimeClient.OnTaskAssigned += OnRealtimeTaskAssigned;
        _realtimeClient.OnTaskStatusChanged += OnRealtimeTaskStatusChanged;
        _realtimeClient.OnWorkstationEventReceived += OnRealtimeWorkstationEventReceived;
        _realtimeClient.OnLeaveUpdated += OnRealtimeLeaveUpdated;
        _realtimeClient.OnConnectionStateChanged += OnRealtimeConnectionChanged;

        Opened += MainWindow_Opened;
        Closing += MainWindow_Closing;
        PropertyChanged += (sender, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                UpdateMaximizeButtonIcon();
            }
        };
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        try
        {
            ApplyPlatformChromeLayout();
            UpdateStationIdentifierFromWorkMode();

            // Load saved workstation credentials if remembered
            var (savedUser, savedPass, remember) = CredentialStorage.LoadCredentials();
            if (remember && !string.IsNullOrEmpty(savedUser))
            {
                TxtLoginEmail.Text = savedUser;
                TxtLoginPassword.Text = savedPass;
                ChkRememberCredentials.IsChecked = true;
            }
            else
            {
                TxtLoginEmail.Text = string.Empty;
                TxtLoginPassword.Text = string.Empty;
                ChkRememberCredentials.IsChecked = true;
            }

            if (!_apiClient.IsAuthenticated)
            {
                LoginModal.IsVisible = true;
            }
            else
            {
                await InitializeWorkstationSessionAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow_Opened] Error: {ex.Message}");
        }
    }

    private void ApplyPlatformChromeLayout()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MacTrafficLightsStack.IsVisible = false;
            WindowsCaptionButtons.IsVisible = true;
            HeaderLeftStack.Margin = new Avalonia.Thickness(4, 0, 0, 0);
            HeaderRightStack.Margin = new Avalonia.Thickness(0, 0, 0, 0);
            TxtPlatformEdition.Text = "Windows 11 Mica";
            TxtTelemetryHook.Text = "Win32 & System Hook";
            Title = "Punnya Heart Desk — Workstation Desk & Management Console";
        }
        else
        {
            MacTrafficLightsStack.IsVisible = true;
            WindowsCaptionButtons.IsVisible = false;
            HeaderLeftStack.Margin = new Avalonia.Thickness(4, 0, 0, 0);
            HeaderRightStack.Margin = new Avalonia.Thickness(0, 0, 8, 0);
            TxtPlatformEdition.Text = "macOS Edition";
            TxtTelemetryHook.Text = "macOS CoreGraphics & Cocoa";
            Title = "Punnya Heart Desk — macOS Console";
        }
        UpdateMaximizeButtonIcon();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TitleBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleWindowState();
    }

    private void BtnTitleMinimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnTitleMaximize_Click(object? sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void BtnTitleClose_Click(object? sender, RoutedEventArgs e)
    {
        PerformFullAppShutdown();
    }

    private void MacClose_Click(object? sender, RoutedEventArgs e)
    {
        PerformFullAppShutdown();
    }

    private void MacMinimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MacMaximize_Click(object? sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButtonIcon();
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (TxtMaximizeIcon != null)
        {
            TxtMaximizeIcon.Text = WindowState == WindowState.Maximized ? "❐" : "□";
        }
    }

    private void UpdateStationIdentifierFromWorkMode()
    {
        var machine = Environment.MachineName.ToUpper();
        if (ChkWorkFromHome.IsChecked == true)
        {
            var cleanMachine = machine.Length > 12 ? machine[..12] : machine;
            _stationId = $"WFH-{cleanMachine}";
        }
        else
        {
            _stationId = machine;
        }

        TxtDevicePreview.Text = $"Paired Device: {_stationId}";
        TxtLoginStation.Text = _stationId;
    }

    private async Task InitializeWorkstationSessionAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser?.StationId))
        {
            _stationId = _currentUser.StationId;
        }

        var osName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "macOS";
        TxtUserInfo.Text = _currentUser?.FullName ?? $"{osName} Station";
        _engine.Start();

        _syncWorker.ConfigureUser(
            _currentUser?.Id.ToString() ?? Guid.NewGuid().ToString(),
            _currentUser?.FullName ?? $"{osName} Employee",
            _stationId);
        _syncWorker.Start();

        try
        {
            await _realtimeClient.StartAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Realtime] Initial start failed: {ex.Message}");
        }

        // Configure Role-Based Views
        AdminNavBarBorder.IsVisible = true;
        RoleBadgeBorder.IsVisible = true;
        TxtUserRoleBadge.Text = _currentUser?.Role.ToUpper() ?? "STAFF";

        BtnTabWorkforce.IsVisible = IsAdminOrManager;
        BtnTabTasks.IsVisible = IsAdminOrManager;
        BtnTabStaff.IsVisible = IsAdminOrManager;
        BtnAdminDispatchQuick.IsVisible = IsAdminOrManager;

        if (IsAdminOrManager)
        {
            BtnTabTimesheets.Content = "⏱️ Timesheets";
            BtnTabLeaves.Content = "🏖️ Leave Approvals";
            TxtTsHeaderTitle.Text = "Workforce Timesheets & Efficiency KPIs";
            TxtTsHeaderSubtitle.Text = "Itemized employee work shifts, active focus scoring, and automated compliance";
            TxtTsCard1Title.Text = "ACTIVE STATIONS";
            await LoadStaffDirectoryAsync(silent: true);
        }
        else
        {
            BtnTabTimesheets.Content = "⏱️ My Timesheet";
            BtnTabLeaves.Content = "🏖️ Leaves Center";
            TxtTsHeaderTitle.Text = "My Personal Timesheet & Productivity Log";
            TxtTsHeaderSubtitle.Text = "Review your daily clock in/out logs, active vs idle hours, and tasks completed";
            TxtTsCard1Title.Text = "MY WORKSTATION";
            await SwitchToTabAsync("MY_DESK");
        }

        await LoadAssignedTasksAsync();

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _autoRefreshTimer.Tick += async (_, _) =>
        {
            if (_currentTab == "MY_DESK")
            {
                await LoadAssignedTasksAsync(silent: true);
            }
            else if (_currentTab == "WORKFORCE")
            {
                await LoadWorkforceFleetAsync(silent: true);
            }
        };
        _autoRefreshTimer.Start();
    }

    #region Tab Navigation

    private async void BtnTabMyDesk_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("MY_DESK");
    private async void BtnTabWorkforce_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("WORKFORCE");
    private async void BtnTabTasks_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("TASKS");
    private async void BtnTabTimesheets_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("TIMESHEETS");
    private async void BtnOpenMyTimesheet_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("TIMESHEETS");
    private async void BtnTabLeaves_Click(object? sender, RoutedEventArgs e)
    {
        if (IsAdminOrManager)
        {
            await SwitchToTabAsync("LEAVES");
        }
        else
        {
            BtnOpenLeaveCenter_Click(sender, e);
        }
    }
    private async void BtnTabStaff_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("STAFF");

    private async Task SwitchToTabAsync(string tabName)
    {
        _currentTab = tabName;

        // Reset tab button highlight styles
        SetTabButtonStyle(BtnTabMyDesk, tabName == "MY_DESK");
        SetTabButtonStyle(BtnTabWorkforce, tabName == "WORKFORCE");
        SetTabButtonStyle(BtnTabTasks, tabName == "TASKS");
        SetTabButtonStyle(BtnTabTimesheets, tabName == "TIMESHEETS");
        SetTabButtonStyle(BtnTabLeaves, tabName == "LEAVES");
        SetTabButtonStyle(BtnTabStaff, tabName == "STAFF");

        // Toggle Content Visibility
        ViewMyDesk.IsVisible = tabName == "MY_DESK";
        ViewWorkforce.IsVisible = tabName == "WORKFORCE";
        ViewTaskDispatch.IsVisible = tabName == "TASKS";
        ViewTimesheets.IsVisible = tabName == "TIMESHEETS";
        ViewLeaveApprovals.IsVisible = tabName == "LEAVES";
        ViewStaffDirectory.IsVisible = tabName == "STAFF";

        // Load data on demand
        switch (tabName)
        {
            case "MY_DESK":
                await LoadAssignedTasksAsync(silent: true);
                break;
            case "WORKFORCE":
                await LoadWorkforceFleetAsync();
                break;
            case "TASKS":
                await LoadAllAdminTasksAsync();
                break;
            case "TIMESHEETS":
                await LoadTimesheetSummaryAsync();
                break;
            case "LEAVES":
                await LoadAdminLeavesAsync();
                break;
            case "STAFF":
                await LoadStaffDirectoryAsync();
                break;
        }
    }

    private static void SetTabButtonStyle(Button btn, bool isActive)
    {
        btn.Classes.Clear();
        btn.Classes.Add(isActive ? "tabBtnActive" : "tabBtn");
    }

    #endregion

    #region Telemetry & Realtime Callbacks

    private void OnForegroundFocusChanged(string process, string title)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                TxtActiveApp.Text = $"Active App: {process}";
                TxtStatusBar.Text = $"Focus: {title} ({DateTime.Now:HH:mm:ss})";
            }
            catch { }
        });
    }

    private void OnBatchSynced(int count, DateTime syncedAt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                TxtSyncInfo.Text = $"Synced {count} records ({syncedAt:HH:mm:ss})";
            }
            catch { }
        });
    }

    private void OnRealtimeConnectionChanged(bool isConnected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (isConnected)
                {
                    TxtConnectionStatus.Text = "LIVE CONNECTED";
                    TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                }
                else
                {
                    TxtConnectionStatus.Text = "RECONNECTING...";
                    TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Error handling OnRealtimeConnectionChanged: {ex.Message}");
            }
        });
    }

    private void OnRealtimeTaskAssigned(TaskResponseDto task)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (task == null) return;
                var title = string.IsNullOrWhiteSpace(task.Title) ? "New Task" : task.Title;
                ShowToast("📋 New Task Assigned", $"You have been assigned: {title}");
                await LoadAssignedTasksAsync(silent: true);
                if (IsAdminOrManager)
                {
                    await LoadAllAdminTasksAsync(silent: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Error handling OnRealtimeTaskAssigned: {ex.Message}");
            }
        });
    }

    private void OnRealtimeTaskStatusChanged(Guid taskId, string newStatus, string? deliverableUrl)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await LoadAssignedTasksAsync(silent: true);
                if (IsAdminOrManager)
                {
                    await LoadAllAdminTasksAsync(silent: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Error handling OnRealtimeTaskStatusChanged: {ex.Message}");
            }
        });
    }

    private void OnRealtimeWorkstationEventReceived(LiveWorkstationEventDto evt)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (evt == null) return;

                if (evt.EventType != null && evt.EventType.StartsWith("LEAVE_"))
                {
                    await LoadLeaveHistoryAsync();
                    if (IsAdminOrManager)
                    {
                        await LoadAdminLeavesAsync(silent: true);
                    }
                    ShowToast("🏖️ Leave Request", evt.Details ?? "Leave update received");
                }
                else
                {
                    await LoadAssignedTasksAsync(silent: true);
                    if (IsAdminOrManager && _currentTab == "WORKFORCE")
                    {
                        await LoadWorkforceFleetAsync(silent: true);
                    }
                }
                TxtStatusBar.Text = $"Event: {evt.EventType} • {evt.Details}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Error handling OnRealtimeWorkstationEventReceived: {ex.Message}");
            }
        });
    }

    private void OnRealtimeLeaveUpdated(LiveWorkstationEventDto evt)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (evt == null) return;
                ShowToast("🏖️ Leave Request Reviewed", evt.Details ?? "Your leave request was reviewed.");
                if (LeaveCenterModal.IsVisible)
                {
                    await LoadLeaveHistoryAsync();
                }
                if (IsAdminOrManager && _currentTab == "LEAVES")
                {
                    await LoadAdminLeavesAsync(silent: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Realtime] Error handling OnRealtimeLeaveUpdated: {ex.Message}");
            }
        });
    }

    #endregion

    #region Data Loading Methods

    private async Task LoadAssignedTasksAsync(bool silent = false)
    {
        try
        {
            var tasks = await _apiClient.GetAssignedTasksAsync(_currentUser?.Id, _stationId, includeUnassigned: true);

            _tasks.Clear();
            foreach (var t in tasks)
            {
                bool isActive = _activeTaskId == t.Id;
                _tasks.Add(new TaskItemViewModel(t, isActive));
            }

            if (!silent)
            {
                TxtStatusBar.Text = $"Tasks loaded ({_tasks.Count} active) at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tasks] Load error: {ex.Message}");
            if (!silent)
            {
                TxtStatusBar.Text = "Connected to local workstation desk.";
            }
        }
    }

    private async Task LoadWorkforceFleetAsync(bool silent = false)
    {
        try
        {
            var stations = await _apiClient.GetWorkstationsStatusAsync();
            _workforce.Clear();

            int activeCount = 0;
            int idleCount = 0;

            foreach (var s in stations)
            {
                _workforce.Add(new AdminWorkstationItemViewModel(s));
                if (s.State.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) activeCount++;
                else if (s.State.Equals("IDLE", StringComparison.OrdinalIgnoreCase)) idleCount++;
            }

            TxtFleetActiveCount.Text = activeCount.ToString();
            TxtFleetIdleCount.Text = idleCount.ToString();
            TxtFleetTotalCount.Text = stations.Count.ToString();

            if (!silent)
            {
                TxtStatusBar.Text = $"Fleet telemetry updated ({stations.Count} workstations) at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Workforce] Error: {ex.Message}");
        }
    }

    private async Task LoadAllAdminTasksAsync(bool silent = false)
    {
        try
        {
            var tasks = await _apiClient.GetAllTasksAsync();
            _rawAdminTasks = tasks;
            ApplyAdminTaskFilter();
            UpdateSelectedTasksCount();
            await CheckBackupRemindersAsync();

            if (!silent)
            {
                TxtStatusBar.Text = $"Workforce tasks updated ({tasks.Count} tasks) at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AllTasks] Error: {ex.Message}");
        }
    }

    private void ApplyAdminTaskFilter()
    {
        var filtered = _rawAdminTasks.AsEnumerable();

        if (_taskStatusFilter != "ALL")
        {
            filtered = filtered.Where(t => string.Equals(t.Status, _taskStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_taskSearchQuery))
        {
            var q = _taskSearchQuery.Trim();
            filtered = filtered.Where(t =>
                (t.Title != null && t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.TaskCode != null && t.TaskCode.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.AssignedToName != null && t.AssignedToName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.Description != null && t.Description.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var list = filtered.ToList();
        _allTasks.Clear();
        foreach (var t in list)
        {
            _allTasks.Add(new AdminTaskItemViewModel(t));
        }

        TxtAdminTasksCount.Text = _rawAdminTasks.Count == list.Count
            ? $"All Workforce Tasks ({list.Count} total)"
            : $"Filtered Tasks ({list.Count} of {_rawAdminTasks.Count} total)";
    }

    private void TxtAdminTaskSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _taskSearchQuery = TxtAdminTaskSearch.Text ?? string.Empty;
        ApplyAdminTaskFilter();
    }

    private void BtnFilterTaskStatus_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string status)
        {
            _taskStatusFilter = status;

            BtnFilterTaskAll.Classes.Clear();
            BtnFilterTaskAll.Classes.Add(_taskStatusFilter == "ALL" ? "primary" : "glass");

            BtnFilterTaskInProgress.Classes.Clear();
            BtnFilterTaskInProgress.Classes.Add(_taskStatusFilter == "IN_PROGRESS" ? "primary" : "glass");

            BtnFilterTaskAssigned.Classes.Clear();
            BtnFilterTaskAssigned.Classes.Add(_taskStatusFilter == "ASSIGNED" ? "primary" : "glass");

            BtnFilterTaskCompleted.Classes.Clear();
            BtnFilterTaskCompleted.Classes.Add(_taskStatusFilter == "COMPLETED" ? "primary" : "glass");

            ApplyAdminTaskFilter();
        }
    }

    private void ChkAdminTaskItem_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        UpdateSelectedTasksCount();
    }

    private void ChkAdminSelectAllTasks_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        var isChecked = ChkAdminSelectAllTasks.IsChecked == true;
        foreach (var task in _allTasks)
        {
            task.IsSelected = isChecked;
        }
        UpdateSelectedTasksCount();
    }

    private void UpdateSelectedTasksCount()
    {
        var count = _allTasks.Count(t => t.IsSelected);
        if (count > 0)
        {
            BtnDeleteSelectedTasks.Content = $"🗑️ Delete Selected ({count})";
            BtnDeleteSelectedTasks.IsVisible = true;
        }
        else
        {
            BtnDeleteSelectedTasks.IsVisible = false;
        }
    }

    private async void BtnDeleteSelectedTasks_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _allTasks.Where(t => t.IsSelected).Select(t => t.Id).ToList();
        if (selected.Count == 0) return;

        try
        {
            BtnDeleteSelectedTasks.IsEnabled = false;
            var res = await _apiClient.BulkDeleteTasksAsync(selected);
            ShowToast("🗑️ Tasks Deleted", $"Successfully deleted {res?.DeletedCount ?? selected.Count} selected tasks.");
            await LoadAllAdminTasksAsync(silent: true);
            await LoadAssignedTasksAsync(silent: true);
        }
        catch (Exception ex)
        {
            ShowToast("⚠️ Delete Failed", ex.Message);
        }
        finally
        {
            BtnDeleteSelectedTasks.IsEnabled = true;
            UpdateSelectedTasksCount();
        }
    }

    private void BtnOpenDeleteAllModal_Click(object? sender, RoutedEventArgs e)
    {
        TxtDeleteAllWarningMessage.Text = $"Are you sure you want to delete ALL {_allTasks.Count} tasks across all staff members? An automated 30-day backup snapshot will be preserved.";
        TxtDeleteAllStatus.Text = string.Empty;
        DeleteAllTasksModal.IsVisible = true;
    }

    private void BtnCloseDeleteAllModal_Click(object? sender, RoutedEventArgs e)
    {
        DeleteAllTasksModal.IsVisible = false;
    }

    private async void BtnConfirmDeleteAll_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            BtnConfirmDeleteAll.IsEnabled = false;
            TxtDeleteAllStatus.Text = "Creating backup snapshot and purging tasks...";

            var res = await _apiClient.DeleteAllTasksAsync();
            DeleteAllTasksModal.IsVisible = false;
            ShowToast("📦 All Tasks Deleted", $"Deleted {res?.DeletedCount ?? 0} tasks. A 30-day backup snapshot was created.");

            await LoadAllAdminTasksAsync(silent: true);
            await LoadAssignedTasksAsync(silent: true);
            await CheckBackupRemindersAsync();
        }
        catch (Exception ex)
        {
            TxtDeleteAllStatus.Text = $"Error: {ex.Message}";
            ShowToast("⚠️ Delete All Failed", ex.Message);
        }
        finally
        {
            BtnConfirmDeleteAll.IsEnabled = true;
        }
    }

    private async void BtnOpenBackupsModal_Click(object? sender, RoutedEventArgs e)
    {
        TaskBackupsModal.IsVisible = true;
        await LoadBackupsAsync();
    }

    private void BtnCloseBackupsModal_Click(object? sender, RoutedEventArgs e)
    {
        TaskBackupsModal.IsVisible = false;
    }

    private async Task LoadBackupsAsync()
    {
        try
        {
            var backups = await _apiClient.GetTaskBackupsAsync();
            _taskBackups.Clear();
            foreach (var b in backups)
            {
                _taskBackups.Add(b);
            }
            TxtEmptyBackups.IsVisible = _taskBackups.Count == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Backups] Error loading: {ex.Message}");
        }
    }

    private async void BtnRestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid backupId)
        {
            try
            {
                btn.IsEnabled = false;
                var res = await _apiClient.RestoreTaskBackupAsync(backupId);
                ShowToast("🎉 Backup Restored", $"Successfully restored {res?.RestoredCount ?? 0} tasks back into the system!");
                TaskBackupsModal.IsVisible = false;
                await LoadAllAdminTasksAsync(silent: true);
                await LoadAssignedTasksAsync(silent: true);
                await CheckBackupRemindersAsync();
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Restore Failed", ex.Message);
                btn.IsEnabled = true;
            }
        }
    }

    private async Task CheckBackupRemindersAsync()
    {
        try
        {
            var backups = await _apiClient.GetTaskBackupsAsync();
            var active = backups.FirstOrDefault(b => !b.IsRestored && !b.IsExpired && b.ActiveReminderMilestone.HasValue);
            if (active?.ActiveReminderMilestone is int milestone)
            {
                _activeReminderBackupId = active.Id;
                _activeReminderMilestone = milestone;
                TxtReminderTitle.Text = $"Task Backup Retention Reminder (Day {milestone} of 30)";
                TxtReminderSubtitle.Text = $"Snapshot contains {active.TaskCount} deleted tasks (created on {active.CreatedAt:yyyy-MM-dd HH:mm} UTC). Expiring in {active.RemainingDays} days. Would you like to restore them?";
                AdminBackupReminderBanner.IsVisible = true;
            }
            else
            {
                AdminBackupReminderBanner.IsVisible = false;
                _activeReminderBackupId = null;
                _activeReminderMilestone = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Reminder] Error: {ex.Message}");
        }
    }

    private async void BtnReminderRestore_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeReminderBackupId.HasValue)
        {
            try
            {
                var res = await _apiClient.RestoreTaskBackupAsync(_activeReminderBackupId.Value);
                ShowToast("🎉 Tasks Restored", $"Restored {res?.RestoredCount ?? 0} tasks from backup!");
                AdminBackupReminderBanner.IsVisible = false;
                await LoadAllAdminTasksAsync(silent: true);
                await LoadAssignedTasksAsync(silent: true);
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Restore Failed", ex.Message);
            }
        }
    }

    private async void BtnReminderDismiss_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeReminderBackupId.HasValue && _activeReminderMilestone.HasValue)
        {
            try
            {
                await _apiClient.DismissBackupReminderAsync(_activeReminderBackupId.Value, _activeReminderMilestone.Value);
            }
            catch { }
        }
        AdminBackupReminderBanner.IsVisible = false;
    }

    private async void BtnDeleteAdminTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            try
            {
                var ok = await _apiClient.DeleteTaskAsync(taskId);
                if (ok)
                {
                    ShowToast("🗑 Task Deleted", "Task removed from workforce board.");
                    await LoadAllAdminTasksAsync(silent: true);
                    await LoadAssignedTasksAsync(silent: true);
                }
                else
                {
                    ShowToast("⚠️ Delete Failed", "Could not remove task.");
                }
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Delete Failed", ex.Message);
            }
        }
    }

    private Guid? _activeEditingTaskId;

    private void BtnEditAdminTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var taskVm = _allTasks.FirstOrDefault(t => t.Id == taskId);
            if (taskVm == null) return;

            _activeEditingTaskId = taskId;
            TxtEditTaskCodeDisplay.Text = taskVm.TaskCode;
            TxtEditTaskTitle.Text = taskVm.Title;
            TxtEditTaskDescription.Text = taskVm.Description == "—" ? "" : taskVm.Description;

            // Populate assignee combobox
            CmbEditTaskAssignee.Items.Clear();
            CmbEditTaskAssignee.Items.Add(new ComboBoxItem { Content = "Unassigned", Tag = null });
            int selectIdx = 0;
            int curIdx = 1;

            foreach (var staff in _allStaff)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{staff.FullName} ({staff.Email})",
                    Tag = staff.Id
                };
                CmbEditTaskAssignee.Items.Add(item);
                if (staff.FullName.Equals(taskVm.AssigneeName, StringComparison.OrdinalIgnoreCase))
                {
                    selectIdx = curIdx;
                }
                curIdx++;
            }
            CmbEditTaskAssignee.SelectedIndex = selectIdx;

            // Priority
            foreach (var item in CmbEditTaskPriority.Items.OfType<ComboBoxItem>())
            {
                if ((string?)item.Tag == taskVm.Priority)
                {
                    CmbEditTaskPriority.SelectedItem = item;
                    break;
                }
            }

            // Status
            foreach (var item in CmbEditTaskStatus.Items.OfType<ComboBoxItem>())
            {
                if ((string?)item.Tag == taskVm.Status)
                {
                    CmbEditTaskStatus.SelectedItem = item;
                    break;
                }
            }

            // Due Date
            TxtEditTaskDueDate.Text = taskVm.Dto.DueDate.HasValue
                ? taskVm.Dto.DueDate.Value.ToString("yyyy-MM-dd HH:mm")
                : "";

            ChkEditTaskDeliverableRequired.IsChecked = taskVm.DeliverableRequired;
            TxtEditTaskError.IsVisible = false;
            EditTaskModal.IsVisible = true;
        }
    }

    private void BtnCancelEditTask_Click(object? sender, RoutedEventArgs e)
    {
        EditTaskModal.IsVisible = false;
        _activeEditingTaskId = null;
    }

    private async void BtnDeleteFromEditModal_Click(object? sender, RoutedEventArgs e)
    {
        if (!_activeEditingTaskId.HasValue) return;

        try
        {
            var ok = await _apiClient.DeleteTaskAsync(_activeEditingTaskId.Value);
            if (ok)
            {
                EditTaskModal.IsVisible = false;
                ShowToast("🗑 Task Removed", "Task was deleted from workforce board.");
                await LoadAllAdminTasksAsync(silent: true);
                await LoadAssignedTasksAsync(silent: true);
            }
            else
            {
                TxtEditTaskError.Text = "Failed to delete task.";
                TxtEditTaskError.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            TxtEditTaskError.Text = ex.Message;
            TxtEditTaskError.IsVisible = true;
        }
    }

    private async void BtnSaveEditTask_Click(object? sender, RoutedEventArgs e)
    {
        if (!_activeEditingTaskId.HasValue) return;

        var title = TxtEditTaskTitle.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            TxtEditTaskError.Text = "Task title is required.";
            TxtEditTaskError.IsVisible = true;
            return;
        }

        Guid? assignedTo = null;
        if (CmbEditTaskAssignee.SelectedItem is ComboBoxItem selStaff && selStaff.Tag is Guid staffId)
        {
            assignedTo = staffId;
        }

        var priority = (CmbEditTaskPriority.SelectedItem as ComboBoxItem)?.Tag as string ?? "MEDIUM";
        var status = (CmbEditTaskStatus.SelectedItem as ComboBoxItem)?.Tag as string ?? "ASSIGNED";

        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(TxtEditTaskDueDate.Text))
        {
            if (DateTime.TryParse(TxtEditTaskDueDate.Text.Trim(), out var parsedDate))
            {
                dueDate = parsedDate;
            }
        }

        try
        {
            var request = new UpdateTaskRequest
            {
                Title = title,
                Description = TxtEditTaskDescription.Text?.Trim(),
                AssignedTo = assignedTo,
                Priority = priority,
                Status = status,
                DueDate = dueDate,
                DeliverableRequired = ChkEditTaskDeliverableRequired.IsChecked ?? true
            };

            var updated = await _apiClient.UpdateTaskAsync(_activeEditingTaskId.Value, request);
            if (updated != null)
            {
                EditTaskModal.IsVisible = false;
                ShowToast("✓ Task Updated", $"{updated.TaskCode} updated successfully.");
                await LoadAllAdminTasksAsync(silent: true);
                await LoadAssignedTasksAsync(silent: true);
            }
            else
            {
                TxtEditTaskError.Text = "Could not update task.";
                TxtEditTaskError.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            TxtEditTaskError.Text = ex.Message;
            TxtEditTaskError.IsVisible = true;
        }
    }

    private async Task LoadTimesheetSummaryAsync(bool silent = false)
    {
        try
        {
            var summary = await _apiClient.GetTimesheetSummaryAsync(_timesheetPeriod);
            _timesheets.Clear();

            if (summary != null)
            {
                if (IsAdminOrManager)
                {
                    TxtTsActiveStations.Text = summary.ActiveStationsCount.ToString();
                }
                else
                {
                    TxtTsActiveStations.Text = !string.IsNullOrWhiteSpace(_currentUser?.StationId) ? _currentUser.StationId : "ONLINE";
                }
                TxtTsTotalHours.Text = $"{summary.TotalHoursLoggedToday:F1}h";
                TxtTsAvgFocus.Text = $"{summary.AverageEfficiencyPercentage:F0}%";
                TxtTsActiveTasks.Text = summary.TasksInProgressCount.ToString();

                foreach (var row in summary.Rows)
                {
                    _timesheets.Add(new AdminTimesheetRowViewModel(row));
                }
            }

            if (!silent)
            {
                TxtStatusBar.Text = $"Timesheet metrics loaded ({_timesheetPeriod}) at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Timesheets] Error: {ex.Message}");
        }
    }

    private async void BtnTimesheetPeriod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string period)
        {
            _timesheetPeriod = period;

            BtnTsFilterToday.Classes.Clear();
            BtnTsFilterToday.Classes.Add(_timesheetPeriod == "today" ? "primary" : "glass");

            BtnTsFilterWeek.Classes.Clear();
            BtnTsFilterWeek.Classes.Add(_timesheetPeriod == "week" ? "primary" : "glass");

            BtnTsFilterMonth.Classes.Clear();
            BtnTsFilterMonth.Classes.Add(_timesheetPeriod == "month" ? "primary" : "glass");

            await LoadTimesheetSummaryAsync(silent: true);
        }
    }

    private async Task LoadAdminLeavesAsync(bool silent = false)
    {
        try
        {
            var leaves = await _apiClient.GetAllLeavesAsync();
            _adminLeaves.Clear();

            foreach (var l in leaves)
            {
                _adminLeaves.Add(new AdminLeaveApprovalViewModel(l));
            }

            if (!silent)
            {
                TxtStatusBar.Text = $"Leave applications updated ({leaves.Count} records) at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AdminLeaves] Error: {ex.Message}");
        }
    }

    private async Task LoadStaffDirectoryAsync(bool silent = false)
    {
        try
        {
            var staff = await _apiClient.GetAllStaffAsync();
            _staffMembers = staff;
            ApplyStaffFilter();

            PopulateDispatchAssigneeDropdown();

            if (!silent)
            {
                TxtStatusBar.Text = $"Staff directory updated ({staff.Count} members) at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Staff] Error: {ex.Message}");
        }
    }

    private void ApplyStaffFilter()
    {
        var filtered = _staffMembers.AsEnumerable();

        if (_staffStatusFilter == "ACTIVE")
        {
            filtered = filtered.Where(s => s.IsActive);
        }
        else if (_staffStatusFilter == "SUSPENDED")
        {
            filtered = filtered.Where(s => !s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(_staffSearchQuery))
        {
            var q = _staffSearchQuery.Trim();
            filtered = filtered.Where(s =>
                (s.FullName != null && s.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (s.Email != null && s.Email.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (s.Username != null && s.Username.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (s.Role != null && s.Role.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (s.StationId != null && s.StationId.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        _allStaff.Clear();
        foreach (var s in filtered)
        {
            _allStaff.Add(new AdminStaffMemberViewModel(s));
        }
    }

    private void TxtStaffSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _staffSearchQuery = TxtStaffSearch.Text ?? string.Empty;
        ApplyStaffFilter();
    }

    private void BtnFilterStaffStatus_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string status)
        {
            _staffStatusFilter = status;

            BtnFilterStaffAll.Classes.Clear();
            BtnFilterStaffAll.Classes.Add(_staffStatusFilter == "ALL" ? "primary" : "glass");

            BtnFilterStaffActive.Classes.Clear();
            BtnFilterStaffActive.Classes.Add(_staffStatusFilter == "ACTIVE" ? "primary" : "glass");

            BtnFilterStaffSuspended.Classes.Clear();
            BtnFilterStaffSuspended.Classes.Add(_staffStatusFilter == "SUSPENDED" ? "primary" : "glass");

            ApplyStaffFilter();
        }
    }

    private void PopulateDispatchAssigneeDropdown()
    {
        var items = new List<ComboBoxItem>
        {
            new() { Content = "Unassigned (Team Pool)", Tag = "" }
        };

        foreach (var s in _staffMembers.OrderBy(m => m.FullName))
        {
            items.Add(new ComboBoxItem
            {
                Content = $"{s.FullName} ({s.Role} • {s.WorkMode})",
                Tag = s.Id.ToString()
            });
        }

        CmbDispatchAssignee.ItemsSource = items;
        CmbDispatchAssignee.SelectedIndex = 0;
    }

    #endregion

    #region Authentication & Shift Management

    private async void BtnDoLogin_Click(object? sender, RoutedEventArgs e)
    {
        TxtLoginError.IsVisible = false;
        var email = TxtLoginEmail.Text?.Trim() ?? "";
        var password = TxtLoginPassword.Text ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TxtLoginError.Text = "Please enter both email and password.";
            TxtLoginError.IsVisible = true;
            return;
        }

        BtnDoLogin.IsEnabled = false;
        BtnDoLogin.Content = "Signing In & Clocking In...";

        try
        {
            var response = await _apiClient.LoginAsync(email, password);
            if (response != null && response.User != null)
            {
                _currentUser = response.User;

                // Sync work mode and station identifier with user profile
                bool isRemoteWfh = _currentUser.WorkMode == "REMOTE_WFH" || _currentUser.WorkMode == "HYBRID";
                ChkWorkFromHome.IsChecked = isRemoteWfh;

                if (!string.IsNullOrWhiteSpace(_currentUser.StationId))
                {
                    _stationId = _currentUser.StationId;
                }
                else
                {
                    UpdateStationIdentifierFromWorkMode();
                }

                // Save or clear credentials based on "Remember" checkbox
                if (ChkRememberCredentials.IsChecked == true)
                {
                    CredentialStorage.SaveCredentials(email, password, true);
                }
                else
                {
                    CredentialStorage.ClearCredentials();
                }

                LoginModal.IsVisible = false;

                // Automatic Shift Clock In
                await ClockInInternalAsync();
                await InitializeWorkstationSessionAsync();
            }
            else
            {
                TxtLoginError.Text = "Invalid username or password.";
                TxtLoginError.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            TxtLoginError.Text = $"Connection error: {ex.Message}";
            TxtLoginError.IsVisible = true;
        }
        finally
        {
            BtnDoLogin.IsEnabled = true;
            BtnDoLogin.Content = "Clock In & Launch Desk";
        }
    }

    private async void BtnClockToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (TxtShiftState.Text == "Clocked In")
        {
            await ClockOutInternalAsync();
        }
        else
        {
            await ClockInInternalAsync();
        }
    }

    private async Task ClockInInternalAsync()
    {
        try
        {
            await _apiClient.ClockInAsync(_stationId);
            TxtShiftState.Text = "Clocked In";
            TxtShiftState.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            BtnClockToggle.Content = "Clock Out";
            BtnClockToggle.Classes.Clear();
            BtnClockToggle.Classes.Add("primary");
            TxtShiftHours.Text = $"Shift Started: {DateTime.Now:hh:mm tt}";

            _shiftStartTime = DateTime.UtcNow;
            ShiftClockBadge.IsVisible = true;
            TxtShiftDigitalClock.Text = "00:00:00";
            StartShiftClock();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Shift] Clock-in error: {ex.Message}");
        }
    }

    private async Task ClockOutInternalAsync()
    {
        try
        {
            await _apiClient.ClockOutAsync(_stationId);
            TxtShiftState.Text = "Clocked Out";
            TxtShiftState.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            BtnClockToggle.Content = "Clock In";
            BtnClockToggle.Classes.Clear();
            BtnClockToggle.Classes.Add("accentGreen");
            TxtShiftHours.Text = $"Clocked out at {DateTime.Now:hh:mm tt}";

            StopShiftClock();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Shift] Clock-out error: {ex.Message}");
        }
    }

    private void StartShiftClock()
    {
        _shiftTimer?.Stop();
        _shiftTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _shiftTimer.Tick += (_, _) =>
        {
            if (_shiftStartTime.HasValue)
            {
                var elapsed = DateTime.UtcNow - _shiftStartTime.Value;
                TxtShiftDigitalClock.Text = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        };
        _shiftTimer.Start();
    }

    private void StopShiftClock()
    {
        _shiftTimer?.Stop();
        _shiftTimer = null;
        ShiftClockBadge.IsVisible = false;
    }

    #endregion

    #region Task Item Actions & Deliverables

    private async void BtnStartPauseTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var target = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (target == null) return;

            if (_activeTaskId == taskId)
            {
                // Pause task
                _activeTaskId = null;
                _activeTaskCode = null;
                target.IsCurrentActive = false;
                _engine.SetCurrentTask(null, null);
                TxtActiveTaskBadge.Text = "No task linked";

                try
                {
                    await _apiClient.UpdateTaskStatusAsync(taskId, TaskItemStatus.ASSIGNED.ToString());
                }
                catch { }

                ShowToast("Task Paused", $"Paused tracking for {target.TaskCode}");
            }
            else
            {
                // Start or switch to this task
                foreach (var t in _tasks) t.IsCurrentActive = false;
                target.IsCurrentActive = true;
                _activeTaskId = target.Id;
                _activeTaskCode = target.TaskCode;

                _engine.SetCurrentTask(target.Id, target.TaskCode);
                TxtActiveTaskBadge.Text = $"Linked: {target.TaskCode}";

                try
                {
                    await _apiClient.UpdateTaskStatusAsync(taskId, TaskItemStatus.IN_PROGRESS.ToString());
                }
                catch { }

                ShowToast("Task Active", $"Now tracking {target.TaskCode}: {target.Title}");
            }
        }
    }

    private void BtnViewDeliverable_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var personalTask = _tasks.FirstOrDefault(t => t.Id == taskId)?.Dto;
            var adminTask = _allTasks.FirstOrDefault(t => t.Id == taskId)?.Dto;
            var task = personalTask ?? adminTask;

            if (task == null) return;

            string targetUrl;
            if (!string.IsNullOrWhiteSpace(task.DeliverableUrl) &&
                (task.DeliverableUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 task.DeliverableUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                targetUrl = task.DeliverableUrl;
            }
            else
            {
                var baseUri = _apiClient.BaseUrl.TrimEnd('/');
                targetUrl = $"{baseUri}/api/v1/tasks/{task.Id}/deliverable";
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Deliverable Link", ex.Message);
            }
        }
    }

    private void BtnAttachDeliverable_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            _modalTargetTaskId = taskId;
            _stagedFilePath = null;
            TxtSelectedFilePath.Text = "No file selected";
            TxtDeliverableUrlInput.Text = "";
            AttachDeliverableModal.IsVisible = true;
        }
    }

    private async void BtnBrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Work Deliverable Proof",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                _stagedFilePath = files[0].TryGetLocalPath() ?? (files[0].Path.IsFile ? files[0].Path.LocalPath : null);
                if (!string.IsNullOrWhiteSpace(_stagedFilePath) && File.Exists(_stagedFilePath))
                {
                    TxtSelectedFilePath.Text = $"Selected: {Path.GetFileName(_stagedFilePath)} ({new FileInfo(_stagedFilePath).Length / 1024} KB)";
                }
                else
                {
                    TxtSelectedFilePath.Text = $"Selected: {files[0].Name}";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Deliverable] Browse file error: {ex.Message}");
            ShowToast("⚠️ File Selection Error", ex.Message);
            TxtSelectedFilePath.Text = $"⚠️ Error: {ex.Message}";
        }
    }

    private void BtnCancelDeliverable_Click(object? sender, RoutedEventArgs e)
    {
        AttachDeliverableModal.IsVisible = false;
        _modalTargetTaskId = null;
        _stagedFilePath = null;
    }

    private async void BtnSaveDeliverable_Click(object? sender, RoutedEventArgs e)
    {
        if (!_modalTargetTaskId.HasValue) return;

        var url = TxtDeliverableUrlInput.Text?.Trim();
        var hasValidFile = !string.IsNullOrWhiteSpace(_stagedFilePath) && File.Exists(_stagedFilePath);
        var hasValidUrl = !string.IsNullOrWhiteSpace(url);

        if (!hasValidFile && !hasValidUrl)
        {
            TxtSelectedFilePath.Text = "⚠️ Please select a file or enter an external URL.";
            return;
        }

        try
        {
            if (hasValidUrl)
            {
                await _apiClient.AttachDeliverableLinkAsync(_modalTargetTaskId.Value, url!, "External Proof Link");
            }
            else if (hasValidFile)
            {
                await _apiClient.UploadDeliverableAsync(_modalTargetTaskId.Value, _stagedFilePath!);
            }

            AttachDeliverableModal.IsVisible = false;
            _stagedFilePath = null;
            _modalTargetTaskId = null;
            ShowToast("✓ Deliverable Attached", "Work proof recorded. You may now complete this task.");
            await LoadAssignedTasksAsync(silent: true);
            if (IsAdminOrManager)
            {
                await LoadAllAdminTasksAsync(silent: true);
            }
        }
        catch (Exception ex)
        {
            TxtSelectedFilePath.Text = $"Upload failed: {ex.Message}";
            ShowToast("⚠️ Upload Failed", ex.Message);
        }
    }

    private async void BtnMarkCompleted_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            try
            {
                await _apiClient.UpdateTaskStatusAsync(taskId, "COMPLETED");
                if (_activeTaskId == taskId)
                {
                    _activeTaskId = null;
                    _activeTaskCode = null;
                    _engine.SetCurrentTask(null, null);
                    TxtActiveTaskBadge.Text = "No task linked";
                }

                ShowToast("🎉 Task Completed", "Marked complete and submitted for manager review.");
                await LoadAssignedTasksAsync(silent: true);
                if (IsAdminOrManager)
                {
                    await LoadAllAdminTasksAsync(silent: true);
                }
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Failed to complete task: {ex.Message}";
            }
        }
    }

    #endregion

    #region Admin Management Console Actions

    private void BtnOpenDispatchModal_Click(object? sender, RoutedEventArgs e)
    {
        TxtDispatchTitle.Text = "";
        TxtDispatchDescription.Text = "";
        TxtDispatchDueDate.Text = "";
        TxtDispatchError.IsVisible = false;
        ChkDispatchDeliverableRequired.IsChecked = true;
        DispatchTaskModal.IsVisible = true;
    }

    private void BtnCancelDispatch_Click(object? sender, RoutedEventArgs e)
    {
        DispatchTaskModal.IsVisible = false;
    }

    private async void BtnSubmitDispatch_Click(object? sender, RoutedEventArgs e)
    {
        TxtDispatchError.IsVisible = false;
        var title = TxtDispatchTitle.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            TxtDispatchError.Text = "Please enter a task title.";
            TxtDispatchError.IsVisible = true;
            return;
        }

        var description = TxtDispatchDescription.Text?.Trim();
        var priorityItem = CmbDispatchPriority.SelectedItem as ComboBoxItem;
        var priority = priorityItem?.Tag?.ToString() ?? "MEDIUM";
        var deliverableReq = ChkDispatchDeliverableRequired.IsChecked == true;

        Guid? assignedTo = null;
        if (CmbDispatchAssignee.SelectedItem is ComboBoxItem assigneeItem &&
            assigneeItem.Tag is string assigneeStr &&
            Guid.TryParse(assigneeStr, out var assignedGuid))
        {
            assignedTo = assignedGuid;
        }

        DateTime? dueDate = null;
        if (DateTime.TryParse(TxtDispatchDueDate.Text?.Trim(), out var parsedDue))
        {
            dueDate = parsedDue;
        }

        try
        {
            var request = new CreateTaskRequest
            {
                Title = title,
                Description = description,
                Priority = priority,
                AssignedTo = assignedTo,
                DeliverableRequired = deliverableReq,
                DueDate = dueDate
            };

            await _apiClient.CreateTaskAsync(request);
            DispatchTaskModal.IsVisible = false;
            ShowToast("🚀 Task Dispatched", $"Created: {title}");

            await LoadAllAdminTasksAsync(silent: true);
            await LoadAssignedTasksAsync(silent: true);
        }
        catch (Exception ex)
        {
            TxtDispatchError.Text = $"Failed to dispatch: {ex.Message}";
            TxtDispatchError.IsVisible = true;
        }
    }

    private void BtnApproveLeave_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid leaveId)
        {
            _reviewTargetLeaveId = leaveId;
            var item = _adminLeaves.FirstOrDefault(l => l.Id == leaveId);

            TxtReviewLeaveTitle.Text = "Approve Leave Request";
            TxtReviewLeaveDetails.Text = $"Employee: {item?.UserName} ({item?.TotalDaysText}) — {item?.DateRangeText}";
            TxtReviewLeaveReason.Text = $"Reason: {item?.Reason}";
            TxtReviewSupervisorComments.Text = "Approved.";
            ReviewLeaveModal.IsVisible = true;
        }
    }

    private void BtnRejectLeave_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid leaveId)
        {
            _reviewTargetLeaveId = leaveId;
            var item = _adminLeaves.FirstOrDefault(l => l.Id == leaveId);

            TxtReviewLeaveTitle.Text = "Reject Leave Request";
            TxtReviewLeaveDetails.Text = $"Employee: {item?.UserName} ({item?.TotalDaysText}) — {item?.DateRangeText}";
            TxtReviewLeaveReason.Text = $"Reason: {item?.Reason}";
            TxtReviewSupervisorComments.Text = "";
            ReviewLeaveModal.IsVisible = true;
        }
    }

    private void BtnCancelReviewLeave_Click(object? sender, RoutedEventArgs e)
    {
        ReviewLeaveModal.IsVisible = false;
        _reviewTargetLeaveId = null;
    }

    private async void BtnConfirmApproveLeave_Click(object? sender, RoutedEventArgs e)
    {
        if (!_reviewTargetLeaveId.HasValue) return;

        try
        {
            var comments = TxtReviewSupervisorComments.Text?.Trim();
            await _apiClient.ReviewLeaveAsync(_reviewTargetLeaveId.Value, "APPROVE", comments);
            ReviewLeaveModal.IsVisible = false;
            _reviewTargetLeaveId = null;

            ShowToast("✓ Leave Approved", "Employee application was approved.");
            await LoadAdminLeavesAsync(silent: true);
        }
        catch (Exception ex)
        {
            ShowToast("⚠️ Review Error", ex.Message);
        }
    }

    private async void BtnConfirmRejectLeave_Click(object? sender, RoutedEventArgs e)
    {
        if (!_reviewTargetLeaveId.HasValue) return;

        try
        {
            var comments = TxtReviewSupervisorComments.Text?.Trim();
            await _apiClient.ReviewLeaveAsync(_reviewTargetLeaveId.Value, "REJECT", comments);
            ReviewLeaveModal.IsVisible = false;
            _reviewTargetLeaveId = null;

            ShowToast("✕ Leave Rejected", "Employee application was rejected.");
            await LoadAdminLeavesAsync(silent: true);
        }
        catch (Exception ex)
        {
            ShowToast("⚠️ Review Error", ex.Message);
        }
    }

    private void BtnOpenAddStaffModal_Click(object? sender, RoutedEventArgs e)
    {
        TxtAddStaffFullName.Text = "";
        TxtAddStaffEmail.Text = "";
        TxtAddStaffUsername.Text = "";
        TxtAddStaffPassword.Text = "";
        TxtAddStaffStationId.Text = "";
        TxtAddStaffError.IsVisible = false;
        AddStaffModal.IsVisible = true;
    }

    private void BtnCancelAddStaff_Click(object? sender, RoutedEventArgs e)
    {
        AddStaffModal.IsVisible = false;
    }

    private async void BtnSubmitAddStaff_Click(object? sender, RoutedEventArgs e)
    {
        TxtAddStaffError.IsVisible = false;
        var fullName = TxtAddStaffFullName.Text?.Trim();
        var email = TxtAddStaffEmail.Text?.Trim();
        var password = TxtAddStaffPassword.Text?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TxtAddStaffError.Text = "Full Name, Email and Password are required.";
            TxtAddStaffError.IsVisible = true;
            return;
        }

        var roleItem = CmbAddStaffRole.SelectedItem as ComboBoxItem;
        var role = roleItem?.Tag?.ToString() ?? "EMPLOYEE";

        var workModeItem = CmbAddStaffWorkMode.SelectedItem as ComboBoxItem;
        var workMode = workModeItem?.Tag?.ToString() ?? "IN_OFFICE";

        var username = TxtAddStaffUsername.Text?.Trim();
        var stationId = TxtAddStaffStationId.Text?.Trim();

        try
        {
            var request = new CreateStaffRequest
            {
                FullName = fullName,
                Email = email,
                Username = string.IsNullOrWhiteSpace(username) ? null : username,
                Password = password,
                Role = role,
                WorkMode = workMode,
                StationId = string.IsNullOrWhiteSpace(stationId) ? null : stationId
            };

            await _apiClient.CreateStaffAsync(request);
            AddStaffModal.IsVisible = false;
            ShowToast("👥 Team Member Created", $"{fullName} has been added.");

            await LoadStaffDirectoryAsync(silent: true);
        }
        catch (Exception ex)
        {
            TxtAddStaffError.Text = $"Failed to add staff: {ex.Message}";
            TxtAddStaffError.IsVisible = true;
        }
    }

    private async void BtnExportTimesheetCsv_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            TxtStatusBar.Text = $"Downloading timesheet CSV export ({_timesheetPeriod})...";
            var csvBytes = await _apiClient.ExportTimesheetCsvAsync(period: _timesheetPeriod);

            if (csvBytes != null && csvBytes.Length > 0)
            {
                var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var filePath = Path.Combine(desktopDir, $"Punnya_Timesheet_{_timesheetPeriod}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                await File.WriteAllBytesAsync(filePath, csvBytes);

                ShowToast("⬇ CSV Exported", $"Saved to Desktop: {Path.GetFileName(filePath)}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            else
            {
                ShowToast("⚠️ Export CSV", $"No timesheet records available for {_timesheetPeriod}.");
            }
        }
        catch (Exception ex)
        {
            ShowToast("⚠️ Export Failed", ex.Message);
        }
    }

    private async void BtnOpenBulkImportModal_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            TxtBulkImportCsv.Text = string.Empty;
            _parsedTasks.Clear();
            TxtBulkImportSummary.Text = "Paste or load CSV data above and click 'Parse & Validate'.";
            TxtBulkImportProgress.Text = string.Empty;
            ChkBulkSelectAll.IsChecked = true;
            BulkImportTasksModal.IsVisible = true;

            if (_staffMembers == null || _staffMembers.Count == 0)
            {
                await LoadStaffDirectoryAsync(silent: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BulkImport] Open error: {ex.Message}");
        }
    }

    private void BtnCloseBulkImport_Click(object? sender, RoutedEventArgs e)
    {
        BulkImportTasksModal.IsVisible = false;
    }

    private void BtnBulkImportSampleMatrix_Click(object? sender, RoutedEventArgs e)
    {
        TxtBulkImportCsv.Text = TaskCsvImporter.GetSampleMatrixCsv();
        BtnBulkImportParse_Click();
    }

    private void BtnBulkImportSampleLinear_Click(object? sender, RoutedEventArgs e)
    {
        TxtBulkImportCsv.Text = TaskCsvImporter.GetSampleLinearCsv();
        BtnBulkImportParse_Click();
    }

    private async void BtnBulkImportBrowse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open CSV/TSV File for Task Dispatch",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("CSV and TSV files") { Patterns = new[] { "*.csv", "*.tsv", "*.txt" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                string text = string.Empty;
                var localPath = file.TryGetLocalPath() ?? (file.Path.IsFile ? file.Path.LocalPath : null);

                if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                {
                    try
                    {
                        // Open with FileShare.ReadWrite so even if Excel has the CSV open, it won't crash!
                        await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(stream);
                        text = await reader.ReadToEndAsync();
                    }
                    catch (IOException)
                    {
                        // Fallback to Avalonia storage provider stream
                        await using var fallbackStream = await file.OpenReadAsync();
                        using var fallbackReader = new StreamReader(fallbackStream);
                        text = await fallbackReader.ReadToEndAsync();
                    }
                }
                else
                {
                    await using var stream = await file.OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    text = await reader.ReadToEndAsync();
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    TxtBulkImportCsv.Text = text;
                    BtnBulkImportParse_Click();
                }
                else
                {
                    ShowToast("⚠️ Empty File", "The selected file contains no text.");
                }
            }
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"[BulkImport] File in use: {ioEx.Message}");
            ShowToast("⚠️ File In Use", "The CSV file is currently open in Excel or another program. Please close Excel or paste the CSV text directly.");
            TxtBulkImportSummary.Text = "⚠️ The file is in use by another program (e.g. Excel). Close Excel or paste the CSV text directly.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BulkImport] Error opening file: {ex.Message}");
            ShowToast("⚠️ Error Opening File", ex.Message);
            TxtBulkImportSummary.Text = $"⚠️ Error opening file: {ex.Message}";
        }
    }

    private void BtnBulkImportParse_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        try
        {
            var text = TxtBulkImportCsv.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                TxtBulkImportSummary.Text = "Please enter or paste CSV text first.";
                return;
            }

            var result = TaskCsvImporter.Parse(text, _staffMembers);
            _parsedTasks.Clear();
            foreach (var t in result.Tasks)
            {
                _parsedTasks.Add(t);
            }

            TxtBulkImportSummary.Text = $"Parsed {result.TotalFound} tasks ({result.DetectedFormatLabel}) • {result.MatchedStaffCount} staff matched • {result.UnassignedCount} unassigned";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BulkImport] Parse error: {ex.Message}");
            ShowToast("⚠️ Parse Error", $"Failed to parse CSV: {ex.Message}");
            TxtBulkImportSummary.Text = $"⚠️ Error parsing CSV: {ex.Message}";
        }
    }

    private void ChkBulkSelectAll_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        var isChecked = ChkBulkSelectAll.IsChecked == true;
        foreach (var t in _parsedTasks)
        {
            t.IsSelected = isChecked;
        }
    }

    private async void BtnSubmitBulkImport_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = _parsedTasks.Where(t => t.IsSelected && !string.IsNullOrWhiteSpace(t.Title)).ToList();
            if (selected.Count == 0)
            {
                TxtBulkImportProgress.Text = "⚠️ No tasks selected for import.";
                return;
            }

            BtnSubmitBulkImport.IsEnabled = false;
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                TxtBulkImportProgress.Text = $"Dispatching task {i + 1} of {selected.Count}: {item.Title}...";

                try
                {
                    DateTime? dueDateUtc = item.DueDate.HasValue
                        ? DateTime.SpecifyKind(item.DueDate.Value, DateTimeKind.Utc)
                        : null;

                    await _apiClient.CreateTaskAsync(new CreateTaskRequest
                    {
                        Title = item.Title,
                        Description = item.Description,
                        Priority = item.Priority,
                        AssignedTo = item.AssignedTo,
                        DeliverableRequired = item.DeliverableRequired,
                        DueDate = dueDateUtc
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BulkImport] Error creating task: {ex.Message}");
                    failCount++;
                }
            }

            BulkImportTasksModal.IsVisible = false;
            ShowToast("🚀 Bulk Tasks Dispatched", $"Successfully dispatched {successCount} tasks ({failCount} failed).");

            await LoadAllAdminTasksAsync(silent: true);
            await LoadAssignedTasksAsync(silent: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BulkImport] Dispatch error: {ex.Message}");
            ShowToast("⚠️ Dispatch Error", ex.Message);
            TxtBulkImportProgress.Text = $"⚠️ Error: {ex.Message}";
        }
        finally
        {
            BtnSubmitBulkImport.IsEnabled = true;
        }
    }

    private void BtnEditStaff_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid staffId)
        {
            var member = _staffMembers.FirstOrDefault(s => s.Id == staffId);
            if (member == null) return;

            _editStaffTargetId = staffId;
            TxtEditStaffFullName.Text = member.FullName;
            TxtEditStaffEmail.Text = member.Email;
            TxtEditStaffUsername.Text = member.Username ?? string.Empty;
            TxtEditStaffStationId.Text = member.StationId ?? string.Empty;
            ChkEditStaffIsActive.IsChecked = member.IsActive;
            TxtEditStaffError.IsVisible = false;

            for (int i = 0; i < CmbEditStaffRole.Items.Count; i++)
            {
                if (CmbEditStaffRole.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), member.Role, StringComparison.OrdinalIgnoreCase))
                {
                    CmbEditStaffRole.SelectedIndex = i;
                    break;
                }
            }

            for (int i = 0; i < CmbEditStaffWorkMode.Items.Count; i++)
            {
                if (CmbEditStaffWorkMode.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), member.WorkMode, StringComparison.OrdinalIgnoreCase))
                {
                    CmbEditStaffWorkMode.SelectedIndex = i;
                    break;
                }
            }

            EditStaffModal.IsVisible = true;
        }
    }

    private void BtnCancelEditStaff_Click(object? sender, RoutedEventArgs e)
    {
        EditStaffModal.IsVisible = false;
        _editStaffTargetId = null;
    }

    private async void BtnSubmitEditStaff_Click(object? sender, RoutedEventArgs e)
    {
        if (!_editStaffTargetId.HasValue) return;

        TxtEditStaffError.IsVisible = false;
        var fullName = TxtEditStaffFullName.Text?.Trim();
        var email = TxtEditStaffEmail.Text?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            TxtEditStaffError.Text = "Full Name and Email are required.";
            TxtEditStaffError.IsVisible = true;
            return;
        }

        var roleItem = CmbEditStaffRole.SelectedItem as ComboBoxItem;
        var role = roleItem?.Tag?.ToString() ?? "EMPLOYEE";

        var workModeItem = CmbEditStaffWorkMode.SelectedItem as ComboBoxItem;
        var workMode = workModeItem?.Tag?.ToString() ?? "IN_OFFICE";

        var username = TxtEditStaffUsername.Text?.Trim();
        var stationId = TxtEditStaffStationId.Text?.Trim();
        var isActive = ChkEditStaffIsActive.IsChecked == true;

        try
        {
            await _apiClient.UpdateStaffAsync(_editStaffTargetId.Value, new UpdateStaffRequest
            {
                FullName = fullName,
                Email = email,
                Username = string.IsNullOrWhiteSpace(username) ? null : username,
                Role = role,
                WorkMode = workMode,
                StationId = string.IsNullOrWhiteSpace(stationId) ? null : stationId,
                IsActive = isActive
            });

            EditStaffModal.IsVisible = false;
            _editStaffTargetId = null;
            ShowToast("✓ Profile Updated", $"{fullName}'s profile was updated.");
            await LoadStaffDirectoryAsync(silent: true);
        }
        catch (Exception ex)
        {
            TxtEditStaffError.Text = $"Update failed: {ex.Message}";
            TxtEditStaffError.IsVisible = true;
        }
    }

    private void BtnResetStaffPassword_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid staffId)
        {
            var member = _staffMembers.FirstOrDefault(s => s.Id == staffId);
            if (member == null) return;

            _resetPasswordTargetId = staffId;
            TxtResetPasswordTargetInfo.Text = $"Target: {member.FullName} ({member.Email})";
            TxtResetPasswordNewPassword.Text = string.Empty;
            TxtResetPasswordError.IsVisible = false;
            ResetStaffPasswordModal.IsVisible = true;
        }
    }

    private void BtnCancelResetPassword_Click(object? sender, RoutedEventArgs e)
    {
        ResetStaffPasswordModal.IsVisible = false;
        _resetPasswordTargetId = null;
    }

    private async void BtnSubmitResetPassword_Click(object? sender, RoutedEventArgs e)
    {
        if (!_resetPasswordTargetId.HasValue) return;

        TxtResetPasswordError.IsVisible = false;
        var newPassword = TxtResetPasswordNewPassword.Text?.Trim();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            TxtResetPasswordError.Text = "Please enter a valid password (minimum 4 characters).";
            TxtResetPasswordError.IsVisible = true;
            return;
        }

        try
        {
            await _apiClient.ResetStaffPasswordAsync(_resetPasswordTargetId.Value, newPassword);
            ResetStaffPasswordModal.IsVisible = false;
            _resetPasswordTargetId = null;
            ShowToast("🔑 Password Reset", "New temporary credentials saved.");
        }
        catch (Exception ex)
        {
            TxtResetPasswordError.Text = $"Reset failed: {ex.Message}";
            TxtResetPasswordError.IsVisible = true;
        }
    }

    private async void BtnToggleStaffStatus_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid staffId)
        {
            try
            {
                var ok = await _apiClient.ToggleStaffStatusAsync(staffId);
                if (ok)
                {
                    ShowToast("Staff Status Changed", "Staff account status toggled.");
                    await LoadStaffDirectoryAsync(silent: true);
                }
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Status Change Failed", ex.Message);
            }
        }
    }

    private async void BtnDeleteStaff_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid staffId)
        {
            var member = _staffMembers.FirstOrDefault(s => s.Id == staffId);
            var name = member?.FullName ?? "staff member";

            if (_currentUser != null && staffId == _currentUser.Id)
            {
                ShowToast("⚠️ Action Blocked", "You cannot delete your own active administrator account.");
                return;
            }

            try
            {
                var ok = await _apiClient.DeleteStaffAsync(staffId);
                if (ok)
                {
                    ShowToast("🗑 Staff Deleted", $"{name} was removed from the workforce directory.");
                    await LoadStaffDirectoryAsync(silent: true);
                    await LoadAllAdminTasksAsync(silent: true);
                }
                else
                {
                    ShowToast("⚠️ Delete Failed", "Could not remove staff member.");
                }
            }
            catch (Exception ex)
            {
                ShowToast("⚠️ Delete Failed", ex.Message);
            }
        }
    }

    private async void BtnRefreshWorkforce_Click(object? sender, RoutedEventArgs e) => await LoadWorkforceFleetAsync();
    private async void BtnRefreshAllTasks_Click(object? sender, RoutedEventArgs e) => await LoadAllAdminTasksAsync();
    private async void BtnRefreshTimesheet_Click(object? sender, RoutedEventArgs e) => await LoadTimesheetSummaryAsync();
    private async void BtnRefreshAdminLeaves_Click(object? sender, RoutedEventArgs e) => await LoadAdminLeavesAsync();
    private async void BtnRefreshStaff_Click(object? sender, RoutedEventArgs e) => await LoadStaffDirectoryAsync();

    #endregion

    #region Leave Center (Personal)

    private async void BtnOpenLeaveCenter_Click(object? sender, RoutedEventArgs e)
    {
        TxtLeaveStartDate.Text = DateTime.UtcNow.ToString("yyyy-MM-dd");
        TxtLeaveEndDate.Text = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        TxtLeaveReason.Text = "";
        TxtLeaveFormMessage.IsVisible = false;

        LeaveCenterModal.IsVisible = true;
        await LoadLeaveHistoryAsync();
    }

    private void BtnCloseLeaveCenter_Click(object? sender, RoutedEventArgs e)
    {
        LeaveCenterModal.IsVisible = false;
    }

    private async void BtnRefreshLeaves_Click(object? sender, RoutedEventArgs e)
    {
        await LoadLeaveHistoryAsync();
    }

    private async Task LoadLeaveHistoryAsync()
    {
        try
        {
            var leaves = await _apiClient.GetMyLeavesAsync();
            _leaves.Clear();
            foreach (var l in leaves)
            {
                _leaves.Add(new LeaveItemViewModel(l));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Leaves] Load error: {ex.Message}");
        }
    }

    private async void BtnSubmitLeaveApplication_Click(object? sender, RoutedEventArgs e)
    {
        TxtLeaveFormMessage.IsVisible = false;

        var leaveTypeItem = CmbLeaveType.SelectedItem as ComboBoxItem;
        var leaveType = leaveTypeItem?.Tag?.ToString() ?? "CASUAL";
        var startStr = TxtLeaveStartDate.Text?.Trim() ?? "";
        var endStr = TxtLeaveEndDate.Text?.Trim() ?? "";
        var reason = TxtLeaveReason.Text?.Trim() ?? "";

        if (!DateTime.TryParse(startStr, out var startDate) || !DateTime.TryParse(endStr, out var endDate))
        {
            TxtLeaveFormMessage.Text = "Please enter valid dates (YYYY-MM-DD).";
            TxtLeaveFormMessage.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLeaveFormMessage.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TxtLeaveFormMessage.Text = "Please provide a reason or justification.";
            TxtLeaveFormMessage.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLeaveFormMessage.IsVisible = true;
            return;
        }

        try
        {
            var request = new CreateLeaveRequestDto
            {
                LeaveType = leaveType,
                StartDate = startDate,
                EndDate = endDate,
                Reason = reason
            };

            await _apiClient.ApplyLeaveAsync(request);

            TxtLeaveFormMessage.Text = "✓ Application submitted for supervisor review!";
            TxtLeaveFormMessage.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            TxtLeaveFormMessage.IsVisible = true;
            TxtLeaveReason.Text = "";

            await LoadLeaveHistoryAsync();
        }
        catch (Exception ex)
        {
            TxtLeaveFormMessage.Text = $"Submission failed: {ex.Message}";
            TxtLeaveFormMessage.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLeaveFormMessage.IsVisible = true;
        }
    }

    #endregion

    #region Standard Desktop Actions & Lifecycle

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await LoadAssignedTasksAsync();
    }

    private async void BtnSyncNow_Click(object? sender, RoutedEventArgs e)
    {
        TxtSyncInfo.Text = "Syncing now...";
        await _syncWorker.SyncPendingBatchAsync();
    }

    private void ShowToast(string title, string message)
    {
        TxtToastTitle.Text = title;
        TxtToastMessage.Text = message;
        NotificationToast.IsVisible = true;

        _toastDismissTimer?.Stop();
        _toastDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _toastDismissTimer.Tick += (_, _) =>
        {
            NotificationToast.IsVisible = false;
            _toastDismissTimer.Stop();
        };
        _toastDismissTimer.Start();
    }

    private void BtnDismissToast_Click(object? sender, RoutedEventArgs e)
    {
        NotificationToast.IsVisible = false;
        _toastDismissTimer?.Stop();
    }

    private void BtnLogout_Click(object? sender, RoutedEventArgs e)
    {
        StopShiftClock();
        _engine.Stop();
        _syncWorker.Stop();
        _apiClient.Logout();
        _tasks.Clear();
        _leaves.Clear();
        _workforce.Clear();
        _allTasks.Clear();
        _timesheets.Clear();
        _adminLeaves.Clear();
        _allStaff.Clear();

        AdminNavBarBorder.IsVisible = false;
        RoleBadgeBorder.IsVisible = false;
        ViewMyDesk.IsVisible = true;
        ViewWorkforce.IsVisible = false;
        ViewTaskDispatch.IsVisible = false;
        ViewTimesheets.IsVisible = false;
        ViewLeaveApprovals.IsVisible = false;
        ViewStaffDirectory.IsVisible = false;

        // Reload remembered credentials into textboxes
        var (savedUser, savedPass, remember) = CredentialStorage.LoadCredentials();
        if (remember && !string.IsNullOrEmpty(savedUser))
        {
            TxtLoginEmail.Text = savedUser;
            TxtLoginPassword.Text = savedPass;
            ChkRememberCredentials.IsChecked = true;
        }
        else
        {
            TxtLoginEmail.Text = string.Empty;
            TxtLoginPassword.Text = string.Empty;
            ChkRememberCredentials.IsChecked = true;
        }

        LoginModal.IsVisible = true;
    }

    private bool _isShuttingDown = false;

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        PerformFullAppShutdown();
    }

    private void PerformFullAppShutdown()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        try
        {
            StopShiftClock();
            _autoRefreshTimer?.Stop();
            _toastDismissTimer?.Stop();

            try { _engine.Stop(); } catch { }
            try { _engine.Dispose(); } catch { }
            try { _syncWorker.Stop(); } catch { }
            try { _syncWorker.Dispose(); } catch { }

            // Fire-and-forget realtime stop so it never hangs or deadlocks
            Task.Run(async () =>
            {
                try { await _realtimeClient.StopAsync(); } catch { }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Shutdown] Error: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
            catch { }

            // Force kill the process completely so it never lingers in Task Manager
            Environment.Exit(0);
        }
    }

    #endregion
}

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
    private List<StaffMemberDto> _staffMembers = new();

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
        if (IsAdminOrManager)
        {
            AdminNavBarBorder.IsVisible = true;
            RoleBadgeBorder.IsVisible = true;
            TxtUserRoleBadge.Text = _currentUser?.Role.ToUpper() ?? "ADMIN";
            await LoadStaffDirectoryAsync(silent: true);
        }
        else
        {
            AdminNavBarBorder.IsVisible = false;
            RoleBadgeBorder.IsVisible = false;
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
    private async void BtnTabLeaves_Click(object? sender, RoutedEventArgs e) => await SwitchToTabAsync("LEAVES");
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
            _allTasks.Clear();

            foreach (var t in tasks)
            {
                _allTasks.Add(new AdminTaskItemViewModel(t));
            }

            TxtAdminTasksCount.Text = $"All Workforce Tasks ({tasks.Count} total)";

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

    private async Task LoadTimesheetSummaryAsync(bool silent = false)
    {
        try
        {
            var summary = await _apiClient.GetTimesheetSummaryAsync("today");
            _timesheets.Clear();

            if (summary != null)
            {
                TxtTsActiveStations.Text = summary.ActiveStationsCount.ToString();
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
                TxtStatusBar.Text = $"Timesheet metrics loaded at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Timesheets] Error: {ex.Message}");
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
            _allStaff.Clear();

            foreach (var s in staff)
            {
                _allStaff.Add(new AdminStaffMemberViewModel(s));
            }

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
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Work Deliverable Proof",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            _stagedFilePath = files[0].Path.LocalPath;
            TxtSelectedFilePath.Text = $"Selected: {Path.GetFileName(_stagedFilePath)} ({new FileInfo(_stagedFilePath).Length / 1024} KB)";
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
        if (string.IsNullOrWhiteSpace(_stagedFilePath) && string.IsNullOrWhiteSpace(url))
        {
            TxtSelectedFilePath.Text = "⚠️ Please select a file or enter an external URL.";
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_stagedFilePath) && File.Exists(_stagedFilePath))
            {
                await _apiClient.UploadDeliverableAsync(_modalTargetTaskId.Value, _stagedFilePath);
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                await _apiClient.AttachDeliverableLinkAsync(_modalTargetTaskId.Value, url, "External Proof Link");
            }

            AttachDeliverableModal.IsVisible = false;
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
            TxtStatusBar.Text = "Downloading timesheet CSV export...";
            var csvBytes = await _apiClient.ExportTimesheetCsvAsync(period: "today");

            if (csvBytes != null && csvBytes.Length > 0)
            {
                var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var filePath = Path.Combine(desktopDir, $"Punnya_Timesheet_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
                ShowToast("⚠️ Export CSV", "No timesheet records available for today.");
            }
        }
        catch (Exception ex)
        {
            ShowToast("⚠️ Export Failed", ex.Message);
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

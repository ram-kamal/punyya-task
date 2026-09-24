using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
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

    private readonly ObservableCollection<TaskItemViewModel> _tasks = new();
    private readonly ObservableCollection<LeaveItemViewModel> _leaves = new();
    private DispatcherTimer? _autoRefreshTimer;
    private DispatcherTimer? _toastDismissTimer;
    private DispatcherTimer? _shiftTimer;
    private DateTime? _shiftStartTime;

    public MainWindow()
    {
        InitializeComponent();

        TasksItemsControl.ItemsSource = _tasks;
        LeavesItemsControl.ItemsSource = _leaves;

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
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
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

    private void ApplyPlatformChromeLayout()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows 11 Fluent Mica TitleBar layout:
            // Native minimize/maximize/close are placed on the top-right (~140px)
            MacTrafficLightsStack.IsVisible = false;
            HeaderLeftStack.Margin = new Avalonia.Thickness(12, 0, 0, 0);
            HeaderRightStack.Margin = new Avalonia.Thickness(0, 0, 140, 0);
            TxtPlatformEdition.Text = "Windows 11 Mica";
            TxtTelemetryHook.Text = "Win32 & System Hook";
            Title = "Punnya Heart Desk — Windows 11 Mica Workstation";
        }
        else
        {
            // macOS TitleBar layout:
            // Native traffic lights are placed on the top-left (~76px)
            MacTrafficLightsStack.IsVisible = false;
            HeaderLeftStack.Margin = new Avalonia.Thickness(76, 0, 0, 0);
            HeaderRightStack.Margin = new Avalonia.Thickness(0, 0, 12, 0);
            TxtPlatformEdition.Text = "macOS Edition";
            TxtTelemetryHook.Text = "macOS CoreGraphics & Cocoa";
            Title = "Punnya Heart Desk — macOS Workstation";
        }
    }

    private void UpdateStationIdentifierFromWorkMode()
    {
        var machine = Environment.MachineName.ToUpper();
        var osPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "WIN" : "MAC";
        if (ChkWorkFromHome.IsChecked == true)
        {
            var cleanMachine = machine.Length > 10 ? machine[..10] : machine;
            _stationId = $"WFH-{osPrefix}-{cleanMachine}";
        }
        else
        {
            _stationId = $"{osPrefix}-{machine}";
        }

        TxtDevicePreview.Text = $"Paired Device: {_stationId}";
        TxtLoginStation.Text = _stationId;
    }

    private async Task InitializeWorkstationSessionAsync()
    {
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

        await LoadAssignedTasksAsync();

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _autoRefreshTimer.Tick += async (_, _) => await LoadAssignedTasksAsync(silent: true);
        _autoRefreshTimer.Start();
    }

    private void OnForegroundFocusChanged(string process, string title)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TxtActiveApp.Text = $"Active App: {process}";
            TxtStatusBar.Text = $"Focus: {title} ({DateTime.Now:HH:mm:ss})";
        });
    }

    private void OnBatchSynced(int count, DateTime syncedAt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TxtSyncInfo.Text = $"Synced {count} records ({syncedAt:HH:mm:ss})";
        });
    }

    private void OnRealtimeConnectionChanged(bool isConnected)
    {
        Dispatcher.UIThread.Post(() =>
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
        });
    }

    private void OnRealtimeTaskAssigned(TaskResponseDto task)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            ShowToast("📋 New Task Assigned", $"You have been assigned: {task.Title}");
            await LoadAssignedTasksAsync(silent: true);
        });
    }

    private void OnRealtimeTaskStatusChanged(Guid taskId, string newStatus, string? deliverableUrl)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            await LoadAssignedTasksAsync(silent: true);
        });
    }

    private void OnRealtimeWorkstationEventReceived(LiveWorkstationEventDto evt)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (evt.EventType != null && evt.EventType.StartsWith("LEAVE_"))
            {
                await LoadLeaveHistoryAsync();
                ShowToast("🏖️ Leave Request", evt.Details ?? "Leave update received");
            }
            else
            {
                await LoadAssignedTasksAsync(silent: true);
            }
            TxtStatusBar.Text = $"Event: {evt.EventType} • {evt.Details}";
        });
    }

    private void OnRealtimeLeaveUpdated(LiveWorkstationEventDto evt)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            ShowToast("🏖️ Leave Request Reviewed", evt.Details ?? "Your leave request was reviewed.");
            if (LeaveCenterModal.IsVisible)
            {
                await LoadLeaveHistoryAsync();
            }
        });
    }

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

            if (_tasks.Count == 0)
            {
                _tasks.Add(new TaskItemViewModel(new TaskResponseDto
                {
                    Id = Guid.NewGuid(),
                    TaskCode = "TSK-101",
                    Title = "Verify Ambulatory ECG Telemetry Stream",
                    Description = "Inspect real-time holter telemetry packets and log baseline cardiac metrics.",
                    Priority = "CRITICAL",
                    Status = "IN_PROGRESS",
                    DeliverableRequired = true,
                    DeliverableFileName = "ECG_Telemetry_Batch.pdf"
                }, true));

                _tasks.Add(new TaskItemViewModel(new TaskResponseDto
                {
                    Id = Guid.NewGuid(),
                    TaskCode = "TSK-102",
                    Title = "Weekly Cardiac Screening Camp Schedule",
                    Description = "Coordinate medical van dispatch, nurse assignments, and diagnostic kits.",
                    Priority = "HIGH",
                    Status = "ASSIGNED",
                    DeliverableRequired = true
                }, false));

                _tasks.Add(new TaskItemViewModel(new TaskResponseDto
                {
                    Id = Guid.NewGuid(),
                    TaskCode = "TSK-103",
                    Title = "Foundation OPD Workstation Audit",
                    Description = "Confirm clinician availability and workstation pairing for upcoming week.",
                    Priority = "MEDIUM",
                    Status = "COMPLETED",
                    DeliverableRequired = true,
                    DeliverableFileName = "OPD_Audit_Approved.xlsx"
                }, false));
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
                TxtShiftDigitalClock.Text = elapsed.ToString(@"hh\:mm\:ss");
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

    private void BtnStartPauseTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var taskVm = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (taskVm == null) return;

            if (_activeTaskId == taskId)
            {
                // Pause current task
                _activeTaskId = null;
                _activeTaskCode = null;
                taskVm.IsCurrentActive = false;
                _engine.SetCurrentTask(null, null);

                TxtActiveTaskBadge.Text = "No task linked";
                TxtActiveTaskBadge.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
            else
            {
                // Clear any other active task
                foreach (var t in _tasks) t.IsCurrentActive = false;

                // Start this task
                _activeTaskId = taskId;
                _activeTaskCode = taskVm.TaskCode;
                taskVm.IsCurrentActive = true;
                _engine.SetCurrentTask(taskId, taskVm.TaskCode);

                TxtActiveTaskBadge.Text = $"Linked: {taskVm.TaskCode}";
                TxtActiveTaskBadge.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
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
                await _apiClient.UpdateTaskStatusAsync(_modalTargetTaskId.Value, TaskItemStatus.IN_PROGRESS.ToString(), url, "External Link");
            }

            AttachDeliverableModal.IsVisible = false;
            ShowToast("✓ Deliverable Attached", "Work proof recorded. You may now complete this task.");
            await LoadAssignedTasksAsync(silent: true);
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
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Failed to complete task: {ex.Message}";
            }
        }
    }

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await LoadAssignedTasksAsync();
    }

    private async void BtnSyncNow_Click(object? sender, RoutedEventArgs e)
    {
        TxtSyncInfo.Text = "Syncing now...";
        await _syncWorker.SyncPendingBatchAsync();
    }

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

        // Reload remembered credentials into textboxes so employee doesn't have to retype
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

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        StopShiftClock();
        _autoRefreshTimer?.Stop();
        _toastDismissTimer?.Stop();
        _engine.Dispose();
        _syncWorker.Dispose();
        _realtimeClient.Dispose();
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PHF.Client.Core.Http;
using PHF.Client.Core.Realtime;
using PHF.Client.Core.Storage;
using PHF.Client.Core.Sync;
using PHF.Client.Telemetry;
using PHF.Core.DTOs;
using PHF.Core.Enums;

namespace PHF.Client.Wpf;

public partial class MainWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly TamperProofDatabase _database;
    private readonly WorkstationTelemetryEngine _engine;
    private readonly TelemetrySyncWorker _syncWorker;
    private readonly RealtimeClient _realtimeClient;

    private UserProfileDto? _currentUser;
    private string _stationId = $"WFH-{Environment.MachineName}";
    private Guid? _activeTaskId = null;
    private string? _activeTaskCode = null;
    private Guid? _modalTargetTaskId = null;
    private string? _stagedFilePath = null;

    private readonly ObservableCollection<TaskItemViewModel> _tasks = new();
    private readonly ObservableCollection<LeaveItemViewModel> _leaves = new();
    private DispatcherTimer? _autoRefreshTimer;
    private DispatcherTimer? _toastDismissTimer;

    public MainWindow()
    {
        InitializeComponent();

        TasksItemsControl.ItemsSource = _tasks;
        LeavesItemsControl.ItemsSource = _leaves;

        // Initialize Services (default production Render or saved config)
        var initialUrl = !string.IsNullOrWhiteSpace(TxtServerUrl.Text) ? TxtServerUrl.Text.Trim() : DefaultCloudRenderUrl;
        _apiClient = new ApiClient(initialUrl);
        _database = new TamperProofDatabase();
        _engine = new WorkstationTelemetryEngine(idleThresholdSeconds: 300);
        _syncWorker = new TelemetrySyncWorker(_database, _apiClient, _engine, syncIntervalSeconds: 30);
        _realtimeClient = new RealtimeClient(initialUrl, () => _apiClient.AuthToken);

        // Hook engine telemetry updates to UI
        _engine.FocusChanged += OnForegroundFocusChanged;
        _syncWorker.OnBatchSynced += OnBatchSynced;
        _realtimeClient.OnTaskAssigned += OnRealtimeTaskAssigned;
        _realtimeClient.OnTaskStatusChanged += OnRealtimeTaskStatusChanged;
        _realtimeClient.OnWorkstationEventReceived += OnRealtimeWorkstationEventReceived;
        _realtimeClient.OnLeaveUpdated += OnRealtimeLeaveUpdated;
        _realtimeClient.OnConnectionStateChanged += OnRealtimeConnectionChanged;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private const string DefaultCloudRenderUrl = "https://punnya-api.onrender.com";

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSavedServerConfig();
        UpdateStationIdentifierFromWorkMode();

        // Check authentication or prompt login
        if (!_apiClient.IsAuthenticated)
        {
            LoginModal.Visibility = Visibility.Visible;
        }
        else
        {
            await InitializeWorkstationSessionAsync();
        }
    }

    private void ChkWorkFromHome_Changed(object sender, RoutedEventArgs e)
    {
        UpdateStationIdentifierFromWorkMode();
    }

    private void UpdateStationIdentifierFromWorkMode()
    {
        if (ChkWorkFromHome == null || TxtLoginStation == null || TxtDevicePreview == null)
            return;

        var machine = Environment.MachineName.ToUpper();
        if (ChkWorkFromHome.IsChecked == true)
        {
            var cleanMachine = machine.Length > 12 ? machine.Substring(0, 12) : machine;
            TxtLoginStation.Text = $"WFH-{cleanMachine}";
            TxtDevicePreview.Text = $"Paired Device: WFH-{cleanMachine}";
        }
        else
        {
            var cleanMachine = machine.Length > 6 ? machine.Substring(0, 6) : machine;
            TxtLoginStation.Text = $"ST-{cleanMachine}";
            TxtDevicePreview.Text = $"Office Station: ST-{cleanMachine}";
        }
    }


    private void LoadSavedServerConfig()
    {
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHF");
            var configFile = Path.Combine(configDir, "server_url.txt");
            if (File.Exists(configFile))
            {
                var saved = File.ReadAllText(configFile).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    TxtServerUrl.Text = saved;
                }
            }
        }
        catch { }
    }

    private void SaveServerConfig(string url)
    {
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHF");
            Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "server_url.txt"), url);
        }
        catch { }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _autoRefreshTimer?.Stop();
        _toastDismissTimer?.Stop();
        _engine.Stop();
        _syncWorker.Stop();
        _ = _realtimeClient.StopAsync();
    }

    private async Task InitializeWorkstationSessionAsync()
    {
        LoginModal.Visibility = Visibility.Collapsed;

        if (_currentUser != null)
        {
            TxtUserInfo.Text = $"{_currentUser.FullName} ({_stationId})";
        }

        // Configure sync worker and start engines
        if (_currentUser != null)
        {
            _syncWorker.ConfigureUser(_currentUser.Id.ToString(), _currentUser.FullName, _stationId);
        }

        _engine.Start();
        _syncWorker.Start();

        // 10-second periodic auto-refresh timer as robust background sync
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _autoRefreshTimer.Tick += async (s, e) =>
        {
            await RefreshTasksAsync();
            await RefreshLeavesAsync();
            await RefreshShiftStatusAsync();
        };
        _autoRefreshTimer.Start();

        // Connect Realtime SignalR (graceful fallback if server is waking up)
        try
        {
            await _realtimeClient.StartAsync(_stationId, _currentUser?.Id.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignalR init warning: {ex.Message}");
        }

        // Load tasks, shift status, and leaves immediately
        await RefreshTasksAsync();
        await RefreshShiftStatusAsync();
        await RefreshLeavesAsync();

        TxtStatusBar.Text = $"Active Workstation: {_stationId} • Real-time Task & Telemetry Sync Active";
    }

    private async Task RefreshShiftStatusAsync()
    {
        try
        {
            var shift = await _apiClient.GetCurrentShiftAsync();
            if (shift != null && shift.Status == ShiftStatus.ACTIVE.ToString())
            {
                TxtShiftState.Text = "Clocked In";
                TxtShiftState.Foreground = (SolidColorBrush)FindResource("AccentGreen");
                TxtShiftHours.Text = $"Logged Today: {shift.TotalHoursToday:F1}h";
                BtnClockToggle.Content = "Clock Out";
                BtnClockToggle.Background = (SolidColorBrush)FindResource("AccentAmber");
            }
            else
            {
                TxtShiftState.Text = "Clocked Out";
                TxtShiftState.Foreground = (SolidColorBrush)FindResource("AccentAmber");
                TxtShiftHours.Text = "Shift not currently active";
                BtnClockToggle.Content = "Clock In";
                BtnClockToggle.Background = (SolidColorBrush)FindResource("AccentGreen");
            }
        }
        catch
        {
            // Server may be offline or starting
        }
    }

    private async Task RefreshTasksAsync()
    {
        try
        {
            var tasks = await _apiClient.GetAssignedTasksAsync(_currentUser?.Id, _stationId, includeUnassigned: true);
            _tasks.Clear();

            foreach (var task in tasks)
            {
                var isCurrentActive = _activeTaskId == task.Id;
                _tasks.Add(new TaskItemViewModel(task, isCurrentActive));
            }

            // Restore active task if in progress
            if (!_activeTaskId.HasValue)
            {
                var inProgressTask = tasks.FirstOrDefault(t => t.Status == TaskItemStatus.IN_PROGRESS.ToString());
                if (inProgressTask != null)
                {
                    SetActiveTask(inProgressTask.Id, inProgressTask.TaskCode);
                }
            }
        }
        catch (Exception ex)
        {
            TxtStatusBar.Text = $"Error loading tasks: {ex.Message}";
        }
    }

    private void SetActiveTask(Guid? taskId, string? taskCode)
    {
        _activeTaskId = taskId;
        _activeTaskCode = taskCode;
        _engine.SetCurrentTask(taskId, taskCode);

        if (taskId.HasValue && !string.IsNullOrWhiteSpace(taskCode))
        {
            TxtActiveTaskBadge.Text = $"Linked: {taskCode}";
            TxtActiveTaskBadge.Visibility = Visibility.Visible;
        }
        else
        {
            TxtActiveTaskBadge.Text = "No task linked";
        }

        // Update view models button state
        foreach (var t in _tasks)
        {
            t.IsCurrentActive = t.Id == taskId;
        }
    }

    private void OnForegroundFocusChanged(string processName, string windowTitle)
    {
        Dispatcher.Invoke(() =>
        {
            TxtActiveApp.Text = $"Active App: {processName}";
            TxtStatusBar.Text = $"Focus changed: {processName} — \"{windowTitle}\"";
        });
    }

    private void OnBatchSynced(int recordsCount, DateTime syncedAt)
    {
        Dispatcher.Invoke(() =>
        {
            TxtSyncInfo.Text = $"Tamper-locked SQLite buffer: {recordsCount} synced ({syncedAt:HH:mm:ss})";
        });
    }

    private void OnRealtimeTaskAssigned(TaskResponseDto? task)
    {
        Dispatcher.Invoke(async () =>
        {
            await RefreshTasksAsync();
            if (task != null)
            {
                bool isRelevant = task.AssignedTo == null || 
                                  task.AssignedTo == _currentUser?.Id || 
                                  (!string.IsNullOrWhiteSpace(_stationId) && task.AssignedToStation == _stationId);

                if (isRelevant)
                {
                    ShowTaskToastNotification(task);
                }
            }
        });
    }

    private void OnRealtimeTaskStatusChanged(Guid taskId, string newStatus, string? deliverableUrl)
    {
        Dispatcher.Invoke(async () =>
        {
            await RefreshTasksAsync();
        });
    }

    private void ShowTaskToastNotification(TaskResponseDto task)
    {
        try
        {
            TxtToastIcon.Text = "⚡";
            TxtToastTitle.Text = $"New Task: {task.TaskCode}";
            TxtToastTitle.Foreground = (SolidColorBrush)FindResource("AccentRoseLight");
            TxtToastMessage.Text = $"{task.Title}\nPriority: {task.Priority} • Deliverable: {(task.DeliverableRequired ? "Required" : "Optional")}";
            NotificationToast.Visibility = Visibility.Visible;

            _toastDismissTimer?.Stop();
            _toastDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            _toastDismissTimer.Tick += (s, e) =>
            {
                NotificationToast.Visibility = Visibility.Collapsed;
                _toastDismissTimer?.Stop();
            };
            _toastDismissTimer.Start();
        }
        catch { }
    }

    private void OnRealtimeConnectionChanged(bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            if (isConnected)
            {
                TxtConnectionStatus.Text = "● ONLINE";
                StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                StatusBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129));
                TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }
            else
            {
                TxtConnectionStatus.Text = "○ RECONNECTING";
                StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(40, 225, 29, 72));
                StatusBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(70, 225, 29, 72));
                TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 113, 133));
            }
        });
    }

    private void OnRealtimeLeaveUpdated(LiveWorkstationEventDto evt)
    {
        Dispatcher.Invoke(async () =>
        {
            await RefreshLeavesAsync();
            ShowLeaveToastNotification(evt);
            TxtStatusBar.Text = $"Leave Update: {evt.Details}";
        });
    }

    private void OnRealtimeWorkstationEventReceived(LiveWorkstationEventDto evt)
    {
        if (evt.EventType != null && evt.EventType.StartsWith("LEAVE_"))
        {
            Dispatcher.Invoke(async () =>
            {
                await RefreshLeavesAsync();
                ShowLeaveToastNotification(evt);
                TxtStatusBar.Text = $"Leave Update: {evt.Details}";
            });
        }
        else if (evt.EventType != null && evt.EventType.StartsWith("TASK_"))
        {
            Dispatcher.Invoke(async () =>
            {
                await RefreshTasksAsync();
                TxtStatusBar.Text = $"Task Update: {evt.Details}";
            });
        }
    }

    private void ShowLeaveToastNotification(LiveWorkstationEventDto evt)
    {
        try
        {
            var isApproved = evt.Details?.Contains("APPROVED", StringComparison.OrdinalIgnoreCase) == true;
            var isRejected = evt.Details?.Contains("REJECTED", StringComparison.OrdinalIgnoreCase) == true;

            TxtToastIcon.Text = isApproved ? "✅" : (isRejected ? "❌" : "🏖️");
            TxtToastTitle.Text = isApproved ? "Leave Request Approved!" : (isRejected ? "Leave Request Rejected" : "Leave Request Updated");
            TxtToastTitle.Foreground = isApproved 
                ? (SolidColorBrush)FindResource("AccentGreenSoft") 
                : (isRejected ? (SolidColorBrush)FindResource("AccentRedHover") : (SolidColorBrush)FindResource("AccentBlueLight"));

            TxtToastMessage.Text = evt.Details ?? "Your leave request status was updated by management.";
            NotificationToast.Visibility = Visibility.Visible;

            _toastDismissTimer?.Stop();
            _toastDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(7)
            };
            _toastDismissTimer.Tick += (s, e) =>
            {
                _toastDismissTimer?.Stop();
                NotificationToast.Visibility = Visibility.Collapsed;
            };
            _toastDismissTimer.Start();
        }
        catch
        {
            // Ignore UI toast errors
        }
    }

    private void BtnDismissToast_Click(object sender, RoutedEventArgs e)
    {
        _toastDismissTimer?.Stop();
        NotificationToast.Visibility = Visibility.Collapsed;
    }

    // --- Action Handlers ---

    private async void BtnDoLogin_Click(object sender, RoutedEventArgs e)
    {
        var email = TxtLoginEmail.Text.Trim();
        var password = TxtLoginPassword.Password;
        var station = TxtLoginStation.Text.Trim();
        var currentUrl = TxtServerUrl.Text.Trim();

        TxtLoginError.Visibility = Visibility.Collapsed;
        BtnDoLogin.IsEnabled = false;

        try
        {
            // Auto-candidate URLs to try silently so employees never need to type any URL
            var candidateUrls = new List<string>();
            if (!string.IsNullOrWhiteSpace(currentUrl)) candidateUrls.Add(currentUrl);
            if (!candidateUrls.Contains(DefaultCloudRenderUrl)) candidateUrls.Add(DefaultCloudRenderUrl);

            AuthResponse? auth = null;
            string? workingUrl = null;
            Exception? lastEx = null;

            foreach (var url in candidateUrls)
            {
                try
                {
                    _apiClient.SetBaseUrl(url);
                    _realtimeClient.SetBaseUrl(url);
                    auth = await _apiClient.LoginAsync(email, password, station);
                    if (auth != null)
                    {
                        workingUrl = url;
                        TxtServerUrl.Text = url;
                        SaveServerConfig(url);
                        break;
                    }
                }
                catch (AccountSuspendedException ex)
                {
                    // Account suspended or explicit business failure
                    TxtLoginError.Text = ex.Message;
                    TxtLoginError.Visibility = Visibility.Visible;
                    BtnDoLogin.IsEnabled = true;
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    // Silently try next candidate
                }
            }

            if (auth != null)
            {
                _currentUser = auth.User;
                _stationId = !string.IsNullOrWhiteSpace(station) ? station : (auth.User.StationId ?? $"WFH-{Environment.MachineName}");
                await InitializeWorkstationSessionAsync();
            }
            else if (lastEx != null && workingUrl == null)
            {
                TxtLoginError.Text = $"Connection failed: {lastEx.Message}. (Tried local and Render cloud)";
                TxtLoginError.Visibility = Visibility.Visible;
            }
            else
            {
                TxtLoginError.Text = "Invalid username/email or password. Please try again.";
                TxtLoginError.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            TxtLoginError.Text = $"Connection failed: {ex.Message}";
            TxtLoginError.Visibility = Visibility.Visible;
        }
        finally
        {
            BtnDoLogin.IsEnabled = true;
        }
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _engine.Stop();
        _syncWorker.Stop();
        _ = _realtimeClient.StopAsync();

        _currentUser = null;
        _activeTaskId = null;
        _activeTaskCode = null;

        LoginModal.Visibility = Visibility.Visible;
    }

    private async void BtnClockToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (BtnClockToggle.Content?.ToString() == "Clock In")
            {
                await _apiClient.ClockInAsync(_stationId);
            }
            else
            {
                await _apiClient.ClockOutAsync(_stationId);
            }

            await RefreshShiftStatusAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to toggle shift: {ex.Message}", "Shift Management", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnStartPauseTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var vm = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (vm == null || vm.IsCompleted) return;

            if (_activeTaskId == taskId)
            {
                // Pause task
                SetActiveTask(null, null);
            }
            else
            {
                // Set as active task and update status to IN_PROGRESS if not already
                SetActiveTask(taskId, vm.TaskCode);

                if (vm.Status != TaskItemStatus.IN_PROGRESS.ToString())
                {
                    try
                    {
                        await _apiClient.UpdateTaskStatusAsync(taskId, TaskItemStatus.IN_PROGRESS.ToString());
                        await RefreshTasksAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not update task status: {ex.Message}", "Task Management", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
    }

    private void BtnAttachDeliverable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var vm = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (vm == null || vm.IsCompleted) return;

            _modalTargetTaskId = taskId;
            _stagedFilePath = null;
            TxtSelectedFilePath.Text = "No file selected";
            TxtDeliverableUrlInput.Text = "";
            AttachDeliverableModal.Visibility = Visibility.Visible;
        }
    }

    private void BtnCancelDeliverable_Click(object sender, RoutedEventArgs e)
    {
        AttachDeliverableModal.Visibility = Visibility.Collapsed;
        _modalTargetTaskId = null;
        _stagedFilePath = null;
    }

    private async void BtnSaveDeliverable_Click(object sender, RoutedEventArgs e)
    {
        if (!_modalTargetTaskId.HasValue) return;

        var taskId = _modalTargetTaskId.Value;
        var urlInput = TxtDeliverableUrlInput.Text.Trim();

        try
        {
            if (!string.IsNullOrWhiteSpace(_stagedFilePath) && File.Exists(_stagedFilePath))
            {
                await _apiClient.UploadDeliverableAsync(taskId, _stagedFilePath);
            }
            else if (!string.IsNullOrWhiteSpace(urlInput))
            {
                await _apiClient.UpdateTaskStatusAsync(taskId, TaskItemStatus.IN_PROGRESS.ToString(), urlInput, "External Link");
            }
            else
            {
                MessageBox.Show("Please select a deliverable file or enter a valid URL.", "Proof of Work Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AttachDeliverableModal.Visibility = Visibility.Collapsed;
            _modalTargetTaskId = null;
            _stagedFilePath = null;

            await RefreshTasksAsync();
            MessageBox.Show("Deliverable attached successfully! The task is now eligible for completion.", "Deliverable Gate", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to attach deliverable: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                _stagedFilePath = files[0];
                TxtSelectedFilePath.Text = Path.GetFileName(_stagedFilePath);
            }
        }
    }

    private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Task Deliverable (Proof of Work)",
            Filter = "All Files (*.*)|*.*|Zip Archives (*.zip)|*.zip|Documents (*.pdf;*.docx)|*.pdf;*.docx|Media Files (*.mp4;*.mov)|*.mp4;*.mov"
        };

        if (dlg.ShowDialog() == true)
        {
            _stagedFilePath = dlg.FileName;
            TxtSelectedFilePath.Text = Path.GetFileName(_stagedFilePath);
        }
    }

    private async void BtnMarkCompleted_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var vm = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (vm == null || vm.IsCompleted) return;

            // Deliverable Gate Validation (punnya-combined-spec.pptx)
            if (vm.DeliverableRequired && !vm.HasDeliverable)
            {
                MessageBox.Show(
                    "Deliverable Gate Enforced:\nA task cannot be marked completed without an attached file or link as proof of work.",
                    "Proof of Work Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _apiClient.UpdateTaskStatusAsync(taskId, TaskItemStatus.COMPLETED.ToString());

                // If active, clear active task
                if (_activeTaskId == taskId)
                {
                    SetActiveTask(null, null);
                }

                await RefreshTasksAsync();
                MessageBox.Show($"Task {vm.TaskCode} successfully completed!", "Task Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to mark task completed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnSyncNow_Click(object sender, RoutedEventArgs e)
    {
        BtnSyncNow.IsEnabled = false;
        TxtStatusBar.Text = "Flushing offline SQLite buffer to central API...";
        await _syncWorker.SyncPendingBatchAsync();
        BtnSyncNow.IsEnabled = true;
        TxtStatusBar.Text = "Telemetry sync completed.";
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        BtnRefresh.IsEnabled = false;
        await RefreshTasksAsync();
        await RefreshShiftStatusAsync();
        await RefreshLeavesAsync();
        BtnRefresh.IsEnabled = true;
    }

    private async void BtnOpenLeaveCenter_Click(object sender, RoutedEventArgs e)
    {
        LeaveCenterModal.Visibility = Visibility.Visible;
        TxtLeaveFormMessage.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(TxtLeaveStartDate.Text))
        {
            TxtLeaveStartDate.Text = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
            TxtLeaveEndDate.Text = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd");
        }
        await RefreshLeavesAsync();
    }

    private void BtnCloseLeaveCenter_Click(object sender, RoutedEventArgs e)
    {
        LeaveCenterModal.Visibility = Visibility.Collapsed;
    }

    private async void BtnRefreshLeaves_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLeavesAsync();
    }

    private async void BtnSubmitLeaveApplication_Click(object sender, RoutedEventArgs e)
    {
        TxtLeaveFormMessage.Visibility = Visibility.Collapsed;

        var typeItem = (ComboBoxItem)CmbLeaveType.SelectedItem;
        var leaveType = typeItem?.Tag?.ToString() ?? "CASUAL";

        if (!DateTime.TryParse(TxtLeaveStartDate.Text.Trim(), out var startDate))
        {
            MessageBox.Show("Please enter a valid start date (YYYY-MM-DD).", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!DateTime.TryParse(TxtLeaveEndDate.Text.Trim(), out var endDate))
        {
            MessageBox.Show("Please enter a valid end date (YYYY-MM-DD).", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (endDate.Date < startDate.Date)
        {
            MessageBox.Show("End date cannot be earlier than start date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reason = TxtLeaveReason.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            MessageBox.Show("Please provide a reason for the leave application.", "Reason Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnSubmitLeaveApplication.IsEnabled = false;

        try
        {
            var res = await _apiClient.ApplyLeaveAsync(new CreateLeaveRequestDto
            {
                LeaveType = leaveType,
                StartDate = startDate,
                EndDate = endDate,
                Reason = reason
            });

            if (res != null)
            {
                TxtLeaveReason.Text = "";
                TxtLeaveFormMessage.Text = $"✓ {leaveType} leave applied successfully ({res.TotalDays} day{(res.TotalDays > 1 ? "s" : "")})!";
                TxtLeaveFormMessage.Visibility = Visibility.Visible;
                await RefreshLeavesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to submit leave application: {ex.Message}", "Submission Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSubmitLeaveApplication.IsEnabled = true;
        }
    }

    private async Task RefreshLeavesAsync()
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
            System.Diagnostics.Debug.WriteLine($"Failed to refresh leaves: {ex.Message}");
        }
    }
}

public class TaskItemViewModel : INotifyPropertyChanged
{
    private readonly TaskResponseDto _dto;
    private bool _isCurrentActive;

    public TaskItemViewModel(TaskResponseDto dto, bool isCurrentActive = false)
    {
        _dto = dto;
        _isCurrentActive = isCurrentActive;
    }

    public Guid Id => _dto.Id;
    public string TaskCode => _dto.TaskCode;
    public string Title => _dto.Title;
    public string Description => _dto.Description ?? "No description provided.";
    public string Priority => _dto.Priority;
    public string Status => _dto.Status;
    public bool DeliverableRequired => _dto.DeliverableRequired;
    public string? DeliverableUrl => _dto.DeliverableUrl;
    public string? DeliverableFileName => _dto.DeliverableFileName;

    public bool HasDeliverable => !string.IsNullOrWhiteSpace(_dto.DeliverableUrl) || 
                                  !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) ||
                                  _dto.Attachments.Any();

    // Deliverable Gate: Enabled only when deliverable is attached (or not required)
    public bool CanComplete => !DeliverableRequired || HasDeliverable;

    // Completed State: Hides active action buttons (Start/Attach/Complete) and displays completed ribbon
    public bool IsCompleted => _dto.Status == "COMPLETED";
    public Visibility ActionButtonsVisibility => IsCompleted ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CompletedBadgeVisibility => IsCompleted ? Visibility.Visible : Visibility.Collapsed;

    public bool IsCurrentActive
    {
        get => _isCurrentActive;
        set
        {
            _isCurrentActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartPauseButtonText));
            OnPropertyChanged(nameof(StartPauseButtonBrush));
        }
    }

    public string StatusText => _dto.Status switch
    {
        "IN_PROGRESS" => "In Progress",
        "ASSIGNED" => "Awaiting Start",
        "UNDER_REVIEW" => "Under Review",
        "COMPLETED" => "Completed",
        _ => _dto.Status
    };

    public SolidColorBrush StatusColorBrush => _dto.Status switch
    {
        "COMPLETED" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromRgb(225, 29, 72)),
        _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
    };

    // Slide 4 Wireframe Text
    public string DeliverableStatusText
    {
        get
        {
            if (_dto.Status == "COMPLETED")
            {
                var name = !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) ? _dto.DeliverableFileName : "Deliverable";
                return $"{name} attached ✓";
            }

            if (HasDeliverable)
            {
                var name = !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) ? _dto.DeliverableFileName : "Deliverable";
                return $"{name} attached ✓ — Ready to complete";
            }

            if (_dto.Status == "IN_PROGRESS")
            {
                return "Deliverable required to close — not yet attached";
            }

            return "No deliverable due yet";
        }
    }

    public SolidColorBrush DeliverableColorBrush
    {
        get
        {
            if (HasDeliverable || _dto.Status == "COMPLETED")
            {
                return new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Green
            }

            if (_dto.Status == "IN_PROGRESS")
            {
                return new SolidColorBrush(Color.FromRgb(251, 191, 36)); // Amber
            }

            return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Muted
        }
    }

    public SolidColorBrush PriorityBgBrush => _dto.Priority switch
    {
        "CRITICAL" => new SolidColorBrush(Color.FromArgb(50, 225, 29, 72)),
        "HIGH" => new SolidColorBrush(Color.FromArgb(50, 245, 158, 11)),
        "MEDIUM" => new SolidColorBrush(Color.FromArgb(40, 244, 63, 94)),
        _ => new SolidColorBrush(Color.FromArgb(30, 148, 163, 184))
    };

    public SolidColorBrush PriorityFgBrush => _dto.Priority switch
    {
        "CRITICAL" => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        "HIGH" => new SolidColorBrush(Color.FromRgb(252, 211, 77)),
        "MEDIUM" => new SolidColorBrush(Color.FromRgb(253, 164, 175)),
        _ => new SolidColorBrush(Color.FromRgb(203, 213, 225))
    };

    public SolidColorBrush StatusBgBrush => _dto.Status switch
    {
        "COMPLETED" => new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromArgb(40, 225, 29, 72)),
        _ => new SolidColorBrush(Color.FromArgb(25, 148, 163, 184))
    };

    public SolidColorBrush DeliverableBgBrush
    {
        get
        {
            if (HasDeliverable || _dto.Status == "COMPLETED")
                return new SolidColorBrush(Color.FromArgb(35, 16, 185, 129));
            if (_dto.Status == "IN_PROGRESS")
                return new SolidColorBrush(Color.FromArgb(35, 245, 158, 11));
            return new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        }
    }

    public string StartPauseButtonText => _isCurrentActive ? "Pause Task" : "Start Task";

    public SolidColorBrush StartPauseButtonBrush => _isCurrentActive
        ? new SolidColorBrush(Color.FromRgb(245, 158, 11))  // Amber for Pause
        : new SolidColorBrush(Color.FromRgb(225, 29, 72));  // Rose for Start

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class LeaveItemViewModel
{
    private readonly LeaveResponseDto _dto;

    public LeaveItemViewModel(LeaveResponseDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string LeaveType => _dto.LeaveType;
    public string Reason => _dto.Reason;
    public string Status => _dto.Status;

    public string DateRangeText =>
        $"{_dto.StartDate:MMM dd} – {_dto.EndDate:MMM dd, yyyy} • {_dto.TotalDays} Day{(_dto.TotalDays > 1 ? "s" : "")}";

    public SolidColorBrush StatusBgBrush => _dto.Status switch
    {
        "APPROVED" => new SolidColorBrush(Color.FromArgb(40, 34, 197, 94)),
        "REJECTED" => new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11))
    };

    public SolidColorBrush StatusFgBrush => _dto.Status switch
    {
        "APPROVED" => new SolidColorBrush(Color.FromRgb(110, 231, 183)),
        "REJECTED" => new SolidColorBrush(Color.FromRgb(252, 165, 165)),
        _ => new SolidColorBrush(Color.FromRgb(252, 211, 77))
    };

    public Visibility ReviewSectionVisibility =>
        !string.IsNullOrWhiteSpace(_dto.ReviewComments) || !string.IsNullOrWhiteSpace(_dto.ReviewerName)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string ReviewText =>
        $"Manager: {_dto.ReviewerName ?? "Supervisor"} — \"{_dto.ReviewComments ?? "Reviewed"}\"";
}
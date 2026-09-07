using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private string _stationId = "ST-101";
    private Guid? _activeTaskId = null;
    private string? _activeTaskCode = null;
    private Guid? _modalTargetTaskId = null;
    private string? _stagedFilePath = null;

    private readonly ObservableCollection<TaskItemViewModel> _tasks = new();

    public MainWindow()
    {
        InitializeComponent();

        TasksItemsControl.ItemsSource = _tasks;

        // Initialize Services (default port 5020)
        _apiClient = new ApiClient("http://localhost:5020");
        _database = new TamperProofDatabase();
        _engine = new WorkstationTelemetryEngine(idleThresholdSeconds: 300);
        _syncWorker = new TelemetrySyncWorker(_database, _apiClient, _engine, syncIntervalSeconds: 30);
        _realtimeClient = new RealtimeClient("http://localhost:5020", () => _apiClient.AuthToken);

        // Hook engine telemetry updates to UI
        _engine.FocusChanged += OnForegroundFocusChanged;
        _syncWorker.OnBatchSynced += OnBatchSynced;
        _realtimeClient.OnTaskAssigned += OnRealtimeTaskAssigned;
        _realtimeClient.OnConnectionStateChanged += OnRealtimeConnectionChanged;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
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

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
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

        // Connect Realtime SignalR
        await _realtimeClient.StartAsync(_stationId);

        // Load tasks and shift status
        await RefreshShiftStatusAsync();
        await RefreshTasksAsync();

        TxtStatusBar.Text = $"Active Workstation: {_stationId} • Telemetry hook listening on user32.dll";
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
            var tasks = await _apiClient.GetAssignedTasksAsync(_currentUser?.Id);
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

    private void OnRealtimeTaskAssigned(TaskResponseDto task)
    {
        Dispatcher.Invoke(async () =>
        {
            await RefreshTasksAsync();
            MessageBox.Show(
                $"New Task Dispatched by Manager!\n\n[{task.TaskCode}] {task.Title}\nPriority: {task.Priority}",
                "Task Assigned — PHF Work Management",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    private void OnRealtimeConnectionChanged(bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            if (isConnected)
            {
                TxtConnectionStatus.Text = "● ONLINE";
                StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(22, 101, 52));
                TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(134, 239, 172));
            }
            else
            {
                TxtConnectionStatus.Text = "○ OFFLINE";
                StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165));
            }
        });
    }

    // --- Action Handlers ---

    private async void BtnDoLogin_Click(object sender, RoutedEventArgs e)
    {
        var email = TxtLoginEmail.Text.Trim();
        var password = TxtLoginPassword.Password;
        var station = TxtLoginStation.Text.Trim();
        var serverUrl = TxtServerUrl.Text.Trim();

        TxtLoginError.Visibility = Visibility.Collapsed;
        BtnDoLogin.IsEnabled = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                _apiClient.SetBaseUrl(serverUrl);
                _realtimeClient.SetBaseUrl(serverUrl);
            }

            AuthResponse? auth = null;
            try
            {
                auth = await _apiClient.LoginAsync(email, password, station);
            }
            catch (Exception)
            {
                // If connecting failed, try auto-fallback to alternate port (5020 <-> 5000)
                var alternateUrl = serverUrl.Contains("5020") 
                    ? serverUrl.Replace("5020", "5000") 
                    : (serverUrl.Contains("5000") ? serverUrl.Replace("5000", "5020") : null);

                if (!string.IsNullOrWhiteSpace(alternateUrl))
                {
                    try
                    {
                        _apiClient.SetBaseUrl(alternateUrl);
                        _realtimeClient.SetBaseUrl(alternateUrl);
                        auth = await _apiClient.LoginAsync(email, password, station);
                        if (auth != null)
                        {
                            TxtServerUrl.Text = alternateUrl;
                        }
                    }
                    catch
                    {
                        // Restore original to display error
                        _apiClient.SetBaseUrl(serverUrl);
                        _realtimeClient.SetBaseUrl(serverUrl);
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            if (auth != null)
            {
                _currentUser = auth.User;
                _stationId = !string.IsNullOrWhiteSpace(station) ? station : (auth.User.StationId ?? "ST-101");
                await InitializeWorkstationSessionAsync();
            }
            else
            {
                TxtLoginError.Text = "Invalid email or password. Please try again.";
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
            if (vm == null) return;

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
            if (vm == null) return;

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
        BtnRefresh.IsEnabled = true;
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
        "COMPLETED" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
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

    public string StartPauseButtonText => _isCurrentActive ? "Pause Task" : "Start Task";

    public SolidColorBrush StartPauseButtonBrush => _isCurrentActive
        ? new SolidColorBrush(Color.FromRgb(245, 158, 11))  // Amber for Pause
        : new SolidColorBrush(Color.FromRgb(59, 130, 246));  // Blue for Start

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
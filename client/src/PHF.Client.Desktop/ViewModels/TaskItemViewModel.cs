using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using PHF.Core.DTOs;

namespace PHF.Client.Desktop.ViewModels;

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
    public string TaskCode => _dto.TaskCode ?? $"TSK-{_dto.Id.ToString()[..4].ToUpper()}";
    public string Title => _dto.Title;
    public string Description => !string.IsNullOrWhiteSpace(_dto.Description) ? _dto.Description : "No additional description specified.";
    public string Priority => _dto.Priority ?? "MEDIUM";
    public string Status => _dto.Status ?? "ASSIGNED";

    public bool IsCompleted => _dto.Status == "COMPLETED";
    public bool HasDeliverable => !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) || !string.IsNullOrWhiteSpace(_dto.DeliverableUrl) || (_dto.Attachments != null && _dto.Attachments.Count > 0);
    public bool CanComplete => HasDeliverable && _dto.Status != "COMPLETED";

    public bool IsActionButtonsVisible => !IsCompleted;
    public bool IsCompletedBadgeVisible => IsCompleted;

    public bool IsCurrentActive
    {
        get => _isCurrentActive;
        set
        {
            if (_isCurrentActive != value)
            {
                _isCurrentActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartPauseButtonText));
                OnPropertyChanged(nameof(StartPauseButtonBrush));
            }
        }
    }

    public string StatusText => _dto.Status switch
    {
        "IN_PROGRESS" => "In Progress",
        "ASSIGNED" => "Awaiting Start",
        "UNDER_REVIEW" => "Under Review",
        "COMPLETED" => "Completed",
        _ => _dto.Status ?? "Assigned"
    };

    public IBrush StatusColorBrush => _dto.Status switch
    {
        "COMPLETED" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromRgb(225, 29, 72)),
        _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
    };

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
                return "Proof of work deliverable required to close";
            }

            return "No deliverable due yet";
        }
    }

    public IBrush DeliverableColorBrush
    {
        get
        {
            if (HasDeliverable || _dto.Status == "COMPLETED")
            {
                return new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Emerald green
            }

            if (_dto.Status == "IN_PROGRESS")
            {
                return new SolidColorBrush(Color.FromRgb(251, 191, 36)); // Amber
            }

            return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Muted
        }
    }

    public IBrush PriorityBgBrush => _dto.Priority switch
    {
        "CRITICAL" => new SolidColorBrush(Color.FromArgb(40, 225, 29, 72)),
        "HIGH" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
        "MEDIUM" => new SolidColorBrush(Color.FromArgb(35, 244, 63, 94)),
        _ => new SolidColorBrush(Color.FromArgb(30, 148, 163, 184))
    };

    public IBrush PriorityFgBrush => _dto.Priority switch
    {
        "CRITICAL" => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        "HIGH" => new SolidColorBrush(Color.FromRgb(252, 211, 77)),
        "MEDIUM" => new SolidColorBrush(Color.FromRgb(253, 164, 175)),
        _ => new SolidColorBrush(Color.FromRgb(203, 213, 225))
    };

    public IBrush StatusBgBrush => _dto.Status switch
    {
        "COMPLETED" => new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromArgb(35, 225, 29, 72)),
        _ => new SolidColorBrush(Color.FromArgb(25, 148, 163, 184))
    };

    public IBrush DeliverableBgBrush
    {
        get
        {
            if (HasDeliverable || _dto.Status == "COMPLETED")
                return new SolidColorBrush(Color.FromArgb(30, 16, 185, 129));
            if (_dto.Status == "IN_PROGRESS")
                return new SolidColorBrush(Color.FromArgb(30, 245, 158, 11));
            return new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
        }
    }

    public string StartPauseButtonText => _isCurrentActive ? "Pause Task" : "Start Task";

    public IBrush StartPauseButtonBrush => _isCurrentActive
        ? new SolidColorBrush(Color.FromRgb(245, 158, 11))  // Amber for Pause
        : new SolidColorBrush(Color.FromRgb(225, 29, 72));  // Rose for Start

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

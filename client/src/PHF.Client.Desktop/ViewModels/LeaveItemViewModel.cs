using System;
using Avalonia.Media;
using PHF.Core.DTOs;

namespace PHF.Client.Desktop.ViewModels;

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

    public IBrush StatusBgBrush => _dto.Status switch
    {
        "APPROVED" => new SolidColorBrush(Color.FromArgb(40, 34, 197, 94)),
        "REJECTED" => new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11))
    };

    public IBrush StatusFgBrush => _dto.Status switch
    {
        "APPROVED" => new SolidColorBrush(Color.FromRgb(110, 231, 183)),
        "REJECTED" => new SolidColorBrush(Color.FromRgb(252, 165, 165)),
        _ => new SolidColorBrush(Color.FromRgb(252, 211, 77))
    };

    public bool HasReview => !string.IsNullOrWhiteSpace(_dto.ReviewComments) || !string.IsNullOrWhiteSpace(_dto.ReviewerName);

    public string ReviewText =>
        $"Reviewer: {_dto.ReviewerName ?? "Manager"} — \"{_dto.ReviewComments ?? "Reviewed"}\"";
}

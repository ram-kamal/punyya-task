using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize(Roles = "ADMIN,MANAGER,MONITOR")]
[ApiController]
[Route("api/v1/[controller]")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllStaff(CancellationToken cancellationToken)
    {
        var staff = await _staffService.GetAllStaffAsync(cancellationToken);
        return Ok(staff);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStaffById(Guid id, CancellationToken cancellationToken)
    {
        var member = await _staffService.GetStaffByIdAsync(id, cancellationToken);
        if (member == null) return NotFound(new { message = $"Staff member {id} not found." });
        return Ok(member);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email or Username is required." });

        try
        {
            var created = await _staffService.CreateStaffAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetStaffById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var success = await _staffService.ToggleActiveStatusAsync(id, cancellationToken);
        if (!success) return NotFound(new { message = $"Staff member {id} not found." });
        return Ok(new { success = true });
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "New password cannot be empty." });

        var success = await _staffService.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
        if (!success) return NotFound(new { message = $"Staff member {id} not found." });
        return Ok(new { success = true, message = "Password updated successfully." });
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPatch("{id:guid}/work-mode")]
    public async Task<IActionResult> UpdateWorkMode(Guid id, [FromBody] UpdateWorkModeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkMode))
            return BadRequest(new { message = "Work mode must be IN_OFFICE or REMOTE_WFH." });

        var success = await _staffService.UpdateWorkModeAsync(id, request.WorkMode, cancellationToken);
        if (!success) return NotFound(new { message = $"Staff member {id} not found." });
        return Ok(new { success = true });
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        try
        {
            var updated = await _staffService.UpdateStaffAsync(id, request, cancellationToken);
            if (updated == null)
                return NotFound(new { message = $"Staff member {id} not found." });

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}


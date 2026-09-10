using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class LeavesController : ControllerBase
{
    private readonly ILeaveService _leaveService;

    public LeavesController(ILeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpGet]
    public async Task<IActionResult> GetAllLeaves([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var leaves = await _leaveService.GetAllLeavesAsync(status, cancellationToken);
        return Ok(leaves);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyLeaves(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var leaves = await _leaveService.GetMyLeavesAsync(userId, cancellationToken);
        return Ok(leaves);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLeaveById(Guid id, CancellationToken cancellationToken)
    {
        var leave = await _leaveService.GetLeaveByIdAsync(id, cancellationToken);
        if (leave == null) return NotFound(new { message = $"Leave request {id} not found." });

        var userId = GetCurrentUserId();
        var isManagerOrAdmin = User.IsInRole("ADMIN") || User.IsInRole("MANAGER");
        if (!isManagerOrAdmin && leave.UserId != userId)
        {
            return Forbid();
        }

        return Ok(leave);
    }

    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] CreateLeaveRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetCurrentUserId();
            var leave = await _leaveService.ApplyLeaveAsync(request, userId, cancellationToken);
            return CreatedAtAction(nameof(GetLeaveById), new { id = leave.Id }, leave);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to apply for leave: {ex.Message}" });
        }
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> ApproveLeave(
        Guid id, 
        [FromBody] ReviewLeaveRequestDto? dto, 
        CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        var updated = await _leaveService.ReviewLeaveAsync(id, "APPROVE", dto?.Comments, reviewerId, cancellationToken);
        if (updated == null) return NotFound(new { message = $"Leave request {id} not found." });
        return Ok(updated);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> RejectLeave(
        Guid id, 
        [FromBody] ReviewLeaveRequestDto? dto, 
        CancellationToken cancellationToken)
    {
        var reviewerId = GetCurrentUserId();
        var updated = await _leaveService.ReviewLeaveAsync(id, "REJECT", dto?.Comments, reviewerId, cancellationToken);
        if (updated == null) return NotFound(new { message = $"Leave request {id} not found." });
        return Ok(updated);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var guid))
        {
            return guid;
        }

        throw new UnauthorizedAccessException("Authentication token does not contain a valid user identity claim.");
    }
}

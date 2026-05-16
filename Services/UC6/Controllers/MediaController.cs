using ConnectHub.Media.Models.Options;
using ConnectHub.Media.Controllers;
using ConnectHub.Media.Data;
using ConnectHub.Media.Middleware;
using ConnectHub.Media.Models.DTOs;
using ConnectHub.Media.Models.Entities;
using ConnectHub.Media.Models.Events;
using ConnectHub.Media.Repositories.Implementations;
using ConnectHub.Media.Repositories.Interfaces;
using ConnectHub.Media.Services.Implementations;
using ConnectHub.Media.Services.Interfaces;
using System.Security.Claims;


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.Media.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IMediaService mediaService, ILogger<MediaController> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponseDto<MediaFileDto>), 201)]
    [ProducesResponseType(400)]
    [RequestSizeLimit(104_857_600)] // 100MB
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromForm] int? messageId = null,
        [FromForm] int? roomId = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponseDto<object>.Fail("No file provided."));

        var callerId = GetCallerId();
        var result = await _mediaService.UploadFile(file, callerId, messageId, roomId);

        return StatusCode(201, ApiResponseDto<MediaFileDto>.Ok(result, "File uploaded successfully."));
    }
    [HttpGet("{fileId}")]
    [ProducesResponseType(typeof(ApiResponseDto<MediaFileDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(string fileId)
    {
        var file = await _mediaService.GetFileById(fileId);
        if (file is null)
            return NotFound(ApiResponseDto<object>.Fail($"File {fileId} not found."));

        return Ok(ApiResponseDto<MediaFileDto>.Ok(file));
    }
    [HttpGet("by-user/{userId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MediaFileDto>>), 200)]
    public async Task<IActionResult> GetFilesByUser(int userId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return StatusCode(403, ApiResponseDto<object>.Fail("You can only view your own files."));

        var files = await _mediaService.GetFilesByUser(userId);
        return Ok(ApiResponseDto<IList<MediaFileDto>>.Ok(files));
    }
    [HttpGet("by-room/{roomId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MediaFileDto>>), 200)]
    public async Task<IActionResult> GetFilesByRoom(int roomId)
    {
        var files = await _mediaService.GetFilesByRoom(roomId);
        return Ok(ApiResponseDto<IList<MediaFileDto>>.Ok(files));
    }
    [HttpGet("by-message/{messageId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MediaFileDto>>), 200)]
    public async Task<IActionResult> GetFilesByMessage(int messageId)
    {
        var files = await _mediaService.GetFilesByMessage(messageId);
        return Ok(ApiResponseDto<IList<MediaFileDto>>.Ok(files));
    }
    [HttpGet("{fileId}/sas-url")]
    [ProducesResponseType(typeof(ApiResponseDto<SasUrlDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSasUrl(string fileId)
    {
        var sasUrl = await _mediaService.GenerateSasUrl(fileId);
        return Ok(ApiResponseDto<SasUrlDto>.Ok(sasUrl));
    }
    [HttpDelete("{fileId}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteFile(string fileId)
    {
        var callerId = GetCallerId();
        var role = GetCallerRole();
        await _mediaService.DeleteFile(fileId, callerId, role);
        return Ok(ApiResponseDto<object>.Ok(new { }, "File deleted successfully."));
    }

    /// <summary>
    /// GET /api/media/stats
    /// Admin: file counts and sizes by content type.
    /// GetFileStats() as per class diagram.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponseDto<FileStatsDto>), 200)]
    public async Task<IActionResult> GetFileStats()
    {
        var stats = await _mediaService.GetFileStats();
        return Ok(ApiResponseDto<FileStatsDto>.Ok(stats));
    }

    // ── Helper ─────────────────────────────────────────────────────

    private int GetCallerId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (claim is null || !int.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("Unable to determine caller identity.");

        return id;
    }

    private string GetCallerRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
}





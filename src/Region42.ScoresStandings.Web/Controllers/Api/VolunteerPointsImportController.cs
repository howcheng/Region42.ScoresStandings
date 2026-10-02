using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Exceptions;
using Region42.ScoresStandings.Application.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers.Api;

[ApiController]
[Route("api/volunteer-points")]
#if DEBUG
[AllowAnonymous]
#else
[Authorize(Policy = "VolunteerPointsImportPolicy")]
#endif
public class VolunteerPointsImportController : ControllerBase
{
	private readonly IVolunteerPointsService _volunteerPointsService;
	private readonly ILogger<VolunteerPointsImportController> _logger;

	public VolunteerPointsImportController(
		IVolunteerPointsService volunteerPointsService,
		ILogger<VolunteerPointsImportController> logger)
	{
		_volunteerPointsService = volunteerPointsService;
		_logger = logger;
	}

	[HttpGet("sync-context")]
	public async Task<ActionResult<VolunteerPointsSyncContextDto>> GetSyncContext(CancellationToken cancellationToken = default)
	{
		var context = await _volunteerPointsService.GetSyncContextAsync(cancellationToken);
		if (context.SeasonId <= 0)
		{
			return NotFound(new { message = "No active season is configured." });
		}

		return Ok(context);
	}

	[HttpPost("import")]
	public async Task<ActionResult<VolunteerPointsImportResultDto>> Import(
		[FromBody] VolunteerPointsBulkUpdateDto request,
		[FromQuery] bool dryRun = false,
		[FromQuery] bool authoritativeSync = false,
		CancellationToken cancellationToken = default)
	{
		if (request == null)
		{
			return BadRequest("Request body is required.");
		}

		var importedBy = User.FindFirst("email")?.Value
#if DEBUG
			?? "volunteer-sync-local";
#else
			?? "volunteer-sync";
#endif

		try
		{
			var result = await _volunteerPointsService.BulkImportAsync(
				request,
				dryRun,
				importedBy,
				authoritativeSync,
				cancellationToken);

			if (result.ValidationErrors.Count > 0 && result.ImportedCount == 0 && result.SkippedCount > 0)
			{
				return UnprocessableEntity(result);
			}

			return Ok(result);
		}
		catch (StorageWriteLockHeldException ex)
		{
			_logger.LogWarning("Volunteer points import rejected because lock {LockName} is held", ex.LockName);
			return Conflict(new { message = ex.Message, lockName = ex.LockName });
		}
	}
}

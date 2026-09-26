using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Exceptions;
using Region42.ScoresStandings.Application.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers.Api;

[ApiController]
[Route("api/volunteer-points")]
[Authorize(Policy = "VolunteerPointsImportPolicy")]
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

	[HttpPost("import")]
	public async Task<ActionResult<VolunteerPointsImportResultDto>> Import(
		[FromBody] VolunteerPointsBulkUpdateDto request,
		[FromQuery] bool dryRun = false,
		CancellationToken cancellationToken = default)
	{
		if (request == null)
		{
			return BadRequest("Request body is required.");
		}

		var importedBy = User.FindFirst("email")?.Value ?? "volunteer-sync";

		try
		{
			var result = await _volunteerPointsService.BulkImportAsync(
				request,
				dryRun,
				importedBy,
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Region42.ScoresStandings.Web.Authorization;

/// <summary>
/// Authorization requirement that validates the user's email domain
/// </summary>
public class DomainRequirement : IAuthorizationRequirement
{
	public string AllowedDomain { get; }

	public DomainRequirement(string allowedDomain)
	{
		AllowedDomain = allowedDomain ?? throw new ArgumentNullException(nameof(allowedDomain));
	}
}

/// <summary>
/// Handler that checks if the authenticated user's email belongs to the allowed domain
/// </summary>
public class DomainRequirementHandler : AuthorizationHandler<DomainRequirement>
{
	protected override Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		DomainRequirement requirement)
	{
		if (context.User?.Identity?.IsAuthenticated != true)
		{
			return Task.CompletedTask;
		}

		// Get email from claims (Google OAuth provides this)
		var emailClaim = context.User.FindFirst(ClaimTypes.Email)
			?? context.User.FindFirst("email");

		if (emailClaim == null)
		{
			return Task.CompletedTask;
		}

		var email = emailClaim.Value;

		// Check if email ends with the allowed domain
		if (email.EndsWith($"@{requirement.AllowedDomain}", StringComparison.OrdinalIgnoreCase))
		{
			context.Succeed(requirement);
		}

		return Task.CompletedTask;
	}
}

using Microsoft.AspNetCore.Builder;

namespace Region42.ScoresStandings.Web.Middleware;

public static class SecurityHeadersMiddlewareExtensions
{
	public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
	{
		return builder.UseMiddleware<SecurityHeadersMiddleware>();
	}
}

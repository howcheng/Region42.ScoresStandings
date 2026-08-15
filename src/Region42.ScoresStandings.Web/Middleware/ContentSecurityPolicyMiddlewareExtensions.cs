namespace Region42.ScoresStandings.Web.Middleware;

public static class ContentSecurityPolicyMiddlewareExtensions
{
	public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder builder)
	{
		return builder.UseMiddleware<ContentSecurityPolicyMiddleware>();
	}
}

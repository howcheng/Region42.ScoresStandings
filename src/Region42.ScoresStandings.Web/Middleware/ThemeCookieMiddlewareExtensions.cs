namespace Region42.ScoresStandings.Web.Middleware;

public static class ThemeCookieMiddlewareExtensions
{
	public static IApplicationBuilder UseThemeCookie(this IApplicationBuilder builder)
	{
		return builder.UseMiddleware<ThemeCookieMiddleware>();
	}
}

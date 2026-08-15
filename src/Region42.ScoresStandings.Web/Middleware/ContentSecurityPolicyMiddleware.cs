namespace Region42.ScoresStandings.Web.Middleware;

public class ContentSecurityPolicyMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IWebHostEnvironment _environment;
	private readonly ILogger<ContentSecurityPolicyMiddleware> _logger;

	public ContentSecurityPolicyMiddleware(
		RequestDelegate next,
		IWebHostEnvironment environment,
		ILogger<ContentSecurityPolicyMiddleware> logger)
	{
		_next = next;
		_environment = environment;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var cspPolicy = BuildCspPolicy();
		context.Response.Headers.Append("Content-Security-Policy", cspPolicy);

		// In Development, allow Browser Link to use the unload event
		if (_environment.IsDevelopment())
		{
			context.Response.Headers.Append("Permissions-Policy", "unload=*");
		}

		_logger.LogDebug("CSP Header applied: {CspPolicy}", cspPolicy);

		await _next(context);
	}

	private string BuildCspPolicy()
	{
		var directives = new List<string>
		{
			"default-src 'self'",
			"script-src 'self'",
			"style-src 'self'",
			"img-src 'self' data:",
			"font-src 'self'",
			"connect-src 'self'",
			"frame-ancestors 'none'",
			"base-uri 'self'",
			"form-action 'self'"
		};

		// In Development, add Browser Link support
		if (_environment.IsDevelopment())
		{
			// Browser Link requires connections to localhost on specific ports
			var connectSrcIndex = directives.FindIndex(d => d.StartsWith("connect-src"));
			if (connectSrcIndex >= 0)
			{
				directives[connectSrcIndex] = "connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*";
			}

			_logger.LogInformation("CSP configured for Development environment with Browser Link support");
		}

		return string.Join("; ", directives);
	}
}

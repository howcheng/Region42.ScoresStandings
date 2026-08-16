namespace Region42.ScoresStandings.Web.Middleware;

public class SecurityHeadersMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IWebHostEnvironment _environment;
	private readonly ILogger<SecurityHeadersMiddleware> _logger;

	public SecurityHeadersMiddleware(
		RequestDelegate next,
		IWebHostEnvironment environment,
		ILogger<SecurityHeadersMiddleware> logger)
	{
		_next = next;
		_environment = environment;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		// 1. Content-Security-Policy
		var cspPolicy = BuildCspPolicy();
		context.Response.Headers.Append("Content-Security-Policy", cspPolicy);

		// 2. X-Frame-Options (Clickjacking protection)
		context.Response.Headers.Append("X-Frame-Options", "DENY");

		// 3. X-Content-Type-Options (MIME-sniffing protection)
		context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

		// 4. Referrer-Policy
		context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

		// 5. Permissions-Policy
		// Disable intrusive features (camera, microphone, geolocation) by default.
		// In Development, additionally allow Browser Link to use the unload event for page-reload detection.
		if (_environment.IsDevelopment())
		{
			context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), unload=*");
		}
		else
		{
			context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
		}

		// 6. Cross-Origin-Opener-Policy (Spectre protection)
		context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");

		// 7. Cross-Origin-Resource-Policy (Spectre / cross-origin read protection)
		context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");

		_logger.LogDebug("OWASP Security Headers and CSP applied successfully.");

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

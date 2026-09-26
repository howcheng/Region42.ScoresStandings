using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.VolunteerSync.Configuration;
using Region42.ScoresStandings.VolunteerSync.Interfaces;

namespace Region42.ScoresStandings.VolunteerSync.Services;

/// <summary>
/// Downloads the latest volunteer points export using cookie-aware HTTP requests.
/// Login and download paths must be configured once the source application format is known.
/// </summary>
public class VolunteerPointsSourceClient : IVolunteerPointsSourceClient
{
	private readonly VolunteerSyncOptions _options;
	private readonly ILogger<VolunteerPointsSourceClient> _logger;

	public VolunteerPointsSourceClient(
		IOptions<VolunteerSyncOptions> options,
		ILogger<VolunteerPointsSourceClient> logger)
	{
		_options = options.Value;
		_logger = logger;
	}

	public async Task<Stream> DownloadLatestFileAsync(CancellationToken cancellationToken = default)
	{
		ValidateConfiguration();

		var handler = new HttpClientHandler
		{
			UseCookies = true,
			CookieContainer = new CookieContainer(),
			AllowAutoRedirect = true
		};

		using var client = new HttpClient(handler)
		{
			BaseAddress = new Uri(_options.SourceBaseUrl.TrimEnd('/') + "/")
		};

		await LoginAsync(client, cancellationToken);

		var downloadUri = BuildUri(_options.SourceDownloadPath);
		_logger.LogInformation("Downloading volunteer points file from {DownloadUri}", downloadUri);

		using var response = await client.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		response.EnsureSuccessStatusCode();

		var memoryStream = new MemoryStream();
		await response.Content.CopyToAsync(memoryStream, cancellationToken);
		memoryStream.Position = 0;
		return memoryStream;
	}

	private async Task LoginAsync(HttpClient client, CancellationToken cancellationToken)
	{
		var loginUri = BuildUri(_options.SourceLoginPath);
		_logger.LogInformation("Authenticating to volunteer points source at {LoginUri}", loginUri);

		var formValues = new Dictionary<string, string>
		{
			["username"] = _options.SourceUsername,
			["password"] = _options.SourcePassword
		};

		using var content = new FormUrlEncodedContent(formValues);
		using var response = await client.PostAsync(loginUri, content, cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	private void ValidateConfiguration()
	{
		if (string.IsNullOrWhiteSpace(_options.SourceBaseUrl))
		{
			throw new InvalidOperationException("VolunteerSync:SourceBaseUrl is required.");
		}

		if (string.IsNullOrWhiteSpace(_options.SourceDownloadPath))
		{
			throw new InvalidOperationException("VolunteerSync:SourceDownloadPath is required.");
		}

		if (string.IsNullOrWhiteSpace(_options.SourceUsername) || string.IsNullOrWhiteSpace(_options.SourcePassword))
		{
			throw new InvalidOperationException("VolunteerSync source credentials are required.");
		}
	}

	private Uri BuildUri(string path)
	{
		if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
		{
			return absoluteUri;
		}

		return new Uri(new Uri(_options.SourceBaseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));
	}
}

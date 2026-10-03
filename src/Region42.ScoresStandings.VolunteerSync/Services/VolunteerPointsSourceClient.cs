using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.VolunteerSync.Configuration;
using Region42.ScoresStandings.VolunteerSync.Interfaces;

namespace Region42.ScoresStandings.VolunteerSync.Services;

/// <summary>
/// Downloads the latest volunteer points export from cgisports using session user tokens.
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

		var sessionUser = await LoginAsync(client, cancellationToken);
		_logger.LogInformation(
			"Authenticated to cgisports as session user {SessionUserPrefix}…",
			TruncateForLog(sessionUser));

		if (!string.IsNullOrWhiteSpace(_options.SourceAssignmentLogPath))
		{
			var assignmentUri = BuildUri(string.Format(_options.SourceAssignmentLogPath, sessionUser));
			using var confirmResponse = await client.GetAsync(assignmentUri, cancellationToken);
			confirmResponse.EnsureSuccessStatusCode();
		}

		var downloadUri = BuildUri(string.Format(_options.SourceDownloadPath, sessionUser));
		_logger.LogInformation("Downloading volunteer points file from {DownloadPath}", downloadUri.PathAndQuery);

		var formValues = new Dictionary<string, string>
		{
			["user"] = sessionUser,
			["ck"] = "ok"
		};

		using var content = new FormUrlEncodedContent(formValues);
		using var response = await client.PostAsync(downloadUri, content, cancellationToken);
		response.EnsureSuccessStatusCode();

		var memoryStream = new MemoryStream();
		await response.Content.CopyToAsync(memoryStream, cancellationToken);
		memoryStream.Position = 0;

		ValidateDownloadBody(memoryStream);
		memoryStream.Position = 0;
		return memoryStream;
	}

	private async Task<string> LoginAsync(HttpClient client, CancellationToken cancellationToken)
	{
		var loginUri = BuildUri(_options.SourceLoginPath);
		_logger.LogInformation("Authenticating to volunteer points source at {LoginUri}", loginUri);

		var formValues = new Dictionary<string, string>
		{
			["userid"] = _options.SourceUsername,
			["password"] = _options.SourcePassword,
			["login"] = "Login",
			["view_schedule"] = string.Empty
		};

		using var content = new FormUrlEncodedContent(formValues);
		using var response = await client.PostAsync(loginUri, content, cancellationToken);
		response.EnsureSuccessStatusCode();

		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		var sessionUser = CgiSportsSessionParser.TryExtractSessionUser(response.RequestMessage?.RequestUri, body)
			?? CgiSportsSessionParser.TryExtractSessionUser(response.Headers.Location, body);

		if (string.IsNullOrWhiteSpace(sessionUser))
		{
			throw new InvalidOperationException("Could not determine cgisports session user after login.");
		}

		return sessionUser;
	}

	private static void ValidateDownloadBody(Stream stream)
	{
		if (stream.Length == 0)
		{
			throw new InvalidOperationException("Volunteer points download returned an empty file.");
		}

		Span<byte> header = stackalloc byte[4];
		var read = stream.Read(header);
		stream.Position = 0;
		if (read < 4)
		{
			throw new InvalidOperationException("Volunteer points download is too small to be a valid Excel file.");
		}

		var isOle = header[0] == 0xD0 && header[1] == 0xCF;
		var isOpenXml = header[0] == 0x50 && header[1] == 0x4B;
		if (!isOle && !isOpenXml)
		{
			throw new InvalidOperationException("Volunteer points download does not appear to be an Excel workbook.");
		}
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
		return CgiSportsUriHelper.Combine(_options.SourceBaseUrl, path);
	}

	private static string TruncateForLog(string sessionUser)
	{
		var dot = sessionUser.IndexOf('.');
		return dot > 0 ? sessionUser[..dot] : sessionUser;
	}
}

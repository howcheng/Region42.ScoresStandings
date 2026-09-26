using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.VolunteerSync.Configuration;
using Region42.ScoresStandings.VolunteerSync.Interfaces;
using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Services;

public class ScoresStandingsApiClient : IScoresStandingsApiClient
{
	private const string MetadataFlavorHeader = "Metadata-Flavor";
	private const string MetadataFlavorValue = "Google";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private readonly VolunteerSyncOptions _options;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<ScoresStandingsApiClient> _logger;

	public ScoresStandingsApiClient(
		IOptions<VolunteerSyncOptions> options,
		IHttpClientFactory httpClientFactory,
		ILogger<ScoresStandingsApiClient> logger)
	{
		_options = options.Value;
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<VolunteerPointsImportResultDto> ImportVolunteerPointsAsync(
		VolunteerPointsBulkUpdateDto request,
		bool dryRun,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(_options.TargetApiBaseUrl))
		{
			throw new InvalidOperationException("VolunteerSync:TargetApiBaseUrl is required.");
		}

		var client = _httpClientFactory.CreateClient(nameof(ScoresStandingsApiClient));
		client.BaseAddress = new Uri(_options.TargetApiBaseUrl.TrimEnd('/') + "/");

		var identityToken = await GetIdentityTokenAsync(cancellationToken);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", identityToken);

		var importPath = _options.TargetImportPath.TrimStart('/');
		var requestUri = $"{importPath}?dryRun={(dryRun ? "true" : "false")}";

		_logger.LogInformation("Posting volunteer points import to {RequestUri} (dryRun={DryRun})", requestUri, dryRun);

		using var response = await client.PostAsJsonAsync(requestUri, request, JsonOptions, cancellationToken);
		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(
				$"Volunteer points import failed with status {(int)response.StatusCode}: {responseBody}");
		}

		return JsonSerializer.Deserialize<VolunteerPointsImportResultDto>(responseBody, JsonOptions)
			?? throw new InvalidOperationException("Volunteer points import returned an empty response.");
	}

	private async Task<string> GetIdentityTokenAsync(CancellationToken cancellationToken)
	{
		var audience = _options.TargetApiBaseUrl.TrimEnd('/');
		var metadataUrl =
			$"http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/identity?audience={Uri.EscapeDataString(audience)}";

		using var request = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
		request.Headers.Add(MetadataFlavorHeader, MetadataFlavorValue);

		var metadataClient = _httpClientFactory.CreateClient("GoogleMetadata");
		using var response = await metadataClient.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		var token = await response.Content.ReadAsStringAsync(cancellationToken);
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new InvalidOperationException("Metadata server returned an empty identity token.");
		}

		return token;
	}
}

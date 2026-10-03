using FluentAssertions;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync.Tests.Services;

public class CgiSportsUriHelperTests
{
	[Fact]
	public void Combine_RootRelativePath_UsesHttpsBaseNotFileScheme()
	{
		var uri = CgiSportsUriHelper.Combine("https://cgisports.com", "/ref/2130/");

		uri.Scheme.Should().Be("https");
		uri.Host.Should().Be("cgisports.com");
		uri.AbsolutePath.Should().Be("/ref/2130/");
	}

	[Fact]
	public void Combine_AbsoluteHttpsPath_ReturnsAsIs()
	{
		var uri = CgiSportsUriHelper.Combine("https://cgisports.com", "https://cgisports.com/ref/2130/");

		uri.ToString().Should().Be("https://cgisports.com/ref/2130/");
	}
}

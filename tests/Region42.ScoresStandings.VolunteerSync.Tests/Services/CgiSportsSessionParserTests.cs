using FluentAssertions;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync.Tests.Services;

public class CgiSportsSessionParserTests
{
	[Fact]
	public void TryExtractUserFromUri_ParsesUserQueryParameter()
	{
		var uri = new Uri("https://cgisports.com/ref/2130?user=howcheng.179046626153486&search=1");
		CgiSportsSessionParser.TryExtractUserFromUri(uri).Should().Be("howcheng.179046626153486");
	}

	[Fact]
	public void TryExtractSessionUser_ParsesHiddenInputValue()
	{
		var html = """<input type="hidden" name="user" id="user" value="howcheng.179046626153486">""";
		CgiSportsSessionParser.TryExtractSessionUser(null, html).Should().Be("howcheng.179046626153486");
	}
}

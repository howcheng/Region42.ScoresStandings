using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using System.Security.Claims;

namespace Region42.ScoresStandings.Web.Tests.Helpers;

/// <summary>
/// Helper class for setting up controller contexts for testing.
/// </summary>
public static class ControllerTestHelper
{
	/// <summary>
	/// Sets up a controller with HttpContext, TempData, and optional user claims.
	/// </summary>
	public static void SetupControllerContext(Controller controller, string? userName = null, params Claim[] additionalClaims)
	{
		var httpContext = new DefaultHttpContext();

		if (userName != null)
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Name, userName),
				new Claim(ClaimTypes.NameIdentifier, userName)
			};
			claims.AddRange(additionalClaims);

			var identity = new ClaimsIdentity(claims, "TestAuth");
			var principal = new ClaimsPrincipal(identity);
			httpContext.User = principal;
		}

		var tempDataProvider = new Mock<ITempDataProvider>();
		var tempDataDictionaryFactory = new TempDataDictionaryFactory(tempDataProvider.Object);
		var tempData = tempDataDictionaryFactory.GetTempData(httpContext);

		controller.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		controller.TempData = tempData;
	}

	/// <summary>
	/// Creates an authenticated user ClaimsPrincipal for testing.
	/// </summary>
	public static ClaimsPrincipal CreateAuthenticatedUser(string userName, string email)
	{
		var claims = new[]
		{
			new Claim(ClaimTypes.Name, userName),
			new Claim(ClaimTypes.NameIdentifier, userName),
			new Claim(ClaimTypes.Email, email)
		};

		var identity = new ClaimsIdentity(claims, "TestAuth");
		return new ClaimsPrincipal(identity);
	}
}

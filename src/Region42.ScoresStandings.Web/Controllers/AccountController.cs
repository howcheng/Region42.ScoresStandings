using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Region42.ScoresStandings.Web.Controllers;

public class AccountController : Controller
{
	/// <summary>
	/// Initiates Google OAuth login flow.
	/// User will be redirected to Google, then back to returnUrl after authentication.
	/// </summary>
	[AllowAnonymous]
	public IActionResult Login(string? returnUrl = null)
	{
		var properties = new AuthenticationProperties
		{
			RedirectUri = returnUrl ?? Url.Action("Standings", "Home")
		};

		return Challenge(properties, "Google");
	}

	/// <summary>
	/// Logs out the user and clears authentication cookies.
	/// </summary>
	[Authorize]
	public async Task<IActionResult> Logout()
	{
		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		return RedirectToAction("Standings", "Home");
	}

	/// <summary>
	/// Access denied page (when user tries to access protected resource without permission).
	/// </summary>
	[AllowAnonymous]
	public IActionResult AccessDenied()
	{
		return View();
	}
}

# Domain-Restricted Authentication Setup

## Overview

Your application now has **two layers of domain restriction** for `aysoregion42.org`:

1. **OAuth Consent Screen (Google Cloud)** - Primary restriction
2. **Application-Level Validation** - Additional security layer

---

## Layer 1: OAuth Consent Screen (Primary) ⭐

### Configuration Required in Google Cloud Console

**This is the MAIN way to restrict logins to your domain.**

#### Steps:

1. **Navigate to OAuth Consent Screen:**
   ```
   https://console.cloud.google.com/apis/credentials/consent?project=ayso-region-42
   ```

2. **Set User Type:**
   - Click **"EDIT APP"** if already created, or **"CREATE"** if new
   - Select **"Internal"** as the User Type
   - ✅ This automatically restricts authentication to `@aysoregion42.org` users only!

3. **Configure App Information:**
   - **App name:** Region 42 Scores & Standings
   - **User support email:** howard.cheng@aysoregion42.org
   - **App domain:** (add your Cloud Run URL when deployed)
   - **Developer contact:** howard.cheng@aysoregion42.org

4. **Add Required Scopes:**
   - `.../auth/userinfo.email` ✅ Already added in code
   - `.../auth/userinfo.profile`

5. **Save and Continue**

#### What "Internal" Does:

- ✅ **Only** users with `@aysoregion42.org` email can authenticate
- ✅ No consent screen shown to your domain users (seamless login)
- ✅ Google enforces this at the OAuth level (very secure)
- ❌ External users cannot even attempt to log in

#### Verification:

- Try logging in with a non-`@aysoregion42.org` account
- Google should show: "You can't sign in to this app because it isn't verified"

---

## Layer 2: Application-Level Validation (Defense in Depth)

### What Was Added

I've added custom authorization in your application code:

#### 1. Domain Requirement Handler
**File:** `src/Region42.ScoresStandings.Web/Authorization/DomainRequirement.cs`

```csharp
// Validates email domain from authenticated user's claims
public class DomainRequirementHandler : AuthorizationHandler<DomainRequirement>
{
	// Checks if email ends with @aysoregion42.org
}
```

#### 2. Authorization Policies
**File:** `src/Region42.ScoresStandings.Web/Program.cs`

```csharp
builder.Services.AddAuthorization(options =>
{
	// AdminPolicy: Requires @aysoregion42.org domain
	options.AddPolicy("AdminPolicy", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.Requirements.Add(new DomainRequirement("aysoregion42.org"));
	});
});
```

#### 3. Email Scope Added
```csharp
options.Scope.Add("email"); // Ensures we get user's email in claims
```

---

## How to Use in Controllers/Pages

### Require Admin Access (Domain-Restricted)

```csharp
[Authorize(Policy = "AdminPolicy")] // Only @aysoregion42.org users
public class AdminController : Controller
{
	// All actions require aysoregion42.org domain
}
```

Or on individual actions:

```csharp
[Authorize(Policy = "AdminPolicy")]
public IActionResult UploadCsv()
{
	// Only @aysoregion42.org users can access
}
```

### Require Any Authenticated User

```csharp
[Authorize] // Any authenticated user (if you remove domain requirement)
public IActionResult ViewStandings()
{
	// Any authenticated user can access
}
```

### Check Domain in Code

If you need to check domain programmatically:

```csharp
public IActionResult MyAction()
{
	var email = User.FindFirst(ClaimTypes.Email)?.Value;

	if (email?.EndsWith("@aysoregion42.org") != true)
	{
		return Forbid();
	}

	// Proceed with action
}
```

---

## Testing Domain Restriction

### Test 1: Valid Domain User

1. Run your application
2. Navigate to a protected page
3. Sign in with `your.name@aysoregion42.org`
4. ✅ Should be allowed access

### Test 2: Invalid Domain User

1. Try to sign in with `someone@gmail.com`
2. If OAuth is set to "Internal":
   - ❌ Google blocks at OAuth level
   - Error: "You can't sign in to this app because it isn't verified"
3. If OAuth is set to "External" (not recommended):
   - ✅ Google allows authentication
   - ❌ Your app blocks access (403 Forbidden)
   - User sees: "Access Denied" or similar

---

## Configuration Values

### Current Settings

- **Allowed Domain:** `aysoregion42.org`
- **Admin Policy:** Requires authenticated user from `aysoregion42.org`
- **OAuth Scopes:** `email`, `profile`

### To Change Allowed Domain

Edit `Program.cs`:

```csharp
options.AddPolicy("AdminPolicy", policy =>
{
	policy.RequireAuthenticatedUser();
	policy.Requirements.Add(new DomainRequirement("yournewdomain.org")); // Change here
});
```

Or make it configurable:

```csharp
var allowedDomain = builder.Configuration["Authentication:AllowedDomain"] 
	?? "aysoregion42.org";

options.AddPolicy("AdminPolicy", policy =>
{
	policy.RequireAuthenticatedUser();
	policy.Requirements.Add(new DomainRequirement(allowedDomain));
});
```

Then add to `appsettings.json`:

```json
{
  "Authentication": {
	"AllowedDomain": "aysoregion42.org",
	"Google": {
	  "ClientId": "...",
	  "ClientSecret": "..."
	}
  }
}
```

---

## Security Best Practices

### ✅ Recommended Setup

1. **Set OAuth Consent Screen to "Internal"** (Primary defense)
2. **Keep application-level validation** (Defense in depth)
3. **Use `[Authorize(Policy = "AdminPolicy")]`** on sensitive actions
4. **Review audit logs** regularly (CreatedBy/ModifiedBy fields)

### ⚠️ What If You Need External Users?

If you later need to allow non-`@aysoregion42.org` users:

1. **Change OAuth to "External"** in Google Cloud Console
2. **Add a whitelist** in your User table:
   ```csharp
   var user = await _userRepository.GetByEmailAsync(email);
   if (user == null || !user.IsActive)
   {
	   return Forbid();
   }
   ```
3. **Keep domain requirement** for most admin actions
4. **Create separate policies** for different access levels

---

## Troubleshooting

### Issue: "You can't sign in to this app"

**Cause:** OAuth consent screen is set to "Internal" but user is not in your organization  
**Solution:** User must have `@aysoregion42.org` email address

### Issue: "Access Denied" after successful Google login

**Cause:** Application-level domain validation blocked the user  
**Solution:** 
1. Check user's email claim: `User.FindFirst(ClaimTypes.Email)`
2. Verify email ends with `@aysoregion42.org`
3. Check if email scope was granted

### Issue: Email claim is null

**Cause:** Email scope not requested or not granted  
**Solution:** 
1. Verify `options.Scope.Add("email");` in Program.cs ✅
2. Check OAuth consent screen includes `userinfo.email` scope
3. Delete cookies and re-authenticate

---

## CLI Commands (Limited Support)

**Note:** OAuth consent screen configuration is **not fully supported** via gcloud CLI. You must use the Google Cloud Console.

### What You CAN Do via CLI:

```bash
# List OAuth clients
gcloud alpha iap oauth-clients list --format=json

# Describe a specific client
gcloud alpha iap oauth-clients describe CLIENT_ID --format=json
```

### What You CANNOT Do via CLI:

- ❌ Set consent screen user type (Internal vs External)
- ❌ Configure allowed domains
- ❌ Add/remove scopes from consent screen
- ❌ Configure consent screen branding

**You must use the Console for these settings.**

---

## Summary

✅ **Two-layer security:**
1. Google OAuth (Internal) - Blocks at authentication
2. Application code - Validates email domain

✅ **Current configuration:**
- Allowed domain: `aysoregion42.org`
- Admin policy: Requires domain + authentication
- Email scope: Requested ✅

✅ **Files modified:**
- `src/Region42.ScoresStandings.Web/Authorization/DomainRequirement.cs` (new)
- `src/Region42.ScoresStandings.Web/Program.cs` (updated)

⏳ **Next step:**
- Go to Google Cloud Console and set OAuth consent screen to "Internal"
- URL: https://console.cloud.google.com/apis/credentials/consent?project=ayso-region-42

---

## References

- [OAuth Consent Screen Guide](https://support.google.com/cloud/answer/10311615)
- [ASP.NET Core Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [Google OAuth Scopes](https://developers.google.com/identity/protocols/oauth2/scopes)

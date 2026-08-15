# Content Security Policy (CSP) Implementation

## Overview

This application implements a strict Content Security Policy to prevent XSS attacks and other code injection vulnerabilities. The CSP is implemented via middleware that adds appropriate headers to all responses.

## CSP Policy

The following directives are enforced:

- **default-src**: `'self'` - Only allow resources from the same origin
- **script-src**: `'self'` - Scripts from same origin only
- **style-src**: `'self'` - Styles from same origin only
- **img-src**: `'self' data:` - Images from same origin or data URIs
- **font-src**: `'self'` - Fonts from same origin only
- **connect-src**: `'self'` - AJAX/WebSocket connections to same origin only
- **frame-ancestors**: `'none'` - Prevent clickjacking
- **base-uri**: `'self'` - Restrict base tag to same origin
- **form-action**: `'self'` - Forms can only submit to same origin

### Development Environment

In Development mode, the CSP middleware adds Browser Link support:

**CSP Header:**
```
connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*
```

**Permissions-Policy Header:**
```
unload=*
```

This allows Browser Link to:
- Establish WebSocket connections to localhost
- Use the `unload` event for page refresh detection

## Architecture: Page-Specific CSS and JavaScript

To comply with the strict CSP policy (no inline scripts or styles), this application uses **page-specific external CSS and JavaScript files**.

### File Organization

```
wwwroot/
├── css/
│   ├── site.css              # Global styles
│   ├── standings.css         # Standings page styles
│   └── [page-name].css       # Other page-specific styles
└── js/
	├── site.js               # Global scripts
	├── standings.js          # Standings page scripts (IIFE)
	├── csv-upload.js         # CSV upload page scripts (IIFE)
	└── [page-name].js        # Other page-specific scripts (IIFE)
```

### JavaScript Pattern: IIFEs

All page-specific JavaScript files use **Immediately Invoked Function Expressions (IIFEs)** to avoid polluting the global namespace:

```javascript
/**
 * Page Description
 */
(function() {
	'use strict';

	// Private functions and variables
	function myFunction() {
		// Implementation
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();
```

### View Integration

#### Adding Page-Specific CSS

```razor
@section Styles {
	<link rel="stylesheet" href="~/css/standings.css" asp-append-version="true" />
}
```

#### Adding Page-Specific JavaScript

```razor
@section Scripts {
	<script src="~/js/standings.js" asp-append-version="true"></script>
}
```

### Layout Support

The `_Layout.cshtml` file includes:

```razor
<head>
	<!-- Global styles -->
	<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />

	<!-- Page-specific styles -->
	@await RenderSectionAsync("Styles", required: false)
</head>
<body>
	<!-- Content -->

	<!-- Global scripts -->
	<script src="~/js/site.js" asp-append-version="true"></script>

	<!-- Page-specific scripts -->
	@await RenderSectionAsync("Scripts", required: false)
</body>
```

## Migrating to External Files

### No More Inline Scripts or Styles

**Prohibited:**
- `<script>` tags with inline code
- `<style>` tags with inline CSS
- Inline event handlers: `onclick="..."`, `onchange="..."`, etc.
- Inline style attributes: `style="display: none;"`

**Required:**
- External `.js` files for all JavaScript
- External `.css` files for all styles
- Event listeners attached via JavaScript
- CSS classes for styling

### Migration Steps

#### 1. Extract Inline Styles

**Before:**
```razor
<div style="display: none;">Content</div>
```

**After:**

Create or update `wwwroot/css/[page-name].css`:
```css
.hidden {
	display: none;
}
```

Update view:
```razor
<div class="hidden">Content</div>
```

#### 2. Extract Inline Scripts

**Before:**
```razor
<script>
	function myFunction() {
		console.log('Hello');
	}
</script>
```

**After:**

Create `wwwroot/js/[page-name].js`:
```javascript
(function() {
	'use strict';

	function myFunction() {
		console.log('Hello');
	}

	// Initialization
	function initialize() {
		myFunction();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();
```

Update view:
```razor
@section Scripts {
	<script src="~/js/[page-name].js" asp-append-version="true"></script>
}
```

#### 3. Replace Inline Event Handlers

**Before:**
```razor
<select onchange="handleChange()">
	<option>A</option>
</select>
```

**After:**

Remove the `onchange` attribute:
```razor
<select id="mySelect">
	<option>A</option>
</select>
```

In the external JavaScript file:
```javascript
function initialize() {
	const select = document.getElementById('mySelect');
	if (select) {
		select.addEventListener('change', handleChange);
	}
}

function handleChange(event) {
	// Implementation
}
```

#### 4. Use Data Attributes for Dynamic Values

**Before:**
```razor
<button onclick="showDetail('@item.Name', @item.Id)">Show</button>
```

**After:**
```razor
<button class="detail-btn" data-name="@item.Name" data-id="@item.Id">Show</button>
```

In JavaScript (using event delegation):
```javascript
document.addEventListener('click', function(event) {
	const button = event.target.closest('.detail-btn');
	if (button) {
		event.preventDefault();
		const name = button.dataset.name;
		const id = button.dataset.id;
		showDetail(name, id);
	}
});
```

## Example: Standings Page

### Files

1. **`Views/Home/Standings.cshtml`** - View markup (no inline scripts/styles)
2. **`wwwroot/css/standings.css`** - Page-specific styles
3. **`wwwroot/js/standings.js`** - Page-specific scripts (IIFE)

### Implementation

**Standings.cshtml:**
```razor
@section Styles {
	<link rel="stylesheet" href="~/css/standings.css" asp-append-version="true" />
}

<!-- Markup with NO inline event handlers or styles -->
<select id="divisionSelect" class="form-select">
	<!-- options -->
</select>

@section Scripts {
	<script src="~/js/standings.js" asp-append-version="true"></script>
}
```

**standings.js:**
```javascript
(function() {
	'use strict';

	function filterByDivision() {
		const divisionId = document.getElementById('divisionSelect').value;
		window.location.href = `/Home/Standings?divisionId=${divisionId}`;
	}

	function initialize() {
		const select = document.getElementById('divisionSelect');
		if (select) {
			select.addEventListener('change', filterByDivision);
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();
```

## Testing CSP

To verify CSP is working:

1. Open browser developer tools (F12)
2. Navigate to the Network tab
3. Load a page
4. Check the response headers for `Content-Security-Policy`
5. Any CSP violations will appear in the Console tab as errors

## Files Modified/Created

### New Files
1. `Middleware/ContentSecurityPolicyMiddleware.cs` - Core CSP middleware
2. `Middleware/ContentSecurityPolicyMiddlewareExtensions.cs` - Extension methods
3. `wwwroot/css/standings.css` - Standings page styles
4. `wwwroot/js/standings.js` - Standings page scripts
5. `wwwroot/js/csv-upload.js` - CSV upload page scripts

### Modified Files
1. `Program.cs` - Added middleware registration
2. `Views/_ViewImports.cshtml` - Cleaned up
3. `Views/Shared/_Layout.cshtml` - Added `Styles` section support
4. `Views/Home/Standings.cshtml` - Removed inline scripts/styles
5. `Views/CsvImport/Upload.cshtml` - Removed inline scripts
6. `wwwroot/css/site.css` - Added `.hidden` utility class

## Security Notes

- No `'unsafe-inline'` or `'unsafe-eval'` - maximum security
- All scripts and styles loaded from same origin only
- External resources (CDNs) are blocked by default
- IIFEs prevent global namespace pollution
- Event delegation used for dynamic content

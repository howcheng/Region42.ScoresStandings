# CSP Migration Checklist

## Views That Need Migration

The following views contain inline styles, scripts, or event handlers that need to be moved to external files to work with the Content Security Policy.

### ✅ Completed
- [x] `Views/Home/Standings.cshtml`
  - ✅ Created `wwwroot/css/standings.css` for page styles
  - ✅ Created `wwwroot/js/standings.js` with IIFE pattern
  - ✅ Removed inline `onchange` event handlers
  - ✅ Removed inline `onclick` event handlers
  - ✅ Removed inline `<script>` tag
  - ✅ Removed inline `<style>` tag
  - ✅ Used data attributes and event delegation for dynamic buttons

- [x] `Views/CsvImport/Upload.cshtml`
  - ✅ Inline style moved to CSS class (`.hidden`)
  - ✅ Created `wwwroot/js/csv-upload.js` with IIFE pattern
  - ✅ Removed inline `<script>` tag

### ⚠️ Needs Migration

#### 1. `Views/VolunteerPoints/Entry.cshtml`
**Issues:**
- Line 62: `style="min-width: 150px;"`
- Line 65: `style="min-width: 80px;"`
- Line 91: `style="width: 70px; margin: 0 auto;"`
- Line 95: `style="font-size: 0.7rem;"`
- Line 153: `<script>` tag with inline code

**Action Items:**
1. Create `wwwroot/css/volunteer-points.css`
2. Move inline styles to CSS classes
3. Create `wwwroot/js/volunteer-points.js` with IIFE
4. Extract script to external file
5. Update view to reference external files

#### 2. `Views/Scores/Entry.cshtml`
**Issues:**
- Line 106: `style="width: 70px;"`
- Line 115: `style="width: 70px;"`
- Line 188: `<script>` tag with inline code

**Action Items:**
1. Create `wwwroot/css/scores-entry.css`
2. Move inline styles to CSS classes
3. Create `wwwroot/js/scores-entry.js` with IIFE
4. Extract script to external file
5. Update view to reference external files

#### 3. `Views/CsvImport/Preview.cshtml`
**Issues:**
- Line 152: `<script>` tag with inline code

**Action Items:**
1. Create `wwwroot/js/csv-preview.js` with IIFE
2. Extract script to external file
3. Update view to reference external file

#### 4. `Views/Teams/Index.cshtml`
**Issues:**
- Line 120: `<script>` tag with inline code

**Action Items:**
1. Create `wwwroot/js/teams-index.js` with IIFE
2. Extract script to external file
3. Update view to reference external file

## Migration Pattern

### Step 1: Create External CSS File (if needed)

**File:** `wwwroot/css/[page-name].css`

```css
/* Page-Specific Styles for [Page Name] */

.class-name {
	property: value;
}
```

### Step 2: Create External JavaScript File

**File:** `wwwroot/js/[page-name].js`

```javascript
/**
 * [Page Name] Page JavaScript
 * Description of functionality
 */
(function() {
	'use strict';

	// Private functions
	function myFunction() {
		// Implementation
	}

	// Event handlers
	function handleEvent(event) {
		// Implementation
	}

	// Initialize
	function initialize() {
		// Attach event listeners
		const element = document.getElementById('elementId');
		if (element) {
			element.addEventListener('change', handleEvent);
		}
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();
```

### Step 3: Update View

**Before:**
```razor
<select id="mySelect" onchange="handleChange()">
	<option>Option 1</option>
</select>

<div style="display: none;">Content</div>

<script>
	function handleChange() {
		console.log('Changed');
	}
</script>

<style>
	.custom { color: red; }
</style>
```

**After:**
```razor
@section Styles {
	<link rel="stylesheet" href="~/css/[page-name].css" asp-append-version="true" />
}

<select id="mySelect">
	<option>Option 1</option>
</select>

<div class="hidden">Content</div>

@section Scripts {
	<script src="~/js/[page-name].js" asp-append-version="true"></script>
}
```

## Common Patterns

### Pattern 1: Inline Event Handlers → Event Listeners

**Before:**
```razor
<button onclick="doSomething()">Click</button>
```

**After:**
```razor
<button id="myButton">Click</button>
```

```javascript
function initialize() {
	const button = document.getElementById('myButton');
	if (button) {
		button.addEventListener('click', doSomething);
	}
}
```

### Pattern 2: Inline Styles → CSS Classes

**Before:**
```razor
<input style="width: 70px;" />
```

**After (CSS):**
```css
.input-width-70 {
	width: 70px;
}
```

**After (HTML):**
```razor
<input class="input-width-70" />
```

### Pattern 3: Dynamic Buttons with Data

**Before:**
```razor
@foreach (var item in Model.Items)
{
	<button onclick="showDetail('@item.Name', @item.Id)">Show</button>
}
```

**After (HTML):**
```razor
@foreach (var item in Model.Items)
{
	<button class="detail-btn" data-name="@item.Name" data-id="@item.Id">Show</button>
}
```

**After (JavaScript - Event Delegation):**
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

## Testing After Migration

After updating each view:

1. Run the application
2. Navigate to the updated page
3. Open browser DevTools (F12)
4. Check the **Console** tab for CSP violations
5. Check the **Network** tab to verify external files are loading
6. Verify all functionality still works as expected

## Progress

- **Total Views to Migrate**: 6
- **Completed**: 2 (Standings, CSV Upload)
- **Remaining**: 4
- **Completion**: 33%

## Next Steps

Recommended migration order:
1. `Views/Teams/Index.cshtml` (simple - script only)
2. `Views/CsvImport/Preview.cshtml` (simple - script only)
3. `Views/Scores/Entry.cshtml` (moderate - styles + script)
4. `Views/VolunteerPoints/Entry.cshtml` (moderate - styles + script)

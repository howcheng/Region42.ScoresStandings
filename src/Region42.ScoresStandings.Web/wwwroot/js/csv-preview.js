/**
 * CSV Import Preview Page JavaScript
 * Handles form submission with loading state
 */
(function() {
	'use strict';

	/**
	 * Handle form submission with loading state
	 */
	function handleFormSubmit(event) {
		const form = event.target;

		// Check if form is valid before disabling button
		if (!form.checkValidity()) {
			return; // Let browser handle validation
		}

		const submitBtn = document.getElementById('confirmImportBtn');
		const btnIcon = document.getElementById('btnIcon');
		const btnSpinner = document.getElementById('btnSpinner');
		const btnText = document.getElementById('btnText');
		const cancelBtn = document.getElementById('cancelBtn');

		// Disable button and show spinner
		submitBtn.disabled = true;
		cancelBtn.classList.add('disabled');
		btnIcon.classList.add('d-none');
		btnSpinner.classList.remove('d-none');
		btnText.textContent = ' Importing...';
	}

	/**
	 * Re-enable button if user navigates back (browser back button)
	 */
	function handlePageShow(event) {
		if (event.persisted || performance.getEntriesByType("navigation")[0].type === 'back_forward') {
			const submitBtn = document.getElementById('confirmImportBtn');
			const btnIcon = document.getElementById('btnIcon');
			const btnSpinner = document.getElementById('btnSpinner');
			const btnText = document.getElementById('btnText');
			const cancelBtn = document.getElementById('cancelBtn');

			submitBtn.disabled = false;
			cancelBtn.classList.remove('disabled');
			btnIcon.classList.remove('d-none');
			btnSpinner.classList.add('d-none');
			btnText.textContent = ' Confirm Import';
		}
	}

	/**
	 * Initialize event listeners
	 */
	function initialize() {
		const form = document.getElementById('importForm');

		if (form) {
			form.addEventListener('submit', handleFormSubmit);
		}

		window.addEventListener('pageshow', handlePageShow);
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

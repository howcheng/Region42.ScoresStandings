/**
 * Teams Index Page JavaScript
 * Handles division filtering
 */
(function() {
	'use strict';

	/**
	 * Filter teams by selected division
	 */
	function filterByDivision() {
		const divisionId = document.getElementById('divisionSelect').value;
		if (divisionId) {
			window.location.href = `/Teams/Index?divisionId=${divisionId}`;
		} else {
			window.location.href = '/Teams/Index';
		}
	}

	/**
	 * Initialize event listeners
	 */
	function initialize() {
		const divisionSelect = document.getElementById('divisionSelect');

		if (divisionSelect) {
			divisionSelect.addEventListener('change', filterByDivision);
		}
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

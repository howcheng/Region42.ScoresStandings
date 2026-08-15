/**
 * CSV Upload Page JavaScript
 * Handles season selection toggle
 */
(function() {
	'use strict';

	/**
	 * Toggle visibility of new season name input
	 */
	function toggleNewSeasonName() {
		const seasonId = document.getElementById('seasonId').value;
		const newSeasonDiv = document.getElementById('newSeasonNameDiv');

		if (newSeasonDiv) {
			if (seasonId === '0') {
				newSeasonDiv.classList.remove('hidden');
			} else {
				newSeasonDiv.classList.add('hidden');
			}
		}
	}

	/**
	 * Initialize event listeners
	 */
	function initialize() {
		const seasonSelect = document.getElementById('seasonId');

		if (seasonSelect) {
			// Attach change listener
			seasonSelect.addEventListener('change', toggleNewSeasonName);

			// Initialize on page load
			toggleNewSeasonName();
		}
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

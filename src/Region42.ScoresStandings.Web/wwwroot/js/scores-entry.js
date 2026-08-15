/**
 * Scores Entry Page JavaScript
 * Handles division/round filtering and score validation
 */
(function() {
	'use strict';

	/**
	 * Reload page with selected division and round filters
	 */
	function reloadPage() {
		const divisionId = document.getElementById('divisionSelect').value;
		const round = document.getElementById('roundSelect').value;

		// Build URL with selected parameters
		let url = '/Scores/Entry';
		const params = [];

		if (divisionId) {
			params.push(`divisionId=${divisionId}`);
		}
		if (round) {
			params.push(`round=${round}`);
		}

		if (params.length > 0) {
			url += '?' + params.join('&');
		}

		window.location.href = url;
	}

	/**
	 * Validate that both scores are entered for each game
	 */
	function validateScores(event) {
		const homeScores = document.querySelectorAll('input[name*="HomeScore"]');
		const awayScores = document.querySelectorAll('input[name*="AwayScore"]');
		let hasPartialScore = false;
		let partialGameIndices = [];

		for (let i = 0; i < homeScores.length; i++) {
			const homeValue = homeScores[i].value.trim();
			const awayValue = awayScores[i].value.trim();
			const hasHome = homeValue !== '';
			const hasAway = awayValue !== '';

			// Check if only one score is entered
			if (hasHome !== hasAway) {
				hasPartialScore = true;
				partialGameIndices.push(i + 1);
			}
		}

		if (hasPartialScore) {
			event.preventDefault();
			alert('Error: Both home and away scores must be entered for a game to be complete.\n\n' +
				'Games with incomplete scores: ' + partialGameIndices.join(', ') + '\n\n' +
				'Please either enter both scores or leave both fields empty.');
			return false;
		}

		return true;
	}

	/**
	 * Initialize event listeners
	 */
	function initialize() {
		// Attach change listeners to filters
		const divisionSelect = document.getElementById('divisionSelect');
		const roundSelect = document.getElementById('roundSelect');

		if (divisionSelect) {
			divisionSelect.addEventListener('change', reloadPage);
		}

		if (roundSelect) {
			roundSelect.addEventListener('change', reloadPage);
		}

		// Attach form validation
		const form = document.querySelector('form[action*="Entry"]');
		if (form) {
			form.addEventListener('submit', validateScores);
		}
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

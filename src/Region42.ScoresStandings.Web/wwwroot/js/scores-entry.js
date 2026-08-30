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

		// Validate that rescheduled games have a new date/time entered
		const statusSelects = document.querySelectorAll('.game-status-select');
		let missingRescheduleDates = [];
		statusSelects.forEach((select, i) => {
			const rescheduleFields = document.getElementById(`rescheduleFields_${select.dataset.index}`);
			if (!rescheduleFields) {
				return;
			}
			const isRescheduled = select.options[select.selectedIndex].text === 'Rescheduled';
			const newDateTime = rescheduleFields.querySelector('input[type="datetime-local"]');
			if (isRescheduled && (!newDateTime || newDateTime.value.trim() === '')) {
				missingRescheduleDates.push(i + 1);
			}
		});

		if (missingRescheduleDates.length > 0) {
			event.preventDefault();
			alert('Error: A new date/time is required for rescheduled games.\n\n' +
				'Games missing a new date/time: ' + missingRescheduleDates.join(', '));
			return false;
		}

		return true;
	}

	/**
	 * Toggle visibility of reschedule fields based on selected game status
	 */
	function toggleRescheduleFields(select) {
		const rescheduleFields = document.getElementById(`rescheduleFields_${select.dataset.index}`);
		if (!rescheduleFields) {
			return;
		}
		const isRescheduled = select.options[select.selectedIndex].text === 'Rescheduled';
		rescheduleFields.classList.toggle('d-none', !isRescheduled);
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

		// Attach status change listeners for reschedule field toggling
		document.querySelectorAll('.game-status-select').forEach(select => {
			select.addEventListener('change', () => toggleRescheduleFields(select));
		});
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

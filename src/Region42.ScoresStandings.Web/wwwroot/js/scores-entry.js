/**
 * Scores Entry Page JavaScript
 * Handles division/round filtering, tournament toggles, and score validation
 */
(function() {
	'use strict';

	/**
	 * Reload page with selected division and round filters
	 */
	function reloadPage() {
		const divisionId = document.getElementById('divisionSelect').value;
		const round = document.getElementById('roundSelect').value;

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

		globalThis.location.href = url;
	}

	/**
	 * Validate scores, forfeits, penalty shootouts, and rescheduled dates
	 */
	function validateScores(event) {
		const rows = document.querySelectorAll('.score-row[data-game-index]');
		const partialGameIndices = [];
		const invalidForfeitIndices = [];
		const missingPkIndices = [];
		const tiedPkIndices = [];
		const unexpectedPkIndices = [];

		rows.forEach((row, i) => {
			const idx = row.dataset.gameIndex;
			const isElimination = row.dataset.isElimination === 'true';
			const homeScoreInput = document.getElementById(`homeScore_${idx}`);
			const awayScoreInput = document.getElementById(`awayScore_${idx}`);

			if (!homeScoreInput || !awayScoreInput || homeScoreInput.disabled) {
				return;
			}

			const homeValue = homeScoreInput.value.trim();
			const awayValue = awayScoreInput.value.trim();
			const hasHome = homeValue !== '';
			const hasAway = awayValue !== '';

			if (hasHome !== hasAway) {
				partialGameIndices.push(i + 1);
				return;
			}

			if (!hasHome && !hasAway) {
				return;
			}

			const homeScore = Number.parseInt(homeValue, 10);
			const awayScore = Number.parseInt(awayValue, 10);
			const forfeitCheckbox = document.getElementById(`isForfeit_${idx}`);
			const isForfeit = forfeitCheckbox ? forfeitCheckbox.checked : false;

			if (isForfeit) {
				const validForfeitScore = (homeScore === 1 && awayScore === 0) || (homeScore === 0 && awayScore === 1);
				if (!validForfeitScore) {
					invalidForfeitIndices.push(i + 1);
				}
				return;
			}

			if (isElimination) {
				const pkCheckbox = document.getElementById(`isDecidedByPenalties_${idx}`);
				const homePkInput = document.getElementById(`homePenaltyScore_${idx}`);
				const awayPkInput = document.getElementById(`awayPenaltyScore_${idx}`);
				const homePkVal = homePkInput ? homePkInput.value.trim() : '';
				const awayPkVal = awayPkInput ? awayPkInput.value.trim() : '';
				const hasHomePk = homePkVal !== '';
				const hasAwayPk = awayPkVal !== '';

				if (homeScore === awayScore) {
					if (!hasHomePk || !hasAwayPk) {
						missingPkIndices.push(i + 1);
					} else if (Number.parseInt(homePkVal, 10) === Number.parseInt(awayPkVal, 10)) {
						tiedPkIndices.push(i + 1);
					}
				} else if ((pkCheckbox && pkCheckbox.checked) || hasHomePk || hasAwayPk) {
					unexpectedPkIndices.push(i + 1);
				}
			}
		});

		if (partialGameIndices.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: Both home and away scores must be entered for a game to be complete.\n\n' +
				'Games with incomplete scores: ' + partialGameIndices.join(', ') + '\n\n' +
				'Please either enter both scores or leave both fields empty.');
			return false;
		}

		if (invalidForfeitIndices.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: Forfeit games must be scored as 1-0 or 0-1.\n\n' +
				'Games with invalid forfeit scores: ' + invalidForfeitIndices.join(', '));
			return false;
		}

		if (missingPkIndices.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: Elimination games that end in a tie in regulation require both Home PK and Away PK shootout scores.\n\n' +
				'Games missing PK scores: ' + missingPkIndices.join(', '));
			return false;
		}

		if (tiedPkIndices.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: Penalty shootout scores cannot be tied.\n\n' +
				'Games with tied PK scores: ' + tiedPkIndices.join(', '));
			return false;
		}

		if (unexpectedPkIndices.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: Penalty shootout scores can only be entered when regulation scores are tied.\n\n' +
				'Games with unexpected PK scores: ' + unexpectedPkIndices.join(', '));
			return false;
		}

		const statusSelects = document.querySelectorAll('.game-status-select');
		const missingRescheduleDates = [];
		const missingScheduleDates = [];
		statusSelects.forEach((select, i) => {
			const rescheduleFields = document.getElementById(`rescheduleFields_${select.dataset.index}`);
			if (!rescheduleFields) {
				return;
			}
			const selectedStatus = select.options[select.selectedIndex].text;
			const wasUnscheduled = select.dataset.wasUnscheduled === 'true';
			const newDateTime = rescheduleFields.querySelector('input[type="datetime-local"]');
			const hasDateTime = newDateTime && newDateTime.value.trim() !== '';

			if (selectedStatus === 'Rescheduled' && !hasDateTime) {
				missingRescheduleDates.push(i + 1);
			} else if (selectedStatus === 'Scheduled' && wasUnscheduled && !hasDateTime) {
				missingScheduleDates.push(i + 1);
			}
		});

		if (missingRescheduleDates.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: A new date/time is required for rescheduled games.\n\n' +
				'Games missing a new date/time: ' + missingRescheduleDates.join(', '));
			return false;
		}

		if (missingScheduleDates.length > 0) {
			event.preventDefault();
			globalThis.alert('Error: A date/time is required when changing an unscheduled game to Scheduled.\n\n' +
				'Games missing a date/time: ' + missingScheduleDates.join(', '));
			return false;
		}

		return true;
	}

	/**
	 * Toggle visibility of schedule/reschedule fields based on selected game status
	 */
	function toggleRescheduleFields(select) {
		const index = select.dataset.index;
		const rescheduleFields = document.getElementById(`rescheduleFields_${index}`);
		if (!rescheduleFields) {
			return;
		}
		const selectedStatus = select.options[select.selectedIndex].text;
		const wasUnscheduled = select.dataset.wasUnscheduled === 'true';
		const isRescheduled = selectedStatus === 'Rescheduled';
		const isUnscheduled = selectedStatus === 'Unscheduled';
		const isSchedulingUnscheduled = selectedStatus === 'Scheduled' && wasUnscheduled;

		rescheduleFields.classList.toggle('d-none', !(isRescheduled || isUnscheduled || isSchedulingUnscheduled));

		const newRoundContainer = document.getElementById(`newRoundContainer_${index}`);
		if (newRoundContainer) {
			newRoundContainer.classList.toggle('d-none', !isRescheduled);
		}
	}

	/**
	 * Toggle visibility of penalty shootout inputs for an elimination game
	 */
	function updatePkVisibility(index) {
		const forfeitCheckbox = document.getElementById(`isForfeit_${index}`);
		const pkCheckbox = document.getElementById(`isDecidedByPenalties_${index}`);
		const homePkContainer = document.getElementById(`homePkContainer_${index}`);
		const awayPkContainer = document.getElementById(`awayPkContainer_${index}`);
		const homePkInput = document.getElementById(`homePenaltyScore_${index}`);
		const awayPkInput = document.getElementById(`awayPenaltyScore_${index}`);

		if (!pkCheckbox || !homePkContainer || !awayPkContainer) {
			return;
		}

		const isForfeit = forfeitCheckbox ? forfeitCheckbox.checked : false;
		if (isForfeit) {
			pkCheckbox.checked = false;
			pkCheckbox.disabled = true;
		} else {
			pkCheckbox.disabled = false;
		}

		const showPk = !isForfeit && pkCheckbox.checked;
		homePkContainer.classList.toggle('d-none', !showPk);
		awayPkContainer.classList.toggle('d-none', !showPk);

		if (!showPk && homePkInput && awayPkInput) {
			homePkInput.value = '';
			awayPkInput.value = '';
		}
	}

	/**
	 * Auto-toggle PK checkbox when regulation scores become tied on an elimination game
	 */
	function handleRegulationScoreChange(index) {
		const pkCheckbox = document.getElementById(`isDecidedByPenalties_${index}`);
		if (!pkCheckbox) {
			return;
		}

		const homeScoreInput = document.getElementById(`homeScore_${index}`);
		const awayScoreInput = document.getElementById(`awayScore_${index}`);
		if (!homeScoreInput || !awayScoreInput) {
			return;
		}

		const homeVal = homeScoreInput.value.trim();
		const awayVal = awayScoreInput.value.trim();
		if (homeVal !== '' && awayVal !== '') {
			const isTied = Number.parseInt(homeVal, 10) === Number.parseInt(awayVal, 10);
			pkCheckbox.checked = isTied;
			updatePkVisibility(index);
		}
	}

	/**
	 * Confirm before resetting tournament games that already have scores
	 */
	function handleRegenerateTournamentSubmit(event) {
		const form = event.currentTarget;
		if (form.dataset.hasScores === 'true') {
			const confirmed = globalThis.confirm(
				'Scores have already been entered for one or more tournament games. Regenerating the tournament will clear existing tournament scores and re-seed from Round 9 standings. Continue?');
			if (!confirmed) {
				event.preventDefault();
			}
		}
	}

	/**
	 * Initialize event listeners
	 */
	function initialize() {
		const divisionSelect = document.getElementById('divisionSelect');
		const roundSelect = document.getElementById('roundSelect');

		if (divisionSelect) {
			divisionSelect.addEventListener('change', reloadPage);
		}

		if (roundSelect) {
			roundSelect.addEventListener('change', reloadPage);
		}

		const form = document.querySelector('form[action*="Entry"]');
		if (form) {
			form.addEventListener('submit', validateScores);
		}

		const regenerateForm = document.getElementById('regenerateTournamentForm');
		if (regenerateForm) {
			regenerateForm.addEventListener('submit', handleRegenerateTournamentSubmit);
		}

		document.querySelectorAll('.game-status-select').forEach(select => {
			select.addEventListener('change', () => toggleRescheduleFields(select));
		});

		document.querySelectorAll('.forfeit-toggle').forEach(checkbox => {
			checkbox.addEventListener('change', () => updatePkVisibility(checkbox.dataset.index));
		});

		document.querySelectorAll('.pk-toggle').forEach(checkbox => {
			checkbox.addEventListener('change', () => updatePkVisibility(checkbox.dataset.index));
		});

		document.querySelectorAll('.score-input[data-index]').forEach(input => {
			input.addEventListener('change', () => handleRegulationScoreChange(input.dataset.index));
		});
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

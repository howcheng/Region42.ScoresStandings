/**
 * Standings Page JavaScript
 * Handles filtering and modal interactions for the standings view
 */
(function() {
	'use strict';

	/**
	 * Filter standings by selected division
	 */
	function filterByDivision() {
		const divisionId = document.getElementById('divisionSelect').value;
		const roundSelect = document.getElementById('roundSelect');

		if (divisionId) {
			roundSelect.disabled = false;
			window.location.href = `/Home/Standings?divisionId=${divisionId}`;
		} else {
			roundSelect.disabled = true;
			window.location.href = '/Home/Standings';
		}
	}

	/**
	 * Filter standings by selected round
	 */
	function filterByRound() {
		const divisionId = document.getElementById('divisionSelect').value;
		const throughRound = document.getElementById('roundSelect').value;

		if (divisionId) {
			let url = `/Home/Standings?divisionId=${divisionId}`;
			if (throughRound) {
				url += `&throughRound=${throughRound}`;
			}
			window.location.href = url;
		}
	}

	/**
	 * Show points breakdown modal for a team
	 * @param {string} teamName - Name of the team
	 * @param {number} gamePoints - Game points earned
	 * @param {number} volunteerPoints - Volunteer points earned
	 * @param {number} totalPoints - Total points
	 */
	function showPointsDetail(teamName, gamePoints, volunteerPoints, totalPoints) {
		document.getElementById('modalTeamName').textContent = teamName;
		document.getElementById('modalGamePoints').textContent = gamePoints;
		document.getElementById('modalVolunteerPoints').textContent = volunteerPoints;
		document.getElementById('modalTotalPoints').textContent = totalPoints;

		const modalElement = document.getElementById('pointsDetailModal');
		const modal = new bootstrap.Modal(modalElement);
		modal.show();
	}

	/**
	 * Initialize event listeners when DOM is ready
	 */
	function initialize() {
		// Attach event listeners to select elements
		const divisionSelect = document.getElementById('divisionSelect');
		const roundSelect = document.getElementById('roundSelect');

		if (divisionSelect) {
			divisionSelect.addEventListener('change', filterByDivision);
		}

		if (roundSelect) {
			roundSelect.addEventListener('change', filterByRound);
		}

		// Use event delegation for points detail buttons
		document.addEventListener('click', function(event) {
			const button = event.target.closest('.points-detail-btn');
			if (button) {
				event.preventDefault();
				const teamName = button.dataset.teamName;
				const gamePoints = button.dataset.gamePoints;
				const volunteerPoints = button.dataset.volunteerPoints;
				const totalPoints = button.dataset.totalPoints;

				showPointsDetail(teamName, gamePoints, volunteerPoints, totalPoints);
			}
		});
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

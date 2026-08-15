/**
 * Volunteer Points Entry Page JavaScript
 * Handles division filtering, mobile round selection, input highlighting, and keyboard navigation
 */
(function() {
	'use strict';

	/**
	 * Reload page with selected division filter
	 */
	function reloadPage() {
		const divisionId = document.getElementById('divisionSelect').value;
		let url = '/VolunteerPoints/Entry';

		if (divisionId) {
			url += `?divisionId=${divisionId}`;
		}

		window.location.href = url;
	}

	/**
	 * Show only the selected round column on mobile
	 */
	function showRound(roundNumber) {
		// Hide all round columns
		const allRoundColumns = document.querySelectorAll('.round-column');
		allRoundColumns.forEach(col => col.classList.remove('active-round'));

		// Show only the selected round
		const selectedColumns = document.querySelectorAll(`.round-column[data-round="${roundNumber}"]`);
		selectedColumns.forEach(col => col.classList.add('active-round'));
	}

	/**
	 * Highlight row and column when input is focused
	 */
	function handleInputFocus(event) {
		const input = event.target;
		const td = input.closest('td');
		const tr = input.closest('tr');
		const table = tr.closest('table');
		const colIndex = Array.from(tr.children).indexOf(td);

		// Highlight row
		tr.style.backgroundColor = '#fff3cd';

		// Highlight column header
		const thead = table.querySelector('thead tr');
		if (thead && thead.children[colIndex]) {
			thead.children[colIndex].style.backgroundColor = '#fff3cd';
		}
	}

	/**
	 * Remove highlight when input loses focus
	 */
	function handleInputBlur(event) {
		const input = event.target;
		const td = input.closest('td');
		const tr = input.closest('tr');
		const table = tr.closest('table');
		const colIndex = Array.from(tr.children).indexOf(td);

		// Remove highlight from row
		tr.style.backgroundColor = '';

		// Remove highlight from column header
		const thead = table.querySelector('thead tr');
		if (thead && thead.children[colIndex]) {
			thead.children[colIndex].style.backgroundColor = '';
		}
	}

	/**
	 * Handle Tab key navigation (vertical movement through teams in same round)
	 */
	function handleTabNavigation(event) {
		const input = event.target;
		const td = input.closest('td');
		const tr = input.closest('tr');
		const table = tr.closest('table');
		const tbody = table.querySelector('tbody');
		const rows = Array.from(tbody.querySelectorAll('tr'));
		const colIndex = Array.from(tr.children).indexOf(td);
		const rowIndex = rows.indexOf(tr);

		if (event.key === 'Tab' && !event.shiftKey) {
			event.preventDefault();

			// Try to move to next row in same column
			if (rowIndex < rows.length - 1) {
				const nextRow = rows[rowIndex + 1];
				const nextInput = nextRow.children[colIndex]?.querySelector('input[type="number"]');
				if (nextInput) {
					nextInput.focus();
					nextInput.select();
					return;
				}
			}

			// If at last row, move to first row of next column
			if (colIndex < tr.children.length - 1) {
				const firstRow = rows[0];
				const nextColInput = firstRow.children[colIndex + 1]?.querySelector('input[type="number"]');
				if (nextColInput) {
					nextColInput.focus();
					nextColInput.select();
				}
			}
		} else if (event.key === 'Tab' && event.shiftKey) {
			// Handle Shift+Tab to move up through teams in same round
			event.preventDefault();

			// Try to move to previous row in same column
			if (rowIndex > 0) {
				const prevRow = rows[rowIndex - 1];
				const prevInput = prevRow.children[colIndex]?.querySelector('input[type="number"]');
				if (prevInput) {
					prevInput.focus();
					prevInput.select();
					return;
				}
			}

			// If at first row, move to last row of previous column
			if (colIndex > 1) { // colIndex > 1 because 0 is the team name column
				const lastRow = rows[rows.length - 1];
				const prevColInput = lastRow.children[colIndex - 1]?.querySelector('input[type="number"]');
				if (prevColInput) {
					prevColInput.focus();
					prevColInput.select();
				}
			}
		}
	}

	/**
	 * Initialize all event listeners
	 */
	function initialize() {
		// Attach division select change listener
		const divisionSelect = document.getElementById('divisionSelect');
		if (divisionSelect) {
			divisionSelect.addEventListener('change', reloadPage);
		}

		// Mobile round selector functionality
		const roundSelect = document.getElementById('roundSelect');
		const table = document.querySelector('table');

		if (roundSelect && table) {
			// Show round 1 by default on mobile
			showRound(1);

			roundSelect.addEventListener('change', function() {
				const selectedRound = parseInt(this.value);
				showRound(selectedRound);
			});
		}

		// Attach input event listeners for highlighting and navigation
		if (table) {
			const inputs = table.querySelectorAll('input[type="number"]');
			inputs.forEach(input => {
				input.addEventListener('focus', handleInputFocus);
				input.addEventListener('blur', handleInputBlur);
				input.addEventListener('keydown', handleTabNavigation);
			});
		}
	}

	// Initialize when DOM is ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initialize);
	} else {
		initialize();
	}

})();

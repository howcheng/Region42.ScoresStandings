// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Dark mode / light mode theme toggle
(function () {
    var THEME_COOKIE_NAME = "region42_theme";
    var THEME_COOKIE_DAYS = 400; // ~13 months; refreshed on the server on every request

    function getCookie(name) {
        var match = document.cookie.match(new RegExp("(?:^|; )" + name + "=([^;]*)"));
        return match ? decodeURIComponent(match[1]) : null;
    }

    function setCookie(name, value, days) {
        var expires = new Date();
        expires.setTime(expires.getTime() + days * 24 * 60 * 60 * 1000);
        document.cookie = name + "=" + encodeURIComponent(value) +
            "; expires=" + expires.toUTCString() +
            "; path=/; SameSite=Lax";
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute("data-bs-theme", theme);
        var toggle = document.getElementById("theme-toggle");
        if (toggle) {
            var isDark = theme === "dark";
            toggle.innerHTML = isDark
                ? '<i class="bi bi-sun-fill" aria-hidden="true"></i>'
                : '<i class="bi bi-moon-fill" aria-hidden="true"></i>';
            var label = isDark ? "Switch to light mode" : "Switch to dark mode";
            toggle.setAttribute("title", label);
            toggle.setAttribute("aria-label", label);
        }
    }

    function currentTheme() {
        return document.documentElement.getAttribute("data-bs-theme") === "dark" ? "dark" : "light";
    }

    document.addEventListener("DOMContentLoaded", function () {
        var toggle = document.getElementById("theme-toggle");
        if (!toggle) {
            return;
        }

        // Ensure the button reflects whatever theme was applied by the inline
        // bootstrap script in the layout <head> (to avoid a flash of the wrong theme).
        applyTheme(currentTheme());

        toggle.addEventListener("click", function () {
            var newTheme = currentTheme() === "dark" ? "light" : "dark";
            applyTheme(newTheme);
            setCookie(THEME_COOKIE_NAME, newTheme, THEME_COOKIE_DAYS);
        });
    });
})();

// Theme handling functions
let themeToggleComponent = null;

// Check if dark mode is active
function isDarkMode() {
    // Check localStorage first
    if (localStorage.getItem('theme') === 'dark') {
        return true;
    }
    
    // Then check system preference
    if (localStorage.getItem('theme') === 'light') {
        return false;
    }
    
    // Default to system preference
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

// Toggle theme based on selected mode
function toggleTheme(isDark) {
    if (isDark) {
        document.documentElement.classList.add('dark-theme');
        localStorage.setItem('theme', 'dark');
    } else {
        document.documentElement.classList.remove('dark-theme');
        localStorage.setItem('theme', 'light');
    }
}

// Initialize theme on page load
function initializeTheme() {
    if (isDarkMode()) {
        document.documentElement.classList.add('dark-theme');
    } else {
        document.documentElement.classList.remove('dark-theme');
    }
    
    // Listen for system theme changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        // Only respond to system changes if no theme preference is stored
        if (localStorage.getItem('theme') === null && themeToggleComponent) {
            themeToggleComponent.invokeMethodAsync('OnSystemThemeChanged', e.matches);
        }
    });
}

// Register theme toggle component for JS callbacks
function registerThemeToggle(component) {
    themeToggleComponent = component;
}

// Unregister theme toggle component
function unregisterThemeToggle() {
    themeToggleComponent = null;
}

// Initialize theme when the document is loaded
document.addEventListener('DOMContentLoaded', initializeTheme);

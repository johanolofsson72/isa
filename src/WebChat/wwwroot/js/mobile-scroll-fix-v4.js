// Mobile Scroll Fix - Ensure proper scrolling on all pages

// Function to restore normal scrolling (called when leaving chat page)
window.restoreNormalScrolling = () => {
    try {
        if (!document.body) return;

        // Remove any scroll prevention styles
        document.body.style.overflow = '';
        document.body.style.position = '';
        document.body.style.width = '';
        document.body.style.height = '';
        document.documentElement.style.overflow = '';

        // Remove any no-scroll classes
        document.body.classList.remove('no-scroll');

        // Ensure touch-action allows scrolling
        document.body.style.touchAction = 'pan-y';
        document.body.style.webkitOverflowScrolling = 'touch';

        // Force cleanup of any chat scroll prevention
        if (window.cleanupChatScrollPrevention) {
            window.cleanupChatScrollPrevention();
        }
    } catch (error) {
        console.warn('Error in restoreNormalScrolling:', error);
    }
};

// Function to ensure mobile scrolling works on non-chat pages
window.ensureMobileScrolling = () => {
    try {
        if (window.innerWidth <= 767 && document.body) {
            // Make sure we're not on the chat page
            const chatContainer = document.querySelector('.chat-container');
            if (!chatContainer) {
                // Not on chat page, ensure scrolling works
                restoreNormalScrolling();

                // Add mobile-friendly scroll styles
                document.body.style.webkitOverflowScrolling = 'touch';
                document.body.style.touchAction = 'pan-y';

                // Force remove any lingering scroll prevention
                document.body.style.overflow = '';
                document.body.style.position = '';
                document.documentElement.style.overflow = '';

                console.log('Mobile scrolling enabled for non-chat page');
            } else {
                console.log('Chat page detected, keeping chat scroll prevention');
            }
        }
    } catch (error) {
        console.warn('Error in ensureMobileScrolling:', error);
    }
};

// Call on page load/navigation
document.addEventListener('DOMContentLoaded', ensureMobileScrolling);

// Also call when Blazor navigates (for SPA navigation)
if (window.Blazor) {
    window.Blazor.addEventListener('enhancedload', ensureMobileScrolling);
} else {
    // Fallback for when Blazor isn't loaded yet
    document.addEventListener('DOMContentLoaded', () => {
        if (window.Blazor) {
            window.Blazor.addEventListener('enhancedload', ensureMobileScrolling);
        }
    });
}

// Mobile scroll fix is now active without debug UI

// Isa JavaScript functions for WebChat

// Function to get mute cookie
window.isaGetMuteCookie = function() {
    const cookieName = "isa-mute";
    const cookies = document.cookie.split(';');
    
    for (let cookie of cookies) {
        const [name, value] = cookie.trim().split('=');
        if (name === cookieName) {
            return value === 'true';
        }
    }
    
    return false; // Default to not muted
};

// Function to set mute cookie
window.isaSetMuteCookie = function(isMuted) {
    const cookieName = "isa-mute";
    const cookieValue = isMuted ? 'true' : 'false';
    const expiryDate = new Date();
    expiryDate.setFullYear(expiryDate.getFullYear() + 1); // 1 year expiry
    
    document.cookie = `${cookieName}=${cookieValue}; expires=${expiryDate.toUTCString()}; path=/`;
};

// Function to toggle mute state
window.isaToggleMute = function() {
    const currentMute = window.isaGetMuteCookie();
    const newMute = !currentMute;
    window.isaSetMuteCookie(newMute);
    return newMute;
};

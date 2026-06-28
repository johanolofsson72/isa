// JavaScript functions for chat functionality
window.scrollToBottom = function (elementRef) {
    if (elementRef !== null) {
        elementRef.scrollTop = elementRef.scrollHeight;
    }
};

// Function to focus the message input field
window.focusMessageInput = function () {
    try {
        const messageInput = document.querySelector('.message-input');
        if (messageInput) {
            messageInput.focus();
        }
    } catch (error) {
        console.log('Could not focus message input:', error);
    }
};

window.isaSetMuteCookie = function (value) {
    try {
        localStorage.setItem('isa-muted', value ? '1' : '0');
        // Alternativt: document.cookie = `isa-muted=${value ? '1' : '0'}; path=/; max-age=31536000`;
    } catch {}
}
window.isaGetMuteCookie = function () {
    try {
        var v = localStorage.getItem('isa-muted');
        if (v === '1') return true;
        if (v === '0') return false;
    } catch {}
    return true; // default: muted
}

window.isaStopSpeech = function () {
    try {
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
        // Om Azure Speech SDK används:
        if (window.speechSynthesisVoice && window.speechSynthesisVoice.synthesizer) {
            try { window.speechSynthesisVoice.synthesizer.stopSpeakingAsync(); } catch {}
        }
        if (window.azureSpeechSynthesizer && window.azureSpeechSynthesizer.stopSpeakingAsync) {
            try { window.azureSpeechSynthesizer.stopSpeakingAsync(); window.azureSpeechSynthesizer = null; } catch {}
        }
        // Nytt: anropa även stopAzureSpeech om den finns
        if (window.stopAzureSpeech) {
            try { window.stopAzureSpeech(); } catch {}
        }
    } catch {}
}

// File input trigger is now handled in Chat.razor with Safari support

// Function to clear the message input field
window.clearInputField = function () {
    try {
        const messageInput = document.getElementById('messageInput');
        if (messageInput) {
            messageInput.value = '';
            console.log('🔍 JS DEBUG: Input field cleared successfully');
        } else {
            console.log('🔍 JS DEBUG: messageInput element not found');
        }
    } catch (error) {
        console.log('🔍 JS DEBUG: Error clearing input field:', error);
    }
};


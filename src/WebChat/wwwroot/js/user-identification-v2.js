/**
 * User Identification System for Cross-Device Support
 * Handles automatic user recognition and device synchronization
 */

class UserIdentificationManager {
    constructor() {
        this.agentBaseUrl = 'https://localhost:7042';
        this.deviceFingerprint = this.getOrCreateDeviceFingerprint();
        this.lastIdentificationCheck = null;
        this.identificationPromptShown = false;
    }

    /**
     * Get or create device fingerprint from localStorage
     */
    getOrCreateDeviceFingerprint() {
        let fingerprint = localStorage.getItem('ISA_DID_COOKIE');
        if (!fingerprint) {
            fingerprint = this.generateDeviceFingerprint();
            localStorage.setItem('ISA_DID_COOKIE', fingerprint);
            console.log('🆕 Created new device fingerprint:', fingerprint);
        } else {
            console.log('📱 Using existing device fingerprint:', fingerprint);
        }
        return fingerprint;
    }

    /**
     * Generate a unique device fingerprint
     */
    generateDeviceFingerprint() {
        const timestamp = Date.now();
        const random = Math.random().toString(36).substring(2);
        const userAgent = navigator.userAgent.substring(0, 50);
        const screenInfo = `${window.screen.width}x${window.screen.height}`;

        const combined = `${timestamp}-${random}-${userAgent}-${screenInfo}`;
        return btoa(combined).substring(0, 32);
    }

    /**
     * Check if user should be prompted for identification
     */
    async checkShouldPromptForIdentification() {
        try {
            // Don't check too frequently
            if (this.lastIdentificationCheck && 
                Date.now() - this.lastIdentificationCheck < 60000) { // 1 minute
                return false;
            }

            this.lastIdentificationCheck = Date.now();

            const response = await fetch(
                `${this.agentBaseUrl}/api/user-identification/should-prompt/${this.deviceFingerprint}`
            );

            if (response.ok) {
                const data = await response.json();
                console.log('🔍 Should prompt for identification:', data.shouldPrompt);
                return data.shouldPrompt;
            }
        } catch (error) {
            console.warn('Failed to check identification prompt status:', error);
        }
        return false;
    }

    /**
     * Show identification prompt to user
     */
    showIdentificationPrompt() {
        if (this.identificationPromptShown) {
            return;
        }

        this.identificationPromptShown = true;

        const promptHtml = `
            <div id="user-identification-prompt" style="
                position: fixed;
                top: 20px;
                right: 20px;
                background: #f0f8ff;
                border: 2px solid #4a90e2;
                border-radius: 8px;
                padding: 15px;
                max-width: 300px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                z-index: 1000;
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            ">
                <div style="font-weight: bold; margin-bottom: 10px; color: #2c3e50;">
                    👋 Hej! Vem är du?
                </div>
                <div style="font-size: 14px; margin-bottom: 15px; color: #555;">
                    För att jag ska komma ihåg dig över olika enheter, kan du berätta vem du är?
                </div>
                <input type="text" id="user-name-input" placeholder="t.ex. Anna Andersson" style="
                    width: 100%;
                    padding: 8px;
                    border: 1px solid #ddd;
                    border-radius: 4px;
                    margin-bottom: 10px;
                    box-sizing: border-box;
                ">
                <div style="display: flex; gap: 8px;">
                    <button id="identify-user-btn" style="
                        flex: 1;
                        background: #4a90e2;
                        color: white;
                        border: none;
                        padding: 8px 12px;
                        border-radius: 4px;
                        cursor: pointer;
                        font-size: 14px;
                    ">Identifiera</button>
                    <button id="skip-identification-btn" style="
                        background: #95a5a6;
                        color: white;
                        border: none;
                        padding: 8px 12px;
                        border-radius: 4px;
                        cursor: pointer;
                        font-size: 14px;
                    ">Hoppa över</button>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', promptHtml);

        // Add event listeners
        document.getElementById('identify-user-btn').addEventListener('click', () => {
            this.handleUserIdentification();
        });

        document.getElementById('skip-identification-btn').addEventListener('click', () => {
            this.hideIdentificationPrompt();
        });

        document.getElementById('user-name-input').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                this.handleUserIdentification();
            }
        });

        // Auto-focus the input
        setTimeout(() => {
            document.getElementById('user-name-input').focus();
        }, 100);
    }

    /**
     * Handle user identification
     */
    async handleUserIdentification() {
        const nameInput = document.getElementById('user-name-input');
        const userName = nameInput.value.trim();

        if (!userName) {
            alert('Vänligen ange ditt namn');
            return;
        }

        try {
            const response = await fetch(`${this.agentBaseUrl}/api/user-identification/identify`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    deviceFingerprint: this.deviceFingerprint,
                    userName: userName
                })
            });

            const data = await response.json();

            if (response.ok && data.success) {
                console.log('✅ User identified successfully:', data);
                this.showSuccessMessage(`Hej ${userName}! Jag kommer nu ihåg dig på denna enhet.`);
                this.hideIdentificationPrompt();
            } else {
                console.warn('❌ User identification failed:', data);
                this.showErrorMessage(data.message || 'Kunde inte identifiera användaren');
            }
        } catch (error) {
            console.error('Failed to identify user:', error);
            this.showErrorMessage('Ett fel uppstod vid identifiering');
        }
    }

    /**
     * Hide identification prompt
     */
    hideIdentificationPrompt() {
        const prompt = document.getElementById('user-identification-prompt');
        if (prompt) {
            prompt.remove();
        }
        this.identificationPromptShown = false;
    }

    /**
     * Show success message
     */
    showSuccessMessage(message) {
        this.showMessage(message, '#27ae60');
    }

    /**
     * Show error message
     */
    showErrorMessage(message) {
        this.showMessage(message, '#e74c3c');
    }

    /**
     * Show temporary message
     */
    showMessage(message, color) {
        const messageHtml = `
            <div style="
                position: fixed;
                top: 20px;
                right: 20px;
                background: ${color};
                color: white;
                padding: 12px 20px;
                border-radius: 6px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                z-index: 1001;
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                font-size: 14px;
                max-width: 300px;
            ">${message}</div>
        `;

        const messageElement = document.createElement('div');
        messageElement.innerHTML = messageHtml;
        document.body.appendChild(messageElement);

        setTimeout(() => {
            messageElement.remove();
        }, 3000);
    }

    /**
     * Initialize user identification system
     */
    async initialize() {
        console.log('🚀 Initializing User Identification Manager');
        
        // Check if we should prompt for identification after a short delay
        setTimeout(async () => {
            const shouldPrompt = await this.checkShouldPromptForIdentification();
            if (shouldPrompt) {
                this.showIdentificationPrompt();
            }
        }, 2000); // Wait 2 seconds after page load
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    try {
        window.userIdentificationManager = new UserIdentificationManager();
        window.userIdentificationManager.initialize();
    } catch (error) {
        console.error('Error initializing UserIdentificationManager:', error);
    }
});

// Export for use in other scripts
window.UserIdentificationManager = UserIdentificationManager;

// Also make generateDeviceFingerprint available globally for backward compatibility
window.generateDeviceFingerprint = () => {
    try {
        if (window.userIdentificationManager) {
            return window.userIdentificationManager.generateDeviceFingerprint();
        } else {
            // Fallback implementation
            const timestamp = Date.now();
            const random = Math.random().toString(36).substring(2);
            return `fallback-${timestamp}-${random}`;
        }
    } catch (error) {
        console.error('Error generating device fingerprint:', error);
        return `error-${Date.now()}`;
    }
};

// Speech Recognition JavaScript for WebChat
let recognition = null;
let isRecognizing = false;
let dotNetHelper = null;

function startSpeechRecognition(dotNetObjectReference) {
    if (!('webkitSpeechRecognition' in window) && !('SpeechRecognition' in window)) {
        dotNetObjectReference.invokeMethodAsync('OnSpeechError', 'Röstigenkänning stöds inte i denna webbläsare');
        return;
    }

    try {
        dotNetHelper = dotNetObjectReference;
        
        // Create speech recognition instance
        recognition = new (window.SpeechRecognition || window.webkitSpeechRecognition)();
        
        // Configure recognition
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = 'sv-SE'; // Swedish language
        recognition.maxAlternatives = 1;

        let finalTranscript = '';
        let interimTranscript = '';

        recognition.onresult = function(event) {
            interimTranscript = '';
            finalTranscript = '';

            for (let i = event.resultIndex; i < event.results.length; i++) {
                const transcript = event.results[i][0].transcript;
                
                if (event.results[i].isFinal) {
                    finalTranscript += transcript;
                } else {
                    interimTranscript += transcript;
                }
            }

            // Send the current transcript to Blazor
            const currentTranscript = finalTranscript + interimTranscript;
            if (currentTranscript.trim()) {
                dotNetHelper.invokeMethodAsync('OnSpeechResult', currentTranscript.trim());
            }
        };

        recognition.onerror = function(event) {
            console.error('Speech recognition error:', event.error);
            let errorMessage = 'Okänt fel';
            
            switch(event.error) {
                case 'no-speech':
                    errorMessage = 'Inget tal detekterat';
                    break;
                case 'audio-capture':
                    errorMessage = 'Ingen mikrofon hittades';
                    break;
                case 'not-allowed':
                    errorMessage = 'Mikrofonåtkomst nekad';
                    break;
                case 'network':
                    errorMessage = 'Nätverksfel';
                    break;
                default:
                    errorMessage = event.error;
            }
            
            dotNetHelper.invokeMethodAsync('OnSpeechError', errorMessage);
            isRecognizing = false;
        };

        recognition.onend = function() {
            isRecognizing = false;
            console.log('Speech recognition ended');
        };

        recognition.onstart = function() {
            isRecognizing = true;
            console.log('Speech recognition started');
        };

        // Start recognition
        recognition.start();
        
    } catch (error) {
        console.error('Error starting speech recognition:', error);
        dotNetObjectReference.invokeMethodAsync('OnSpeechError', error.message);
    }
}

function stopSpeechRecognition() {
    if (recognition && isRecognizing) {
        recognition.stop();
        isRecognizing = false;
        console.log('Speech recognition stopped manually');
    }
}

// Auto-scroll function for chat messages
function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

// Azure Speech text-to-speech (sv-SE-EbbaNeural)
window.speakWithAzure = function (text, subscriptionKey, region) {
    console.log("speakWithAzure called", text, subscriptionKey, region);
    if (!window.SpeechSDK) {
        console.error("Azure Speech SDK not loaded!");
        return;
    }
    if (typeof window.AudioContext !== "undefined" || typeof window.webkitAudioContext !== "undefined") {
        console.log("AudioContext is available");
    } else {
        console.warn("AudioContext is NOT available. Audio output may not work.");
    }
    try {
        const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(subscriptionKey, region);
        speechConfig.speechSynthesisVoiceName = "sv-SE-SofieNeural";
        console.log("SpeechConfig created", speechConfig);
        const synthesizer = new SpeechSDK.SpeechSynthesizer(speechConfig);
        window.azureSpeechSynthesizer = synthesizer; // Spara global referens
        console.log("SpeechSynthesizer created", synthesizer);
        synthesizer.speakTextAsync(
            text,
            function (result) {
                console.log("Speech synthesis succeeded", result);
                synthesizer.close();
                if (window.azureSpeechSynthesizer === synthesizer) window.azureSpeechSynthesizer = null;
            },
            function (err) {
                console.error("Speech synthesis error:", err);
                synthesizer.close();
                if (window.azureSpeechSynthesizer === synthesizer) window.azureSpeechSynthesizer = null;
            }
        );
        console.log("speakTextAsync called");
    } catch (e) {
        console.error("Exception in speakWithAzure:", e);
    }
};

window.stopAzureSpeech = function () {
    try {
        if (window.azureSpeechSynthesizer && window.azureSpeechSynthesizer.stopSpeakingAsync) {
            window.azureSpeechSynthesizer.stopSpeakingAsync();
            window.azureSpeechSynthesizer = null;
        }
    } catch {}
}

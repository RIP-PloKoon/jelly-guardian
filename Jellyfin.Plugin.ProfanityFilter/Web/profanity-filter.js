// Jellyfin Profanity Filter - Client-Side Integration
// This script should be injected into the Jellyfin web client

(function() {
    'use strict';

    class ProfanityFilter {
        constructor() {
            this.enabled = false;
            this.muteRanges = [];
            this.currentItemId = null;
            this.audioContext = null;
            this.originalVolume = 1.0;
            this.checkInterval = null;
        }

        async init() {
            console.log('[ProfanityFilter] Initializing...');
            
            // Get user preferences
            await this.loadUserPreferences();
            
            // Hook into video player
            this.hookVideoPlayer();
        }

        async loadUserPreferences() {
            try {
                const userId = ApiClient.getCurrentUserId();
                const response = await ApiClient.getJSON(
                    ApiClient.getUrl(`ProfanityFilter/UserPreferences/${userId}`)
                );
                this.enabled = response.Enabled;
                console.log(`[ProfanityFilter] User preference loaded: ${this.enabled ? 'Enabled' : 'Disabled'}`);
            } catch (error) {
                console.error('[ProfanityFilter] Failed to load user preferences:', error);
                this.enabled = false;
            }
        }

        async loadMetadata(itemId) {
            if (!this.enabled || !itemId) {
                this.muteRanges = [];
                return;
            }

            try {
                const response = await ApiClient.getJSON(
                    ApiClient.getUrl(`ProfanityFilter/Metadata/${itemId}`)
                );
                this.muteRanges = response.muteRanges || [];
                console.log(`[ProfanityFilter] Loaded ${this.muteRanges.length} mute ranges for item ${itemId}`);
            } catch (error) {
                console.warn('[ProfanityFilter] No profanity metadata found for this item');
                this.muteRanges = [];
            }
        }

        hookVideoPlayer() {
            // Listen for playback events
            Events.on(playbackManager, 'playbackstart', (e, player) => {
                this.onPlaybackStart(player);
            });

            Events.on(playbackManager, 'playbackstop', () => {
                this.onPlaybackStop();
            });
        }

        async onPlaybackStart(player) {
            const state = playbackManager.getPlayerState();
            if (!state || !state.NowPlayingItem) {
                return;
            }

            const itemId = state.NowPlayingItem.Id;
            this.currentItemId = itemId;
            console.log(`[ProfanityFilter] Playback started for item ${itemId}`);

            // Load profanity metadata for this item
            await this.loadMetadata(itemId);

            if (this.muteRanges.length > 0) {
                this.startMonitoring(player);
            }
        }

        onPlaybackStop() {
            console.log('[ProfanityFilter] Playback stopped');
            this.currentItemId = null;
            this.stopMonitoring();
        }

        startMonitoring(player) {
            this.stopMonitoring();

            // Get video element
            const videoElement = player.currentMediaElement;
            if (!videoElement) {
                console.error('[ProfanityFilter] Could not find video element');
                return;
            }

            // Store original volume
            this.originalVolume = videoElement.volume;

            // Check current time every 100ms
            this.checkInterval = setInterval(() => {
                this.checkAndMute(videoElement);
            }, 100);

            console.log('[ProfanityFilter] Started monitoring');
        }

        stopMonitoring() {
            if (this.checkInterval) {
                clearInterval(this.checkInterval);
                this.checkInterval = null;
                console.log('[ProfanityFilter] Stopped monitoring');
            }
        }

        checkAndMute(videoElement) {
            if (!videoElement || !this.enabled || this.muteRanges.length === 0) {
                return;
            }

            const currentTimeMs = videoElement.currentTime * 1000;

            // Check if current time is within any mute range
            const shouldMute = this.muteRanges.some(range => 
                currentTimeMs >= range.start && currentTimeMs <= range.end
            );

            if (shouldMute && videoElement.volume > 0) {
                // Mute
                this.originalVolume = videoElement.volume;
                videoElement.volume = 0;
            } else if (!shouldMute && videoElement.volume === 0) {
                // Unmute
                videoElement.volume = this.originalVolume;
            }
        }

        async toggleFilter() {
            this.enabled = !this.enabled;
            console.log(`[ProfanityFilter] Filter ${this.enabled ? 'enabled' : 'disabled'}`);

            // Save preference
            try {
                const userId = ApiClient.getCurrentUserId();
                await ApiClient.ajax({
                    type: 'POST',
                    url: ApiClient.getUrl(`ProfanityFilter/UserPreferences/${userId}`),
                    data: JSON.stringify({ Enabled: this.enabled }),
                    contentType: 'application/json'
                });
            } catch (error) {
                console.error('[ProfanityFilter] Failed to save preference:', error);
            }

            // If currently playing, reload metadata
            const state = playbackManager.getPlayerState();
            if (state && state.NowPlayingItem) {
                await this.loadMetadata(state.NowPlayingItem.Id);
            }
        }

        getStatus() {
            return {
                enabled: this.enabled,
                muteRangeCount: this.muteRanges.length,
                currentItemId: this.currentItemId
            };
        }
    }

    function initWhenReady(attemptsLeft) {
        if (window.profanityFilter) {
            return;
        }

        if (typeof ApiClient !== 'undefined' && typeof playbackManager !== 'undefined' && typeof Events !== 'undefined') {
            window.profanityFilter = new ProfanityFilter();
            window.profanityFilter.init();
            console.log('[ProfanityFilter] Plugin loaded. Use profanityFilter.toggleFilter() to toggle.');
            return;
        }

        if (attemptsLeft > 0) {
            setTimeout(function() {
                initWhenReady(attemptsLeft - 1);
            }, 1000);
        } else {
            console.warn('[ProfanityFilter] Jellyfin web APIs were not available; plugin script did not initialize.');
        }
    }

    initWhenReady(30);
})();

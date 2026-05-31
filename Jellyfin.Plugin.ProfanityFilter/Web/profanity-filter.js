// Jellyfin Profanity Filter - Client-Side Integration
// This script can be injected into the Jellyfin web client.

(function() {
    'use strict';

    const LOG_PREFIX = '[ProfanityFilter]';
    const CHECK_INTERVAL_MS = 100;
    const DISCOVERY_INTERVAL_MS = 500;
    const GUID_PATTERN = '[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}';

    class JellyfinClient {
        constructor() {
            this.baseUrl = this.findBaseUrl();
            this.auth = this.findAuth();
        }

        get hasLegacyApiClient() {
            return typeof window.ApiClient !== 'undefined';
        }

        get hasLegacyPlaybackApi() {
            return (
                typeof window.playbackManager !== 'undefined' &&
                typeof window.Events !== 'undefined'
            );
        }

        getCurrentUserId() {
            if (this.hasLegacyApiClient && typeof window.ApiClient.getCurrentUserId === 'function') {
                return window.ApiClient.getCurrentUserId();
            }

            this.auth = this.auth || this.findAuth();
            return this.auth && this.auth.userId;
        }

        async getJson(path) {
            if (this.hasLegacyApiClient && typeof window.ApiClient.getJSON === 'function') {
                return window.ApiClient.getJSON(window.ApiClient.getUrl(path));
            }

            return this.fetchJson(path);
        }

        async postJson(path, body) {
            if (this.hasLegacyApiClient && typeof window.ApiClient.ajax === 'function') {
                return window.ApiClient.ajax({
                    type: 'POST',
                    url: window.ApiClient.getUrl(path),
                    data: JSON.stringify(body),
                    contentType: 'application/json'
                });
            }

            return this.fetchJson(path, {
                method: 'POST',
                body: JSON.stringify(body)
            });
        }

        async fetchJson(path, options) {
            this.auth = this.auth || this.findAuth();
            const response = await fetch(this.getUrl(path), {
                method: options && options.method ? options.method : 'GET',
                headers: this.getHeaders(),
                body: options && options.body,
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(`Request failed: ${response.status} ${response.statusText}`);
            }

            return response.json();
        }

        getUrl(path) {
            const cleanPath = path.replace(/^\/+/, '');
            return `${this.baseUrl}/${cleanPath}`;
        }

        getHeaders() {
            const headers = {
                Accept: 'application/json',
                'Content-Type': 'application/json'
            };

            if (this.auth && this.auth.accessToken) {
                headers.Authorization = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${this.auth.deviceId || 'profanity-filter-web'}", Version="1.0.0", Token="${this.auth.accessToken}"`;
            }

            return headers;
        }

        findBaseUrl() {
            if (this.hasLegacyApiClient && typeof window.ApiClient.getUrl === 'function') {
                const markerUrl = window.ApiClient.getUrl('ProfanityFilter/profanity-filter.js');
                return markerUrl.replace(/\/ProfanityFilter\/profanity-filter\.js.*$/, '');
            }

            const script = document.currentScript || Array.from(document.scripts).find(candidate =>
                candidate.src && candidate.src.indexOf('/ProfanityFilter/profanity-filter.js') !== -1
            );

            if (script && script.src) {
                return script.src.replace(/\/ProfanityFilter\/profanity-filter\.js.*$/, '');
            }

            return window.location.origin;
        }

        findAuth() {
            const candidates = [];
            this.collectStorageCandidates(window.localStorage, candidates);
            this.collectStorageCandidates(window.sessionStorage, candidates);

            const currentOrigin = window.location.origin.toLowerCase();
            const currentHost = window.location.host.toLowerCase();
            const matching = candidates.find(candidate => {
                const server = `${candidate.address || ''} ${candidate.localAddress || ''} ${candidate.remoteAddress || ''}`.toLowerCase();
                return server.indexOf(currentOrigin) !== -1 || server.indexOf(currentHost) !== -1;
            });

            return matching || candidates[0] || null;
        }

        collectStorageCandidates(storage, candidates) {
            if (!storage) {
                return;
            }

            for (let index = 0; index < storage.length; index++) {
                const key = storage.key(index);
                const value = storage.getItem(key);
                this.collectAuthCandidates(value, candidates);
            }
        }

        collectAuthCandidates(value, candidates) {
            if (!value || value.length > 100000) {
                return;
            }

            try {
                this.walkAuthObject(JSON.parse(value), candidates);
            } catch (error) {
                return;
            }
        }

        walkAuthObject(value, candidates) {
            if (!value || typeof value !== 'object') {
                return;
            }

            const accessToken = value.AccessToken || value.accessToken;
            const userId = value.UserId || value.userId;

            if (accessToken) {
                candidates.push({
                    accessToken,
                    userId,
                    deviceId: value.DeviceId || value.deviceId,
                    address: value.Address || value.address,
                    localAddress: value.LocalAddress || value.localAddress,
                    remoteAddress: value.RemoteAddress || value.remoteAddress
                });
            }

            if (Array.isArray(value)) {
                value.forEach(item => this.walkAuthObject(item, candidates));
                return;
            }

            Object.keys(value).forEach(key => this.walkAuthObject(value[key], candidates));
        }
    }

    class ProfanityFilter {
        constructor() {
            this.client = new JellyfinClient();
            this.enabled = false;
            this.muteRanges = [];
            this.currentItemId = null;
            this.currentVideoElement = null;
            this.originalVolume = 1.0;
            this.checkInterval = null;
            this.discoveryInterval = null;
            this.lastVideoSource = null;
        }

        async init() {
            console.log(`${LOG_PREFIX} Initializing...`);
            await this.loadUserPreferences();

            if (this.client.hasLegacyPlaybackApi) {
                this.hookLegacyVideoPlayer();
            }

            this.startDiscovery();
            console.log(`${LOG_PREFIX} Plugin loaded. Use profanityFilter.toggleFilter() to toggle.`);
        }

        async loadUserPreferences() {
            try {
                const userId = this.client.getCurrentUserId();
                if (!userId) {
                    console.warn(`${LOG_PREFIX} Could not find current Jellyfin user; defaulting to enabled for this page session.`);
                    this.enabled = true;
                    return;
                }

                const response = await this.client.getJson(`ProfanityFilter/UserPreferences/${userId}`);
                this.enabled = response.Enabled !== false;
                console.log(`${LOG_PREFIX} User preference loaded: ${this.enabled ? 'Enabled' : 'Disabled'}`);
            } catch (error) {
                console.warn(`${LOG_PREFIX} Failed to load user preferences; defaulting to enabled for this page session.`, error);
                this.enabled = true;
            }
        }

        async loadMetadata(itemId) {
            if (!this.enabled || !itemId) {
                this.muteRanges = [];
                return;
            }

            try {
                const response = await this.client.getJson(`ProfanityFilter/Metadata/${itemId}`);
                this.muteRanges = response.muteRanges || [];
                console.log(`${LOG_PREFIX} Loaded ${this.muteRanges.length} mute ranges for item ${itemId}`);
            } catch (error) {
                console.warn(`${LOG_PREFIX} No profanity metadata found for item ${itemId}.`, error);
                this.muteRanges = [];
            }
        }

        hookLegacyVideoPlayer() {
            window.Events.on(window.playbackManager, 'playbackstart', (event, player) => {
                this.onLegacyPlaybackStart(player);
            });

            window.Events.on(window.playbackManager, 'playbackstop', () => {
                this.onPlaybackStop();
            });
        }

        async onLegacyPlaybackStart(player) {
            const state = window.playbackManager.getPlayerState();
            if (!state || !state.NowPlayingItem) {
                return;
            }

            await this.loadForPlayback(state.NowPlayingItem.Id, player.currentMediaElement);
        }

        startDiscovery() {
            this.stopDiscovery();
            this.discoveryInterval = setInterval(() => this.discoverPlayback(), DISCOVERY_INTERVAL_MS);
            this.discoverPlayback();
        }

        stopDiscovery() {
            if (this.discoveryInterval) {
                clearInterval(this.discoveryInterval);
                this.discoveryInterval = null;
            }
        }

        async discoverPlayback() {
            const videoElement = document.querySelector('video');
            if (!videoElement) {
                return;
            }

            const source = videoElement.currentSrc || videoElement.src || '';
            if (!source || source === this.lastVideoSource) {
                return;
            }

            const itemId = this.findItemId(videoElement, source);
            if (!itemId || itemId === this.currentItemId) {
                this.lastVideoSource = source;
                return;
            }

            this.lastVideoSource = source;
            await this.loadForPlayback(itemId, videoElement);
        }

        async loadForPlayback(itemId, videoElement) {
            this.currentItemId = itemId;
            this.currentVideoElement = videoElement;
            console.log(`${LOG_PREFIX} Playback detected for item ${itemId}`);

            await this.loadMetadata(itemId);

            if (this.muteRanges.length > 0 && videoElement) {
                this.startMonitoring(videoElement);
            } else {
                this.stopMonitoring();
            }
        }

        findItemId(videoElement, source) {
            const values = [
                source,
                videoElement.src,
                videoElement.currentSrc,
                window.location.href,
                ...performance.getEntriesByType('resource')
                    .map(entry => entry.name)
                    .filter(name => name.indexOf('/Videos/') !== -1)
                    .slice(-10)
            ];

            for (const value of values) {
                const itemId = this.findGuidInUrl(value);
                if (itemId) {
                    return itemId;
                }
            }

            return null;
        }

        findGuidInUrl(value) {
            if (!value) {
                return null;
            }

            const videosMatch = value.match(new RegExp(`/Videos/(${GUID_PATTERN})(?:/|\\?|$)`, 'i'));
            if (videosMatch) {
                return videosMatch[1];
            }

            const queryMatch = value.match(new RegExp(`[?&](?:id|itemId)=(${GUID_PATTERN})(?:&|$)`, 'i'));
            return queryMatch ? queryMatch[1] : null;
        }

        onPlaybackStop() {
            console.log(`${LOG_PREFIX} Playback stopped`);
            this.currentItemId = null;
            this.currentVideoElement = null;
            this.muteRanges = [];
            this.stopMonitoring();
        }

        startMonitoring(videoElement) {
            this.stopMonitoring();
            this.currentVideoElement = videoElement;
            this.originalVolume = videoElement.volume;
            this.checkInterval = setInterval(() => this.checkAndMute(videoElement), CHECK_INTERVAL_MS);
            console.log(`${LOG_PREFIX} Started monitoring`);
        }

        stopMonitoring() {
            if (this.checkInterval) {
                clearInterval(this.checkInterval);
                this.checkInterval = null;
                console.log(`${LOG_PREFIX} Stopped monitoring`);
            }
        }

        checkAndMute(videoElement) {
            if (!videoElement || !this.enabled || this.muteRanges.length === 0) {
                return;
            }

            const currentTimeMs = videoElement.currentTime * 1000;
            const shouldMute = this.muteRanges.some(range =>
                currentTimeMs >= range.start && currentTimeMs <= range.end
            );

            if (shouldMute && videoElement.volume > 0) {
                this.originalVolume = videoElement.volume;
                videoElement.volume = 0;
            } else if (!shouldMute && videoElement.volume === 0) {
                videoElement.volume = this.originalVolume;
            }
        }

        async toggleFilter() {
            this.enabled = !this.enabled;
            console.log(`${LOG_PREFIX} Filter ${this.enabled ? 'enabled' : 'disabled'}`);

            try {
                const userId = this.client.getCurrentUserId();
                if (userId) {
                    await this.client.postJson(`ProfanityFilter/UserPreferences/${userId}`, { Enabled: this.enabled });
                }
            } catch (error) {
                console.error(`${LOG_PREFIX} Failed to save preference:`, error);
            }

            if (this.currentItemId) {
                await this.loadMetadata(this.currentItemId);
            }
        }

        async refresh() {
            await this.discoverPlayback();
            if (this.currentItemId) {
                await this.loadMetadata(this.currentItemId);
            }

            return this.getStatus();
        }

        getStatus() {
            return {
                enabled: this.enabled,
                muteRangeCount: this.muteRanges.length,
                currentItemId: this.currentItemId,
                hasLegacyApiClient: this.client.hasLegacyApiClient,
                hasLegacyPlaybackApi: this.client.hasLegacyPlaybackApi,
                hasAuthToken: !!(this.client.auth && this.client.auth.accessToken),
                videoDetected: !!document.querySelector('video')
            };
        }
    }

    if (!window.profanityFilter) {
        window.profanityFilter = new ProfanityFilter();
        window.profanityFilter.init();
    }
})();

(function () {
    'use strict';

    const marker = 'data-profanity-filter-client';
    if (document.querySelector(`script[${marker}]`) || window.profanityFilter) {
        return;
    }

    const loader = document.currentScript;
    if (!loader || !loader.src) {
        console.error('[ProfanityFilter] The loader must be loaded as a script element.');
        return;
    }

    const clientScript = document.createElement('script');
    clientScript.setAttribute(marker, 'true');
    clientScript.src = new URL('profanity-filter.js', loader.src).href;
    clientScript.async = false;
    clientScript.onerror = function () {
        console.error(`[ProfanityFilter] Could not load ${clientScript.src}`);
    };
    document.head.appendChild(clientScript);
})();

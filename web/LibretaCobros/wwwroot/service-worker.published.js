// Soporte offline del shell de la app. Ver https://aka.ms/blazor-offline-considerations
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => {
    self.skipWaiting();
    event.waitUntil(onInstall(event));
});
self.addEventListener('activate', event => {
    event.waitUntil(onActivate(event).then(() => self.clients.claim()));
});
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

const base = self.location.pathname.includes('/AppCobros/') ? '/AppCobros/' : '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(new URL(asset.url, baseUrl).href, { cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    if (event.request.method === 'GET') {
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        if (shouldServeIndexHtml) {
            try {
                const networkResponse = await fetch(event.request);
                if (networkResponse.ok) return networkResponse;
            } catch (err) { /* offline */ }
            const cache = await caches.open(cacheName);
            return (await cache.match('index.html')) || fetch(event.request);
        }

        const cache = await caches.open(cacheName);
        const cachedResponse = await cache.match(event.request);
        if (cachedResponse) return cachedResponse;
    }
    return fetch(event.request);
}

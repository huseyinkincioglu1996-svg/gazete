"use strict";

const CACHE_PREFIX = "gazete-shell-";
const CACHE_NAME = `${CACHE_PREFIX}v2`;
const OFFLINE_URL = "/offline.html";
const SHELL_ASSETS = [
  OFFLINE_URL,
  "/manifest.webmanifest",
  "/css/site.css",
  "/js/site.js",
  "/lib/bootstrap/dist/css/bootstrap.min.css",
  "/lib/bootstrap/dist/js/bootstrap.bundle.min.js",
  "/lib/jquery/dist/jquery.min.js",
  "/icons/icon-192.png",
  "/icons/icon-512.png",
  "/icons/icon-maskable-512.png",
  "/icons/apple-touch-icon.png",
  "/favicon.ico"
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS))
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(
        keys
          .filter((key) => key.startsWith(CACHE_PREFIX) && key !== CACHE_NAME)
          .map((key) => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("message", (event) => {
  if (event.data?.type === "SKIP_WAITING") {
    self.skipWaiting();
  }
});

const isShellAsset = (path) => path === "/manifest.webmanifest"
  || path === "/favicon.ico"
  || path === OFFLINE_URL
  || path.endsWith(".styles.css")
  || path.startsWith("/css/")
  || path.startsWith("/js/")
  || path.startsWith("/lib/")
  || path.startsWith("/icons/");

self.addEventListener("fetch", (event) => {
  const request = event.request;

  if (request.method !== "GET") {
    return;
  }

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) {
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request).catch(() => caches.match(OFFLINE_URL))
    );
    return;
  }

  if (!isShellAsset(url.pathname)) {
    return;
  }

  event.respondWith(
    caches.open(CACHE_NAME).then(async (cache) => {
      const cached = await cache.match(request);
      const fresh = fetch(request)
        .then(async (response) => {
          if (response.ok && response.type === "basic") {
            await cache.put(request, response.clone());
          }
          return response;
        })
        .catch(() => null);

      return cached || await fresh || Response.error();
    })
  );
});

// background.js - Service Worker for EDM Browser Extension Capturer (Chrome MV3)
// Communicates with EDM Desktop App via Native Messaging Host "com.edm.downloader"

const NATIVE_HOST = "com.edm.downloader";
const MEDIA_REGEX = /\.(mp4|m3u8|mpd|webm|mp3|m4a|aac|flac|ts|mkv|pdf|zip|rar|exe|iso)(\?.*)?$/i;

// Store intercepted stream links per tab
const tabMediaStreams = new Map();

// Settings cache
let settings = {
  enabled: true,
  excludedDomains: [],
  fileExtensions: ["mp4", "m3u8", "mpd", "mp3", "webm", "zip", "rar", "exe"]
};

// Load settings from storage
chrome.storage.local.get(["edmSettings"], (result) => {
  if (result.edmSettings) {
    settings = { ...settings, ...result.edmSettings };
  }
});

// Setup Context Menus on Install
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "edm_download_link",
    title: "Download with EDM",
    contexts: ["link", "image", "video", "audio"]
  });

  chrome.contextMenus.create({
    id: "edm_download_selected",
    title: "Download selected with EDM",
    contexts: ["selection"]
  });

  chrome.contextMenus.create({
    id: "edm_download_all",
    title: "Download all links with EDM",
    contexts: ["page"]
  });
});

// Context Menu Click Handler
chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (!tab) return;

  if (info.menuItemId === "edm_download_link" && info.linkUrl) {
    sendToNativeHost(info.linkUrl, deriveFilename(info.linkUrl), "Context Menu Link");
  } else if (info.menuItemId === "edm_download_selected" && info.selectionText) {
    const urls = info.selectionText.match(/https?:\/\/[^\s]+/g);
    if (urls) {
      urls.forEach(url => sendToNativeHost(url, deriveFilename(url), "Selected Link"));
    }
  } else if (info.menuItemId === "edm_download_all" && tab.id) {
    chrome.tabs.sendMessage(tab.id, { action: "EXTRACT_ALL_LINKS" }, (response) => {
      if (response && response.links) {
        response.links.forEach(url => sendToNativeHost(url, deriveFilename(url), "Download All"));
      }
    });
  }
});

// Network request sniffing for video/audio streams
chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    if (!settings.enabled || !details.url || !details.tabId || details.tabId < 0) return;

    if (MEDIA_REGEX.test(details.url)) {
      let streams = tabMediaStreams.get(details.tabId) || [];
      if (!streams.some(s => s.url === details.url)) {
        let quality = "Media Stream";
        if (details.url.endsWith(".m3u8")) quality = "HLS Stream (M3U8)";
        else if (details.url.endsWith(".mpd")) quality = "DASH Stream (MPD)";
        else if (details.url.includes("1080")) quality = "1080p Full HD";
        else if (details.url.includes("720")) quality = "720p HD";
        else if (details.url.endsWith(".mp3") || details.url.endsWith(".m4a")) quality = "Audio Stream";

        streams.push({
          url: details.url,
          quality: quality,
          filename: deriveFilename(details.url),
          timestamp: Date.now()
        });

        tabMediaStreams.set(details.tabId, streams);

        // Notify floating widget on active tab
        chrome.tabs.sendMessage(details.tabId, {
          action: "MEDIA_DETECTED",
          streams: streams
        }).catch(() => {});
      }
    }
  },
  { urls: ["<all_urls>"] }
);

// Listen for messages from content.js
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === "START_EDM_DOWNLOAD") {
    sendToNativeHost(request.url, request.filename, request.quality);
    sendResponse({ status: "SENT_TO_EDM" });
  } else if (request.action === "GET_MEDIA_STREAMS") {
    const tabId = sender.tab ? sender.tab.id : null;
    const streams = tabId ? (tabMediaStreams.get(tabId) || []) : [];
    sendResponse({ streams: streams, settings: settings });
  } else if (request.action === "UPDATE_SETTINGS") {
    settings = { ...settings, ...request.settings };
    chrome.storage.local.set({ edmSettings: settings });
    sendResponse({ status: "UPDATED" });
  }
  return true;
});

// Deterministic Handoff Download Interception State Machine
chrome.downloads.onCreated.addListener((item) => {
  try {
    if (!settings.enabled) return;

    const url = item.url || "";
    if (!url || !/^https?:\/\//i.test(url)) return;

    // Check domain exclusion
    try {
      const hostname = new URL(url).hostname;
      if (settings.excludedDomains.some(d => hostname.includes(d))) return;
    } catch (e) {}

    const correlationId = "edm_corr_" + Date.now() + "_" + Math.random().toString(36).substring(2, 9);
    const filename = item.filename || deriveFilename(url);

    // Handoff to native host with correlation handshake before cancelling browser download
    sendToNativeHostWithHandshake(correlationId, url, filename, "Browser Intercept", (success) => {
      if (success) {
        // Safe cancellation only after EDM host confirms receipt
        chrome.downloads.cancel(item.id, () => {
          if (chrome.runtime.lastError) {
            console.warn("[EDM Background] Download cancel notice:", chrome.runtime.lastError.message);
          }
        });
      } else {
        console.warn("[EDM Background] EDM Host handoff failed. Allowing native browser download to continue safely.");
      }
    });
  } catch (err) {
    console.error("[EDM Background] Interception error:", err);
  }
});

function sendToNativeHost(url, filename, quality) {
  const correlationId = "edm_corr_" + Date.now() + "_" + Math.random().toString(36).substring(2, 9);
  sendToNativeHostWithHandshake(correlationId, url, filename, quality, null);
}

function sendToNativeHostWithHandshake(correlationId, url, filename, quality, callback) {
  if (chrome.cookies && chrome.cookies.getAll) {
    try {
      chrome.cookies.getAll({ url: url }, (cookies) => {
        const cookieHeader = (cookies || []).map((c) => `${c.name}=${c.value}`).join("; ");
        dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, cookieHeader, callback);
      });
      return;
    } catch (e) {}
  }
  dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, "", callback);
}

function dispatchNativeMessageWithHandshake(correlationId, url, filename, quality, cookies, callback) {
  const message = {
    action: "DOWNLOAD_REQUEST",
    correlationId: correlationId,
    url: url,
    filename: filename,
    quality: quality,
    cookies: cookies,
    source: "ChromeExtension",
    timestamp: new Date().toISOString()
  };

  chrome.runtime.sendNativeMessage(NATIVE_HOST, message, (response) => {
    if (chrome.runtime.lastError) {
      console.warn("[EDM Extension] Native host warning:", chrome.runtime.lastError.message);
      if (callback) callback(false);
    } else {
      const isSuccess = response && response.success === true;
      if (callback) callback(isSuccess);
    }
  });
}

function deriveFilename(url) {
  try {
    const parsed = new URL(url);
    const pathname = parsed.pathname;
    const segments = pathname.split("/").filter(Boolean);
    if (segments.length > 0) {
      const last = segments[segments.length - 1];
      if (last.includes(".")) return decodeURIComponent(last);
    }
  } catch (e) {}
  return "EDM_Media_" + Date.now() + ".mp4";
}

// EDM Firefox WebExtension Background Script
browser.downloads.onCreated.addListener((downloadItem) => {
    browser.runtime.sendNativeMessage('com.edm.downloader', {
        action: 'intercept',
        url: downloadItem.url,
        filename: downloadItem.filename
    }).then((response) => {
        if (response && response.status === 'handed_off') {
            browser.downloads.cancel(downloadItem.id);
        }
    });
});

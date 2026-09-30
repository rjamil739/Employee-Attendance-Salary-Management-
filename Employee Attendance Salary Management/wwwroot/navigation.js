(function () {
    function stop() { document.body.classList.remove('route-loading'); }
    const bootObserver = new MutationObserver(function () {
        const boot = document.getElementById('app-boot');
        if (boot && document.querySelector('main, .emp-shell, .dashboard-shell, .settings-shell, .sm-shell')) {
            boot.remove();
            bootObserver.disconnect();
        }
    });
    bootObserver.observe(document.body, { childList: true, subtree: true });
    document.addEventListener('click', function (event) {
        const link = event.target.closest('a[href]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin || url.href === location.href || link.target === '_blank' || link.hasAttribute('download')) return;
        document.body.classList.add('route-loading');
    }, true);
    document.addEventListener('enhancedload', stop);
    window.addEventListener('pageshow', stop);
    window.addEventListener('popstate', function () { document.body.classList.add('route-loading'); });
})();

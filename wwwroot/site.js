(function () {
    function initSiteHeader() {
        var header = document.getElementById('site-header');
        var toggle = document.getElementById('site-nav-toggle');
        var nav = document.getElementById('site-nav');
        if (!header || !toggle || !nav) return;

        toggle.addEventListener('click', function () {
            var open = header.classList.toggle('site-header-open');
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            toggle.setAttribute('aria-label', open ? '메뉴 닫기' : '메뉴 열기');
        });

        nav.querySelectorAll('a').forEach(function (link) {
            link.addEventListener('click', function () {
                header.classList.remove('site-header-open');
                toggle.setAttribute('aria-expanded', 'false');
                toggle.setAttribute('aria-label', '메뉴 열기');
            });
        });

        window.addEventListener('scroll', function () {
            header.classList.toggle('site-header-scrolled', window.scrollY > 24);
        }, { passive: true });
    }

    function initGalleryTabs() {
        var tabsRoot = document.querySelector('[data-ssf-gallery-tabs]');
        var grid = document.querySelector('[data-ssf-gallery-grid]');
        if (!tabsRoot || !grid) return;

        var tabs = Array.prototype.slice.call(tabsRoot.querySelectorAll('[data-ssf-gallery-tab]'));
        var items = Array.prototype.slice.call(grid.querySelectorAll('[data-ssf-gallery-cat]'));

        function applyFilter(category) {
            tabs.forEach(function (tab) {
                tab.classList.toggle('is-active', tab.getAttribute('data-ssf-gallery-tab') === category);
            });
            items.forEach(function (item) {
                var cat = item.getAttribute('data-ssf-gallery-cat');
                var show = category === '전체' || cat === category;
                item.classList.toggle('is-hidden', !show);
            });
        }

        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                applyFilter(tab.getAttribute('data-ssf-gallery-tab') || '전체');
            });
        });

        applyFilter('전체');
    }

    function boot() {
        initSiteHeader();
        initGalleryTabs();
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', boot);
    else
        boot();
})();

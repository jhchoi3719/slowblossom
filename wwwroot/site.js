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

    function initGalleryViewer() {
        var triggers = Array.prototype.slice.call(document.querySelectorAll('[data-ssf-gallery-open]'));
        if (!triggers.length) return;

        var images = [];
        var index = 0;
        var lastTrigger = null;
        var viewer = buildViewer();

        function buildViewer() {
            var root = document.createElement('div');
            root.className = 'ssf-viewer';
            root.setAttribute('role', 'dialog');
            root.setAttribute('aria-modal', 'true');
            root.setAttribute('aria-label', '갤러리 사진 보기');
            root.hidden = true;
            root.innerHTML =
                '<div class="ssf-viewer-backdrop" data-ssf-viewer-close></div>' +
                '<div class="ssf-viewer-panel">' +
                '<button type="button" class="ssf-viewer-close" data-ssf-viewer-close aria-label="닫기"></button>' +
                '<button type="button" class="ssf-viewer-nav ssf-viewer-prev" data-ssf-viewer-prev aria-label="이전 사진"></button>' +
                '<div class="ssf-viewer-stage"><img class="ssf-viewer-image" alt="" /></div>' +
                '<button type="button" class="ssf-viewer-nav ssf-viewer-next" data-ssf-viewer-next aria-label="다음 사진"></button>' +
                '<div class="ssf-viewer-info">' +
                '<p class="ssf-viewer-caption"></p>' +
                '<p class="ssf-viewer-counter" aria-live="polite"></p>' +
                '<div class="ssf-viewer-dots" data-ssf-viewer-dots></div>' +
                '</div>' +
                '</div>';
            document.body.appendChild(root);
            return {
                root: root,
                image: root.querySelector('.ssf-viewer-image'),
                caption: root.querySelector('.ssf-viewer-caption'),
                counter: root.querySelector('.ssf-viewer-counter'),
                dots: root.querySelector('[data-ssf-viewer-dots]'),
                prev: root.querySelector('[data-ssf-viewer-prev]'),
                next: root.querySelector('[data-ssf-viewer-next]')
            };
        }

        function parseImages(trigger) {
            try {
                var parsed = JSON.parse(trigger.getAttribute('data-ssf-gallery-images') || '[]');
                return Array.isArray(parsed) ? parsed.filter(function (url) { return !!url; }) : [];
            } catch (err) {
                return [];
            }
        }

        function preload(url) {
            if (!url) return;
            var img = new Image();
            img.src = url;
        }

        // 마지막 장 다음은 첫 장으로, 첫 장 이전은 마지막 장으로 돌아옵니다.
        function show(next) {
            if (!images.length) return;
            var count = images.length;
            index = ((next % count) + count) % count;
            viewer.image.src = images[index];
            viewer.counter.textContent = count > 1 ? (index + 1) + ' / ' + count : '';
            Array.prototype.forEach.call(viewer.dots.children, function (dot, i) {
                dot.classList.toggle('is-active', i === index);
            });
            preload(images[(index + 1) % count]);
            preload(images[(index - 1 + count) % count]);
        }

        function renderDots(count) {
            viewer.dots.innerHTML = '';
            if (count < 2) return;
            for (var i = 0; i < count; i++) {
                var dot = document.createElement('button');
                dot.type = 'button';
                dot.className = 'ssf-viewer-dot';
                dot.setAttribute('aria-label', (i + 1) + '번째 사진');
                dot.addEventListener('click', (function (target) {
                    return function () { show(target); };
                })(i));
                viewer.dots.appendChild(dot);
            }
        }

        function open(trigger) {
            var list = parseImages(trigger);
            if (!list.length) return;

            images = list;
            lastTrigger = trigger;
            viewer.caption.textContent = trigger.getAttribute('data-ssf-gallery-caption') || '';
            viewer.root.classList.toggle('is-single', images.length < 2);
            renderDots(images.length);
            viewer.root.hidden = false;
            document.body.classList.add('ssf-viewer-open');
            show(0);
            viewer.next.focus();
        }

        function close() {
            viewer.root.hidden = true;
            viewer.image.removeAttribute('src');
            document.body.classList.remove('ssf-viewer-open');
            if (lastTrigger) lastTrigger.focus();
        }

        triggers.forEach(function (trigger) {
            trigger.addEventListener('click', function () { open(trigger); });
        });

        viewer.prev.addEventListener('click', function () { show(index - 1); });
        viewer.next.addEventListener('click', function () { show(index + 1); });
        viewer.root.querySelectorAll('[data-ssf-viewer-close]').forEach(function (el) {
            el.addEventListener('click', close);
        });

        document.addEventListener('keydown', function (event) {
            if (viewer.root.hidden) return;
            if (event.key === 'Escape') close();
            else if (event.key === 'ArrowRight') show(index + 1);
            else if (event.key === 'ArrowLeft') show(index - 1);
        });

        var touchStartX = null;
        viewer.root.addEventListener('touchstart', function (event) {
            touchStartX = event.touches.length === 1 ? event.touches[0].clientX : null;
        }, { passive: true });
        viewer.root.addEventListener('touchend', function (event) {
            if (touchStartX === null) return;
            var delta = event.changedTouches[0].clientX - touchStartX;
            touchStartX = null;
            if (Math.abs(delta) > 40) show(delta < 0 ? index + 1 : index - 1);
        });
    }

    function boot() {
        initSiteHeader();
        initGalleryTabs();
        initGalleryViewer();
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', boot);
    else
        boot();
})();

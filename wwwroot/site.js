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

    function initHeroSlider() {
        var root = document.querySelector('[data-site-hero-slider]');
        if (!root) return;

        var slides = Array.prototype.slice.call(root.querySelectorAll('[data-site-hero-slide]'));
        var dots = Array.prototype.slice.call(document.querySelectorAll('[data-site-hero-dot]'));
        var prevBtn = document.querySelector('[data-site-hero-prev]');
        var nextBtn = document.querySelector('[data-site-hero-next]');
        var playBtn = document.querySelector('[data-site-hero-play]');
        if (slides.length === 0) return;

        var index = 0;
        var timer = null;
        var playing = true;
        var delayMs = 6000;

        function show(nextIndex) {
            index = (nextIndex + slides.length) % slides.length;
            slides.forEach(function (slide, i) {
                slide.classList.toggle('is-active', i === index);
            });
            dots.forEach(function (dot, i) {
                dot.classList.toggle('is-active', i === index);
                dot.setAttribute('aria-selected', i === index ? 'true' : 'false');
            });
        }

        function next() { show(index + 1); }
        function prev() { show(index - 1); }

        function stopTimer() {
            if (timer) {
                clearInterval(timer);
                timer = null;
            }
        }

        function startTimer() {
            stopTimer();
            if (!playing) return;
            timer = setInterval(next, delayMs);
        }

        if (prevBtn) prevBtn.addEventListener('click', function () { prev(); startTimer(); });
        if (nextBtn) nextBtn.addEventListener('click', function () { next(); startTimer(); });

        dots.forEach(function (dot) {
            dot.addEventListener('click', function () {
                var target = parseInt(dot.getAttribute('data-site-hero-dot'), 10);
                if (!isNaN(target)) {
                    show(target);
                    startTimer();
                }
            });
        });

        if (playBtn) {
            playBtn.addEventListener('click', function () {
                playing = !playing;
                playBtn.textContent = playing ? '⏸' : '▶';
                playBtn.setAttribute('aria-label', playing ? '슬라이드 일시정지' : '슬라이드 재생');
                playBtn.setAttribute('aria-pressed', playing ? 'false' : 'true');
                if (playing) startTimer();
                else stopTimer();
            });
        }

        root.addEventListener('mouseenter', stopTimer);
        root.addEventListener('mouseleave', startTimer);

        show(0);
        startTimer();
    }

    function boot() {
        initSiteHeader();
        initHeroSlider();
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', boot);
    else
        boot();
})();

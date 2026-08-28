/**
 * EDM Theme — JavaScript Interactions
 * Premium micro-animations, scroll effects, FAQ, mobile menu, and nav behavior.
 */

(function () {
    'use strict';

    // ── Init Lucide Icons ────────────────────────────────────────
    function initLucide() {
        if (window.lucide) {
            window.lucide.createIcons();
        }
    }

    // ── Navbar Scroll Effect ─────────────────────────────────────
    function initNavScroll() {
        const nav = document.querySelector('.edm-nav');
        if (!nav) return;

        let lastY = 0;
        const handler = () => {
            const y = window.scrollY;
            if (y > 60) {
                nav.classList.add('scrolled');
            } else {
                nav.classList.remove('scrolled');
            }
            lastY = y;
        };

        window.addEventListener('scroll', handler, { passive: true });
        handler();
    }

    // ── Smooth Scroll for Anchor Links ───────────────────────────
    function initSmoothScroll() {
        document.querySelectorAll('a[href^="#"]').forEach(link => {
            link.addEventListener('click', (e) => {
                const target = document.querySelector(link.getAttribute('href'));
                if (!target) return;
                e.preventDefault();
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });

                // Update active nav
                document.querySelectorAll('.edm-nav-link').forEach(l => l.classList.remove('active'));
                link.classList.add('active');
            });
        });
    }

    // ── Intersection Observer Animations ─────────────────────────
    function initScrollAnimations() {
        const observed = new Set();

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting && !observed.has(entry.target)) {
                    observed.add(entry.target);
                    const delay = entry.target.dataset.delay || 0;
                    setTimeout(() => {
                        entry.target.classList.add('edm-visible');
                    }, parseInt(delay));
                }
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -60px 0px' });

        document.querySelectorAll('[data-animate]').forEach(el => {
            el.classList.add('edm-animate-ready');
            observer.observe(el);
        });
    }

    // ── FAQ Toggle ───────────────────────────────────────────────
    function initFAQ() {
        document.querySelectorAll('.edm-faq-question').forEach(btn => {
            btn.addEventListener('click', () => {
                const item = btn.closest('.edm-faq-item');
                const isOpen = item.classList.contains('open');

                // Close all
                document.querySelectorAll('.edm-faq-item.open').forEach(el => {
                    el.classList.remove('open');
                });

                // Open clicked (unless already open)
                if (!isOpen) {
                    item.classList.add('open');
                }
            });
        });
    }

    // ── Mobile Navigation ────────────────────────────────────────
    function initMobileNav() {
        const toggle = document.getElementById('edm-mobile-toggle');
        const mobileMenu = document.getElementById('edm-mobile-menu');
        if (!toggle || !mobileMenu) return;

        toggle.addEventListener('click', () => {
            const isOpen = mobileMenu.classList.toggle('open');
            toggle.setAttribute('aria-expanded', isOpen);
            document.body.style.overflow = isOpen ? 'hidden' : '';
        });

        // Close on outside click
        document.addEventListener('click', (e) => {
            if (!toggle.contains(e.target) && !mobileMenu.contains(e.target)) {
                mobileMenu.classList.remove('open');
                document.body.style.overflow = '';
            }
        });
    }

    // ── Counter Animation ────────────────────────────────────────
    function animateCounter(el, target, duration = 2000, suffix = '') {
        let start = 0;
        const increment = target / (duration / 16);
        const timer = setInterval(() => {
            start += increment;
            if (start >= target) {
                el.textContent = target.toLocaleString() + suffix;
                clearInterval(timer);
            } else {
                el.textContent = Math.floor(start).toLocaleString() + suffix;
            }
        }, 16);
    }

    function initCounters() {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                const el = entry.target;
                const target = parseFloat(el.dataset.count || el.textContent.replace(/[^\d.]/g, ''));
                const suffix = el.dataset.suffix || '';
                if (!isNaN(target)) {
                    animateCounter(el, target, 2000, suffix);
                }
                observer.unobserve(el);
            });
        }, { threshold: 0.5 });

        document.querySelectorAll('[data-count]').forEach(el => observer.observe(el));
    }

    // ── Copy-to-Clipboard ────────────────────────────────────────
    function initClipboard() {
        document.querySelectorAll('[data-copy]').forEach(btn => {
            btn.addEventListener('click', async () => {
                const text = btn.dataset.copy;
                try {
                    await navigator.clipboard.writeText(text);
                    const orig = btn.textContent;
                    btn.textContent = '✓ Copied!';
                    btn.style.color = 'var(--edm-success)';
                    setTimeout(() => {
                        btn.textContent = orig;
                        btn.style.color = '';
                    }, 2000);
                } catch (err) {
                    console.warn('Clipboard error:', err);
                }
            });
        });
    }

    // ── Floating Particles Background ────────────────────────────
    function initParticles() {
        const canvas = document.getElementById('edm-particles');
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        let particles = [];
        let animId;

        function resize() {
            canvas.width  = canvas.offsetWidth;
            canvas.height = canvas.offsetHeight;
        }

        function createParticle() {
            return {
                x:     Math.random() * canvas.width,
                y:     Math.random() * canvas.height,
                r:     Math.random() * 2 + 0.5,
                vx:    (Math.random() - 0.5) * 0.4,
                vy:   -(Math.random() * 0.6 + 0.2),
                alpha: Math.random() * 0.5 + 0.1,
            };
        }

        function init() {
            resize();
            particles = Array.from({ length: 60 }, createParticle);
        }

        function draw() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            particles.forEach(p => {
                p.x  += p.vx;
                p.y  += p.vy;
                p.alpha -= 0.001;

                if (p.y < 0 || p.alpha <= 0) {
                    Object.assign(p, createParticle());
                    p.y = canvas.height;
                }

                ctx.beginPath();
                ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
                ctx.fillStyle = `rgba(130, 120, 250, ${p.alpha})`;
                ctx.fill();
            });

            animId = requestAnimationFrame(draw);
        }

        init();
        draw();
        window.addEventListener('resize', () => { resize(); init(); });
    }

    // ── Active Nav Based on Scroll ────────────────────────────────
    function initActiveNavOnScroll() {
        const sections = document.querySelectorAll('section[id]');
        const navLinks = document.querySelectorAll('.edm-nav-link[href^="#"]');
        if (!sections.length || !navLinks.length) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    navLinks.forEach(l => l.classList.remove('active'));
                    const active = document.querySelector(`.edm-nav-link[href="#${entry.target.id}"]`);
                    if (active) active.classList.add('active');
                }
            });
        }, { threshold: 0.4 });

        sections.forEach(s => observer.observe(s));
    }

    // ── Toast Notification ───────────────────────────────────────
    window.EDMToast = function (message, type = 'success', duration = 3500) {
        const toast = document.createElement('div');
        toast.className = `edm-toast edm-toast-${type}`;
        toast.textContent = message;
        toast.style.cssText = `
            position:fixed; bottom:24px; right:24px; z-index:9999;
            padding:14px 22px; border-radius:12px; font-size:14px; font-weight:600;
            background:${type === 'success' ? 'rgba(16,185,129,0.15)' : type === 'error' ? 'rgba(239,68,68,0.15)' : 'rgba(88,86,214,0.15)'};
            border:1px solid ${type === 'success' ? 'rgba(16,185,129,0.3)' : type === 'error' ? 'rgba(239,68,68,0.3)' : 'rgba(88,86,214,0.3)'};
            color:${type === 'success' ? '#10b981' : type === 'error' ? '#ef4444' : '#818cf8'};
            backdrop-filter:blur(16px); box-shadow:0 8px 32px rgba(0,0,0,0.4);
            transform:translateX(120%); transition:transform 0.35s cubic-bezier(0.4,0,0.2,1);
        `;
        document.body.appendChild(toast);
        requestAnimationFrame(() => {
            toast.style.transform = 'translateX(0)';
        });
        setTimeout(() => {
            toast.style.transform = 'translateX(120%)';
            setTimeout(() => toast.remove(), 400);
        }, duration);
    };

    // ── Download Button Analytics ────────────────────────────────
    function initDownloadTracking() {
        document.querySelectorAll('[data-track="download"]').forEach(btn => {
            btn.addEventListener('click', () => {
                if (window.EDM_WP && window.EDM_WP.ajaxUrl) {
                    fetch(window.EDM_WP.ajaxUrl, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: `action=edm_track_download&nonce=${window.EDM_WP?.nonce || ''}`
                    }).catch(() => {});
                }
            });
        });
    }

    // ── Main Init ────────────────────────────────────────────────
    function init() {
        initLucide();
        initNavScroll();
        initSmoothScroll();
        initScrollAnimations();
        initFAQ();
        initMobileNav();
        initCounters();
        initClipboard();
        initParticles();
        initActiveNavOnScroll();
        initDownloadTracking();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();

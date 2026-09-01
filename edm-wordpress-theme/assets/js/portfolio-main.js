/* =========================================
   All Global Javascript Handlers
   ========================================= */

function initThemeToggle() {
    const themeToggleBtns = document.querySelectorAll('[data-theme-toggle], #theme-toggle');
    if (!themeToggleBtns.length) return;

    // Check local storage or system preference
    if (localStorage.getItem('theme') === 'dark' || (!('theme' in localStorage) && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
        document.documentElement.classList.add('dark');
    } else {
        document.documentElement.classList.remove('dark');
    }

    const applyTheme = (isDark) => {
        document.documentElement.classList.toggle('dark', isDark);
        localStorage.setItem('theme', isDark ? 'dark' : 'light');
    };

    themeToggleBtns.forEach((btn) => {
        btn.addEventListener('click', function() {
            applyTheme(!document.documentElement.classList.contains('dark'));
        });
    });
}

function initHeaderScrollEffects() {
    const header = document.getElementById('main-header');
    if (!header) return;

    let lastScrollTop = 0;

    const updateHeader = () => {
        const scrollTop = window.scrollY || document.documentElement.scrollTop;
        const isScrolled = scrollTop > 8;

        header.classList.toggle('scrolled', isScrolled);
        header.classList.toggle('shadow-2xl', isScrolled);
        header.classList.toggle('border-white/10', isScrolled);

        if (scrollTop > lastScrollTop && scrollTop > 120) {
            header.classList.add('header-hidden');
        } else {
            header.classList.remove('header-hidden');
        }

        lastScrollTop = scrollTop <= 0 ? 0 : scrollTop;
    };

    updateHeader();
    window.addEventListener('scroll', updateHeader, { passive: true });
}

function aiChatbot() {
    return {
        isOpen: false,
        userInput: '',
        isTyping: false,
        socket: null,
        sessionId: null,
        apiBase: '',
        messages: [
            { sender: 'bot', text: 'Hi there! 👋 I am Al Amin\'s AI assistant. I can help with services, portfolio questions, pricing, and next steps.' }
        ],
        init() {
            this.apiBase = window.location.hostname === 'localhost'
                ? 'http://localhost:3000'
                : window.location.origin;
            this.sessionId = `visitor-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;

            if (typeof io !== 'undefined') {
                this.socket = io(this.apiBase);
                this.socket.on('bot_reply', (replyText) => {
                    this.isTyping = false;
                    this.messages.push({ sender: 'bot', text: replyText });
                    this.scrollToBottom();
                });
            }
        },
        toggle() {
            this.isOpen = !this.isOpen;
            if (this.isOpen) this.scrollToBottom();
        },
        async sendMessage() {
            if (!this.userInput.trim()) return;
            const msg = this.userInput.trim();
            this.messages.push({ sender: 'user', text: msg });
            this.userInput = '';
            this.isTyping = true;
            this.scrollToBottom();

            try {
                const response = await fetch(`${this.apiBase}/api/chatbot`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        message: msg,
                        sessionId: this.sessionId
                    })
                });

                const data = await response.json();
                this.isTyping = false;
                if (data && data.reply) {
                    this.messages.push({ sender: 'bot', text: data.reply });
                } else {
                    this.messages.push({ sender: 'bot', text: 'Sorry, I could not process that right now.' });
                }
                this.scrollToBottom();
            } catch (error) {
                this.isTyping = false;
                // Intelligent Offline Knowledge Base Fallback
                const lower = msg.toLowerCase();
                let reply = "I'm here to assist you! Feel free to ask about EDM (Exclusive Download Manager), SEO Strategy, Software Architecture, or book a strategy session via the Contact page.";
                if (lower.includes('edm') || lower.includes('download')) {
                    reply = "Exclusive Download Manager (EDM) is our flagship 32-socket download accelerator featuring 4K stream sniffing and browser extensions. Visit the <a href='/edm/' class='text-cyan underline font-bold'>EDM Product Hub</a> to download the Windows installer!";
                } else if (lower.includes('price') || lower.includes('cost') || lower.includes('hire') || lower.includes('contact')) {
                    reply = "Alamin offers custom enterprise engineering and growth packages. You can book a free strategy session on the <a href='/contact/' class='text-cyan underline font-bold'>Contact Page</a> or reach out on WhatsApp at +880 1888567189.";
                } else if (lower.includes('service') || lower.includes('skill')) {
                    reply = "Core expertise includes: .NET 10/WPF High-Performance Systems, Next-Gen WordPress Themes, Technical SEO & Growth Engineering, and Full-Stack Cloud Architecture.";
                }
                this.messages.push({ sender: 'bot', text: reply });
                this.scrollToBottom();
            }
        },
        scrollToBottom() {
            setTimeout(() => {
                const container = this.$refs.chatBody;
                if (container) container.scrollTop = container.scrollHeight;
            }, 50);
        }
    }
}

function testimonialSlider(data = []) {
    return {
        currentIndex: 0,
        interval: null,
        visibleCards: 1,
        startX: 0,
        isDragging: false,
        dragOffset: 0,
        testimonials: data.length > 0 ? data : [
            { name: 'David Kovic', role: 'Business Owner', review: 'Highly recommended.', rating: 5.0, img: 'https://ui-avatars.com/api/?name=David+Kovic&background=10B981&color=fff' },
        ],
        init() {
            this.updateVisibleCards();
            window.addEventListener('resize', () => { this.updateVisibleCards() });
            if (this.testimonials.length > this.visibleCards) this.start();
        },
        updateVisibleCards() {
            if (window.innerWidth >= 1024) this.visibleCards = 3;
            else if (window.innerWidth >= 768) this.visibleCards = 2;
            else this.visibleCards = 1;
            if (this.currentIndex > this.testimonials.length - this.visibleCards) {
                this.currentIndex = Math.max(0, this.testimonials.length - this.visibleCards);
            }
        },
        start() { this.interval = setInterval(() => { this.next() }, 3000); },
        pause() { clearInterval(this.interval); },
        resume() { if (this.testimonials.length > this.visibleCards) this.start(); },
        next() { if (this.currentIndex < this.testimonials.length - this.visibleCards) this.currentIndex++; else this.currentIndex = 0; },
        prev() { if (this.currentIndex > 0) this.currentIndex--; else this.currentIndex = this.testimonials.length - this.visibleCards; },
        goTo(index) { this.currentIndex = index; },
        touchStart(e) {
            this.isDragging = true;
            this.pause();
            this.startX = e.touches ? e.touches[0].clientX : e.clientX;
        },
        touchMove(e) {
            if (!this.isDragging) return;
            const currentX = e.touches ? e.touches[0].clientX : e.clientX;
            this.dragOffset = currentX - this.startX;
        },
        touchEnd(e) {
            if (!this.isDragging) return;
            this.isDragging = false;
            if (this.dragOffset < -50) this.next();
            else if (this.dragOffset > 50) this.prev();
            this.dragOffset = 0;
            this.resume();
        }
    }
}

window.addEventListener('load', () => {
    const progressBar = document.getElementById('loader-progress');
    if(progressBar) progressBar.style.width = '100%';
    setTimeout(() => {
        const loader = document.getElementById('loader');
        if(loader) {
            loader.style.opacity = '0';
            setTimeout(() => { loader.style.display = 'none'; }, 500);
        }
    }, 1000);
});

function animateCounter(el) {
    const target = parseInt(el.dataset.target);
    let start = 0;
    const duration = 1800;
    const step = (timestamp) => {
        if (!start) start = timestamp;
        const progress = Math.min((timestamp - start) / duration, 1);
        const ease = 1 - Math.pow(1 - progress, 3);
        el.textContent = Math.floor(ease * target) + (target >= 10 && progress === 1 ? '+' : '');
        if (progress < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
}

const phrases = ["SEO Campaigns", "Google Ads Audit", "Meta Funnel Setup", "Lead Generation Systems", "Virtual Assistance Support"];
let phraseIndex = 0;
let characterIndex = 0;
let isDeleting = false;

function type() {
    const typingTextElement = document.getElementById("typing-text");
    if (!typingTextElement) return;
    const currentPhrase = phrases[phraseIndex];

    if (isDeleting) {
        typingTextElement.textContent = currentPhrase.substring(0, characterIndex - 1);
        characterIndex--;
    } else {
        typingTextElement.textContent = currentPhrase.substring(0, characterIndex + 1);
        characterIndex++;
    }

    let typeSpeed = isDeleting ? 40 : 100;
    if (!isDeleting && characterIndex === currentPhrase.length) {
        typeSpeed = 2000; isDeleting = true;
    } else if (isDeleting && characterIndex === 0) {
        isDeleting = false; phraseIndex = (phraseIndex + 1) % phrases.length; typeSpeed = 400;
    }
    setTimeout(type, typeSpeed);
}

document.addEventListener("DOMContentLoaded", () => {
    initThemeToggle(); // Initialize dark/light mode toggle
    initHeaderScrollEffects();

    setTimeout(type, 1500); 

    const revealEls = document.querySelectorAll('.reveal');
    const skillFills = document.querySelectorAll('.skill-fill');
    const statNums = document.querySelectorAll('.stat-num[data-target]');
    let countersTriggered = false;

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) {
                e.target.classList.add('visible');
                observer.unobserve(e.target);
            }
        });
    }, { threshold: 0.1 });

    const skillObserver = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) {
                e.target.style.width = e.target.dataset.pct + '%';
                skillObserver.unobserve(e.target);
            }
        });
    }, { threshold: 0.3 });

    const counterObserver = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting && !countersTriggered) {
                countersTriggered = true; statNums.forEach(el => animateCounter(el));
            }
        });
    }, { threshold: 0.5 });

    revealEls.forEach(el => observer.observe(el));
    skillFills.forEach(el => skillObserver.observe(el));
    if (statNums.length) counterObserver.observe(statNums[0]);
});
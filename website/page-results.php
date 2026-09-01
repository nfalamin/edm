<?php
/**
 * Template Name: Results, Testimonials & FAQ Page
 * Description: Achievement counters, full multi-card client reviews slider, and interactive FAQ accordion.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header('portfolio');
?>

<main>
    <!-- Page Header Banner -->
    <section class="pt-16 pb-12 px-4 sm:px-6 border-b border-white/5 relative overflow-hidden" style="background: linear-gradient(175deg, #020617 0%, #0a1628 60%, #050f1f 100%);">
        <div class="max-w-7xl mx-auto">
            <div class="flex items-center gap-2 text-xs text-slate-500 mb-4">
                <a href="<?php echo esc_url(home_url('/')); ?>" class="hover:text-cyan transition-colors">Home</a>
                <i class="fa-solid fa-chevron-right text-[8px]"></i>
                <span class="text-slate-400">Results & Testimonials</span>
            </div>
            <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Proven Impact</span>
            <h1 class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display mt-2 mb-3">
                Client Results & Verified Metrics
            </h1>
            <p class="text-slate-300 max-w-2xl text-sm sm:text-base leading-relaxed">
                Measurable growth, client reviews, and answers to common partnership questions.
            </p>
        </div>
    </section>

    <!-- 1. Achievement Counters -->
    <section class="py-16 md:py-20 px-4 sm:px-6" style="background: linear-gradient(135deg, rgba(37,99,235,0.08) 0%, rgba(6,182,212,0.04) 50%, rgba(37,99,235,0.08) 100%);">
        <div class="max-w-7xl mx-auto">
            <div class="text-center mb-12 reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">By The Numbers</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display mt-2">Real Performance. Proven Impact.</h2>
            </div>
            <div class="grid grid-cols-2 md:grid-cols-4 gap-6">
                <?php
                $stat_counters = [
                    ['value' => 100, 'suffix' => '+', 'label' => 'Projects Delivered', 'icon' => 'fa-solid fa-briefcase', 'color' => 'text-blue-400'],
                    ['value' => 50, 'suffix' => '+', 'label' => 'Businesses Scaled', 'icon' => 'fa-solid fa-chart-line', 'color' => 'text-cyan'],
                    ['value' => 5, 'suffix' => 'x', 'label' => 'Average ROAS', 'icon' => 'fa-solid fa-bullseye', 'color' => 'text-gold'],
                    ['value' => 5, 'suffix' => ' Stars', 'label' => 'Client Rating', 'icon' => 'fa-solid fa-star', 'color' => 'text-amber-400'],
                ];
                foreach ($stat_counters as $stat) : ?>
                <div class="glass-panel p-6 sm:p-8 rounded-2xl border border-white/10 flex flex-col items-center text-center reveal hover:border-cyan/30 transition-all duration-300">
                    <span class="text-2xl sm:text-3xl <?php echo $stat['color']; ?> mb-3"><i class="<?php echo $stat['icon']; ?>"></i></span>
                    <span class="text-3xl sm:text-4xl md:text-5xl font-extrabold text-white font-display counter-up" data-target="<?php echo $stat['value']; ?>" data-suffix="<?php echo esc_attr($stat['suffix']); ?>">0</span>
                    <span class="text-xs uppercase tracking-wider text-slate-400 mt-2 font-semibold"><?php echo $stat['label']; ?></span>
                </div>
                <?php endforeach; ?>
            </div>
        </div>
    </section>

    <!-- 2. Full Testimonials Slider (with auto-swipe & drag) -->
    <?php
    $testimonials_data = [];
    $testimonials_query = new WP_Query([
        'post_type'      => 'testimonial',
        'posts_per_page' => 10,
        'orderby'        => 'date',
        'order'          => 'DESC'
    ]);

    if ($testimonials_query->have_posts()) {
        while ($testimonials_query->have_posts()) {
            $testimonials_query->the_post();
            $img = has_post_thumbnail() 
                ? get_the_post_thumbnail_url(get_the_ID(), 'thumbnail') 
                : 'https://ui-avatars.com/api/?name=' . urlencode(get_the_title()) . '&background=0D8ABC&color=fff';
            $testimonials_data[] = [
                'name'   => get_the_title(),
                'role'   => get_post_meta(get_the_ID(), 'client_role', true) ?: 'Verified Client',
                'review' => wp_strip_all_tags(strip_shortcodes(get_the_content())),
                'rating' => (float) (get_post_meta(get_the_ID(), 'rating', true) ?: 5.0),
                'img'    => $img,
            ];
        }
        wp_reset_postdata();
    } else {
        $testimonials_data = [
            ['name' => 'Sarah Jenkins', 'role' => 'Marketing Director', 'review' => 'Al Amin completely transformed our organic growth. His technical SEO audit led to a 150% increase in traffic within 3 months.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Sarah+Jenkins&background=0D8ABC&color=fff'],
            ['name' => 'David Kovic', 'role' => 'Business Owner', 'review' => 'Highly recommended. He is very transparent with the Google Ads budget and scaling strategy. Immediate ROI improvement.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=David+Kovic&background=0D8ABC&color=fff'],
            ['name' => 'Elena Rodriguez', 'role' => 'E-commerce Manager', 'review' => 'We were struggling with ROAS before working with Al Amin. Achieved a 5X return within 2 months. Exceptional!', 'rating' => 4.9, 'img' => 'https://ui-avatars.com/api/?name=Elena+Rodriguez&background=0D8ABC&color=fff'],
            ['name' => 'Mark Stevenson', 'role' => 'CEO', 'review' => 'A true professional. He understands the balance between code and marketing. Lead generation has never been better.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Mark+Stevenson&background=0D8ABC&color=fff'],
            ['name' => 'Jessica Chen', 'role' => 'Startup Founder', 'review' => 'His Facebook Ads strategy was spot on. We reached our target audience much faster than anticipated.', 'rating' => 4.8, 'img' => 'https://ui-avatars.com/api/?name=Jessica+Chen&background=0D8ABC&color=fff'],
            ['name' => 'Tom Harrison', 'role' => 'Local Service Provider', 'review' => 'Our local map pack ranking skyrocketed. We get so many more calls now just from Google Maps.', 'rating' => 5.0, 'img' => 'https://ui-avatars.com/api/?name=Tom+Harrison&background=0D8ABC&color=fff'],
        ];
    }
    ?>
    <section class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5" x-data="testimonialSlider(<?php echo esc_attr(wp_json_encode($testimonials_data)); ?>)" x-init="init()">
        <div class="max-w-7xl mx-auto flex flex-col space-y-10 overflow-hidden">
            <div class="text-center reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Client Reviews</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display mt-2">Client Success Stories</h2>
            </div>

            <!-- Slider Container -->
            <div class="relative w-full reveal" @mouseenter="pause()" @mouseleave="resume()" @touchstart.passive="touchStart($event)" @touchmove.passive="touchMove($event)" @touchend.passive="touchEnd($event)" @mousedown="touchStart($event)" @mousemove="touchMove($event)" @mouseup="touchEnd($event)" @mouseleave="touchEnd($event)">
                <div class="flex transition-transform duration-500 ease-out" :style="'transform: translateX(calc(-' + (currentIndex * (100 / visibleCards)) + '% + ' + dragOffset + 'px))'">
                    <template x-for="(t, idx) in testimonials" :key="idx">
                        <div class="shrink-0 px-3 transition-all duration-500" :style="`width: ${100 / visibleCards}%`">
                            <div class="glass-panel p-6 sm:p-8 rounded-2xl border border-white/10 flex flex-col justify-between space-y-5 h-full hover:-translate-y-1 transition-transform cursor-grab active:cursor-grabbing">
                                <div class="flex items-start justify-between">
                                    <div class="flex items-center gap-1 text-amber-400 text-xs">
                                        <template x-for="i in 5">
                                            <i class="fa-solid fa-star"></i>
                                        </template>
                                    </div>
                                    <span class="text-3xl sm:text-4xl text-white/10"><i class="fa-solid fa-quote-right"></i></span>
                                </div>
                                <p class="text-xs sm:text-sm text-slate-300 leading-relaxed" x-html="'&ldquo;' + t.review + '&rdquo;'"></p>
                                <div class="flex items-center gap-3 pt-4 border-t border-white/5">
                                    <img :src="t.img" class="w-10 h-10 rounded-full object-cover shadow" alt="User">
                                    <div>
                                        <p class="text-xs font-bold text-white font-display" x-text="t.name"></p>
                                        <p class="text-[10px] uppercase tracking-wider text-slate-400" x-text="t.role"></p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </template>
                </div>
            </div>

            <!-- Slider Controls -->
            <div class="flex items-center justify-between mt-4">
                <div class="flex gap-2">
                    <template x-for="(_, i) in Math.max(1, testimonials.length - visibleCards + 1)" :key="i">
                        <button @click="goTo(i)" :class="currentIndex === i ? 'bg-cyan w-8' : 'bg-white/10 w-2 hover:bg-white/20'" class="h-2 rounded-full transition-all duration-300"></button>
                    </template>
                </div>
                <div class="flex gap-3">
                    <button @click="prev()" class="w-10 h-10 rounded-full border border-white/10 flex items-center justify-center text-white hover:bg-white/5 transition-colors shadow-sm"><i class="fa-solid fa-chevron-left text-xs"></i></button>
                    <button @click="next()" class="w-10 h-10 rounded-full border border-white/10 flex items-center justify-center text-white hover:bg-white/5 transition-colors shadow-sm"><i class="fa-solid fa-chevron-right text-xs"></i></button>
                </div>
            </div>
        </div>
    </section>

    <!-- 3. Interactive FAQ Accordion -->
    <section id="faq" class="py-16 md:py-24 px-4 sm:px-6 border-t border-white/5">
        <div class="max-w-4xl mx-auto flex flex-col space-y-10">
            <div class="text-center reveal">
                <span class="text-xs tracking-widest text-gold font-bold uppercase font-display">Common Questions</span>
                <h2 class="text-2xl sm:text-3xl md:text-4xl font-extrabold text-white font-display mt-2">Frequently Asked Questions</h2>
            </div>

            <div class="flex flex-col space-y-3 reveal">
                <?php
                $faqs = [
                    ['q' => 'What types of businesses do you work with?', 'a' => 'I work with startups, SMEs, e-commerce brands, SaaS companies, local service providers, and enterprise-level businesses across the USA, UK, UAE, and APAC regions. My strategies are tailored to your industry, scale, and growth stage.'],
                    ['q' => 'How long does it take to see results from SEO?', 'a' => 'Organic SEO typically shows measurable improvements in 3â€“6 months. For local SEO, visibility gains can appear within 4â€“8 weeks. Google Ads campaigns deliver results immediately from day one of campaign launch.'],
                    ['q' => 'Do you offer one-time projects or ongoing retainers?', 'a' => 'Both. You can engage me for a one-time technical SEO audit, a campaign setup, or an ongoing monthly management retainer. All pricing structures are listed in the Services section above.'],
                    ['q' => 'What is included in a free strategy session?', 'a' => 'During a 30-minute strategy call, I review your current digital presence, identify key bottlenecks, and outline an initial action plan. There is no sales pressure â€” just clear, actionable insights for your business.'],
                    ['q' => 'Can you handle both SEO and Paid Ads simultaneously?', 'a' => 'Yes. My Professional Growth plan integrates both SEO and Google PPC under one strategy, ensuring your organic and paid efforts complement each other rather than compete for the same keywords.'],
                    ['q' => 'How do you measure and report campaign performance?', 'a' => 'I use Google Search Console, GA4, and Ahrefs for organic tracking, and the native Google Ads / Meta Ads dashboards for paid campaigns. You receive bi-weekly or monthly reports with clear KPIs.'],
                ];
                foreach ($faqs as $faq) : ?>
                <div class="faq-item glass-panel rounded-xl border border-white/10 overflow-hidden hover:border-cyan/30 transition-all duration-300">
                    <button class="faq-trigger w-full flex items-center justify-between gap-4 p-5 sm:p-6 text-left" onclick="toggleFaq(this)">
                        <span class="text-xs sm:text-sm font-bold text-white pr-4"><?php echo esc_html($faq['q']); ?></span>
                        <span class="faq-icon flex-shrink-0 w-6 h-6 rounded-full border border-white/20 flex items-center justify-center text-slate-400 transition-transform duration-300">
                            <i class="fa-solid fa-chevron-down text-[10px]"></i>
                        </span>
                    </button>
                    <div class="faq-answer max-h-0 overflow-hidden transition-all duration-300">
                        <p class="px-5 sm:px-6 pb-5 sm:pb-6 text-xs sm:text-sm text-slate-400 leading-relaxed border-t border-white/5 pt-4"><?php echo esc_html($faq['a']); ?></p>
                    </div>
                </div>
                <?php endforeach; ?>
            </div>
        </div>
    </section>

    <!-- FAQ & Counter JS Script -->
    <script>
    function toggleFaq(btn) {
        const item = btn.closest('.faq-item');
        const answer = item.querySelector('.faq-answer');
        const icon = item.querySelector('.faq-icon');
        const isOpen = answer.style.maxHeight && answer.style.maxHeight !== '0px';

        document.querySelectorAll('.faq-item').forEach(function(fi) {
            fi.querySelector('.faq-answer').style.maxHeight = '0px';
            fi.querySelector('.faq-icon').style.transform = '';
            fi.querySelector('.faq-trigger').classList.remove('text-cyan');
        });

        if (!isOpen) {
            answer.style.maxHeight = answer.scrollHeight + 'px';
            icon.style.transform = 'rotate(180deg)';
            btn.classList.add('text-cyan');
        }
    }

    (function() {
        var counters = document.querySelectorAll('.counter-up');
        var observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting && !entry.target.dataset.animated) {
                    entry.target.dataset.animated = '1';
                    var target = parseInt(entry.target.dataset.target, 10);
                    var suffix = entry.target.dataset.suffix || '';
                    var start = 0;
                    var duration = 1200;
                    var startTime = null;
                    function step(ts) {
                        if (!startTime) startTime = ts;
                        var progress = Math.min((ts - startTime) / duration, 1);
                        var ease = 1 - Math.pow(1 - progress, 3);
                        entry.target.textContent = Math.round(start + (target - start) * ease) + suffix;
                        if (progress < 1) requestAnimationFrame(step);
                    }
                    requestAnimationFrame(step);
                }
            });
        }, { threshold: 0.3 });
        counters.forEach(function(c) { observer.observe(c); });
    })();
    </script>
</main>

<?php get_footer('portfolio'); ?>
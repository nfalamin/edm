<?php
if ( ! defined( 'ABSPATH' ) ) exit; // Exit if accessed directly

class Elementor_Testimonial_Carousel_Widget extends \Elementor\Widget_Base {

    public function get_name() {
        return 'premium_testimonial_carousel';
    }

    public function get_title() {
        return __( 'Premium Testimonial Carousel', 'portfolio' );
    }

    public function get_icon() {
        return 'eicon-testimonial-carousel';
    }

    public function get_categories() {
        return [ 'general' ];
    }

    protected function register_controls() {
        // Section for Repeater (Review Cards)
        $this->start_controls_section(
            'content_section',
            [
                'label' => __( 'Testimonials List', 'portfolio' ),
                'tab' => \Elementor\Controls_Manager::TAB_CONTENT,
            ]
        );

        $repeater = new \Elementor\Repeater();

        $repeater->add_control( 'client_name', [
            'label' => __( 'Client Name', 'portfolio' ),
            'type' => \Elementor\Controls_Manager::TEXT,
            'default' => __( 'John Doe', 'portfolio' ),
        ] );

        $repeater->add_control( 'client_role', [
            'label' => __( 'Position / Role', 'portfolio' ),
            'type' => \Elementor\Controls_Manager::TEXT,
        ] );

        $repeater->add_control( 'client_review', [
            'label' => __( 'Review Text', 'portfolio' ),
            'type' => \Elementor\Controls_Manager::TEXTAREA,
        ] );
        
        $repeater->add_control( 'client_rating', [
            'label' => __( 'Rating', 'portfolio' ),
            'type' => \Elementor\Controls_Manager::NUMBER,
            'default' => 10.0,
            'max' => 10.0,
            'step' => 0.1,
        ] );

        $this->add_control( 'testimonials', [
            'label' => __( 'Review Cards', 'portfolio' ),
            'type' => \Elementor\Controls_Manager::REPEATER,
            'fields' => $repeater->get_controls(),
            'default' => [
                [ 'client_name' => 'Markus Sterling', 'client_role' => 'Director of Growth', 'client_rating' => 10.0 ],
                [ 'client_name' => 'Angela Hayes', 'client_role' => 'Head of Operations', 'client_rating' => 9.8 ],
            ],
        ] );

        $this->end_controls_section();
    }

    protected function render() {
        $settings = $this->get_settings_for_display();
        
        // Alpine.js Auto-swiping wrapper
        echo '<div class="relative w-full reveal" x-data="{ init() { let box = this.$refs.slider; setInterval(() => { if(box.scrollLeft + box.clientWidth >= box.scrollWidth) { box.scrollLeft = 0; } else { box.scrollLeft += box.clientWidth; } }, 4000); } }">';
        echo '<div x-ref="slider" class="flex overflow-x-auto snap-x snap-mandatory gap-6 pb-12 pt-4 px-6 md:px-12 scrollbar-hide scroll-smooth w-full">';

        foreach ( $settings['testimonials'] as $item ) {
            // The precise UI code we established in Part 1 maps perfectly here
            echo '<div class="snap-center shrink-0 w-[85vw] sm:w-[350px] md:w-[420px] glass-panel p-6 md:p-8 rounded-3xl flex flex-col space-y-6 hover:-translate-y-1 transition-transform duration-300">';
            echo '<div class="flex items-start justify-between">';
            echo '<div class="flex flex-col">';
            echo '<span class="text-3xl sm:text-4xl md:text-5xl font-black text-transparent bg-clip-text bg-gradient-to-r from-gold to-yellow-400 font-display">' . number_format( (float) $item['client_rating'], 1 ) . '</span>';
            echo '<div class="flex items-center space-x-1 text-gold text-xs mt-2">';
            for ($i = 1; $i <= 10; $i++) {
                if ($i <= floor($item['client_rating'])) {
                    echo '<i class="fa-solid fa-star text-gold"></i>';
                } elseif ($i - $item['client_rating'] == 0.5) {
                    echo '<i class="fa-solid fa-star-half-stroke text-gold"></i>';
                } else {
                    echo '<i class="fa-solid fa-star text-slate-300 dark:text-slate-600"></i>';
                }
            }
            echo '</div></div>';
            echo '<span class="text-4xl md:text-5xl text-slate-200 dark:text-white/5"><i class="fa-solid fa-quote-right"></i></span>';
            echo '</div>';
            echo '<p class="text-sm md:text-base text-slate-600 dark:text-slate-300 leading-relaxed font-medium">"' . esc_html( $item['client_review'] ) . '"</p>';
            echo '<div class="flex items-center space-x-4 pt-4 border-t border-slate-100 dark:border-white/5">';
            echo '<div class="flex flex-col">';
            echo '<span class="text-sm font-bold text-slate-900 dark:text-white font-display">' . esc_html( $item['client_name'] ) . '</span>';
            echo '<span class="text-[10px] uppercase tracking-wider text-slate-500 dark:text-slate-400">' . esc_html( $item['client_role'] ) . '</span>';
            echo '</div></div></div>';
        }
        
        echo '</div></div>';
    }
}
?>
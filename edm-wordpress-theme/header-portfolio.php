<?php
/**
 * Portfolio Header Template for Front Page
 *
 * @package EDM_Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}
?><!DOCTYPE html>
<html <?php language_attributes(); ?> class="scroll-smooth" x-data="{ darkMode: localStorage.getItem('theme') !== 'light', mobileMenuOpen: false }" x-init="$watch('darkMode', val => localStorage.setItem('theme', val ? 'dark' : 'light'))" :class="{ 'dark': darkMode }">
<head>
    <meta charset="<?php bloginfo('charset'); ?>">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta name="description" content="Portfolio of N F Alamin Hossain - Senior Software Architect, Systems Engineer & Creator of Exclusive Download Manager (EDM).">
    <meta name="keywords" content="Alamin Hossain, Software Architect, Systems Developer, C#, .NET 10, WPF, SEO Specialist, Growth Strategist">
    
    <!-- Tailwind CSS Engine & Fallbacks -->
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    colors: {
                        cyan: { DEFAULT: '#06b6d4', light: '#22d3ee', dark: '#0891b2' },
                        gold: { DEFAULT: '#f59e0b', light: '#fbbf24', dark: '#d97706' },
                        navy: { 950: '#020408', 900: '#040d1a', 800: '#08142d', 700: '#111e35' }
                    }
                }
            }
        }
    </script>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css">
    <script defer src="https://cdn.jsdelivr.net/npm/alpinejs@3.x.x/dist/cdn.min.js"></script>

    <?php wp_head(); ?>
</head>
<body <?php body_class("bg-navy-950 text-slate-200 antialiased selection:bg-blue-600 selection:text-white overflow-x-hidden font-sans transition-colors duration-300"); ?>>
    <?php wp_body_open(); ?>

    <!-- INSTANT FADE LOADING SCREEN -->
    <div id="loader" class="fixed inset-0 z-[9999] bg-navy-950 flex flex-col items-center justify-center gap-5 transition-opacity duration-500" style="position: fixed; inset: 0; z-index: 9999; background-color: #020408; display: flex; flex-direction: column; align-items: center; justify-content: center;">
        <div class="font-display font-extrabold text-4xl bg-gradient-to-r from-blue-500 to-cyan-400 bg-clip-text text-transparent" style="font-size: 2.25rem; font-weight: 800; color: #06b6d4;">AH</div>
        <div class="w-48 h-[2px] bg-navy-800 rounded-full overflow-hidden" style="width: 12rem; height: 2px; background-color: #08142d; border-radius: 9999px; overflow: hidden;">
            <div id="loader-progress" class="h-full bg-gradient-to-r from-blue-500 to-cyan-400 w-full" style="height: 100%; width: 100%; background: linear-gradient(90deg, #2563eb, #06b6d4);"></div>
        </div>
    </div>
    <script>
        // Failsafe reveal after DOM is ready
        (function(){
            function dismissLoader() {
                var l = document.getElementById('loader');
                if (l) {
                    l.style.opacity = '0';
                    l.style.transition = 'opacity 0.4s ease';
                    setTimeout(function(){ l.style.display = 'none'; }, 400);
                }
            }
            if (document.readyState === 'complete' || document.readyState === 'interactive') {
                setTimeout(dismissLoader, 300);
            } else {
                window.addEventListener('DOMContentLoaded', function(){ setTimeout(dismissLoader, 300); });
                window.addEventListener('load', function(){ setTimeout(dismissLoader, 100); });
            }
            setTimeout(dismissLoader, 1500); // hard timeout safety
        })();
    </script>

    <?php get_template_part('header-nav'); ?>

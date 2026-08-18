<!DOCTYPE html>
<html <?php language_attributes(); ?> class="scroll-smooth" x-data="{ darkMode: localStorage.getItem('theme') !== 'light', mobileMenuOpen: false }" x-init="$watch('darkMode', val => localStorage.setItem('theme', val ? 'dark' : 'light'))" :class="{ 'dark': darkMode }">
<head>
    <meta charset="<?php bloginfo( 'charset' ); ?>">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta name="description" content="Portfolio of Alamin Hossain - Certified Digital Marketing Expert specializing in SEO, Google Ads, and Social Media Marketing.">
    <meta name="keywords" content="SEO Specialist, Google Ads Expert, Social Media Marketing, Freelance Digital Marketer, Lead Generation">
	

    <!-- Tailwind CSS CDN -->
    <script src="https://cdn.tailwindcss.com"></script>
    
    <!-- Font Awesome Icons -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    
    <!-- Alpine.js -->
    <script defer src="https://cdn.jsdelivr.net/npm/alpinejs@3.x.x/dist/cdn.min.js"></script>

    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    fontFamily: {
                        sans: ['Inter', 'sans-serif'],
                        display: ['Space Grotesk', 'sans-serif'],
                    },
                    colors: {
                        navy: {
                            950: '#020408',
                            900: '#040d1a',
                            800: '#08142d',
                            700: '#111e35',
                            600: '#1e293b',
                        },
                        gold: {
                            DEFAULT: '#f59e0b',
                            light: '#fbbf24',
                            dark: '#d97706'
                        },
                        cyan: {
                            DEFAULT: '#06b6d4',
                            light: '#22d3ee',
                            dark: '#0891b2'
                        }
                    }
                }
            }
        }
    </script>

        <?php wp_head(); ?>
</head>
<body <?php body_class("bg-navy-950 bg-[radial-gradient(ellipse_80%_80%_at_50%_-20%,rgba(37,99,235,0.15),transparent)] text-slate-200 antialiased selection:bg-blue-600 selection:text-white overflow-x-hidden font-sans transition-colors duration-300"); ?>>
    <?php wp_body_open(); ?>

    <!-- LOADING SCREEN -->
    <div id="loader" class="fixed inset-0 z-[9999] bg-navy-950 flex flex-col items-center justify-center gap-5 transition-opacity duration-500">
        <div class="font-display font-extrabold text-4xl bg-gradient-to-r from-blue-500 to-cyan-400 bg-clip-text text-transparent">AH</div>
        <div class="w-48 h-[2px] bg-navy-800 rounded-full overflow-hidden">
            <div id="loader-progress" class="h-full bg-gradient-to-r from-blue-500 to-cyan-400 w-0 transition-all duration-1000 ease-out"></div>
        </div>
    </div>

    
    <?php get_template_part( 'header-nav' ); ?>
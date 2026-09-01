<?php
/**
 * EDM Dedicated Header Template
 * Comprehensive SEO Architecture, JSON-LD Schemas, OpenGraph, and Twitter Cards.
 *
 * @package Portfolio_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$download_url = function_exists('edm_get_download_url') ? edm_get_download_url() : esc_url(home_url('/downloads/EDM-Setup-v2.1.0.exe'));
$version = function_exists('edm_get_latest_version') ? edm_get_latest_version() : '2.1.0';
$page_url = esc_url(home_url('/edm/'));
$site_name = get_bloginfo('name');
?><!DOCTYPE html>
<html <?php language_attributes(); ?> class="dark">
<head>
    <meta charset="<?php bloginfo('charset'); ?>">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <link rel="profile" href="https://gmpg.org/xfn/11">
    <link rel="canonical" href="<?php echo esc_url($page_url); ?>">
    
    <!-- Primary SEO Meta Information -->
    <title>Exclusive Download Manager (EDM) - 32-Socket Turbo Accelerator for Windows 10 & 11</title>
    <meta name="description" content="Exclusive Download Manager (EDM) is the world's premier Windows download accelerator featuring 32 concurrent sockets, 4K/8K stream sniffing, Manifest V3 browser integration, and SQLite crash-proof resume.">
    <meta name="keywords" content="exclusive download manager, edm setup, 32 socket download accelerator, high-speed download manager, 4k video ripper, manifest v3 chrome extension, download manager windows 11, fastest download software">
    <meta name="author" content="Alamin Hossain">
    <meta name="robots" content="index, follow, max-snippet:-1, max-image-preview:large, max-video-preview:-1">

    <!-- Open Graph (Facebook, LinkedIn, Discord) -->
    <meta property="og:type" content="product">
    <meta property="og:site_name" content="<?php echo esc_attr($site_name); ?>">
    <meta property="og:title" content="Exclusive Download Manager (EDM) - 32-Socket Turbo Accelerator">
    <meta property="og:description" content="Accelerate downloads up to 32x with dynamic byte-range splitting, 4K video capture, and zero subscription rental traps.">
    <meta property="og:url" content="<?php echo esc_url($page_url); ?>">
    <meta property="og:image" content="<?php echo esc_url(get_template_directory_uri() . '/screenshot.png'); ?>">
    <meta property="og:image:width" content="1200">
    <meta property="og:image:height" content="630">
    <meta property="og:locale" content="en_US">

    <!-- Twitter Card Meta -->
    <meta name="twitter:card" content="summary_large_image">
    <meta name="twitter:title" content="Exclusive Download Manager (EDM) - 32-Socket Turbo Accelerator">
    <meta name="twitter:description" content="Download files 32x faster on Windows 10 & 11 with native 4K video grabber and Chrome/Edge extensions.">
    <meta name="twitter:image" content="<?php echo esc_url(get_template_directory_uri() . '/screenshot.png'); ?>">

    <!-- Preconnect Resources for Ultra-Fast Core Web Vitals -->
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link rel="preconnect" href="https://unpkg.com">
    <link rel="preconnect" href="https://cdn.jsdelivr.net">
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@400;500;600;700;800;900&family=Space+Grotesk:wght@500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
    
    <!-- Lucide Icons & Font Awesome -->
    <script src="https://unpkg.com/lucide@latest"></script>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">

    <!-- Tailwind CSS CDN & Custom Config -->
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    fontFamily: {
                        sans: ['Plus Jakarta Sans', 'Inter', 'sans-serif'],
                        display: ['Space Grotesk', 'sans-serif'],
                        mono: ['JetBrains Mono', 'monospace'],
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
                        },
                        edm: {
                            primary: '#5D5FEF',
                            hover: '#4F46E5',
                            glow: 'rgba(93, 95, 239, 0.42)'
                        }
                    }
                }
            }
        }
    </script>

    <!-- Master WordPress Stylesheet & Local Stylesheets -->
    <link rel="stylesheet" href="<?php echo esc_url(get_stylesheet_uri()); ?>?ver=2.1.0">
    <link rel="stylesheet" href="<?php echo esc_url(get_template_directory_uri() . '/Assets/css/global.css'); ?>?ver=2.1.0">
    <link rel="stylesheet" href="<?php echo esc_url(get_template_directory_uri() . '/Assets/css/landing.css'); ?>?ver=2.1.0">
    <link rel="stylesheet" href="<?php echo esc_url(get_template_directory_uri() . '/Assets/css/responsive.css'); ?>?ver=2.1.0">

    <?php
    $landing_css_file = get_template_directory() . '/Assets/css/landing.css';
    if (!file_exists($landing_css_file)) {
        $landing_css_file = get_template_directory() . '/assets/css/landing.css';
    }
    if (file_exists($landing_css_file)) {
        echo '<style id="edm-embedded-critical-landing-css">' . file_get_contents($landing_css_file) . '</style>';
    }

    $global_css_file = get_template_directory() . '/Assets/css/global.css';
    if (!file_exists($global_css_file)) {
        $global_css_file = get_template_directory() . '/assets/css/global.css';
    }
    if (file_exists($global_css_file)) {
        echo '<style id="edm-embedded-critical-global-css">' . file_get_contents($global_css_file) . '</style>';
    }

    $resp_css_file = get_template_directory() . '/Assets/css/responsive.css';
    if (!file_exists($resp_css_file)) {
        $resp_css_file = get_template_directory() . '/assets/css/responsive.css';
    }
    if (file_exists($resp_css_file)) {
        echo '<style id="edm-embedded-critical-responsive-css">' . file_get_contents($resp_css_file) . '</style>';
    }
    ?>

    <!-- JSON-LD Structured Data: SoftwareApplication Schema -->
    <script type="application/ld+json">
    {
        "@context": "https://schema.org",
        "@type": "SoftwareApplication",
        "name": "Exclusive Download Manager",
        "alternateName": "EDM Turbo",
        "operatingSystem": "Windows 10, Windows 11 (x64, ARM64)",
        "applicationCategory": "UtilitiesApplication",
        "softwareVersion": "<?php echo esc_js($version); ?>",
        "downloadUrl": "<?php echo esc_js($download_url); ?>",
        "fileSize": "19.8MB",
        "offers": {
            "@type": "Offer",
            "price": "0.00",
            "priceCurrency": "USD",
            "description": "30-Day Full Turbo Trial with 32-Socket Acceleration"
        },
        "aggregateRating": {
            "@type": "AggregateRating",
            "ratingValue": "4.9",
            "ratingCount": "2840",
            "bestRating": "5",
            "worstRating": "1"
        },
        "author": {
            "@type": "Person",
            "name": "Alamin Hossain",
            "url": "<?php echo esc_js(home_url('/')); ?>"
        }
    }
    </script>

    <!-- JSON-LD Structured Data: FAQPage Schema -->
    <script type="application/ld+json">
    {
        "@context": "https://schema.org",
        "@type": "FAQPage",
        "mainEntity": [
            {
                "@type": "Question",
                "name": "Why is EDM faster than regular browser downloads?",
                "acceptedAnswer": {
                    "@type": "Answer",
                    "text": "Standard browsers download files through a single connection stream (1 socket). EDM dynamically partitions files into up to 32 parallel HTTP range chunks, downloading all segments concurrently and bypassing single-stream server throttling for up to 32x acceleration."
                }
            },
            {
                "@type": "Question",
                "name": "Does EDM support 4K and 8K video downloads?",
                "acceptedAnswer": {
                    "@type": "Answer",
                    "text": "Yes, EDM features an intelligent media stream sniffer that captures high-bitrate video and audio streams from YouTube, Vimeo, Facebook, and HLS/MPEG-DASH sources with native muxing."
                }
            },
            {
                "@type": "Question",
                "name": "Is EDM compatible with Google Chrome and Microsoft Edge?",
                "acceptedAnswer": {
                    "@type": "Answer",
                    "text": "Yes, EDM provides official Manifest V3 certified extensions for Google Chrome, Microsoft Edge Chromium, Brave, Opera, and Mozilla Firefox."
                }
            }
        ]
    }
    </script>

    <?php wp_head(); ?>
</head>
<body <?php body_class('edm-site-body'); ?>>
<?php wp_body_open(); ?>

<div id="page" class="site-wrapper bg-mesh-net">
    <!-- Ambient Side Glowing Blobs -->
    <div class="ambient-side-glow-left"></div>
    <div class="ambient-side-glow-right"></div>

    <a class="skip-link screen-reader-text" href="#primary">
        <?php esc_html_e('Skip to content', 'portfolio'); ?>
    </a>

    <?php 
    // Top Announcement Bar
    get_template_part('template-parts/header/announcement-bar'); 

    // Sticky Glassmorphic Navbar
    get_template_part('template-parts/header/navigation'); 
    ?>
    <main id="primary" class="site-main">

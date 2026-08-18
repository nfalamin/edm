<?php
/**
 * EDM Theme Functions and Definitions
 *
 * @link https://developer.wordpress.org/themes/basics/theme-functions/
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit; // Exit if accessed directly.
}

// 1. Theme Setup & Supports
require_once get_template_directory() . '/inc/setup.php';

// 2. Scripts & Styles Enqueue
require_once get_template_directory() . '/inc/enqueue.php';

// 3. Theme Helper Functions
require_once get_template_directory() . '/inc/theme-functions.php';

// 4. Security & Sanitization
require_once get_template_directory() . '/inc/security.php';

// 5. Routing & Permalink Helpers
require_once get_template_directory() . '/inc/helpers.php';

// 6. Custom Post Types & Portfolio Handlers
require_once get_template_directory() . '/inc/custom-post-types.php';


    <!-- Load Footer & Widgets properly via Template Part (Portfolio only) -->
    <?php 
    if ( ! ( is_page_template('page-nfdashbord.php') || is_page_template('page-dashboard.php') || is_page('nfdashbord') || is_page('dashboard') || is_page_template('page-edm.php') || is_page('edm') ) ) :
        get_template_part( 'footer-widgets' ); 
    ?>
    <!-- Socket.IO Integration for Real-time Messaging -->
    <script src="https://cdn.socket.io/4.7.2/socket.io.min.js"></script>
    <?php endif; ?>

    <?php wp_footer(); ?>
</body>
</html>
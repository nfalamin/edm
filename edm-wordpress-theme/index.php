<?php
/**
 * Main fallback index template
 */

get_header();
?>

<div style="padding: 40px 20px; text-align: center; min-height: 70vh; display: flex; flex-direction: column; align-items: center; justify-content: center;">
    <h1 style="font-size: 28px; color: var(--color-text-main); margin-bottom: 12px;">EDM — Exclusive Download Manager</h1>
    <p style="color: var(--color-text-secondary); max-width: 500px; margin-bottom: 24px;">WordPress Enterprise Theme & SaaS Control Plane.</p>
    <div style="display: flex; gap: 12px;">
        <a href="<?php echo esc_url(home_url('/')); ?>" class="btn btn-primary">Home Landing Page</a>
        <a href="<?php echo esc_url(home_url('/dashboard/')); ?>" class="btn btn-secondary">Admin Dashboard</a>
    </div>
</div>

<?php
get_footer();

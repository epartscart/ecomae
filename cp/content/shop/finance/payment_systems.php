<?php
// Legacy route — redirect to Payment gateways hub.
defined('_ASTEXE_') or die('No access');

$target = '/' . $DP_Config->backend_dir . '/shop/payments/payments?tab=configure';
?>
<script>location.href = <?php echo json_encode($target); ?>;</script>
<p>Redirecting to <a href="<?php echo htmlspecialchars($target, ENT_QUOTES, 'UTF-8'); ?>">Payment gateways</a>…</p>
<?php exit; ?>

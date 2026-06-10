<?php
/**
 * Shared demo checkout page for UAE payment gateway stubs.
 */
if ((!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') && !empty($_POST['EPC_PAY_HANDLER'])) {
	$EPC_PAY_HANDLER = preg_replace('/[^a-z0-9_]/', '', (string)$_POST['EPC_PAY_HANDLER']);
}
if (!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') {
	exit('No handler');
}
$handler = preg_replace('/[^a-z0-9_]/', '', (string)$EPC_PAY_HANDLER);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;
require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$gatewayTitle = ucfirst(str_replace('_', ' ', $handler));
$operationId = isset($_POST['operation_id']) ? (int)$_POST['operation_id'] : 0;
$sum = isset($_POST['sum']) ? (float)$_POST['sum'] : 0;
$desc = isset($_POST['operation_description']) ? (string)$_POST['operation_description'] : 'Payment';
$currency = isset($_POST['currency']) ? (string)$_POST['currency'] : 'AED';
$userId = isset($_POST['user_id']) ? (int)$_POST['user_id'] : 0;
$notifyUrl = '/content/shop/finance/payment_systems/' . $handler . '/notification.php';

if (isset($_POST['action']) && $_POST['action'] === 'pay_execute') {
	if ($_POST['need_result'] === 'success') {
		?>
		<form name="success_form" style="display:none" method="post" action="<?php echo htmlspecialchars($notifyUrl, ENT_QUOTES, 'UTF-8'); ?>">
			<input type="hidden" name="operation_id" value="<?php echo (int)$operationId; ?>">
			<input type="hidden" name="sum" value="<?php echo htmlspecialchars((string)$sum, ENT_QUOTES, 'UTF-8'); ?>">
			<input type="hidden" name="user_id" value="<?php echo (int)$userId; ?>">
			<input type="hidden" name="demo_token" value="epc-demo-ok">
		</form>
		<script>document.forms['success_form'].submit();</script>
		<?php
		exit;
	}
	?>
	<script>location='<?php echo $multilang_params['lang_href']; ?>/shop/balans?error_message=<?php echo urlencode('Demo payment declined'); ?>';</script>
	<?php
	exit;
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title><?php echo htmlspecialchars($gatewayTitle, ENT_QUOTES, 'UTF-8'); ?> — Demo checkout</title>
	<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css">
	<style>
		body { background:#f1f5f9; padding:40px 15px; }
		.demo-card { max-width:480px; margin:0 auto; background:#fff; border-radius:12px; box-shadow:0 4px 24px rgba(0,0,0,.08); overflow:hidden; }
		.demo-head { background:linear-gradient(135deg,#1e40af,#2563eb); color:#fff; padding:20px 24px; }
		.demo-body { padding:24px; }
		.demo-badge { display:inline-block; background:#fef3c7; color:#92400e; font-size:11px; font-weight:700; padding:4px 10px; border-radius:999px; text-transform:uppercase; }
		.amount { font-size:28px; font-weight:700; color:#0f172a; }
	</style>
</head>
<body>
	<div class="demo-card">
		<div class="demo-head">
			<span class="demo-badge">Demo mode</span>
			<h3 style="margin:12px 0 4px;"><?php echo htmlspecialchars($gatewayTitle, ENT_QUOTES, 'UTF-8'); ?></h3>
			<p style="margin:0;opacity:.9;font-size:13px;">No real charge — replace dummy API keys in CP when ready.</p>
		</div>
		<div class="demo-body">
			<p class="text-muted"><?php echo htmlspecialchars($desc, ENT_QUOTES, 'UTF-8'); ?></p>
			<p class="amount"><?php echo number_format($sum, 2); ?> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?></p>
			<p><small class="text-muted">Operation #<?php echo (int)$operationId; ?></small></p>
			<hr>
			<form method="post">
				<input type="hidden" name="action" value="pay_execute">
				<input type="hidden" name="operation_id" value="<?php echo (int)$operationId; ?>">
				<input type="hidden" name="sum" value="<?php echo htmlspecialchars((string)$sum, ENT_QUOTES, 'UTF-8'); ?>">
				<input type="hidden" name="user_id" value="<?php echo (int)$userId; ?>">
				<div class="form-group">
					<label>Simulate result</label>
					<select class="form-control" name="need_result">
						<option value="success">Successful payment</option>
						<option value="error">Declined / failed</option>
					</select>
				</div>
				<button type="submit" class="btn btn-success btn-block btn-lg">Complete demo payment</button>
			</form>
		</div>
	</div>
</body>
</html>

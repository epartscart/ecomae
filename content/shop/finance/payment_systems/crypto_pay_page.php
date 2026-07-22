<?php
/**
 * Crypto checkout — coin picker + NOWPayments/demo invoice.
 */
if ((!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') && !empty($_POST['EPC_PAY_HANDLER'])) {
	$EPC_PAY_HANDLER = preg_replace('/[^a-z0-9_]/', '', (string)$_POST['EPC_PAY_HANDLER']);
}
if (!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') {
	$EPC_PAY_HANDLER = 'nowpayments';
}
$handler = preg_replace('/[^a-z0-9_]/', '', (string)$EPC_PAY_HANDLER);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db_link->query('SET NAMES utf8;');
} catch (Throwable $e) {
	http_response_code(500);
	exit('Database connection error');
}

if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
		try {
			$db_link = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$db_link->query('SET NAMES utf8;');
		} catch (Throwable $e) {
			http_response_code(500);
			exit('Database connection error');
		}
	}
}

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_crypto_payments.php';

$operationId = isset($_POST['operation_id']) ? (int)$_POST['operation_id'] : (isset($_GET['operation_id']) ? (int)$_GET['operation_id'] : 0);
$sum = isset($_POST['sum']) ? (float)$_POST['sum'] : 0;
$desc = isset($_POST['operation_description']) ? (string)$_POST['operation_description'] : 'Crypto payment';
$currency = isset($_POST['currency']) ? strtoupper((string)$_POST['currency']) : 'USD';
$userId = isset($_POST['user_id']) ? (int)$_POST['user_id'] : (int)DP_User::getUserId();
$notifyUrl = '/content/shop/finance/payment_systems/' . $handler . '/notification.php';

$EPC_PAY_HANDLER = $handler;
$operation_id = $operationId;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/get_pay_system_parameters.php';
if (!is_array($paysystem_parameters)) {
	$paysystem_parameters = array();
}
$demoMode = !empty($paysystem_parameters['demo_mode']);
$coins = epc_crypto_allowed_coins(is_array($paysystem_parameters) ? $paysystem_parameters : array());
$selectedCoin = isset($_POST['pay_coin']) ? strtolower(preg_replace('/[^a-z0-9]/', '', (string)$_POST['pay_coin'])) : '';
$invoice = null;
$error = '';

if (isset($_POST['action']) && $_POST['action'] === 'create_invoice') {
	if ($selectedCoin === '' || !isset($coins[$selectedCoin])) {
		$error = 'Select a cryptocurrency.';
	} else {
		if ($demoMode) {
			$invoice = epc_crypto_demo_invoice($operationId, $sum, $currency, $selectedCoin);
		} else {
			$created = epc_crypto_create_nowpayment(
				$paysystem_parameters,
				array(
					'price_amount' => $sum,
					'price_currency' => strtolower($currency),
					'pay_currency' => $selectedCoin,
					'order_id' => (string)$operationId,
					'order_description' => $desc,
					'ipn_callback_url' => rtrim((string)$DP_Config->domain_path, '/') . $notifyUrl,
					'is_fixed_rate' => false,
				)
			);
			if (!$created['ok']) {
				$error = $created['message'] ?: 'Could not create crypto invoice';
			} else {
				$invoice = $created['payment'];
				$invoice['demo'] = false;
			}
		}
	}
}

if (isset($_POST['action']) && $_POST['action'] === 'confirm_demo' && $demoMode) {
	?>
	<form name="success_form" style="display:none" method="post" action="<?php echo htmlspecialchars($notifyUrl, ENT_QUOTES, 'UTF-8'); ?>">
		<input type="hidden" name="operation_id" value="<?php echo (int)$operationId; ?>">
		<input type="hidden" name="sum" value="<?php echo htmlspecialchars((string)$sum, ENT_QUOTES, 'UTF-8'); ?>">
		<input type="hidden" name="user_id" value="<?php echo (int)$userId; ?>">
		<input type="hidden" name="demo_token" value="epc-demo-ok">
		<input type="hidden" name="pay_coin" value="<?php echo htmlspecialchars($selectedCoin, ENT_QUOTES, 'UTF-8'); ?>">
	</form>
	<script>document.forms['success_form'].submit();</script>
	<?php
	exit;
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>Pay with crypto</title>
	<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css">
	<style>
		:root { --ink:#0f172a; --muted:#64748b; --line:#e2e8f0; --accent:#0f766e; --soft:#f8fafc; }
		body { background: linear-gradient(180deg,#ecfeff 0%, #f8fafc 40%, #fff 100%); padding:36px 14px; font-family: Georgia, 'Times New Roman', serif; }
		.card { max-width:520px; margin:0 auto; background:#fff; border:1px solid var(--line); border-radius:14px; box-shadow:0 10px 30px rgba(15,23,42,.08); overflow:hidden; }
		.head { padding:20px 22px; background: radial-gradient(700px 160px at 0% 0%, rgba(15,118,110,.14), transparent 55%), linear-gradient(180deg,#fff,#f8fafc); border-bottom:1px solid var(--line); }
		.head h1 { margin:8px 0 4px; font-size:26px; color:var(--ink); letter-spacing:-.02em; }
		.head p { margin:0; color:var(--muted); font-size:13px; font-family: system-ui, sans-serif; }
		.badge { display:inline-block; background:#ccfbf1; color:#115e59; font:700 11px/1 system-ui,sans-serif; padding:5px 9px; border-radius:8px; text-transform:uppercase; letter-spacing:.04em; }
		.body { padding:22px; font-family: system-ui, -apple-system, sans-serif; }
		.amount { font-size:30px; font-weight:750; color:var(--ink); letter-spacing:-.02em; }
		.coins { display:grid; grid-template-columns:1fr 1fr; gap:8px; margin:14px 0; }
		.coins label { display:block; border:1px solid var(--line); border-radius:10px; padding:10px 12px; cursor:pointer; background:var(--soft); }
		.coins label:has(input:checked) { border-color:#5eead4; background:#f0fdfa; box-shadow:inset 0 0 0 1px #99f6e4; }
		.coins strong { display:block; font-size:13px; color:var(--ink); }
		.coins span { font-size:11px; color:var(--muted); }
		.btn-pay { background:var(--accent); border-color:var(--accent); font-weight:700; }
		.invoice { background:var(--soft); border:1px solid var(--line); border-radius:12px; padding:14px; margin-top:12px; }
		.addr { font-family: ui-monospace, Menlo, monospace; word-break:break-all; font-size:12px; background:#fff; border:1px dashed #99f6e4; padding:10px; border-radius:8px; }
		@keyframes rise { from { opacity:0; transform:translateY(8px);} to { opacity:1; transform:none;} }
		.card { animation: rise .4s ease both; }
	</style>
</head>
<body>
	<div class="card">
		<div class="head">
			<span class="badge"><?php echo $demoMode ? 'Demo crypto' : 'Live crypto'; ?></span>
			<h1>Pay with crypto</h1>
			<p>USDT, Bitcoin, Ethereum and more — settle your order securely.</p>
		</div>
		<div class="body">
			<p class="text-muted" style="margin-top:0;"><?php echo htmlspecialchars($desc, ENT_QUOTES, 'UTF-8'); ?></p>
			<div class="amount"><?php echo number_format($sum, 2); ?> <?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?></div>
			<p><small class="text-muted">Operation #<?php echo (int)$operationId; ?></small></p>

			<?php if ($error !== ''): ?>
				<div class="alert alert-danger"><?php echo htmlspecialchars($error, ENT_QUOTES, 'UTF-8'); ?></div>
			<?php endif; ?>

			<?php if (!$invoice): ?>
			<form method="post">
				<input type="hidden" name="action" value="create_invoice">
				<input type="hidden" name="EPC_PAY_HANDLER" value="<?php echo htmlspecialchars($handler, ENT_QUOTES, 'UTF-8'); ?>">
				<input type="hidden" name="operation_id" value="<?php echo (int)$operationId; ?>">
				<input type="hidden" name="sum" value="<?php echo htmlspecialchars((string)$sum, ENT_QUOTES, 'UTF-8'); ?>">
				<input type="hidden" name="operation_description" value="<?php echo htmlspecialchars($desc, ENT_QUOTES, 'UTF-8'); ?>">
				<input type="hidden" name="user_id" value="<?php echo (int)$userId; ?>">
				<input type="hidden" name="currency" value="<?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>">
				<div class="coins">
					<?php $i = 0; foreach ($coins as $code => $meta): ?>
					<label>
						<input type="radio" name="pay_coin" value="<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>" <?php echo ($i === 0 || $selectedCoin === $code) ? 'checked' : ''; ?>>
						<strong><?php echo htmlspecialchars($meta['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
						<span><?php echo htmlspecialchars($meta['network'] !== '' ? $meta['network'] : strtoupper($code), ENT_QUOTES, 'UTF-8'); ?></span>
					</label>
					<?php $i++; endforeach; ?>
				</div>
				<button type="submit" class="btn btn-success btn-block btn-lg btn-pay">Create crypto invoice</button>
			</form>
			<?php else:
				$payAmount = $invoice['pay_amount'] ?? $invoice['amount'] ?? '';
				$payAddr = $invoice['pay_address'] ?? '';
				$payCur = strtoupper((string)($invoice['pay_currency'] ?? $selectedCoin));
			?>
			<div class="invoice">
				<p style="margin:0 0 8px;"><strong>Send exactly</strong></p>
				<p class="amount" style="font-size:22px;margin:0 0 10px;"><?php echo htmlspecialchars((string)$payAmount, ENT_QUOTES, 'UTF-8'); ?> <?php echo htmlspecialchars($payCur, ENT_QUOTES, 'UTF-8'); ?></p>
				<p style="margin:0 0 6px;color:#64748b;font-size:12px;">To address</p>
				<div class="addr"><?php echo htmlspecialchars((string)$payAddr, ENT_QUOTES, 'UTF-8'); ?></div>
				<p style="margin:12px 0 0;font-size:12px;color:#64748b;">
					Payment ID: <?php echo htmlspecialchars((string)($invoice['payment_id'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
					<?php if (!empty($invoice['demo'])): ?> · Demo invoice (no chain broadcast)<?php endif; ?>
				</p>
			</div>
			<?php if (!empty($invoice['demo'])): ?>
			<form method="post" style="margin-top:14px;">
				<input type="hidden" name="action" value="confirm_demo">
				<input type="hidden" name="EPC_PAY_HANDLER" value="<?php echo htmlspecialchars($handler, ENT_QUOTES, 'UTF-8'); ?>">
				<input type="hidden" name="operation_id" value="<?php echo (int)$operationId; ?>">
				<input type="hidden" name="sum" value="<?php echo htmlspecialchars((string)$sum, ENT_QUOTES, 'UTF-8'); ?>">
				<input type="hidden" name="user_id" value="<?php echo (int)$userId; ?>">
				<input type="hidden" name="pay_coin" value="<?php echo htmlspecialchars($selectedCoin !== '' ? $selectedCoin : (string)($invoice['pay_currency'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>">
				<button type="submit" class="btn btn-success btn-block btn-lg btn-pay">I paid — confirm (demo)</button>
			</form>
			<?php else: ?>
			<p class="text-muted" style="margin-top:14px;font-size:13px;">After the network confirms your transfer, NOWPayments will notify this store and your order will be marked paid automatically. You can close this page.</p>
			<a class="btn btn-default btn-block" href="<?php echo htmlspecialchars($multilang_params['lang_href'] . '/shop/balans', ENT_QUOTES, 'UTF-8'); ?>">Back to balance</a>
			<?php endif; ?>
			<?php endif; ?>
		</div>
	</div>
</body>
</html>

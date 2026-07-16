<?php
/**
 * Public Blockchain BOS proof verification.
 *
 * JSON:
 *   GET /epc-blockchain-verify.php?proof=prf_xxx
 *   GET /epc-blockchain-verify.php?hash=<sha256>&format=json
 *
 * HTML UI (browser default):
 *   GET /epc-blockchain-verify.php
 *   GET /epc-blockchain-verify.php?proof=prf_xxx
 */

declare(strict_types=1);

require_once __DIR__ . '/content/general_pages/epc_blockchain_bos.php';
epc_bc_bos_register_job_handlers();

$key = trim((string)($_GET['proof'] ?? $_GET['hash'] ?? $_GET['id'] ?? ''));
$format = strtolower(trim((string)($_GET['format'] ?? '')));
$accept = (string)($_SERVER['HTTP_ACCEPT'] ?? '');
$wantJson = ($format === 'json')
    || (isset($_GET['json']))
    || (strpos($accept, 'application/json') !== false && strpos($accept, 'text/html') === false);

$result = null;
if ($key !== '') {
    $result = epc_bc_bos_verify($key);
}

if ($wantJson) {
    header('Content-Type: application/json; charset=utf-8');
    header('X-Content-Type-Options: nosniff');
    header('Cache-Control: no-store');
    if ($key === '') {
        http_response_code(400);
        echo json_encode([
            'ok' => false,
            'error' => 'Provide ?proof=<proof_uid> or ?hash=<sha256>',
            'product' => epc_bc_bos_product_name(),
        ], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
        exit;
    }
    if (empty($result['ok'])) {
        http_response_code(503);
    }
    echo json_encode($result, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

// HTML verify UI
header('Content-Type: text/html; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

$product = htmlspecialchars(epc_bc_bos_product_name(), ENT_QUOTES, 'UTF-8');
$tagline = htmlspecialchars(epc_bc_bos_product_tagline(), ENT_QUOTES, 'UTF-8');
$keyEsc = htmlspecialchars($key, ENT_QUOTES, 'UTF-8');

$valid = is_array($result) && !empty($result['valid']);
$found = is_array($result) && !empty($result['proof']);
$statusLabel = 'Enter a proof ID or hash';
$statusClass = 'neutral';
if ($key !== '') {
    if (!$found) {
        $statusLabel = 'Proof not found';
        $statusClass = 'bad';
    } elseif ($valid) {
        $statusLabel = 'Valid — integrity confirmed';
        $statusClass = 'ok';
    } else {
        $statusLabel = 'Found but integrity check failed';
        $statusClass = 'bad';
    }
}

$proof = is_array($result) ? ($result['proof'] ?? null) : null;
$batch = is_array($result) ? ($result['batch'] ?? null) : null;

function epc_bc_h($v): string
{
    return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Verify proof — <?php echo $product; ?></title>
<meta name="description" content="Public cryptographic proof verification for the ECOM AE Blockchain BOS Enterprise System.">
<style>
:root{--bg:#0b1220;--panel:#121a2b;--line:rgba(255,255,255,.08);--text:#e8eefc;--muted:#93a4c3;--ok:#34d399;--bad:#fb7185;--accent:#38bdf8}
*{box-sizing:border-box}
body{margin:0;font-family:ui-sans-serif,system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:radial-gradient(1200px 600px at 20% -10%,#1e3a5f 0%,transparent 55%),radial-gradient(900px 500px at 90% 10%,#0f766e33 0%,transparent 50%),var(--bg);color:var(--text);min-height:100vh}
.wrap{max-width:760px;margin:0 auto;padding:40px 20px 64px}
.brand{font-size:13px;letter-spacing:.08em;text-transform:uppercase;color:var(--accent);margin:0 0 10px}
h1{margin:0 0 8px;font-size:28px;line-height:1.2}
.lead{color:var(--muted);margin:0 0 28px;line-height:1.6}
.card{background:var(--panel);border:1px solid var(--line);border-radius:16px;padding:22px}
form{display:flex;gap:10px;flex-wrap:wrap}
input[type=text]{flex:1;min-width:220px;background:#0a1220;border:1px solid var(--line);color:var(--text);border-radius:10px;padding:12px 14px;font-size:15px}
button,.btn{background:var(--accent);color:#041018;border:0;border-radius:10px;padding:12px 16px;font-weight:700;cursor:pointer;text-decoration:none;display:inline-block}
.status{margin-top:18px;padding:12px 14px;border-radius:10px;font-weight:600}
.status.ok{background:rgba(52,211,153,.12);color:var(--ok);border:1px solid rgba(52,211,153,.35)}
.status.bad{background:rgba(251,113,133,.12);color:var(--bad);border:1px solid rgba(251,113,133,.35)}
.status.neutral{background:rgba(148,163,184,.08);color:var(--muted);border:1px solid var(--line)}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:16px}
@media(max-width:640px){.grid{grid-template-columns:1fr}}
.field{background:#0a1220;border:1px solid var(--line);border-radius:10px;padding:12px}
.field label{display:block;font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:var(--muted);margin-bottom:4px}
.field div{font-size:13px;word-break:break-all}
.foot{margin-top:28px;color:var(--muted);font-size:13px;line-height:1.5}
.foot a{color:var(--accent)}
</style>
</head>
<body>
<div class="wrap">
	<p class="brand"><?php echo $product; ?></p>
	<h1>Verify a business proof</h1>
	<p class="lead"><?php echo $tagline; ?></p>

	<div class="card">
		<form method="get" action="/epc-blockchain-verify.php" accept-charset="utf-8">
			<input type="text" name="proof" value="<?php echo $keyEsc; ?>" placeholder="Proof ID (prf_…) or SHA-256 hash" autocomplete="off" autofocus>
			<button type="submit">Verify</button>
		</form>
		<div class="status <?php echo epc_bc_h($statusClass); ?>"><?php echo epc_bc_h($statusLabel); ?></div>

		<?php if (is_array($proof)): ?>
		<div class="grid">
			<div class="field"><label>Proof ID</label><div><?php echo epc_bc_h($proof['proof_uid'] ?? ''); ?></div></div>
			<div class="field"><label>Status</label><div><?php echo epc_bc_h($proof['status'] ?? ''); ?></div></div>
			<div class="field"><label>Tenant</label><div><?php echo epc_bc_h($proof['tenant_key'] ?? ''); ?></div></div>
			<div class="field"><label>Record</label><div><?php echo epc_bc_h(($proof['record_type'] ?? '') . ' · ' . ($proof['record_id'] ?? '')); ?></div></div>
			<div class="field" style="grid-column:1/-1"><label>Payload hash (SHA-256)</label><div><?php echo epc_bc_h($proof['payload_hash'] ?? ''); ?></div></div>
			<div class="field" style="grid-column:1/-1"><label>Anchor ref</label><div><?php echo epc_bc_h($proof['anchor_ref'] !== '' ? $proof['anchor_ref'] : '— pending batch —'); ?></div></div>
			<?php if (is_array($batch)): ?>
			<div class="field"><label>Merkle root</label><div><?php echo epc_bc_h($batch['merkle_root'] ?? ''); ?></div></div>
			<div class="field"><label>Batch</label><div><?php echo epc_bc_h(($batch['batch_uid'] ?? '') . ' · ' . (int)($batch['proof_count'] ?? 0) . ' proofs'); ?></div></div>
			<?php endif; ?>
		</div>
		<?php endif; ?>
	</div>

	<p class="foot">
		JSON API: append <code>&amp;format=json</code>.
		<a href="https://www.ecomae.com/blockchain">Blockchain BOS</a> ·
		<a href="https://www.ecomae.com/bos">What is Blockchain BOS</a> ·
		<a href="https://www.ecomae.com/">ECOM AE</a>
	</p>
</div>
</body>
</html>

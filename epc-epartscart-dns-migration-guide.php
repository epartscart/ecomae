<?php
/**
 * GoDaddy DNS cutover guide — epartscart.com → ecomae platform (Super CP).
 * https://www.ecomae.com/epc-epartscart-dns-migration-guide.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$platformIp = epc_portal_platform_ip();
$hostname = 'www.epartscart.com';
$bare = 'epartscart.com';
$token = epc_deploy_token();

$inventory = array();
$invUrl = 'https://' . ($_SERVER['HTTP_HOST'] ?? 'www.ecomae.com') . '/epc-epartscart-vps-backup.php?token=' . rawurlencode($token) . '&mode=inventory';
$ctx = stream_context_create(array(
	'http' => array('timeout' => 15, 'ignore_errors' => true),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
));
$raw = @file_get_contents($invUrl, false, $ctx);
if ($raw !== false) {
	$decoded = json_decode($raw, true);
	if (is_array($decoded)) {
		$inventory = $decoded;
	}
}

$serverIp = (string) ($inventory['server_public_ip'] ?? $platformIp);
if ($serverIp === '') {
	$serverIp = $platformIp;
}

header('Content-Type: text/html; charset=utf-8');
?><!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>epartscart.com — DNS migration to ECOM AE platform</title>
<style>
:root { --bg:#0f172a; --card:#1e293b; --text:#e2e8f0; --muted:#94a3b8; --accent:#38bdf8; --ok:#4ade80; --warn:#fbbf24; }
* { box-sizing:border-box; }
body { margin:0; font-family:system-ui,-apple-system,Segoe UI,sans-serif; background:var(--bg); color:var(--text); line-height:1.55; }
.wrap { max-width:920px; margin:0 auto; padding:32px 20px 64px; }
h1 { font-size:1.65rem; margin:0 0 8px; }
.lead { color:var(--muted); margin:0 0 28px; }
.card { background:var(--card); border:1px solid #334155; border-radius:12px; padding:20px 22px; margin:0 0 18px; }
h2 { font-size:1.1rem; margin:0 0 12px; color:var(--accent); }
table { width:100%; border-collapse:collapse; font-size:14px; }
th,td { text-align:left; padding:10px 12px; border-bottom:1px solid #334155; vertical-align:top; }
th { color:var(--muted); font-weight:600; width:28%; }
code, .mono { font-family:ui-monospace,Consolas,monospace; background:#0b1220; padding:2px 6px; border-radius:4px; }
ul { margin:8px 0 0; padding-left:20px; }
li { margin:6px 0; }
.tag { display:inline-block; padding:2px 8px; border-radius:999px; font-size:12px; font-weight:600; }
.tag-ok { background:#14532d; color:var(--ok); }
.tag-warn { background:#713f12; color:var(--warn); }
.probe { margin:6px 0; }
a { color:var(--accent); }
</style>
</head>
<body>
<div class="wrap">
<h1>epartscart.com → ECOM AE platform DNS migration</h1>
<p class="lead">Zero-downtime cutover from dedicated VPS hosting to Super CP platform at <strong>31.97.216.247</strong>. Complete backup before changing DNS or deleting the old VPS.</p>

<div class="card">
<h2>GoDaddy DNS records (exact values)</h2>
<table>
<tr><th>Platform IP</th><td><code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> <span class="tag tag-ok">A record target</span></td></tr>
<tr><th>Server origin IP</th><td><code><?php echo htmlspecialchars($serverIp, ENT_QUOTES, 'UTF-8'); ?></code> (detected from platform server)</td></tr>
<tr><th>TTL before cutover</th><td><code>600</code> seconds (10 min) — lower TTL 24h before cutover if possible</td></tr>
<tr><th>TTL after stable</th><td><code>3600</code> or GoDaddy default</td></tr>
</table>
<table style="margin-top:16px">
<tr><th>Type</th><th>Name</th><th>Value</th><th>Notes</th></tr>
<tr><td><code>A</code></td><td><code>@</code></td><td><code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code></td><td>Apex → platform (cutover script adds 301 to www)</td></tr>
<tr><td><code>A</code></td><td><code>www</code></td><td><code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code></td><td>Primary storefront + CP + ERP</td></tr>
<tr><td><code>CNAME</code></td><td><code>cp</code></td><td><code>www.<?php echo htmlspecialchars($bare, ENT_QUOTES, 'UTF-8'); ?></code></td><td>Optional — tenant CP is at /cp/ on www</td></tr>
</table>
<p style="margin-top:14px;color:var(--warn)"><strong>Remove</strong> old A/CNAME records pointing to previous VPS or Cloudflare-only proxies if you want direct GoDaddy → platform routing.</p>
</div>

<div class="card">
<h2>Pre-cutover checklist</h2>
<ul>
<li>Run backup: <code>epc-epartscart-vps-backup.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;mode=create</code></li>
<li>Download manifest locally to <code>backups/epartscart-migration-YYYYMMDD/</code></li>
<li>Run cutover repair (dry run): <code>epc-epartscart-supercp-cutover.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code></li>
<li>Apply nginx + tenant sync: add <code>&amp;apply=1&amp;clp_pass=...</code></li>
<li>Confirm tenant <code>epartscart</code> is <strong>live</strong> in Super CP registry (db=<code>docpart</code>)</li>
<li>Lower GoDaddy TTL to 600</li>
<li>Verify origin probes PASS on platform IP (cutover script output)</li>
</ul>
</div>

<div class="card">
<h2>Cutover sequence</h2>
<ol>
<li>Apply platform nginx alias + SSL on <code>www.ecomae.com</code> (not a new CloudPanel site)</li>
<li>Update GoDaddy A records @ and www → <code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code></li>
<li>Wait 5–60 minutes for DNS propagation</li>
<li>Re-run cutover probes (public HTTPS)</li>
<li>Test storefront, CP, ERP (see below)</li>
</ol>
</div>

<div class="card">
<h2>Post-cutover verification URLs</h2>
<div class="probe"><a href="https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/</a> — storefront</div>
<div class="probe"><a href="https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/cp/" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/cp/</a> — tenant control panel</div>
<div class="probe"><a href="https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/erp" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/erp</a> — ERP portal</div>
<div class="probe"><a href="https://www.ecomae.com/cp/" target="_blank" rel="noopener">https://www.ecomae.com/cp/</a> — Super CP (unchanged)</div>
</div>

<div class="card">
<h2>When safe to delete old VPS</h2>
<ul>
<li>Backup manifest downloaded and checksums verified</li>
<li>GoDaddy DNS resolves <?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?> to <code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> (not old VPS IP)</li>
<li>All three probes return HTTP 200 (or expected redirects) for 24h+</li>
<li>ERP/CRM data visible in CP (same <code>docpart</code> database)</li>
<li>Email/auth smoke test passed</li>
</ul>
<p><span class="tag tag-warn">DO NOT DELETE</span> until every item above passes.</p>
</div>

<div class="card">
<h2>Rollback</h2>
<ul>
<li>Restore GoDaddy A records to previous VPS IP</li>
<li>Wait TTL propagation (10–60 min with TTL 600)</li>
<li>If data changed on platform only: import <code>docpart-database.sql.gz</code> from backup manifest</li>
<li>Extract <code>epartscart_vps-docroot.tar.gz</code> to old docroot if files were lost</li>
<li>Re-enable old VPS nginx/SSL before deleting anything on platform side</li>
</ul>
</div>

<?php if (!empty($inventory['dns']['public_resolve'])): ?>
<div class="card">
<h2>Current public DNS (detected)</h2>
<table>
<?php foreach ($inventory['dns']['public_resolve'] as $host => $ip): ?>
<tr><th><?php echo htmlspecialchars($host, ENT_QUOTES, 'UTF-8'); ?></th><td><code><?php echo htmlspecialchars((string) $ip, ENT_QUOTES, 'UTF-8'); ?></code></td></tr>
<?php endforeach; ?>
</table>
</div>
<?php endif; ?>

<p style="color:var(--muted);font-size:13px;margin-top:24px">Generated <?php echo gmdate('Y-m-d H:i:s'); ?> UTC · ECOM AE Model C migration</p>
</div>
</body>
</html>

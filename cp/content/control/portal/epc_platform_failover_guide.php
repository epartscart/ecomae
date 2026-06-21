<?php
/**
 * Super CP — Failover splash operator guide (step-by-step).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_failover.php';

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if (!$isSuper) {
	echo '<div class="alert alert-warning">Failover guide is available on <strong>Super CP</strong> (www.ecomae.com) only.</div>';
	return;
}

function epc_ffg_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$host = epc_portal_host();
$status = epc_failover_current_status(false);
$mode = (string) ($status['mode'] ?? 'primary_ok');
$cfg = epc_failover_read_config();
$backupUrl = (string) ($cfg['backup_base_url'] ?? '');
$primaryUrl = (string) ($cfg['primary_url'] ?? 'https://www.ecomae.com/');
$token = function_exists('epc_deploy_token') ? epc_deploy_token() : 'epartscart-deploy-2026';
if (!function_exists('epc_deploy_token')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	$token = epc_deploy_token();
}
$platform = 'https://www.ecomae.com';
$splashPreview = $platform . '/epc-platform-splash.html?epc_splash_preview=1&mode=backup_active';
$statusUrl = $platform . '/epc-platform-status.php';
$jsonUrl = $platform . '/epc-platform-status.json';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-ffg'));
?>
<div class="epc-portal-settings epc-ffg">
	<div class="hpanel">
		<div class="panel-heading">
			<h2><i class="fas fa-shield-alt"></i> Failover &amp; splash</h2>
			<p class="text-muted">Tenants never see generic “down / slow / unreachable” errors. Immediate splash → local premises backup → sticky <strong>LOCAL PREMISES BACKUP</strong> banner on every page. CPU-safe: 60s status poll max, localStorage, static JSON.</p>
		</div>
		<div class="panel-body">
			<p>Current mode on <code><?php echo epc_ffg_h($host); ?></code>:
				<span class="badge-mode"><?php echo epc_ffg_h($mode); ?></span>
				<small class="text-muted">updated <?php echo epc_ffg_h($status['updated_at'] ?? '—'); ?></small>
			</p>

			<h3>1. Preview splash (no outage)</h3>
			<div class="preview-btns">
				<a class="btn btn-primary btn-sm" href="<?php echo epc_ffg_h($splashPreview); ?>" target="_blank" rel="noopener">Backup active preview</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_ffg_h($platform . '/epc-platform-splash.html?epc_splash_preview=1&mode=primary_down'); ?>" target="_blank" rel="noopener">Connecting to backup</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_ffg_h($platform . '/epc-platform-splash.html?epc_splash_preview=1&mode=failback_redirect'); ?>" target="_blank" rel="noopener">Failback countdown</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_ffg_h($statusUrl); ?>" target="_blank" rel="noopener">Status API (JSON)</a>
			</div>

			<h3>2. Configure backup URL (laptop / premises)</h3>
			<p>Current <code>backup_base_url</code>: <strong><?php echo epc_ffg_h($backupUrl !== '' ? $backupUrl : '(not set)'); ?></strong> · Primary: <code><?php echo epc_ffg_h($primaryUrl); ?></code></p>
			<p>Set tunnel or local Docker URL (e.g. <code>http://127.0.0.1:9080</code> or Cloudflare Tunnel HTTPS):</p>
			<pre>curl -sk -X POST "<?php echo epc_ffg_h($statusUrl); ?>" \
  -d "token=<?php echo epc_ffg_h($token); ?>" \
  -d "backup_base_url=https://YOUR-TUNNEL.trycloudflare.com" \
  -d "poll_interval_sec=60"</pre>
			<p>Test backup mode + banner on all storefront pages:</p>
			<pre>curl -sk -X POST "<?php echo epc_ffg_h($statusUrl); ?>" \
  -d "token=<?php echo epc_ffg_h($token); ?>&amp;mode=backup_active"</pre>
			<p>Return to cloud:</p>
			<pre>curl -sk -X POST "<?php echo epc_ffg_h($statusUrl); ?>" \
  -d "token=<?php echo epc_ffg_h($token); ?>&amp;mode=failback_redirect&amp;redirect_seconds=15"
curl -sk -X POST "<?php echo epc_ffg_h($statusUrl); ?>" -d "token=<?php echo epc_ffg_h($token); ?>&amp;mode=primary_ok"</pre>
			<p>Optional subtle badge when healthy: <code>show_cloud_primary_badge=1</code> in same POST.</p>

			<h3>3. Set failover mode (reference)</h3>
			<p>POST <code>mode=</code> only (no config change):</p>
			<pre>curl -sk -X POST "<?php echo epc_ffg_h($statusUrl); ?>" \
  -d "token=<?php echo epc_ffg_h($token); ?>&amp;mode=backup_active"</pre>
			<table class="table table-bordered">
				<thead><tr><th>mode</th><th>When</th></tr></thead>
				<tbody>
					<tr><td><code>primary_ok</code></td><td>Cloud healthy — normal traffic</td></tr>
					<tr><td><code>primary_down</code></td><td>Cloud unreachable — show “connecting to backup”</td></tr>
					<tr><td><code>backup_active</code></td><td>Backup origin serving traffic</td></tr>
					<tr><td><code>failback_sync</code></td><td>Cloud back — “restoring” step</td></tr>
					<tr><td><code>failback_redirect</code></td><td>Countdown return to cloud (optional <code>redirect_seconds</code>)</td></tr>
				</tbody>
			</table>

			<h3>4. nginx — error_page + Save vhost (required)</h3>
			<p class="alert alert-warning" style="margin-bottom:12px"><strong>Operator:</strong> After CLP/nginx edits you must click <strong>Save</strong> on the site in CloudPanel or run <code>nginx -t &amp;&amp; systemctl reload nginx</code>. Until then, new URLs may 404 even though files are deployed.</p>
			<p>Add inside the tenant <code>server { }</code> block (CloudPanel → Sites → domain → Vhost):</p>
			<pre>error_page 502 503 504 525 = /epc-platform-splash.html;
location = /epc-platform-splash.html {
    root /home/ecomae/htdocs/www.ecomae.com;
    internal;
}
location = /epc-platform-status.json {
    root /home/ecomae/htdocs/www.ecomae.com;
    add_header Cache-Control "no-store";
}</pre>
			<p>Reload nginx after edit:</p>
			<pre>sudo nginx -t &amp;&amp; sudo systemctl reload nginx</pre>

			<h3>5. Cloudflare 525 — custom error page</h3>
			<p>While origin TLS is broken, CF shows its own 525 — origin splash cannot run. Configure:</p>
			<ol class="steps">
				<li>Cloudflare → your zone → <strong>Error Pages</strong> (or Custom Pages).</li>
				<li>Create page for <strong>525 SSL handshake failed</strong>.</li>
				<li>Point asset URL to backup server: <code>https://YOUR-BACKUP-HOST/epc-platform-splash.html?epc_immediate=1</code> (host splash on laptop via tunnel), or upload static HTML copy.</li>
				<li>When origin is fixed, remove custom page and set mode <code>primary_ok</code>.</li>
			</ol>

			<h3>6. thejewellerytrend.com (525 / SSL handshake)</h3>
			<p>When Cloudflare shows <strong>525 SSL handshake failed</strong>, visitors never reach PHP — only nginx/SSL or the splash via <code>error_page</code> applies. <strong>Cloudflare may show its own 525 page</strong> before origin <code>error_page</code>; use CF custom error asset or grey-cloud DNS if you need the EPC splash on TLS failure.</p>
			<ol class="steps">
				<li>Deploy splash + run <code>epc-jewellery-fast-live.php?token=...&amp;clp_pass=...&amp;apply=1</code> (Model C cert = platform <code>www.ecomae.com</code>, <code>error_page</code> patch, reload).</li>
				<li>Optional: <code>epc-jewellery-clp-site-live.php</code> for dedicated CLP vhost; <code>epc-ssl-activate-tenants.php</code> for LE activate.</li>
				<li>CloudPanel → Sites → <code>www.ecomae.com</code> → <strong>Save</strong> if scripts report reload blocked.</li>
				<li>Docroot: <code>/home/ecomae/htdocs/www.ecomae.com</code> (splash file at <code>epc-platform-splash.html</code>).</li>
				<li>LE via orange cloud fails (520) — grey-cloud A records to <code>31.97.216.247</code>, issue cert, re-proxy.</li>
				<li>Test <a href="https://www.thejewellerytrend.com/epc-platform-splash.html?epc_splash_preview=1&amp;mode=backup_active" target="_blank" rel="noopener">splash preview</a> and <a href="https://www.thejewellerytrend.com/en/" target="_blank" rel="noopener">/en/</a> (expect HTTP 200).</li>
				<li>When fixed: set mode <code>primary_ok</code>.</li>
			</ol>
			<p class="text-muted">Splash copy for unreachable site: headline “Connecting to backup”, subtitle mentions SSL/origin — driven by status <code>primary_down</code>.</p>

			<h3>7. Deploy files from laptop</h3>
			<pre>cd c:\Users\1\Apple\deploy-epartscart
python tools\push_one.py epc-platform-status.php epc-platform-status.json epc-platform-failover.config.json
python tools\push_one.py epc-platform-splash.html epc-failover-snippet.js
python tools\push_one.py content/general_pages/epc_platform_failover.php content/general_pages/epc_platform_failover_banner.php
python tools\push_one.py templates/nero/desktop.php
python tools\push_one.py cp/content/control/portal/epc_platform_failover_guide.php</pre>

			<h3>8. Cloudflare cache (optional)</h3>
			<ul>
				<li>Page Rule or Configuration Rule: if origin unhealthy, serve static asset <code>/epc-platform-splash.html</code> (Workers optional).</li>
				<li>Keep <code>/epc-platform-status.json</code> bypass cache for health dashboards.</li>
			</ul>

			<h3>9. Prevention checklist (404 / 524 / 526)</h3>
			<p class="alert alert-danger" style="margin-bottom:12px"><strong>Do not repeat:</strong> raw <code>nginx reload</code> without CloudPanel <strong>Save</strong>; duplicate tenant files in <code>sites-enabled</code>; missing <code>listen 8080</code> root block.</p>
			<ol class="steps">
				<li><strong>CloudPanel Save only</strong> — Sites → www.ecomae.com → Vhost → paste → <strong>Save</strong>. Never reload nginx alone after manual edits.</li>
				<li><strong>One enabled config</strong> — only <code>www.ecomae.com.conf</code> in <code>sites-enabled</code>. Run <code>epc-nginx-dedupe-tenants.php?apply=1</code> or <code>backups/NGINX-DEDUPE-KODEE.sh</code> if Certbot/orphan tenant configs appear.</li>
				<li><strong>8080 backend</strong> — must include <code>listen 8080</code>, <code>server_name www.ecomae.com</code>, explicit <code>root /home/ecomae/htdocs/www.ecomae.com</code>. Verify: <code>curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1:8080/</code> ≠ 404.</li>
				<li><strong>Jewellery / tenants CF SSL</strong> — Cloudflare → <strong>Full</strong> (not Strict) until platform cert on all tenant SNI blocks. Then Strict OK.</li>
				<li><strong>Health JSON</strong> — <a href="<?php echo epc_ffg_h($platform . '/epc-platform-health-check.php?token=' . rawurlencode($token)); ?>" target="_blank" rel="noopener">epc-platform-health-check.php</a> (all hosts + nginx audit). Full runbook: <code>docs/EPC-OPS-RUNBOOK.md</code>.</li>
			</ol>

			<p class="text-muted">Full doc: repo <code>docs/EPC-FAILOVER-SPLASH-GUIDE.md</code> · Ops runbook: <code>docs/EPC-OPS-RUNBOOK.md</code> · Local mirror: <code>docs/failover-laptop-setup.md</code></p>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>

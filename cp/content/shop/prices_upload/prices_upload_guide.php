<?php
/**
 * CP documentation: all ways prices enter the system, status dashboard, test checklist.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_guide_format_list_names')) {
	function epc_guide_format_list_names($lists)
	{
		$names = array();
		if (!is_array($lists)) {
			return '—';
		}
		foreach ($lists as $l) {
			$names[] = $l['name'] . ' (' . number_format((int)$l['records_count']) . ')';
		}
		return count($names) ? implode(', ', $names) : '—';
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_upload_guide_data.php';

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed: '
			. htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
		return;
	}
}

$config = array(
	'backend_dir' => $DP_Config->backend_dir,
	'domain_path' => $DP_Config->domain_path,
	'tech_key' => $DP_Config->tech_key,
	'tmp_dir_prices_upload' => $DP_Config->tmp_dir_prices_upload,
);
$snapshotError = '';
try {
	$snapshot = epc_guide_snapshot($db_link, $config);
} catch (Exception $e) {
	$snapshotError = $e->getMessage();
	$snapshot = array(
		'generated_at' => date('Y-m-d H:i:s'),
		'load_modes' => array(),
		'by_load_mode' => array(),
		'price_lists_total' => 0,
		'history_by_source' => array(),
		'cron_tasks' => -1,
		'cron_price_links' => -1,
		'pyprices_pending_tasks' => -1,
		'channels' => epc_guide_channel_definitions($config),
	);
}
// Health checks call pyprices/cron over HTTP (slow). Run via AJAX only — not on initial page load.
$channels = $snapshot['channels'];
$byLoadMode = $snapshot['by_load_mode'];
$loadModeNames = $snapshot['load_modes'];
$historyBySource = $snapshot['history_by_source'];
$backend = '/' . $DP_Config->backend_dir;
if ($snapshotError === '' && isset($snapshot['snapshot_error'])) {
	$snapshotError = (string)$snapshot['snapshot_error'];
}
$cronWget = "wget -O /dev/null -q '" . rtrim($DP_Config->domain_path, '/') . $backend
	. "/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=" . urlencode($DP_Config->tech_key) . "'";

$guideUrlPrimary = $backend . '/shop/prices/guide';
$guideUrlAlt = $backend . '/shop/prices?view=guide';
$mailboxUser = trim((string)(isset($DP_Config->prices_email_username) ? $DP_Config->prices_email_username : ''));
$mailboxServer = trim((string)(isset($DP_Config->prices_email_server) ? $DP_Config->prices_email_server : ''));
$mailboxPort = trim((string)(isset($DP_Config->prices_email_port) ? $DP_Config->prices_email_port : ''));
$emailLists = [];
$allPriceListsForGuide = [];
try {
	$eq = $db_link->query(
		'SELECT p.`id`, p.`name`, p.`load_mode`, p.`sender_email`, p.`file_name_substring`, p.`message_header_substring`,
		 p.`clean_before`, p.`not_mark_seen_email_messages`, p.`last_updated`,
		 (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `records_count`
		 FROM `shop_docpart_prices` p ORDER BY p.`name`'
	);
	while ($row = $eq->fetch(PDO::FETCH_ASSOC)) {
		$allPriceListsForGuide[] = $row;
		if ((int)$row['load_mode'] === 3) {
			$emailLists[] = $row;
		}
	}
} catch (Exception $e) {
	$emailLists = [];
	$allPriceListsForGuide = [];
}
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Price upload — documentation &amp; system status
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo $backend; ?>/shop/prices"><i class="fa fa-arrow-left"></i> Back to price lists</a>
				<button type="button" class="btn btn-primary btn-xs" onclick="epcGuideRunHealth();"><i class="fa fa-stethoscope"></i> Run health checks</button>
			</span>
		</div>
		<div class="panel-body">

			<div class="alert alert-info">
				<strong>Open this guide while logged into the control panel.</strong>
				A public link without a session shows the CP login screen (that is normal).
				<ul class="m-t-sm" style="margin-bottom:0;">
					<li><strong>Primary URL:</strong> <a href="<?php echo htmlspecialchars($guideUrlPrimary, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($guideUrlPrimary, ENT_QUOTES, 'UTF-8'); ?></a></li>
					<li><strong>Alternate URL:</strong> <a href="<?php echo htmlspecialchars($guideUrlAlt, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($guideUrlAlt, ENT_QUOTES, 'UTF-8'); ?></a> (same page on the price lists manager)</li>
					<li>From <a href="<?php echo $backend; ?>/shop/prices">Price lists</a> → blue button <strong>Full upload guide &amp; channel status</strong> or the book icon in the top menu.</li>
				</ul>
			</div>

			<p class="text-muted">Generated <?php echo htmlspecialchars($snapshot['generated_at'], ENT_QUOTES, 'UTF-8'); ?>.
				This page describes <strong>every channel</strong> that loads data into <code>shop_docpart_prices_data</code>,
				how many price lists use each source, and how to test each path.</p>
			<?php if ($snapshotError !== ''): ?>
				<div class="alert alert-warning">
					<strong>Partial data:</strong> <?php echo htmlspecialchars($snapshotError, ENT_QUOTES, 'UTF-8'); ?>
				</div>
			<?php endif; ?>

			<!-- System health (loaded asynchronously — avoids 60s+ page timeout) -->
			<h4><i class="fa fa-heartbeat"></i> System health</h4>
			<div id="epc_guide_health_box">
				<p class="text-muted">Click <strong>Run health checks</strong> above to test pyprices, cron, and upload folders (optional).</p>
			</div>

			<!-- Summary counts -->
			<h4><i class="fa fa-bar-chart"></i> Price lists by update source (load_mode)</h4>
			<table class="table table-striped table-bordered">
				<thead>
					<tr>
						<th>Source type</th>
						<th>Price lists</th>
						<th>Total rows in DB</th>
						<th>Lists (names)</th>
					</tr>
				</thead>
				<tbody>
				<?php
				$modeLabels = [
					1 => 'Manual / file from PC (load_mode 1)',
					2 => 'FTP (load_mode 2)',
					3 => 'E-mail (load_mode 3)',
					4 => 'URL / link (load_mode 4)',
				];
				foreach ($modeLabels as $mid => $label):
					$block = isset($byLoadMode[$mid]) ? $byLoadMode[$mid] : array('count' => 0, 'records' => 0, 'lists' => array());
				?>
					<tr>
						<td><strong><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></strong></td>
						<td><?php echo (int)$block['count']; ?></td>
						<td><?php echo number_format((int)$block['records']); ?></td>
						<td><small><?php echo htmlspecialchars(epc_guide_format_list_names($block['lists']), ENT_QUOTES, 'UTF-8'); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<p>
				<strong>Scheduled cron tasks:</strong> <?php echo (int)$snapshot['cron_tasks']; ?> task(s),
				<?php echo (int)$snapshot['cron_price_links']; ?> price list link(s).
				<strong>Pyprices pending tasks:</strong> <?php echo (int)$snapshot['pyprices_pending_tasks'] >= 0 ? (int)$snapshot['pyprices_pending_tasks'] : 'n/a'; ?>.
				<strong>Total price lists:</strong> <?php echo (int)$snapshot['price_lists_total']; ?>.
			</p>

			<h4><i class="fa fa-history"></i> Recent uploads by channel (upload history)</h4>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>upload_source</th><th>Upload count</th><th>Last upload</th></tr></thead>
				<tbody>
				<?php if (count($historyBySource) === 0): ?>
					<tr><td colspan="3"><em>No history yet — upload a test file to populate.</em></td></tr>
				<?php else: ?>
					<?php foreach ($historyBySource as $src => $info): ?>
						<tr>
							<td><code><?php echo htmlspecialchars($src, ENT_QUOTES, 'UTF-8'); ?></code></td>
							<td><?php echo (int)$info['uploads']; ?></td>
							<td><?php echo htmlspecialchars($info['last_at'], ENT_QUOTES, 'UTF-8'); ?></td>
						</tr>
					<?php endforeach; ?>
				<?php endif; ?>
				</tbody>
			</table>

			<hr>

			<h4><i class="fa fa-envelope"></i> E-mail imports: one price list per message (not “all lists”)</h4>
			<div class="alert alert-warning">
				<strong>Important:</strong> A message to the price mailbox does <em>not</em> update every price list.
				Each list in <strong>E-mail</strong> mode has its own rules. Pyprices scans the inbox and, for <strong>each</strong> configured list,
				looks for an <strong>unread</strong> message that matches that list’s sender, subject (if set), and <strong>attachment file name</strong>.
				Only the matching list is updated; other lists are unchanged.
			</div>
			<p>
				<strong>Site mailbox (IMAP):</strong>
				<?php if ($mailboxUser !== ''): ?>
					<code><?php echo htmlspecialchars($mailboxUser, ENT_QUOTES, 'UTF-8'); ?></code>
					on <code><?php echo htmlspecialchars($mailboxServer !== '' ? $mailboxServer : 'imap host', ENT_QUOTES, 'UTF-8'); ?><?php echo $mailboxPort !== '' ? ':' . htmlspecialchars($mailboxPort, ENT_QUOTES, 'UTF-8') : ''; ?></code>
					(settings in <code>config.php</code>: <code>prices_email_*</code>).
				<?php else: ?>
					<span class="text-danger">Not configured — set <code>prices_email_username</code> / password in <code>config.php</code> before e-mail imports work.</span>
				<?php endif; ?>
			</p>
			<table class="table table-bordered table-condensed">
				<thead>
					<tr>
						<th>List</th>
						<th>ID</th>
						<th>Rows in DB</th>
						<th>Sender filter</th>
						<th>Subject must contain</th>
						<th>Attachment name must contain</th>
						<th>Clear before import</th>
						<th>Example subject / file to send</th>
					</tr>
				</thead>
				<tbody>
				<?php if (count($emailLists) === 0): ?>
					<tr><td colspan="8"><em>No price lists in E-mail mode (<code>load_mode = 3</code>). Edit a list → set update mode to E-mail.</em></td></tr>
				<?php else: ?>
					<?php foreach ($emailLists as $el):
						$sub = trim((string)$el['message_header_substring']);
						$fileSub = trim((string)$el['file_name_substring']);
						if ($fileSub === '') {
							$fileSub = (string)$el['name'];
						}
						$sender = trim((string)$el['sender_email']);
						$exSub = $sub !== '' ? $sub : '(any subject)';
						$exFile = $fileSub . '.xlsx or ' . $fileSub . '.csv';
					?>
					<tr>
						<td><strong><?php echo htmlspecialchars((string)$el['name'], ENT_QUOTES, 'UTF-8'); ?></strong>
							<br><a href="<?php echo $backend; ?>/shop/prices/price?price_id=<?php echo (int)$el['id']; ?>">Edit settings</a></td>
						<td><?php echo (int)$el['id']; ?></td>
						<td><?php echo number_format((int)$el['records_count']); ?></td>
						<td><code><?php echo $sender !== '' ? htmlspecialchars($sender, ENT_QUOTES, 'UTF-8') : '—'; ?></code></td>
						<td><code><?php echo $sub !== '' ? htmlspecialchars($sub, ENT_QUOTES, 'UTF-8') : '— (optional)'; ?></code></td>
						<td><code><?php echo htmlspecialchars($fileSub, ENT_QUOTES, 'UTF-8'); ?></code></td>
						<td><?php echo (int)$el['clean_before'] === 1 ? 'Yes' : 'No'; ?></td>
						<td><small>Subject: <?php echo htmlspecialchars($exSub, ENT_QUOTES, 'UTF-8'); ?><br>File: <strong><?php echo htmlspecialchars($exFile, ENT_QUOTES, 'UTF-8'); ?></strong></small></td>
					</tr>
					<?php endforeach; ?>
				<?php endif; ?>
				</tbody>
			</table>
			<p><strong>Supplier checklist (send separately for each list):</strong></p>
			<ol>
				<li>Use the mailbox above (or an address allowed by <em>sender filter</em>).</li>
				<li>Attach <strong>one file per list</strong>; the file name must include the substring in the table (e.g. <code>S-UAE.xlsx</code> → only list S-UAE).</li>
				<li>Subject line only matters if “Subject must contain” is set for that list; words like “price list” alone do <strong>not</strong> route mail to all lists.</li>
				<li>After sending, on <a href="<?php echo $backend; ?>/shop/prices">Price lists</a> click the <strong>E-mail</strong> icon on that row (manual) or wait for cron (scheduled).</li>
				<li>Confirm <strong>Upload history</strong> on the row shows a new file with <strong>Download</strong>.</li>
			</ol>
			<p class="text-muted"><small>Self-sent test mail (same From and To as the mailbox) is often ignored by pyprices (“no new messages”). Prefer mail from an external supplier address, or use the deploy API / CP upload for testing.</small></p>

			<h5>All price lists (file name rules apply to FTP / e-mail / PC upload)</h5>
			<div class="table-responsive">
				<table class="table table-bordered table-condensed table-striped">
					<thead>
						<tr>
							<th>ID</th>
							<th>Name</th>
							<th>Update mode</th>
							<th>Rows</th>
							<th>File name substring</th>
							<th>Last updated</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
					<?php
					$modeShort = [1 => 'Manual / PC', 2 => 'FTP', 3 => 'E-mail', 4 => 'URL'];
					foreach ($allPriceListsForGuide as $pl):
						$lm = (int)$pl['load_mode'];
						$fileSub = trim((string)$pl['file_name_substring']);
						if ($fileSub === '') {
							$fileSub = (string)$pl['name'];
						}
					?>
						<tr<?php echo ($lm === 3) ? ' class="info"' : ''; ?>>
							<td><?php echo (int)$pl['id']; ?></td>
							<td><strong><?php echo htmlspecialchars((string)$pl['name'], ENT_QUOTES, 'UTF-8'); ?></strong></td>
							<td><?php echo htmlspecialchars(isset($modeShort[$lm]) ? $modeShort[$lm] : ('mode ' . $lm), ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo number_format((int)$pl['records_count']); ?></td>
							<td><code><?php echo htmlspecialchars($fileSub, ENT_QUOTES, 'UTF-8'); ?></code></td>
							<td><small><?php echo htmlspecialchars((string)$pl['last_updated'], ENT_QUOTES, 'UTF-8'); ?></small></td>
							<td><a class="btn btn-default btn-xs" href="<?php echo $backend; ?>/shop/prices/price?price_id=<?php echo (int)$pl['id']; ?>">Edit</a></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>

			<h4><i class="fa fa-download"></i> Upload history (download area)</h4>
			<p>Every bulk import should archive the source file under <code>/content/files/price_upload_history/{price_id}/</code> and show it in CP:</p>
			<ul>
				<li><strong>Price lists</strong> → column <em>Update file / history</em> → green download button (active/last file) or <strong>Upload history</strong> modal.</li>
				<li>Active file = latest successful upload for that list; use <strong>Download</strong> to get the CSV/XLSX that was imported.</li>
				<li>If the archived source is missing (temp cleaned before archive), download falls back to <strong>Export current DB</strong> so you still get the live prices.</li>
				<li>Sources in history: <code>cp_wizard</code>, <code>pyprices_upload</code>, <code>pyprices_ftp</code>, <code>pyprices_email</code>, <code>pyprices_url</code>, <code>deploy_api</code>, etc.</li>
			</ul>

			<hr>

			<h4><i class="fa fa-book"></i> Step-by-step: each way to load prices</h4>

			<div class="panel-group" id="epc_guide_accordion">

				<!-- 1 CP Wizard -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_wizard">1. CP upload wizard (file from PC — CSV / Excel / archive)</a></h5></div>
					<div id="guide_wizard" class="panel-collapse collapse in">
						<div class="panel-body">
							<p><span class="label label-primary">Engine</span> PHP steps ajax_1 → ajax_6. <span class="label label-default">History</span> <code>cp_wizard</code></p>
							<ol>
								<li>Create or edit the price list: <a href="<?php echo $backend; ?>/shop/prices/price">Add price list</a> — set column numbers, separator, rows to skip, <em>file name substring</em>.</li>
								<li>Open <a href="<?php echo $backend; ?>/shop/prices">Price lists</a> → green <strong>Upload</strong> on the row, or go to <code>/shop/prices/upload?price_id=ID</code>.</li>
								<li>Choose file (CSV, TXT, or archive). Optionally enable <strong>clean table before import</strong>.</li>
								<li>Wizard runs: prepare temp dir → extract archive → convert Excel → normalize CSV → <strong>import to DB</strong> → enable keys.</li>
								<li>Check row count on the manager page and <strong>Upload history</strong> (skipped lines CSV if any).</li>
							</ol>
							<p><strong>Test:</strong> Use a 5–10 row CSV with known brand/article/price. Confirm <code>records_count</code> increases and history shows <code>cp_wizard</code>.</p>
							<p><strong>Excel:</strong> Supported via step 3 (<code>ajax_3_excel_convert.php</code>). Prefer UTF-8 CSV for large files.</p>
						</div>
					</div>
				</div>

				<!-- 2 Pyprices PC -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_py_pc" class="collapsed">2. Pyprices — upload file from PC (manager row)</a></h5></div>
					<div id="guide_py_pc" class="panel-collapse collapse">
						<div class="panel-body">
							<p><span class="label label-primary">Engine</span> <code>/pyprices/api.py</code> + <code>upload_file.php</code>. <span class="label label-default">History</span> <code>pyprices_upload</code></p>
							<ol>
								<li>Ensure pyprices health checks above are OK.</li>
								<li>On <a href="<?php echo $backend; ?>/shop/prices">Price lists</a>, use the file input on the row (load_mode 1).</li>
								<li>File is staged; a pyprices task runs; <code>external_tasks_account.php</code> polls until done.</li>
								<li>Refresh — <code>last_updated</code> and record count should change.</li>
							</ol>
							<p><strong>Test:</strong> Small CSV matching <code>file_name_substring</code>. Watch browser console / pyprices task log on the row.</p>
						</div>
					</div>
				</div>

				<!-- 3 FTP -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_ftp" class="collapsed">3. Update from FTP</a></h5></div>
					<div id="guide_ftp" class="panel-collapse collapse">
						<div class="panel-body">
							<p><span class="label label-primary">Engine</span> pyprices FTP. Set <strong>load_mode = FTP</strong> on the price list.</p>
							<ol>
								<li>Edit price list → FTP host, user, password, folder, file name substring (and archive substring if zipped).</li>
								<li>Place the correct file on the FTP server.</li>
								<li>Manual test: on manager row, click <strong>FTP</strong> icon in “Manual update” column.</li>
								<li>For automatic updates: add a <strong>schedule</strong> (cron) — see section 6.</li>
							</ol>
							<p><strong>Test:</strong> Manual FTP button first. If OK but schedule fails → configure server cron (section 6).</p>
						</div>
					</div>
				</div>

				<!-- 4 Email -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_email" class="collapsed">4. Update from E-mail (one list per matching message)</a></h5></div>
					<div id="guide_email" class="panel-collapse collapse">
						<div class="panel-body">
							<p><span class="label label-primary">Engine</span> pyprices IMAP. Set <strong>load_mode = E-mail (3)</strong> on each list that should use mail.</p>
							<ol>
								<li>Configure global mailbox in <code>config.php</code>: <code>prices_email_server</code>, <code>prices_email_port</code>, <code>prices_email_encryption</code>, <code>prices_email_username</code>, <code>prices_email_password</code> (Gmail: App Password, IMAP SSL 993).</li>
								<li>Edit each price list → E-mail block: <strong>sender e-mail</strong> (From filter), <strong>subject substring</strong> (optional), <strong>file name substring</strong> (required — usually the list name, e.g. <code>S-UAE</code>).</li>
								<li>Supplier sends <strong>one attachment per list</strong>; filename must contain that list’s substring. Subject “price list” alone does not update all lists.</li>
								<li>Manual test: on that list’s row → <strong>E-mail</strong> icon in “Manual update”. Scheduled: add list to cron schedule (section 6).</li>
								<li>Check <strong>Upload history</strong> for a downloadable copy of the imported file.</li>
							</ol>
							<p><strong>Test:</strong> External sender → attachment <code>ListName.csv</code> → manual E-mail update on that list only → row count and history update.</p>
							<p>See the table <strong>E-mail imports: one price list per message</strong> above for live settings per list.</p>
						</div>
					</div>
				</div>

				<!-- 5 URL -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_url" class="collapsed">5. Update from URL / link</a></h5></div>
					<div id="guide_url" class="panel-collapse collapse">
						<div class="panel-body">
							<p><span class="label label-primary">Engine</span> pyprices URL or wizard download. Set <strong>load_mode = URL</strong> and fill <strong>link</strong> field.</p>
							<ol>
								<li>Edit price list → paste direct file URL in <code>link</code>.</li>
								<li>Manual test: <strong>link</strong> icon in “Manual update”, or open upload wizard (can download from link when no file selected).</li>
								<li>Schedule for automatic pulls if needed.</li>
							</ol>
						</div>
					</div>
				</div>

				<!-- 6 Cron -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_cron" class="collapsed">6. Scheduled automatic update (cron)</a></h5></div>
					<div id="guide_cron" class="panel-collapse collapse">
						<div class="panel-body">
							<p>The pyprices module works correctly when manual updates work. Scheduled updates require a <strong>cron job every minute</strong> on the server.</p>
							<ol>
								<li>On <a href="<?php echo $backend; ?>/shop/prices">Price lists</a>, create a schedule for a test list (FTP/email/URL as configured).</li>
								<li>Add hosting cron task (every minute):</li>
							</ol>
							<pre style="background:#f5f5f5;padding:10px;word-break:break-all;"><?php echo htmlspecialchars($cronWget, ENT_QUOTES, 'UTF-8'); ?></pre>
							<p>Alternative: server crontab with PHP CLI running <code>cron_task_executor.php</code> (see file header in <code>for_cron/cron_task_executor.php</code>).</p>
							<p><strong>Verify:</strong> After 1–2 minutes, <code>last_updated</code> should change without manual click. If manual FTP/email/URL works but schedule does not → cron is not running.</p>
							<p><strong>Options if cron unavailable:</strong> (1) crontab on VPS, (2) migrate to host with cron, (3) hosting panel “Task scheduler” with wget every minute.</p>
						</div>
					</div>
				</div>

				<!-- 7 Commerce S/P/L -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_commerce" class="collapsed">7. Commerce data (sales / purchase / inventory → warehouses)</a></h5></div>
					<div id="guide_commerce" class="panel-collapse collapse">
						<div class="panel-body">
							<p><a class="btn btn-sm btn-primary" href="<?php echo $backend; ?>/shop/prices/commerce">Open Commerce data upload</a></p>
							<ul>
								<li><strong>Sales (*-S)</strong> — highest sales price per brand+article becomes our shelf price; qty summed. List: <code>BASE-S</code>.</li>
								<li><strong>Purchase (*.P)</strong> — one list per supplier; shelf = cost × (1 + margin%). List: <code>SUPPLIER.P</code>.</li>
								<li><strong>Inventory (*-L)</strong> — stock qty + cost/list with margin. List: <code>BASE-L</code>.</li>
							</ul>
							<p>Creates matching Docpart price lists + warehouses (interface type “Simple price list”) so items appear in storefront search.</p>
							<p><strong>Recurring:</strong> re-upload the Excel periodically, or set a file URL on import (stored as URL load mode) and call <code>/epc-upload-commerce-prices.php?action=refresh_url&amp;price_id=…</code>.</p>
							<p><span class="label label-primary">API</span> <code>/epc-upload-commerce-prices.php</code> — POST <code>token</code>, <code>key</code>, <code>role</code>, <code>base_name</code>, <code>margin_percent</code>, <code>price_file</code>.</p>
						</div>
					</div>
				</div>

				<!-- 8 Deploy API -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_deploy" class="collapsed">8. Deploy API (automation / UAE uploads)</a></h5></div>
					<div id="guide_deploy" class="panel-collapse collapse">
						<div class="panel-body">
							<p><span class="label label-primary">Endpoint</span> <code>/epc-upload-uae-prices.php</code> — POST <code>token</code>, <code>key</code> (tech_key), <code>price_file</code>, <code>price_name</code> or <code>price_id</code>.</p>
							<p>Direct PHP import + upload history (<code>deploy_api</code>). Skipped rows: download from Upload history.</p>
							<p><strong>Test:</strong> curl multipart with small CSV; JSON should include <code>history_id</code>, <code>rows_skipped</code>.</p>
						</div>
					</div>
				</div>

				<!-- 9 Legacy API -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_api" class="collapsed">9. Legacy Treelax API</a></h5></div>
					<div id="guide_api" class="panel-collapse collapse">
						<div class="panel-body">
							<p><code>/api/prices/upload_price.php</code> — POST <code>tech_key</code> + file; chains to CP <code>ajax_5_import_csv_to_db.php</code>.</p>
						</div>
					</div>
				</div>

				<!-- 9 Manual edit -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_edit" class="collapsed">9. Manual grid edit (single rows)</a></h5></div>
					<div id="guide_edit" class="panel-collapse collapse">
						<div class="panel-body">
							<p><a href="<?php echo $backend; ?>/shop/prices/prices_edit">Edit price list records</a> — not a file import; for corrections only.</p>
						</div>
					</div>
				</div>

				<!-- 10 Price review -->
				<div class="panel panel-default">
					<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#guide_review" class="collapsed">10. Price review (cross-check / adjust)</a></h5></div>
					<div id="guide_review" class="panel-collapse collapse">
						<div class="panel-body">
							<p><code>/shop/prices/review?price_id=</code> — updates existing prices after import; does not load new catalog from file.</p>
						</div>
					</div>
				</div>

			</div>

			<hr>
			<h4><i class="fa fa-flask"></i> Test environment checklist</h4>
			<table class="table table-bordered">
				<thead><tr><th>Channel</th><th>Sample data</th><th>Pass criteria</th></tr></thead>
				<tbody>
				<?php foreach ($channels as $ch): ?>
					<tr>
						<td><?php echo htmlspecialchars(isset($ch['title']) ? $ch['title'] : '', ENT_QUOTES, 'UTF-8'); ?></td>
						<td><small><?php echo htmlspecialchars(isset($ch['formats']) ? $ch['formats'] : '', ENT_QUOTES, 'UTF-8'); ?></small></td>
						<td><small><?php echo htmlspecialchars(isset($ch['test']) ? $ch['test'] : '', ENT_QUOTES, 'UTF-8'); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<p><strong>Sample CSV for wizard/API test</strong> (header + 2 rows):</p>
			<pre style="background:#f9f9f9;padding:8px;">Brand,Number,Name,Qty,Price,Delivery
TEST,ABC123,Test part,10,99.50,1
TEST,XYZ999,Another part,5,12.00,2</pre>
			<p>Map columns in price list settings to match your file (manufacturer=1, article=2, etc.).</p>

			<p class="text-muted"><small>Regenerate skipped-line report for an old upload: <code>/epc-regenerate-issues-report.php?token=…&amp;key=…&amp;history_id=</code></small></p>
		</div>
	</div>
</div>

<script>
function epcGuideRunHealth() {
	jQuery('#epc_guide_health_box').html('<p class="text-center"><i class="fa fa-spinner fa-spin"></i> Running checks…</p>');
	jQuery.getJSON('/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_epc_price_upload_diagnostics.php', {
		action: 'health',
		csrf_guard_key: '<?php echo htmlspecialchars(isset($user_session['csrf_guard_key']) ? $user_session['csrf_guard_key'] : '', ENT_QUOTES, 'UTF-8'); ?>'
	}).done(function(res) {
		if (!res.status || !res.health) {
			jQuery('#epc_guide_health_box').html('<p class="text-danger">Health check failed.</p>');
			return;
		}
		var html = '<table class="table table-bordered table-condensed"><thead><tr><th>Check</th><th>Status</th><th>Detail</th></tr></thead><tbody>';
		jQuery.each(res.health.checks, function(name, c) {
			html += '<tr><td>' + name.replace(/_/g, ' ') + '</td><td>' +
				(c.ok ? '<span class="label label-success">OK</span>' : '<span class="label label-danger">Fail</span>') +
				'</td><td><small>' + (c.detail || '') + '</small></td></tr>';
		});
		html += '</tbody></table>';
		if (res.health.all_ok) {
			html += '<p class="text-success"><i class="fa fa-check-circle"></i> All checks passed.</p>';
		} else {
			html += '<p class="text-danger"><i class="fa fa-exclamation-triangle"></i> Fix failed items above.</p>';
		}
		if (res.health.cron_wget_example) {
			html += '<p><strong>Cron command:</strong></p><pre style="background:#f5f5f5;padding:8px;">' + res.health.cron_wget_example + '</pre>';
		}
		jQuery('#epc_guide_health_box').html(html);
	}).fail(function() {
		jQuery('#epc_guide_health_box').html('<p class="text-danger">Request failed.</p>');
	});
}
/* Health checks run on button click only — avoids AJAX errors on page load. */
</script>

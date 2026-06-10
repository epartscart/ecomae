<?php
/**
 * Marketing Broadcast — shared panel (eval-safe via DOCUMENT_ROOT).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_broadcast_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_mb_render_hub(): void
{
	global $db_link, $DP_Config;

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!DP_User::isAdmin()) {
		echo '<div class="alert alert-warning">Admin login required for Marketing broadcast.</div>';
		return;
	}

	$pdo = ($db_link instanceof PDO) ? $db_link : null;
	if (!$pdo instanceof PDO) {
		echo '<div class="alert alert-danger">Database unavailable.</div>';
		return;
	}

	epc_mb_ensure_schema($pdo);
	$tab = preg_replace('/[^a-z_]/', '', strtolower((string) ($_GET['tab'] ?? 'email')));
	if ($tab === '') {
		$tab = 'email';
	}

	$shop = epc_mb_shop_context($DP_Config);
	$stats = epc_mb_dashboard_stats($pdo);
	$campaigns = epc_mb_list_campaigns($pdo, 15);
	$groups = epc_mb_list_groups($pdo);
	$emailTemplates = epc_mb_email_templates();
	$waTemplates = epc_mb_whatsapp_templates();
	$csrf = epc_mb_csrf_token();
	$hubBase = epc_mb_hub_url();
	$guideUrl = epc_mb_hub_url('guide');
	$emailSettingsUrl = '/' . epc_mb_backend() . '/control/portal/epc_tenant_email_settings';
	$smtpDiag = epc_auth_smtp_diagnose();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/epc_whatsapp_notify.php';
	$waApi = epc_wa_api_enabled($DP_Config);

	$flash = null;
	if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_mb_action'])) {
		if (!epc_mb_verify_csrf()) {
			$flash = array('ok' => false, 'message' => 'CSRF validation failed. Refresh and try again.');
		} else {
			$action = (string) $_POST['epc_mb_action'];
			$operatorId = (int) (DP_User::getUserId() ?? 0);
			if ($action === 'send_email') {
				$flash = epc_mb_send_email_campaign($pdo, $_POST, $DP_Config, $operatorId);
			} elseif ($action === 'send_whatsapp') {
				$flash = epc_mb_send_whatsapp_campaign($pdo, $_POST, $DP_Config, $operatorId);
			}
			if ($flash) {
				$stats = epc_mb_dashboard_stats($pdo);
				$campaigns = epc_mb_list_campaigns($pdo, 15);
			}
		}
	}

	epc_cp_page_frame_open(array(
		'class' => 'epc-mb-hub',
		'hero' => array(
			'badge' => 'Marketing broadcast',
			'title' => $shop['shop_name'] . ' — Email & WhatsApp marketing',
			'sub' => 'Send brochure emails and WhatsApp messages to multiple customers at once. Uses your tenant SMTP and wa.me links (or WhatsApp API if enabled).',
			'actions' => array(
				array('label' => 'Guide', 'icon' => 'fa-book', 'url' => $guideUrl, 'primary' => true),
				array('label' => 'Email SMTP', 'icon' => 'fa-envelope', 'url' => $emailSettingsUrl),
			),
		),
	));

	if ($flash !== null) {
		$cls = !empty($flash['ok']) ? 'success' : 'danger';
		echo '<div class="alert alert-' . $cls . '">' . epc_mb_h($flash['message'] ?? '') . '</div>';
		if (!empty($flash['wa_links']) && is_array($flash['wa_links'])) {
			echo '<div class="epc-mb-wa-links"><h4><i class="fa fa-whatsapp"></i> wa.me links — open each to send</h4><ul class="list-unstyled">';
			foreach ($flash['wa_links'] as $wl) {
				if (empty($wl['link'])) {
					continue;
				}
				echo '<li style="margin-bottom:8px"><a class="btn btn-success btn-sm" target="_blank" rel="noopener" href="' . epc_mb_h($wl['link']) . '">'
					. '<i class="fa fa-whatsapp"></i> ' . epc_mb_h($wl['name'] ?? $wl['phone'] ?? 'Customer') . '</a></li>';
			}
			echo '</ul></div>';
		}
	}

	echo '<div class="epc-mb-kpi">';
	foreach (array(
		array('val' => $stats['email_recipients'], 'label' => 'Customers with email'),
		array('val' => $stats['whatsapp_recipients'], 'label' => 'Customers with phone'),
		array('val' => $stats['emails_sent'], 'label' => 'Emails sent (total)'),
		array('val' => $stats['whatsapp_sent'], 'label' => 'WhatsApp sent (total)'),
		array('val' => $stats['campaigns'], 'label' => 'Campaigns'),
	) as $kpi) {
		echo '<div class="epc-mb-kpi__item"><div class="epc-mb-kpi__val">' . (int) $kpi['val'] . '</div><div class="epc-mb-kpi__label">' . epc_mb_h($kpi['label']) . '</div></div>';
	}
	echo '</div>';

	$tabs = array(
		'email' => array('label' => 'Email marketing', 'icon' => 'fa-envelope'),
		'whatsapp' => array('label' => 'WhatsApp marketing', 'icon' => 'fa-whatsapp'),
		'history' => array('label' => 'Campaign history', 'icon' => 'fa-history'),
		'guide' => array('label' => 'Guide', 'icon' => 'fa-book'),
	);
	echo '<div class="epc-mb-tabs">';
	foreach ($tabs as $key => $t) {
		$active = $tab === $key ? ' btn-primary' : ' btn-default';
		echo '<a class="btn btn-sm' . $active . '" href="' . epc_mb_h(epc_mb_hub_url($key)) . '"><i class="fa ' . epc_mb_h($t['icon']) . '"></i> ' . epc_mb_h($t['label']) . '</a>';
	}
	echo '</div>';

	if ($tab === 'email') {
		epc_mb_render_email_tab($pdo, $emailTemplates, $groups, $csrf, $smtpDiag);
	} elseif ($tab === 'whatsapp') {
		epc_mb_render_whatsapp_tab($pdo, $waTemplates, $groups, $csrf, $waApi);
	} elseif ($tab === 'history') {
		epc_mb_render_history_tab($campaigns);
	} else {
		epc_mb_render_guide_tab($shop, $emailSettingsUrl, $guideUrl, $waApi);
	}

	epc_cp_page_frame_close();
}

function epc_mb_render_email_tab(PDO $pdo, array $templates, array $groups, string $csrf, array $smtpDiag): void
{
	$smtpOk = !empty($smtpDiag['ok']);
	echo '<div class="epc-mb-panel">';
	if (!$smtpOk) {
		echo '<div class="alert alert-warning"><i class="fa fa-exclamation-triangle"></i> SMTP not ready: ' . epc_mb_h(implode(' ', $smtpDiag['issues'] ?? array())) . '. Configure under Integrations → Email / SMTP.</div>';
	}
	echo '<form method="post" class="epc-mb-form" id="epc-mb-email-form">';
	echo '<input type="hidden" name="csrf_token" value="' . epc_mb_h($csrf) . '">';
	echo '<input type="hidden" name="epc_mb_action" value="send_email">';

	echo '<div class="row"><div class="col-md-6">';
	echo '<h4><i class="fa fa-users"></i> Audience</h4>';
	echo '<div class="form-group"><label>Send to</label><select name="audience_mode" class="form-control epc-mb-audience-mode" data-channel="email">';
	echo '<option value="all">All customers with email</option>';
	echo '<option value="with_orders">Customers with orders</option>';
	echo '<option value="group">Customer group / segment</option>';
	echo '<option value="manual">Manual list (paste emails)</option>';
	echo '</select></div>';
	echo '<div class="form-group epc-mb-group-select" style="display:none"><label>Group</label><select name="audience_meta_group" class="form-control"><option value="">— Select —</option>';
	foreach ($groups as $g) {
		echo '<option value="' . (int) $g['id'] . '">' . epc_mb_h($g['name'] ?? ('Group #' . $g['id'])) . '</option>';
	}
	echo '</select></div>';
	echo '<div class="form-group epc-mb-manual-input" style="display:none"><label>Paste emails (one per line)</label>';
	echo '<textarea name="audience_meta_manual" class="form-control" rows="4" placeholder="customer@example.com"></textarea></div>';
	echo '<input type="hidden" name="audience_meta" id="epc-mb-email-audience-meta" value="">';
	echo '<p class="help-block"><span id="epc-mb-email-count">—</span> recipients (preview)</p>';
	echo '</div><div class="col-md-6">';
	echo '<h4><i class="fa fa-file-text-o"></i> Brochure template</h4>';
	echo '<div class="form-group"><label>Template</label><select name="template_key" class="form-control epc-mb-template-select" data-channel="email">';
	foreach ($templates as $key => $tpl) {
		echo '<option value="' . epc_mb_h($key) . '">' . epc_mb_h($tpl['label']) . '</option>';
	}
	echo '</select></div>';
	echo '<div class="form-group"><label>Subject</label><input type="text" name="subject" class="form-control" placeholder="Email subject"></div>';
	echo '<div class="form-group"><label>Preview text</label><input type="text" name="preview" class="form-control" placeholder="Inbox preview snippet"></div>';
	echo '</div></div>';
	echo '<div class="form-group"><label>HTML body</label><textarea name="body_html" class="form-control epc-mb-html-body" rows="12" placeholder="Leave blank to use template HTML"></textarea>';
	echo '<p class="help-block">Variables: <code>{{customer_name}}</code> <code>{{shop_name}}</code> <code>{{shop_url}}</code></p></div>';
	echo '<div class="form-group"><label>Batch limit</label><input type="number" name="batch_limit" class="form-control" value="50" min="1" max="100" style="max-width:120px">';
	echo '<p class="help-block">Max recipients per send (rate-limited ~5/sec).</p></div>';
	echo '<button type="submit" class="btn btn-primary"' . ($smtpOk ? '' : ' disabled') . '><i class="fa fa-paper-plane"></i> Send email campaign</button>';
	echo '</form></div>';
}

function epc_mb_render_whatsapp_tab(PDO $pdo, array $templates, array $groups, string $csrf, bool $waApi): void
{
	echo '<div class="epc-mb-panel">';
	if ($waApi) {
		echo '<div class="alert alert-success"><i class="fa fa-check"></i> WhatsApp Cloud API is enabled — messages will be sent automatically.</div>';
	} else {
		echo '<div class="alert alert-info"><i class="fa fa-info-circle"></i> Phase 1 mode: wa.me links will be generated. Open each link in WhatsApp Web/desktop to send manually. Enable API in Configuration for auto-send.</div>';
	}
	echo '<form method="post" class="epc-mb-form" id="epc-mb-wa-form">';
	echo '<input type="hidden" name="csrf_token" value="' . epc_mb_h($csrf) . '">';
	echo '<input type="hidden" name="epc_mb_action" value="send_whatsapp">';

	echo '<div class="row"><div class="col-md-6">';
	echo '<h4><i class="fa fa-users"></i> Audience</h4>';
	echo '<div class="form-group"><label>Send to</label><select name="audience_mode" class="form-control epc-mb-audience-mode" data-channel="whatsapp">';
	echo '<option value="all">All customers with phone</option>';
	echo '<option value="with_orders">Customers with orders</option>';
	echo '<option value="group">Customer group / segment</option>';
	echo '<option value="manual">Manual list (paste phones)</option>';
	echo '</select></div>';
	echo '<div class="form-group epc-mb-group-select" style="display:none"><label>Group</label><select name="audience_meta_group" class="form-control"><option value="">— Select —</option>';
	foreach ($groups as $g) {
		echo '<option value="' . (int) $g['id'] . '">' . epc_mb_h($g['name'] ?? ('Group #' . $g['id'])) . '</option>';
	}
	echo '</select></div>';
	echo '<div class="form-group epc-mb-manual-input" style="display:none"><label>Paste phone numbers</label>';
	echo '<textarea name="audience_meta_manual" class="form-control" rows="4" placeholder="+971501234567"></textarea></div>';
	echo '<input type="hidden" name="audience_meta" id="epc-mb-wa-audience-meta" value="">';
	echo '<p class="help-block"><span id="epc-mb-wa-count">—</span> recipients (preview)</p>';
	echo '</div><div class="col-md-6">';
	echo '<h4><i class="fa fa-whatsapp"></i> Brochure / procedure template</h4>';
	echo '<div class="form-group"><label>Template</label><select name="template_key" class="form-control epc-mb-template-select" data-channel="whatsapp">';
	foreach ($templates as $key => $tpl) {
		echo '<option value="' . epc_mb_h($key) . '">' . epc_mb_h($tpl['label']) . '</option>';
	}
	echo '</select></div>';
	echo '</div></div>';
	echo '<div class="form-group"><label>Message body</label><textarea name="body_text" class="form-control epc-mb-wa-body" rows="10" placeholder="Leave blank to use template"></textarea>';
	echo '<p class="help-block">Variables: <code>{{customer_name}}</code> <code>{{shop_name}}</code> <code>{{shop_url}}</code>. Bilingual EN+AR recommended for UAE.</p></div>';
	echo '<div class="form-group"><label>Batch limit</label><input type="number" name="batch_limit" class="form-control" value="50" min="1" max="100" style="max-width:120px"></div>';
	echo '<button type="submit" class="btn btn-success"><i class="fa fa-whatsapp"></i> ' . ($waApi ? 'Send WhatsApp campaign' : 'Prepare wa.me links') . '</button>';
	echo '</form></div>';
}

function epc_mb_render_history_tab(array $campaigns): void
{
	echo '<div class="epc-mb-panel"><h4><i class="fa fa-history"></i> Recent campaigns</h4>';
	if (!$campaigns) {
		echo '<p class="text-muted">No campaigns yet. Send your first email or WhatsApp broadcast.</p>';
		return;
	}
	echo '<table class="table table-bordered table-condensed"><thead><tr>';
	echo '<th>ID</th><th>Date</th><th>Channel</th><th>Audience</th><th>Targets</th><th>OK</th><th>Failed</th><th>Status</th>';
	echo '</tr></thead><tbody>';
	foreach ($campaigns as $c) {
		echo '<tr>';
		echo '<td>' . (int) $c['id'] . '</td>';
		echo '<td>' . epc_mb_h(date('Y-m-d H:i', (int) $c['created_at'])) . '</td>';
		echo '<td>' . epc_mb_h($c['channel']) . '</td>';
		echo '<td>' . epc_mb_h($c['audience_mode']) . '</td>';
		echo '<td>' . (int) $c['total_targets'] . '</td>';
		echo '<td class="text-success">' . (int) $c['sent_ok'] . '</td>';
		echo '<td class="text-danger">' . (int) $c['sent_fail'] . '</td>';
		echo '<td>' . epc_mb_h($c['status']) . '</td>';
		echo '</tr>';
	}
	echo '</tbody></table></div>';
}

function epc_mb_render_guide_tab(array $shop, string $emailSettingsUrl, string $guideUrl, bool $waApi): void
{
	$steps = array(
		array(
			'title' => 'Configure email (SMTP)',
			'body' => '<p>Go to <a href="' . epc_mb_h($emailSettingsUrl) . '">Integrations → Email / SMTP</a> and enter your tenant mailbox (Gmail App Password, Hostinger, etc.). Run <strong>Test send</strong> before bulk campaigns.</p>',
		),
		array(
			'title' => 'Choose your audience',
			'body' => '<p>Select <strong>all customers</strong>, <strong>customers with orders</strong>, a <strong>user group</strong> (price/access segment), or <strong>paste a manual list</strong>. Each tenant uses its own customer database.</p>',
		),
		array(
			'title' => 'Pick a brochure template',
			'body' => '<p>Email tab: HTML brochures with <code>{{customer_name}}</code> and shop URL. WhatsApp tab: bilingual EN+AR procedure templates. Edit subject/body before sending.</p>',
		),
		array(
			'title' => 'Send email campaign',
			'body' => '<p>Click <strong>Send email campaign</strong>. Up to 100 recipients per batch with rate limiting. Status and counts appear in <strong>Campaign history</strong>.</p>',
		),
		array(
			'title' => 'Send WhatsApp marketing',
			'body' => $waApi
				? '<p>WhatsApp Cloud API is on — messages send automatically via Meta Graph API. Check <code>epc_whatsapp_notify_log</code> for delivery details.</p>'
				: '<p><strong>wa.me mode:</strong> System prepares one link per customer. Open each green button to send from WhatsApp Web. For bulk auto-send, enable WhatsApp API in Configuration (<code>epc_whatsapp_api_enabled</code>).</p>',
		),
		array(
			'title' => 'UAE compliance — opt-in & consent',
			'body' => '<div class="alert alert-warning" style="margin-top:8px"><strong>Important (UAE / TRA / DIFC best practice):</strong><ul>'
				. '<li>Only message customers who <strong>opted in</strong> (registration, order, or explicit consent).</li>'
				. '<li>Include your trade name and a way to opt out (reply STOP or contact email).</li>'
				. '<li>Commercial messages via WhatsApp require valid opt-in under Meta Business Policy.</li>'
				. '<li>Respect quiet hours and avoid misleading promotions (UAE Consumer Protection).</li>'
				. '<li>Keep records of campaigns in <strong>Campaign history</strong> for audit.</li></ul></div>',
		),
	);

	echo '<div class="epc-mb-panel epc-mb-guide">';
	echo '<div class="alert alert-info"><i class="fa fa-book"></i> Guide URL: <a href="' . epc_mb_h($guideUrl) . '"><code>' . epc_mb_h($guideUrl) . '</code></a></div>';
	echo '<p>Marketing broadcast for <strong>' . epc_mb_h($shop['shop_name']) . '</strong> — bulk email brochures and WhatsApp messages from tenant CP.</p>';
	foreach ($steps as $i => $step) {
		echo '<div class="epc-mb-guide-step"><h5 style="margin:0 0 6px">Step ' . ($i + 1) . ' — ' . epc_mb_h($step['title']) . '</h5><div>' . $step['body'] . '</div></div>';
	}
	echo '</div>';
}

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
	$guideUrl = epc_mb_hub_url('guide');
	$emailSettingsUrl = '/' . epc_mb_backend() . '/control/portal/epc_tenant_email_settings';
	$integrationsUrl = '/' . epc_mb_backend() . '/control/portal/epc_integrations_hub';
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

	// Brand bar is the single hero — skip the generic frame hero to avoid double headers.
	epc_cp_page_frame_open(array('class' => 'epc-mb-hub'));

	echo '<header class="epc-mb-brandbar">';
	echo '<div class="epc-mb-brandbar__mark"><i class="fa fa-bullhorn" aria-hidden="true"></i></div>';
	echo '<div><div class="epc-mb-brandbar__name">Marketing broadcast</div>';
	echo '<div class="epc-mb-brandbar__sub">' . epc_mb_h($shop['shop_name']) . ' — Email &amp; WhatsApp · audience → template → preview → send</div></div>';
	echo '<div class="epc-mb-brandbar__actions">';
	echo '<a class="epc-mb-chip-link" href="' . epc_mb_h($guideUrl) . '"><i class="fa fa-book"></i> Guide</a>';
	echo '<a class="epc-mb-chip-link" href="' . epc_mb_h($emailSettingsUrl) . '"><i class="fa fa-envelope"></i> Email SMTP</a>';
	echo '<a class="epc-mb-chip-link" href="' . epc_mb_h($integrationsUrl) . '"><i class="fa fa-plug"></i> Integrations</a>';
	echo '</div></header>';

	if ($flash !== null) {
		$cls = !empty($flash['ok']) ? 'success' : 'danger';
		echo '<div class="alert alert-' . $cls . '">' . epc_mb_h($flash['message'] ?? '') . '</div>';
		if (!empty($flash['wa_links']) && is_array($flash['wa_links'])) {
			echo '<div class="epc-mb-wa-links"><h4><i class="fa fa-whatsapp"></i> wa.me links — open each to send</h4>';
			foreach ($flash['wa_links'] as $wl) {
				if (empty($wl['link'])) {
					continue;
				}
				echo '<a class="btn btn-success btn-sm" target="_blank" rel="noopener" href="' . epc_mb_h($wl['link']) . '">'
					. '<i class="fa fa-whatsapp"></i> ' . epc_mb_h($wl['name'] ?? $wl['phone'] ?? 'Customer') . '</a>';
			}
			echo '</div>';
		}
	}

	$kpis = array(
		array('val' => $stats['email_recipients'], 'label' => 'Customers with email', 'icon' => 'fa-envelope', 'cls' => 'mail'),
		array('val' => $stats['whatsapp_recipients'], 'label' => 'Customers with phone', 'icon' => 'fa-whatsapp', 'cls' => 'wa'),
		array('val' => $stats['emails_sent'], 'label' => 'Emails sent', 'icon' => 'fa-paper-plane', 'cls' => 'mail'),
		array('val' => $stats['whatsapp_sent'], 'label' => 'WhatsApp sent', 'icon' => 'fa-comments', 'cls' => 'wa'),
		array('val' => $stats['campaigns'], 'label' => 'Campaigns', 'icon' => 'fa-flag', 'cls' => ''),
	);
	echo '<div class="epc-mb-kpi" role="group" aria-label="Broadcast metrics">';
	foreach ($kpis as $kpi) {
		$iconCls = $kpi['cls'] !== '' ? ' epc-mb-kpi__icon--' . $kpi['cls'] : '';
		echo '<div class="epc-mb-kpi__item"><div class="epc-mb-kpi__icon' . $iconCls . '"><i class="fa ' . epc_mb_h($kpi['icon']) . '"></i></div>';
		echo '<div><div class="epc-mb-kpi__val">' . (int) $kpi['val'] . '</div><div class="epc-mb-kpi__label">' . epc_mb_h($kpi['label']) . '</div></div></div>';
	}
	echo '</div>';

	$tabs = array(
		'email' => array('label' => 'Email', 'icon' => 'fa-envelope'),
		'whatsapp' => array('label' => 'WhatsApp', 'icon' => 'fa-whatsapp'),
		'history' => array('label' => 'History', 'icon' => 'fa-history'),
		'guide' => array('label' => 'Guide', 'icon' => 'fa-book'),
	);
	echo '<nav class="epc-mb-tabs" aria-label="Broadcast sections">';
	foreach ($tabs as $key => $t) {
		$active = $tab === $key ? ' is-active' : '';
		echo '<a class="' . $active . '" href="' . epc_mb_h(epc_mb_hub_url($key)) . '"><i class="fa ' . epc_mb_h($t['icon']) . '"></i> ' . epc_mb_h($t['label']) . '</a>';
	}
	echo '</nav>';

	if ($tab === 'email') {
		epc_mb_render_email_tab($pdo, $emailTemplates, $groups, $csrf, $smtpDiag, $emailSettingsUrl);
	} elseif ($tab === 'whatsapp') {
		epc_mb_render_whatsapp_tab($pdo, $waTemplates, $groups, $csrf, $waApi);
	} elseif ($tab === 'history') {
		epc_mb_render_history_tab($campaigns);
	} else {
		epc_mb_render_guide_tab($shop, $emailSettingsUrl, $guideUrl, $waApi, $integrationsUrl);
	}

	epc_cp_page_frame_close();
}

/**
 * @param array<string,mixed> $smtpDiag
 * @param array<int,array<string,mixed>> $groups
 * @param array<string,array<string,mixed>> $templates
 */
function epc_mb_render_email_tab(PDO $pdo, array $templates, array $groups, string $csrf, array $smtpDiag, string $emailSettingsUrl): void
{
	$smtpOk = !empty($smtpDiag['ok']);
	echo '<div class="epc-mb-status-row">';
	if ($smtpOk) {
		echo '<span class="epc-mb-badge epc-mb-badge--ok"><i class="fa fa-check-circle"></i> SMTP ready</span>';
	} else {
		echo '<span class="epc-mb-badge epc-mb-badge--warn"><i class="fa fa-exclamation-triangle"></i> SMTP not ready</span>';
		echo '<a class="btn btn-xs btn-default" href="' . epc_mb_h($emailSettingsUrl) . '">Configure SMTP</a>';
	}
	echo '<span class="epc-mb-badge epc-mb-badge--info"><i class="fa fa-info-circle"></i> Rate-limited ~5/sec · max 100 / batch</span>';
	echo '</div>';

	if (!$smtpOk) {
		echo '<div class="alert alert-warning"><i class="fa fa-exclamation-triangle"></i> SMTP not ready: '
			. epc_mb_h(implode(' ', $smtpDiag['issues'] ?? array()))
			. '. Configure under <a href="' . epc_mb_h($emailSettingsUrl) . '">Email / SMTP</a> and run a test send first.</div>';
	}

	echo '<form method="post" class="epc-mb-form" id="epc-mb-email-form">';
	echo '<input type="hidden" name="csrf_token" value="' . epc_mb_h($csrf) . '">';
	echo '<input type="hidden" name="epc_mb_action" value="send_email">';
	$emailTplKeys = array_keys($templates);
	$emailTplDefault = (string) ($emailTplKeys[0] ?? 'promo_sale');
	echo '<input type="hidden" name="template_key" id="epc-mb-email-template-key" value="' . epc_mb_h($emailTplDefault) . '">';

	echo '<div class="epc-mb-compose">';
	echo '<div>';

	echo '<div class="epc-mb-panel"><div class="epc-mb-panel__head"><h4><i class="fa fa-users"></i> Audience</h4><span class="epc-mb-panel__hint">Who receives this email</span></div><div class="epc-mb-panel__body">';
	echo '<div class="epc-mb-step"><div class="epc-mb-step__label"><span class="epc-mb-step__num">1</span> Send to</div>';
	echo '<div class="epc-mb-modes">';
	$modes = array(
		'all' => array('All with email', 'Every customer with an email on file'),
		'with_orders' => array('With orders', 'Customers who have placed orders'),
		'group' => array('Group / segment', 'Price or access group'),
		'manual' => array('Manual list', 'Paste emails, one per line'),
	);
	$first = true;
	foreach ($modes as $val => $meta) {
		echo '<label class="epc-mb-mode' . ($first ? ' is-active' : '') . '"><input type="radio" name="audience_mode" class="epc-mb-audience-mode" data-channel="email" value="' . epc_mb_h($val) . '"' . ($first ? ' checked' : '') . '>';
		echo '<strong>' . epc_mb_h($meta[0]) . '</strong><span>' . epc_mb_h($meta[1]) . '</span></label>';
		$first = false;
	}
	echo '</div>';
	/* keep select for JS compatibility — hidden sync from radios */
	echo '<div class="form-group epc-mb-group-select" style="display:none;margin-top:10px"><label>Group</label><select name="audience_meta_group" class="form-control"><option value="">— Select —</option>';
	foreach ($groups as $g) {
		echo '<option value="' . (int) $g['id'] . '">' . epc_mb_h($g['name'] ?? ('Group #' . $g['id'])) . '</option>';
	}
	echo '</select></div>';
	echo '<div class="form-group epc-mb-manual-input" style="display:none;margin-top:10px"><label>Paste emails (one per line)</label>';
	echo '<textarea name="audience_meta_manual" class="form-control" rows="4" placeholder="customer@example.com"></textarea></div>';
	echo '<input type="hidden" name="audience_meta" id="epc-mb-email-audience-meta" value="">';
	echo '<div class="epc-mb-count"><i class="fa fa-user"></i> <span id="epc-mb-email-count">—</span></div>';
	echo '</div></div></div>';

	echo '<div class="epc-mb-panel"><div class="epc-mb-panel__head"><h4><i class="fa fa-file-text-o"></i> Template &amp; message</h4><span class="epc-mb-panel__hint">Step 2–3</span></div><div class="epc-mb-panel__body">';
	echo '<div class="epc-mb-step"><div class="epc-mb-step__label"><span class="epc-mb-step__num">2</span> Brochure template</div>';
	echo '<div class="epc-mb-templates" id="epc-mb-email-templates">';
	$ti = 0;
	foreach ($templates as $key => $tpl) {
		echo '<button type="button" class="epc-mb-tpl' . ($ti === 0 ? ' is-active' : '') . '" data-template="' . epc_mb_h($key) . '" data-channel="email">';
		echo '<div class="epc-mb-tpl__icon"><i class="fa fa-file-text-o"></i></div>';
		echo '<div class="epc-mb-tpl__label">' . epc_mb_h($tpl['label']) . '</div></button>';
		$ti++;
	}
	echo '</div>';
	echo '<select class="epc-mb-template-select form-control" data-channel="email" style="position:absolute;left:-9999px;width:1px;height:1px;opacity:0" tabindex="-1" aria-hidden="true">';
	foreach ($templates as $key => $tpl) {
		echo '<option value="' . epc_mb_h($key) . '">' . epc_mb_h($tpl['label']) . '</option>';
	}
	echo '</select></div>';

	echo '<div class="epc-mb-step"><div class="epc-mb-step__label"><span class="epc-mb-step__num">3</span> Subject &amp; body</div>';
	echo '<div class="epc-mb-field"><label for="epc-mb-email-subject">Subject</label><input type="text" name="subject" id="epc-mb-email-subject" class="form-control" placeholder="Email subject"></div>';
	echo '<div class="epc-mb-field"><label for="epc-mb-email-preview">Inbox preview text</label><input type="text" name="preview" id="epc-mb-email-preview" class="form-control" placeholder="Short snippet under the subject"></div>';
	echo '<div class="epc-mb-field"><label for="epc-mb-email-body">HTML body</label>';
	echo '<textarea name="body_html" id="epc-mb-email-body" class="form-control epc-mb-html-body" rows="12" placeholder="Leave blank to use template HTML"></textarea>';
	echo '<div class="epc-mb-vars"><code>{{customer_name}}</code><code>{{shop_name}}</code><code>{{shop_url}}</code></div></div>';
	echo '<div class="epc-mb-field"><label for="epc-mb-email-batch">Batch limit</label>';
	echo '<input type="number" name="batch_limit" id="epc-mb-email-batch" class="form-control" value="50" min="1" max="100" style="max-width:140px">';
	echo '<p class="help-block" style="margin:6px 0 0">Max recipients this send (1–100). Sending is paced to protect SMTP reputation.</p></div>';
	echo '</div>';

	echo '<div class="epc-mb-actions">';
	echo '<button type="submit" class="btn btn-primary"' . ($smtpOk ? '' : ' disabled') . ' data-confirm="Send this email campaign to the selected audience?"><i class="fa fa-paper-plane"></i> Send email campaign</button>';
	echo '<button type="button" class="btn btn-default" id="epc-mb-email-reload-tpl"><i class="fa fa-refresh"></i> Reload template</button>';
	echo '</div>';
	echo '</div></div>';

	echo '</div>'; /* left */

	echo '<aside class="epc-mb-panel epc-mb-preview"><div class="epc-mb-panel__head"><h4><i class="fa fa-eye"></i> Live email preview</h4><span class="epc-mb-panel__hint">Updates as you type</span></div>';
	echo '<div class="epc-mb-panel__body"><div class="epc-mb-preview__frame"><iframe id="epc-mb-email-preview-frame" title="Email preview" sandbox=""></iframe></div>';
	echo '<p class="help-block" style="margin:10px 0 0">Preview uses sample name “Customer”. Merge tags resolve per recipient on send.</p></div></aside>';

	echo '</div></form>';
}

/**
 * @param array<string,array<string,mixed>> $templates
 * @param array<int,array<string,mixed>> $groups
 */
function epc_mb_render_whatsapp_tab(PDO $pdo, array $templates, array $groups, string $csrf, bool $waApi): void
{
	echo '<div class="epc-mb-status-row">';
	if ($waApi) {
		echo '<span class="epc-mb-badge epc-mb-badge--ok"><i class="fa fa-check-circle"></i> WhatsApp Cloud API enabled</span>';
	} else {
		echo '<span class="epc-mb-badge epc-mb-badge--info"><i class="fa fa-link"></i> wa.me mode — prepare links, then open in WhatsApp</span>';
	}
	echo '<span class="epc-mb-badge epc-mb-badge--info"><i class="fa fa-language"></i> Bilingual EN + AR recommended (UAE)</span>';
	echo '</div>';

	if (!$waApi) {
		echo '<div class="alert alert-info"><i class="fa fa-info-circle"></i> Phase 1: we generate one <strong>wa.me</strong> link per customer. Open each to send from WhatsApp Web/desktop. Enable WhatsApp API in Configuration for automatic send.</div>';
	} else {
		echo '<div class="alert alert-success"><i class="fa fa-check"></i> WhatsApp Cloud API is enabled — messages will be sent automatically.</div>';
	}

	echo '<form method="post" class="epc-mb-form" id="epc-mb-wa-form">';
	echo '<input type="hidden" name="csrf_token" value="' . epc_mb_h($csrf) . '">';
	echo '<input type="hidden" name="epc_mb_action" value="send_whatsapp">';
	$waTplKeys = array_keys($templates);
	$waTplDefault = (string) ($waTplKeys[0] ?? 'promo_bilingual');
	echo '<input type="hidden" name="template_key" id="epc-mb-wa-template-key" value="' . epc_mb_h($waTplDefault) . '">';

	echo '<div class="epc-mb-compose">';
	echo '<div>';

	echo '<div class="epc-mb-panel"><div class="epc-mb-panel__head"><h4><i class="fa fa-users"></i> Audience</h4><span class="epc-mb-panel__hint">Who receives WhatsApp</span></div><div class="epc-mb-panel__body">';
	echo '<div class="epc-mb-step"><div class="epc-mb-step__label"><span class="epc-mb-step__num">1</span> Send to</div>';
	echo '<div class="epc-mb-modes">';
	$modes = array(
		'all' => array('All with phone', 'Every customer with a phone number'),
		'with_orders' => array('With orders', 'Customers who have placed orders'),
		'group' => array('Group / segment', 'Price or access group'),
		'manual' => array('Manual list', 'Paste phones, one per line'),
	);
	$first = true;
	foreach ($modes as $val => $meta) {
		echo '<label class="epc-mb-mode' . ($first ? ' is-active' : '') . '"><input type="radio" name="audience_mode" class="epc-mb-audience-mode" data-channel="whatsapp" value="' . epc_mb_h($val) . '"' . ($first ? ' checked' : '') . '>';
		echo '<strong>' . epc_mb_h($meta[0]) . '</strong><span>' . epc_mb_h($meta[1]) . '</span></label>';
		$first = false;
	}
	echo '</div>';
	echo '<div class="form-group epc-mb-group-select" style="display:none;margin-top:10px"><label>Group</label><select name="audience_meta_group" class="form-control"><option value="">— Select —</option>';
	foreach ($groups as $g) {
		echo '<option value="' . (int) $g['id'] . '">' . epc_mb_h($g['name'] ?? ('Group #' . $g['id'])) . '</option>';
	}
	echo '</select></div>';
	echo '<div class="form-group epc-mb-manual-input" style="display:none;margin-top:10px"><label>Paste phone numbers</label>';
	echo '<textarea name="audience_meta_manual" class="form-control" rows="4" placeholder="+971501234567"></textarea></div>';
	echo '<input type="hidden" name="audience_meta" id="epc-mb-wa-audience-meta" value="">';
	echo '<div class="epc-mb-count"><i class="fa fa-mobile"></i> <span id="epc-mb-wa-count">—</span></div>';
	echo '</div></div></div>';

	echo '<div class="epc-mb-panel"><div class="epc-mb-panel__head"><h4><i class="fa fa-whatsapp"></i> Template &amp; message</h4></div><div class="epc-mb-panel__body">';
	echo '<div class="epc-mb-step"><div class="epc-mb-step__label"><span class="epc-mb-step__num">2</span> Message template</div>';
	echo '<div class="epc-mb-templates" id="epc-mb-wa-templates">';
	$ti = 0;
	foreach ($templates as $key => $tpl) {
		echo '<button type="button" class="epc-mb-tpl' . ($ti === 0 ? ' is-active' : '') . '" data-template="' . epc_mb_h($key) . '" data-channel="whatsapp">';
		echo '<div class="epc-mb-tpl__icon"><i class="fa fa-whatsapp"></i></div>';
		echo '<div class="epc-mb-tpl__label">' . epc_mb_h($tpl['label']) . '</div></button>';
		$ti++;
	}
	echo '</div>';
	echo '<select class="epc-mb-template-select form-control" data-channel="whatsapp" style="position:absolute;left:-9999px;width:1px;height:1px;opacity:0" tabindex="-1" aria-hidden="true">';
	foreach ($templates as $key => $tpl) {
		echo '<option value="' . epc_mb_h($key) . '">' . epc_mb_h($tpl['label']) . '</option>';
	}
	echo '</select></div>';

	echo '<div class="epc-mb-step"><div class="epc-mb-step__label"><span class="epc-mb-step__num">3</span> Message body</div>';
	echo '<div class="epc-mb-field"><label for="epc-mb-wa-body">WhatsApp text</label>';
	echo '<textarea name="body_text" id="epc-mb-wa-body" class="form-control epc-mb-wa-body" rows="10" placeholder="Leave blank to use template"></textarea>';
	echo '<div class="epc-mb-vars"><code>{{customer_name}}</code><code>{{shop_name}}</code><code>{{shop_url}}</code></div></div>';
	echo '<div class="epc-mb-field"><label for="epc-mb-wa-batch">Batch limit</label>';
	echo '<input type="number" name="batch_limit" id="epc-mb-wa-batch" class="form-control" value="50" min="1" max="100" style="max-width:140px"></div>';
	echo '</div>';

	echo '<div class="epc-mb-actions">';
	echo '<button type="submit" class="btn btn-success" data-confirm="' . ($waApi ? 'Send this WhatsApp campaign now?' : 'Prepare wa.me links for the selected audience?') . '">';
	echo '<i class="fa fa-whatsapp"></i> ' . ($waApi ? 'Send WhatsApp campaign' : 'Prepare wa.me links') . '</button>';
	echo '<button type="button" class="btn btn-default" id="epc-mb-wa-reload-tpl"><i class="fa fa-refresh"></i> Reload template</button>';
	echo '</div>';
	echo '</div></div>';

	echo '</div>';

	echo '<aside class="epc-mb-panel epc-mb-preview"><div class="epc-mb-panel__head"><h4><i class="fa fa-comment"></i> WhatsApp preview</h4><span class="epc-mb-panel__hint">Chat-style</span></div>';
	echo '<div class="epc-mb-panel__body"><div class="epc-mb-wa-bubble-wrap"><div class="epc-mb-wa-bubble" id="epc-mb-wa-preview-bubble">Select a template or type a message…</div>';
	echo '<div class="epc-mb-wa-bubble__meta">Preview · sample customer</div></div></div></aside>';

	echo '</div></form>';
}

function epc_mb_render_history_tab(array $campaigns): void
{
	echo '<div class="epc-mb-panel"><div class="epc-mb-panel__head"><h4><i class="fa fa-history"></i> Recent campaigns</h4><span class="epc-mb-panel__hint">Last 15</span></div><div class="epc-mb-panel__body">';
	if (!$campaigns) {
		echo '<div class="epc-mb-empty"><div class="epc-mb-empty__icon"><i class="fa fa-paper-plane"></i></div>';
		echo '<div class="epc-mb-empty__title">No campaigns yet</div>';
		echo '<p>Send your first email or WhatsApp broadcast — results appear here with OK / failed counts.</p>';
		echo '<p><a class="btn btn-primary btn-sm" href="' . epc_mb_h(epc_mb_hub_url('email')) . '"><i class="fa fa-envelope"></i> Compose email</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_mb_h(epc_mb_hub_url('whatsapp')) . '"><i class="fa fa-whatsapp"></i> Compose WhatsApp</a></p></div>';
		echo '</div></div>';
		return;
	}
	echo '<div class="epc-mb-history-list">';
	foreach ($campaigns as $c) {
		$ch = (string) ($c['channel'] ?? 'email');
		$isWa = ($ch === 'whatsapp');
		echo '<div class="epc-mb-campaign">';
		echo '<div class="epc-mb-campaign__icon' . ($isWa ? ' is-wa' : '') . '"><i class="fa ' . ($isWa ? 'fa-whatsapp' : 'fa-envelope') . '"></i></div>';
		echo '<div><div class="epc-mb-campaign__title">' . epc_mb_h(ucfirst($ch)) . ' · ' . epc_mb_h((string) ($c['audience_mode'] ?? '')) . '</div>';
		echo '<div class="epc-mb-campaign__meta">#' . (int) $c['id'] . ' · ' . epc_mb_h(date('Y-m-d H:i', (int) $c['created_at']))
			. ' · ' . (int) $c['total_targets'] . ' targets · <span class="epc-mb-status-pill">' . epc_mb_h((string) ($c['status'] ?? '')) . '</span></div></div>';
		echo '<div class="epc-mb-campaign__stats"><span class="ok">' . (int) $c['sent_ok'] . ' OK</span><span class="fail">' . (int) $c['sent_fail'] . ' fail</span></div>';
		echo '</div>';
	}
	echo '</div></div></div>';
}

function epc_mb_render_guide_tab(array $shop, string $emailSettingsUrl, string $guideUrl, bool $waApi, string $integrationsUrl): void
{
	$steps = array(
		array(
			'title' => 'Configure email (SMTP)',
			'icon' => 'fa-envelope',
			'body' => '<p>Open <a href="' . epc_mb_h($emailSettingsUrl) . '">Email / SMTP settings</a> (also linked from <a href="' . epc_mb_h($integrationsUrl) . '">Integrations Hub</a>). Enter your tenant mailbox (Gmail App Password, Hostinger, Microsoft 365, etc.).</p><p>Run <strong>Test send</strong> and wait for the SMTP ready badge on the Email tab before bulk campaigns.</p>',
		),
		array(
			'title' => 'Choose your audience',
			'icon' => 'fa-users',
			'body' => '<p>On Email or WhatsApp, pick who receives the message:</p><ul>'
				. '<li><strong>All with email/phone</strong> — every customer with contact data</li>'
				. '<li><strong>With orders</strong> — buyers only</li>'
				. '<li><strong>Group / segment</strong> — price or access group</li>'
				. '<li><strong>Manual list</strong> — paste emails or phones (one per line)</li></ul>'
				. '<p>The live recipient count updates as you change the audience. Each tenant uses its own customer database.</p>',
		),
		array(
			'title' => 'Pick a brochure template',
			'icon' => 'fa-th-large',
			'body' => '<p>Click a template card to load that brochure into the composer and live preview. Switching cards replaces subject / body so you always see the selected design.</p>'
				. '<p><strong>Email:</strong> HTML brochures (sale, new arrivals, service reminder, blank).<br>'
				. '<strong>WhatsApp:</strong> bilingual EN+AR messages (promo, brochure share, follow-up, event, blank).</p>'
				. '<p>Merge tags: <code>{{customer_name}}</code>, <code>{{shop_name}}</code>, <code>{{shop_url}}</code>. After you edit by hand, use <em>Reload template</em> to restore the selected card.</p>',
		),
		array(
			'title' => 'Preview, then send email',
			'icon' => 'fa-paper-plane',
			'body' => '<p>Watch the <strong>Live email preview</strong> while you edit subject and HTML. Set a batch limit (1–100). Click <strong>Send email campaign</strong> — sending is paced (~5/sec) to protect SMTP reputation. Results land in <strong>History</strong>.</p>',
		),
		array(
			'title' => 'Send WhatsApp marketing',
			'icon' => 'fa-whatsapp',
			'body' => $waApi
				? '<p>WhatsApp Cloud API is <strong>on</strong> for this tenant — messages send automatically via Meta Graph API. Review the chat-style preview, then send. Delivery details are logged by the WhatsApp notify module.</p>'
				: '<p><strong>wa.me mode (Phase 1):</strong> Prepare links for each recipient, then open the green buttons to send from WhatsApp Web/desktop.</p>'
					. '<p>For automatic bulk send, enable WhatsApp API in Configuration (<code>epc_whatsapp_api_enabled</code>) via Integrations / WhatsApp settings.</p>',
		),
		array(
			'title' => 'UAE compliance — opt-in &amp; consent',
			'icon' => 'fa-balance-scale',
			'body' => '<div class="epc-mb-guide-compliance"><strong>UAE / TRA / DIFC &amp; Meta best practice</strong><ul>'
				. '<li>Only message customers who <strong>opted in</strong> (registration, order, or explicit consent).</li>'
				. '<li>Show your trade name and a clear opt-out (reply STOP or contact email).</li>'
				. '<li>Commercial WhatsApp needs valid opt-in under Meta Business Policy.</li>'
				. '<li>Respect quiet hours; avoid misleading promotions (UAE Consumer Protection).</li>'
				. '<li>Keep <strong>Campaign history</strong> for audit trails.</li></ul></div>',
		),
	);

	echo '<div class="epc-mb-panel"><div class="epc-mb-panel__head"><h4><i class="fa fa-book"></i> Operator guide</h4>';
	echo '<span class="epc-mb-panel__hint"><a href="' . epc_mb_h($guideUrl) . '">' . epc_mb_h($guideUrl) . '</a></span></div>';
	echo '<div class="epc-mb-panel__body epc-mb-guide">';
	echo '<div class="epc-mb-guide__intro"><strong>' . epc_mb_h($shop['shop_name']) . '</strong> — bulk email brochures and WhatsApp from tenant CP. '
		. 'Follow the steps below for a reliable, compliant campaign.</div>';
	foreach ($steps as $i => $step) {
		echo '<div class="epc-mb-guide-step">';
		echo '<div class="epc-mb-guide-step__num">' . ($i + 1) . '</div>';
		echo '<div><h5><i class="fa ' . epc_mb_h($step['icon']) . '"></i> ' . epc_mb_h($step['title']) . '</h5>';
		echo '<div>' . $step['body'] . '</div></div></div>';
	}
	echo '</div></div>';
}

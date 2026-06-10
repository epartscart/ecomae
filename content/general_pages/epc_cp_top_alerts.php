<?php
/**
 * CP header connectivity alerts (SSL, email, SMS) — detection + professional-shell rendering.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Professional CP shell hides verbose legacy header strips; use Communications for details.
 */
function epc_cp_top_alerts_use_professional_header(): bool
{
	return true;
}

/**
 * HTTPS redirect is handled outside .htaccess (Cloudflare, nginx, CLP).
 */
function epc_cp_https_redirect_is_configured($DP_Config = null): bool
{
	global $DP_Config;
	if ($DP_Config === null) {
		$DP_Config = $GLOBALS['DP_Config'] ?? null;
	}
	if ($DP_Config === null) {
		return false;
	}

	if (!empty($DP_Config->epc_https_redirect_external) && (string) $DP_Config->epc_https_redirect_external === '1') {
		return true;
	}

	$htaccess = $_SERVER['DOCUMENT_ROOT'] . '/.htaccess';
	if (is_readable($htaccess)) {
		$ht = (string) file_get_contents($htaccess);
		if (strripos($ht, 'RewriteRule ^(.*)$ https://%{HTTP_HOST}%{REQUEST_URI} [L,R=301]') !== false) {
			return true;
		}
		if (preg_match('/RewriteRule\s+\^\(\.\*\)\$\s+https:\/\//i', $ht)) {
			return true;
		}
	}

	$domainPath = (string) ($DP_Config->domain_path ?? '');
	if (stripos($domainPath, 'https://') === 0) {
		if (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') {
			return true;
		}
		$xfwd = isset($_SERVER['HTTP_X_FORWARDED_PROTO']) ? strtolower((string) $_SERVER['HTTP_X_FORWARDED_PROTO']) : '';
		if ($xfwd === 'https') {
			return true;
		}
		if (!empty($_SERVER['HTTP_CF_VISITOR']) && stripos((string) $_SERVER['HTTP_CF_VISITOR'], 'https') !== false) {
			return true;
		}
		if (!empty($_SERVER['HTTP_CF_RAY'])) {
			return true;
		}
	}

	$marker = $_SERVER['DOCUMENT_ROOT'] . '/content/epc_https_redirect.ok';
	if (is_file($marker)) {
		return true;
	}

	return false;
}

/**
 * Platform / Super CP: per-tenant SMTP and SMS are not header-level requirements.
 */
function epc_cp_top_alerts_suppress_email_sms(): bool
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return true;
	}
	if (function_exists('epc_portal_is_platform_operator_host') && epc_portal_is_platform_operator_host()) {
		return true;
	}
	return false;
}

/**
 * Email notification health: 1 ok, 2 stale ok, 3 broken, 4 untested.
 *
 * @param PDO $db_link
 */
function epc_cp_email_notification_state($db_link, $DP_Config): int
{
	if (empty($DP_Config->from_name) || empty($DP_Config->from_email) || empty($DP_Config->smtp_mode)
		|| empty($DP_Config->smtp_encryption) || empty($DP_Config->smtp_host) || empty($DP_Config->smtp_port)
		|| empty($DP_Config->smtp_username) || empty($DP_Config->smtp_password)) {
		return 3;
	}

	$email_debug_query = $db_link->prepare('SELECT * FROM `debug_results` WHERE `name` = ?;');
	$email_debug_query->execute(array('email'));
	$email_debug = $email_debug_query->fetch();

	if ($email_debug === false) {
		return 4;
	}
	if ((int) $email_debug['status'] === 1) {
		return (time() - (int) $email_debug['time'] < 86400) ? 1 : 2;
	}
	return 3;
}

/**
 * SMS notification health: 1 ok, 2 stale ok, 3 broken, 4 untested.
 *
 * @param PDO $db_link
 */
function epc_cp_sms_notification_state($db_link): int
{
	$check_sms_query = $db_link->prepare('SELECT COUNT(*) FROM `sms_api` WHERE `active` = ?;');
	$check_sms_query->execute(array(1));
	if ((int) $check_sms_query->fetchColumn() === 0) {
		return 3;
	}

	$sms_debug_query = $db_link->prepare('SELECT * FROM `debug_results` WHERE `name` = ?;');
	$sms_debug_query->execute(array('sms'));
	$sms_debug = $sms_debug_query->fetch();

	if ($sms_debug === false) {
		return 4;
	}
	if ((int) $sms_debug['status'] === 1) {
		return (time() - (int) $sms_debug['time'] < 86400) ? 1 : 2;
	}
	return 3;
}

/**
 * Show SSL / email / SMS items in the CP top navbar.
 */
function epc_cp_top_alerts_show_in_header(): bool
{
	if (epc_cp_top_alerts_use_professional_header()) {
		return false;
	}
	return true;
}

/**
 * Render SSL checker list item (requires check_ssl.php variables).
 */
function epc_cp_top_alerts_render_ssl_item(): void
{
	if (!epc_cp_top_alerts_show_in_header()) {
		return;
	}
	global $ssl_a_style, $ssl_status_text, $ssl_sign_after;
	?>
	<li class="dropdown hidden-sm hidden-xs hidden-md epc-cp-top-connectivity epc-cp-top-connectivity--ssl" style="pointer-events: none;">
		<a href="#" style="<?php echo $ssl_a_style; ?>border-bottom:1px solid #e4e5e7;">
			<i class="fa fa-lock"></i>
			<div style="font-size:0.5em;display:inline-block;line-height:1em;"><?php echo $ssl_status_text; ?></div><?php echo ' ' . $ssl_sign_after; ?>
		</a>
	</li>
	<?php
}

/**
 * @param int $state
 */
function epc_cp_top_alerts_email_styles($state): array
{
	switch ($state) {
		case 1:
			return array(
				'text' => translate_str_by_id(4022) . '<br>' . translate_str_by_id(4023),
				'style' => '',
				'after' => '',
				'severity' => 'ok',
			);
		case 2:
			return array(
				'text' => translate_str_by_id(4022) . '<br>' . translate_str_by_id(4024),
				'style' => 'background-color:#f5f5f5;color:#000;',
				'after' => '<i class="fa fa-info-circle"></i>',
				'severity' => 'info',
			);
		case 3:
			return array(
				'text' => translate_str_by_id(4022) . '<br>' . translate_str_by_id(4025),
				'style' => 'background-color:#F00;color:#FFF;',
				'after' => '<i class="fa fa-exclamation-triangle"></i>',
				'severity' => 'error',
			);
		case 4:
		default:
			return array(
				'text' => translate_str_by_id(4022) . '<br>' . translate_str_by_id(4026),
				'style' => 'background-color:#ffde00;color:#000;',
				'after' => '<i class="fa fa-exclamation-triangle"></i>',
				'severity' => 'warn',
			);
	}
}

/**
 * @param int $state
 */
function epc_cp_top_alerts_sms_styles($state): array
{
	switch ($state) {
		case 1:
			return array(
				'text' => translate_str_by_id(4028) . '<br>' . translate_str_by_id(4023),
				'style' => '',
				'after' => '',
				'severity' => 'ok',
			);
		case 2:
			return array(
				'text' => translate_str_by_id(4028) . '<br>' . translate_str_by_id(4024),
				'style' => 'background-color:#f5f5f5;color:#000;',
				'after' => '<i class="fa fa-info-circle"></i>',
				'severity' => 'info',
			);
		case 3:
			return array(
				'text' => translate_str_by_id(4028) . '<br>' . translate_str_by_id(4025),
				'style' => 'background-color:#F00;color:#FFF;',
				'after' => '<i class="fa fa-exclamation-triangle"></i>',
				'severity' => 'error',
			);
		case 4:
		default:
			return array(
				'text' => translate_str_by_id(4028) . '<br>' . translate_str_by_id(4026),
				'style' => 'background-color:#ffde00;color:#000;',
				'after' => '<i class="fa fa-exclamation-triangle"></i>',
				'severity' => 'warn',
			);
	}
}

/**
 * @param PDO $db_link
 */
function epc_cp_top_alerts_render_email_item($db_link, $DP_Config): void
{
	if (!epc_cp_top_alerts_show_in_header() || epc_cp_top_alerts_suppress_email_sms()) {
		return;
	}
	$state = epc_cp_email_notification_state($db_link, $DP_Config);
	if ($state === 1) {
		return;
	}
	$ui = epc_cp_top_alerts_email_styles($state);
	$backend = $DP_Config->backend_dir;
	?>
	<li class="dropdown hidden-sm hidden-xs hidden-md epc-cp-top-connectivity epc-cp-top-connectivity--email epc-cp-top-connectivity--<?php echo htmlspecialchars($ui['severity'], ENT_QUOTES, 'UTF-8'); ?>">
		<a href="/<?php echo $backend; ?>/control/communications" title="<?php echo translate_str_by_id(4027); ?>" style="<?php echo $ui['style']; ?>border-bottom:1px solid #e4e5e7;">
			<i class="fa fa-envelope"></i>
			<div style="font-size:0.5em;display:inline-block;line-height:1em;"><?php echo $ui['text']; ?></div><?php echo ' ' . $ui['after']; ?>
		</a>
	</li>
	<?php
}

/**
 * @param PDO $db_link
 */
function epc_cp_top_alerts_render_sms_item($db_link, $DP_Config): void
{
	if (!epc_cp_top_alerts_show_in_header() || epc_cp_top_alerts_suppress_email_sms()) {
		return;
	}
	$state = epc_cp_sms_notification_state($db_link);
	if ($state === 1) {
		return;
	}
	$ui = epc_cp_top_alerts_sms_styles($state);
	$backend = $DP_Config->backend_dir;
	?>
	<li class="dropdown hidden-sm hidden-xs hidden-md epc-cp-top-connectivity epc-cp-top-connectivity--sms epc-cp-top-connectivity--<?php echo htmlspecialchars($ui['severity'], ENT_QUOTES, 'UTF-8'); ?>">
		<a href="/<?php echo $backend; ?>/control/communications" title="<?php echo translate_str_by_id(4027); ?>" style="<?php echo $ui['style']; ?>border-bottom:1px solid #e4e5e7;">
			<i class="fa fa-mobile"></i>
			<div style="font-size:0.5em;display:inline-block;line-height:1em;"><?php echo $ui['text']; ?></div><?php echo ' ' . $ui['after']; ?>
		</a>
	</li>
	<?php
}

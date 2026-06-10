<?php
/**
 * Tenant CP — SMTP / email settings for OTP, notifications, and order mail.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_smtp.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	echo '<div class="alert alert-info">Platform SMTP is configured under <a href="/' . epc_int_backend() . '/control/portal/epc_cp_auth_settings">Modern auth settings</a>. This page is for tenant CP hosts.</div>';
}

if (!DP_User::isAdmin()) {
	echo '<div class="alert alert-warning">Admin login required.</div>';
	return;
}

if (!epc_integrations_feature_enabled('email_smtp')) {
	echo '<div class="alert alert-warning">Email/SMTP is disabled for this tenant. Contact your platform operator.</div>';
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
$integrations = epc_integrations_load_tenant_config($pdo);
$smtp = isset($integrations['smtp']) && is_array($integrations['smtp']) ? $integrations['smtp'] : array();
$diag = epc_auth_smtp_diagnose();
$backend = epc_int_backend();
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_integrations.php';
$adminEmail = class_exists('DP_User') ? (string) DP_User::getAdminEmail() : '';

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-tenant-email',
	'hero' => array(
		'badge' => 'Tenant CP',
		'title' => 'Email / SMTP settings',
		'sub' => 'Configure outgoing mail for OTP codes, order notifications, and customer emails.',
		'actions' => array(
			array('label' => 'Integrations hub', 'icon' => 'fa-plug', 'url' => '/' . $backend . '/control/portal/epc_integrations_hub'),
		),
	),
));
?>

<div id="epc-smtp-flash" class="alert" style="display:none"></div>

<div class="well well-sm">
	<strong>Current transport:</strong> <?php echo epc_int_h($diag['source'] ?? '—'); ?>
	<?php if (!empty($diag['host'])) { ?> — <?php echo epc_int_h($diag['host']); ?>:<?php echo epc_int_h($diag['port']); ?><?php } ?>
</div>

<form id="epc-tenant-smtp-form" class="well" data-ajax-url="<?php echo epc_int_h($ajaxUrl); ?>">
	<input type="hidden" name="action" value="save_tenant_smtp">
	<div class="checkbox">
		<label><input type="checkbox" name="use_tenant_smtp" value="1" <?php echo !empty($smtp['use_tenant_smtp']) ? 'checked' : ''; ?>> Use tenant SMTP (override platform default)</label>
	</div>
	<div class="row">
		<div class="col-md-6">
			<div class="form-group">
				<label>SMTP host</label>
				<input type="text" class="form-control" name="smtp_host" value="<?php echo epc_int_h($smtp['smtp_host'] ?? $diag['host'] ?? ''); ?>">
			</div>
		</div>
		<div class="col-md-3">
			<div class="form-group">
				<label>Port</label>
				<input type="text" class="form-control" name="smtp_port" value="<?php echo epc_int_h($smtp['smtp_port'] ?? $diag['port'] ?? '587'); ?>">
			</div>
		</div>
		<div class="col-md-3">
			<div class="form-group">
				<label>Encryption</label>
				<select class="form-control" name="smtp_encryption">
					<?php
					$enc = (string) ($smtp['smtp_encryption'] ?? $diag['encryption'] ?? 'tls');
					foreach (array('tls', 'ssl', '') as $opt) {
						$sel = ($enc === $opt || ($opt === '' && $enc === 'none')) ? ' selected' : '';
						$label = $opt === '' ? 'None' : strtoupper($opt);
						echo '<option value="' . epc_int_h($opt) . '"' . $sel . '>' . epc_int_h($label) . '</option>';
					}
					?>
				</select>
			</div>
		</div>
	</div>
	<div class="row">
		<div class="col-md-6">
			<div class="form-group">
				<label>Username</label>
				<input type="text" class="form-control" name="smtp_username" value="<?php echo epc_int_h($smtp['smtp_username'] ?? ''); ?>" autocomplete="off">
			</div>
		</div>
		<div class="col-md-6">
			<div class="form-group">
				<label>Password <?php if (!empty($smtp['smtp_password'])) { ?><span class="text-muted">(saved)</span><?php } ?></label>
				<input type="password" class="form-control" name="smtp_password" placeholder="Leave blank to keep current" autocomplete="new-password">
			</div>
		</div>
	</div>
	<div class="row">
		<div class="col-md-6">
			<div class="form-group">
				<label>From name</label>
				<input type="text" class="form-control" name="from_name" value="<?php echo epc_int_h($smtp['from_name'] ?? $diag['from_name'] ?? ''); ?>">
			</div>
		</div>
		<div class="col-md-6">
			<div class="form-group">
				<label>From email</label>
				<input type="email" class="form-control" name="from_email" value="<?php echo epc_int_h($smtp['from_email'] ?? $diag['from_email'] ?? ''); ?>">
			</div>
		</div>
	</div>
	<button type="submit" class="btn btn-success"><i class="fa fa-save"></i> Save SMTP</button>
</form>

<form id="epc-tenant-smtp-test" class="well" style="max-width:520px" data-ajax-url="<?php echo epc_int_h($ajaxUrl); ?>">
	<input type="hidden" name="action" value="test_tenant_smtp">
	<h4 style="margin-top:0">Test send</h4>
	<div class="form-group">
		<label>Send test to</label>
		<input type="email" class="form-control" name="test_to" value="<?php echo epc_int_h($adminEmail); ?>" required>
	</div>
	<button type="submit" class="btn btn-primary"><i class="fa fa-paper-plane"></i> Send test email</button>
</form>

<div class="panel panel-default">
	<div class="panel-heading"><strong><i class="fa fa-book"></i> Activate in 3 steps</strong></div>
	<div class="panel-body">
		<ol>
			<li><strong>Enable</strong> — Platform operator enables Email/SMTP for your tenant.</li>
			<li><strong>Configure</strong> — Enter SMTP host, port, and credentials above (Gmail App Password, SendGrid, etc.).</li>
			<li><strong>Test</strong> — Send test email, then try storefront OTP or CP login.</li>
		</ol>
	</div>
</div>

<?php epc_cp_page_frame_close(); ?>

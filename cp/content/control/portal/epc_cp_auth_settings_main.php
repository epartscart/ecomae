<?php
/**
 * Super CP — Modern CP auth settings (OAuth + SMTP / OTP operator tools).
 * Loaded via include() from epc_cp_auth_settings.php — safe for HTML/return.
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_auth_settings_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
		$authCommonFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_common.php';
		$socialFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_social.php';
		$otpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_email_otp.php';
		$smtpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_smtp.php';
		$depFiles = array(
			'epc_portal.php' => $portalFile,
			'epc_auth_common.php' => $authCommonFile,
			'epc_auth_social.php' => $socialFile,
			'epc_auth_email_otp.php' => $otpFile,
			'epc_auth_smtp.php' => $smtpFile,
		);
		$missing = array();
		foreach ($depFiles as $name => $path) {
			if (!is_file($path)) {
				$missing[] = $name;
			}
		}
		if ($missing !== array()) {
			echo '<div class="col-lg-12"><div class="alert alert-danger">Missing dependencies: '
				. epc_cp_auth_settings_h(implode(', ', $missing))
				. '. Deploy auth modules via <code>tools/push_one.py</code>.</div></div>';
		} else {
			$flash = '';
			$flashClass = 'info';
			$smtpTestResult = null;
			$otpLookup = null;
			$policy = array('password' => true, 'email_otp' => true, 'google_oauth' => true);
			$google = array();
			$configured = false;
			$callback = '';
			$configExists = false;
			$smtpConfigExists = false;
			$smtpDiag = array('ok' => false, 'source' => '—', 'host' => '', 'port' => '', 'encryption' => '', 'from_name' => '', 'from_email' => '', 'username' => '', 'password_length' => 0, 'allow_mail_fallback' => false, 'issues' => array());
			$providers = array();
			$adminEmail = '';
			$renderError = '';

			try {
				require_once $portalFile;
				require_once $authCommonFile;
				require_once $socialFile;
				require_once $otpFile;
				require_once $smtpFile;
				$oauthProvidersFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_oauth_providers.php';
				$oauthLibReady = is_file($oauthProvidersFile);
				if ($oauthLibReady) {
					require_once $oauthProvidersFile;
				}

				$pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
				if (!$pdo instanceof PDO && isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
					$pdo = $GLOBALS['db_link'];
				}

				if ($_SERVER['REQUEST_METHOD'] === 'POST') {
					$action = (string) ($_POST['epc_auth_action'] ?? '');
					if ($action === 'save_toggles' && $pdo instanceof PDO) {
						$toggles = array(
							'password' => !empty($_POST['auth_password']),
							'email_otp' => !empty($_POST['auth_email_otp']),
							'google_oauth' => !empty($_POST['auth_google_oauth']),
						);
						$saved = epc_cp_modern_auth_save($pdo, $toggles);
						$flash = 'Login method toggles saved (password '
							. (!empty($saved['password']) ? 'on' : 'off')
							. ', email OTP '
							. (!empty($saved['email_otp']) ? 'on' : 'off')
							. ', Google '
							. (!empty($saved['google_oauth']) ? 'on' : 'off') . ').';
						$flashClass = 'success';
					}
					if ($action === 'save_smtp' && function_exists('epc_auth_smtp_write_file_config')) {
						$smtpIn = array(
							'smtp_mode' => !empty($_POST['smtp_mode']),
							'smtp_host' => (string) ($_POST['smtp_host'] ?? ''),
							'smtp_port' => (string) ($_POST['smtp_port'] ?? ''),
							'smtp_encryption' => (string) ($_POST['smtp_encryption'] ?? ''),
							'smtp_username' => (string) ($_POST['smtp_username'] ?? ''),
							'smtp_password' => (string) ($_POST['smtp_password'] ?? ''),
							'from_email' => (string) ($_POST['from_email'] ?? ''),
							'from_name' => (string) ($_POST['from_name'] ?? ''),
							'allow_mail_fallback' => !empty($_POST['allow_mail_fallback']),
							'disable_demo_otp_fallback' => !empty($_POST['disable_demo_otp_fallback']),
						);
						$smtpSave = epc_auth_smtp_write_file_config($smtpIn);
						$flash = $smtpSave['message'];
						if (!empty($smtpSave['warnings'])) {
							$flash .= ' Warnings: ' . implode(' ', $smtpSave['warnings']);
						}
						$flashClass = $smtpSave['ok'] ? (!empty($smtpSave['warnings']) ? 'warning' : 'success') : 'danger';
					}
					if ($action === 'smtp_test') {
						$testTo = strtolower(trim((string) ($_POST['smtp_test_to'] ?? '')));
						if ($testTo === '' || !filter_var($testTo, FILTER_VALIDATE_EMAIL)) {
							$flash = 'Enter a valid test recipient email.';
							$flashClass = 'warning';
						} elseif (!function_exists('epc_auth_smtp_send_html')) {
							$flash = 'SMTP module unavailable on server.';
							$flashClass = 'danger';
						} else {
							$subject = 'ECOM AE SMTP test ' . date('Y-m-d H:i:s');
							$html = '<p>SMTP test from <strong>Super CP → Modern auth settings</strong>.</p>';
							$smtpTestResult = epc_auth_smtp_send_html($testTo, $subject, $html);
							$flash = $smtpTestResult['ok']
								? 'Test email sent to ' . $testTo . ' (' . epc_cp_auth_settings_h($smtpTestResult['transport'] ?? 'smtp') . ').'
								: 'Send failed: ' . ($smtpTestResult['message'] ?? 'unknown');
							$flashClass = $smtpTestResult['ok'] ? 'success' : 'danger';
						}
					}
					if ($action === 'save_oauth_provider' && $oauthLibReady && $pdo instanceof PDO) {
						$prov = strtolower(trim((string) ($_POST['oauth_provider'] ?? '')));
						if (!epc_oauth_is_known_provider($prov)) {
							$flash = 'Unknown provider.';
							$flashClass = 'warning';
						} else {
							$clientId = trim((string) ($_POST['oauth_client_id'] ?? ''));
							$clientSecret = trim((string) ($_POST['oauth_client_secret'] ?? ''));
							$enabled = !empty($_POST['oauth_enabled']) ? 1 : 0;
							$extra = array();
							if ($prov === 'microsoft') {
								$tenant = trim((string) ($_POST['oauth_ms_tenant'] ?? ''));
								if ($tenant !== '') {
									$extra['tenant'] = $tenant;
								}
							}
							if ($prov === 'apple') {
								foreach (array('team_id', 'key_id') as $ak) {
									$av = trim((string) ($_POST['oauth_apple_' . $ak] ?? ''));
									if ($av !== '') {
										$extra[$ak] = $av;
									}
								}
								$pk = trim((string) ($_POST['oauth_apple_private_key'] ?? ''));
								if ($pk !== '') {
									$extra['private_key'] = $pk;
								}
							}
							try {
								epc_oauth_ensure_config_schema($pdo);
								// Preserve existing secret / extra when the field is left blank.
								$existing = $pdo->prepare('SELECT `client_secret`, `extra_json` FROM `epc_oauth_config` WHERE `provider` = ? LIMIT 1');
								$existing->execute(array($prov));
								$exRow = $existing->fetch(PDO::FETCH_ASSOC);
								if ($clientSecret === '' && $exRow && (string) $exRow['client_secret'] !== '') {
									$clientSecret = (string) $exRow['client_secret'];
								}
								if ($exRow && !empty($exRow['extra_json'])) {
									$exExtra = json_decode((string) $exRow['extra_json'], true);
									if (is_array($exExtra)) {
										$extra = array_merge($exExtra, $extra);
									}
								}
								$pdo->prepare(
									'INSERT INTO `epc_oauth_config` (`provider`, `client_id`, `client_secret`, `extra_json`, `enabled`, `updated_at`)
									 VALUES (?, ?, ?, ?, ?, ?)
									 ON DUPLICATE KEY UPDATE `client_id` = VALUES(`client_id`), `client_secret` = VALUES(`client_secret`),
										`extra_json` = VALUES(`extra_json`), `enabled` = VALUES(`enabled`), `updated_at` = VALUES(`updated_at`)'
								)->execute(array($prov, $clientId, $clientSecret, json_encode($extra), $enabled, time()));
								$flash = ucfirst($prov) . ' credentials saved ' . ($enabled ? '(enabled).' : '(disabled).');
								$flashClass = 'success';
							} catch (Throwable $e) {
								$flash = 'Could not save ' . $prov . ' credentials: ' . $e->getMessage();
								$flashClass = 'danger';
							}
						}
					}
					if ($action === 'otp_lookup') {
						$lookupEmail = strtolower(trim((string) ($_POST['otp_lookup_email'] ?? '')));
						if (!$pdo instanceof PDO) {
							$flash = 'Platform database unavailable.';
							$flashClass = 'danger';
						} elseif (!function_exists('epc_auth_otp_operator_lookup')) {
							$flash = 'OTP lookup module unavailable.';
							$flashClass = 'danger';
						} else {
							$otpLookup = epc_auth_otp_operator_lookup($pdo, $lookupEmail);
							$flash = $otpLookup['ok']
								? 'Operator-logged OTP found (demo fallback when SMTP failed).'
								: (string) ($otpLookup['message'] ?? 'Not found');
							$flashClass = $otpLookup['ok'] ? 'success' : 'warning';
						}
					}
				}

				$policy = epc_cp_modern_auth_policy($pdo instanceof PDO ? $pdo : null);
				$oauth = epc_auth_oauth_config();
				$google = $oauth['google'] ?? array();
				$configured = trim((string) ($google['client_id'] ?? '')) !== ''
					&& trim((string) ($google['client_secret'] ?? '')) !== '';
				$callback = epc_auth_oauth_central_callback_url();
				$configPath = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-oauth.php';
				$configExists = is_file($configPath);
				$smtpConfigPath = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php';
				$smtpConfigExists = is_file($smtpConfigPath);
				$smtpDiag = epc_auth_smtp_diagnose();
				$providers = epc_auth_social_providers();
				$oauthProviders = array();
				$oauthCallbackUrl = '';
				if ($oauthLibReady) {
					$oauthCallbackUrl = epc_oauth_callback_url();
					$defsAll = epc_oauth_provider_defs();
					foreach ($defsAll as $pid => $def) {
						$creds = epc_oauth_provider_credentials($pid);
						$oauthProviders[$pid] = array(
							'label' => (string) ($def['label'] ?? ucfirst($pid)),
							'client_id' => (string) ($creds['client_id'] ?? ''),
							'has_secret' => trim((string) ($creds['client_secret'] ?? '')) !== '',
							'extra' => is_array($creds['extra'] ?? null) ? $creds['extra'] : array(),
							'enabled' => !empty($creds['enabled']),
							'configured' => epc_oauth_is_configured($pid),
						);
					}
				}
				if (class_exists('DP_User') && method_exists('DP_User', 'getAdminProfile')) {
					$adminProfile = DP_User::getAdminProfile();
					if (is_array($adminProfile) && !empty($adminProfile['email'])) {
						$adminEmail = strtolower(trim((string) $adminProfile['email']));
					}
				}
			} catch (Throwable $e) {
				$renderError = $e->getMessage();
			}

			if ($renderError !== '') {
				echo '<div class="col-lg-12"><div class="alert alert-danger">Modern auth settings error: '
					. epc_cp_auth_settings_h($renderError) . '</div></div>';
			} else {
				?>
<div class="col-lg-12 epc-scp-auth-settings">
<div class="hpanel">
	<div class="panel-heading hbuilt"><i class="fas fa-sign-in-alt"></i> Modern CP authentication</div>
	<div class="panel-body">
		<?php if ($flash !== '') { ?>
		<div class="alert alert-<?php echo epc_cp_auth_settings_h($flashClass); ?>"><?php echo epc_cp_auth_settings_h($flash); ?></div>
		<?php } ?>
		<p class="text-muted">Control which sign-in methods appear on tenant CP, demo CP, and client ERP login shells. Credentials stay in server config files — toggles only show or hide UI.</p>

		<h4><i class="fa fa-toggle-on"></i> Login methods (platform policy)</h4>
		<form method="post" class="well well-sm epc-scp-auth-settings__toggles">
			<input type="hidden" name="epc_auth_action" value="save_toggles" />
			<div class="checkbox"><label><input type="checkbox" name="auth_password" value="1"<?php echo !empty($policy['password']) ? ' checked' : ''; ?>> <strong>Password</strong> — classic CP email/phone + password</label></div>
			<div class="checkbox"><label><input type="checkbox" name="auth_email_otp" value="1"<?php echo !empty($policy['email_otp']) ? ' checked' : ''; ?>> <strong>Email code (OTP)</strong> — 6-digit code via platform SMTP</label></div>
			<div class="checkbox"><label><input type="checkbox" name="auth_google_oauth" value="1"<?php echo !empty($policy['google_oauth']) ? ' checked' : ''; ?>> <strong>Google OAuth</strong> — when <code>config.epc-oauth.php</code> is present</label></div>
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save toggles</button>
		</form>

		<h4>Google OAuth</h4>
		<table class="table table-bordered table-condensed epc-scp-status-table">
			<tr><th>Status</th><td><?php echo $configured ? '<span class="label label-success">Configured</span>' : '<span class="label label-warning">Not configured</span>'; ?></td></tr>
			<tr><th><code>config.epc-oauth.php</code></th><td><?php echo $configExists ? 'Present on server' : 'Missing — copy from <code>config.epc-oauth.example.php</code>'; ?></td></tr>
			<tr><th>Redirect URI (Google Console)</th><td><code><?php echo epc_cp_auth_settings_h($callback); ?></code></td></tr>
			<tr><th>Client ID</th><td><?php echo $configured ? epc_cp_auth_settings_h(substr((string) $google['client_id'], 0, 12) . '…') : '—'; ?></td></tr>
		</table>

		<h4>Registered providers</h4>
		<ul class="epc-scp-auth-settings__providers">
			<?php foreach ($providers as $p) { ?>
			<li><i class="fa <?php echo epc_cp_auth_settings_h($p['icon'] ?? 'fa-link'); ?>"></i>
				<?php echo epc_cp_auth_settings_h($p['label'] ?? $p['id']); ?>
				— <?php echo !empty($p['enabled']) ? 'enabled' : 'disabled (needs credentials)'; ?></li>
			<?php } ?>
		</ul>

		<?php if ($oauthLibReady) { ?>
		<h4><i class="fa fa-users"></i> Social sign-in providers</h4>
		<p class="text-muted">Multi-platform OAuth (Skywork-style). Only providers with a saved <strong>Client ID + Secret</strong> appear on the storefront / CP login. Register this single redirect URI with every provider:</p>
		<p><strong>Redirect URI:</strong> <code><?php echo epc_cp_auth_settings_h($oauthCallbackUrl); ?></code>
			<br><span class="text-muted small">Google may also keep its legacy callback <code><?php echo epc_cp_auth_settings_h($callback); ?></code>.</span></p>
		<div class="epc-scp-oauth-providers">
			<?php foreach ($oauthProviders as $pid => $info) { ?>
			<div class="panel panel-default" style="margin-bottom:12px">
				<div class="panel-heading" style="display:flex;justify-content:space-between;align-items:center">
					<strong><?php echo epc_cp_auth_settings_h($info['label']); ?></strong>
					<?php if ($info['configured']) { ?>
						<span class="label label-success">Configured &amp; live</span>
					<?php } elseif ($info['client_id'] !== '') { ?>
						<span class="label label-warning">Incomplete</span>
					<?php } else { ?>
						<span class="label label-default">Not configured</span>
					<?php } ?>
				</div>
				<div class="panel-body">
					<form method="post" class="form-horizontal">
						<input type="hidden" name="epc_auth_action" value="save_oauth_provider" />
						<input type="hidden" name="oauth_provider" value="<?php echo epc_cp_auth_settings_h($pid); ?>" />
						<div class="form-group">
							<label class="col-sm-3 control-label">Client ID<?php echo $pid === 'apple' ? ' (Services ID)' : ''; ?></label>
							<div class="col-sm-9"><input type="text" name="oauth_client_id" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($info['client_id']); ?>" placeholder="<?php echo $pid === 'apple' ? 'com.yourapp.service' : 'client id'; ?>" /></div>
						</div>
						<div class="form-group">
							<label class="col-sm-3 control-label">Client secret<?php echo $pid === 'apple' ? ' (optional JWT)' : ''; ?></label>
							<div class="col-sm-9"><input type="password" name="oauth_client_secret" class="form-control input-sm" autocomplete="new-password" placeholder="<?php echo $info['has_secret'] ? '•••••• (leave blank to keep)' : 'client secret'; ?>" /></div>
						</div>
						<?php if ($pid === 'microsoft') { ?>
						<div class="form-group">
							<label class="col-sm-3 control-label">Directory (tenant)</label>
							<div class="col-sm-9"><input type="text" name="oauth_ms_tenant" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h((string) ($info['extra']['tenant'] ?? '')); ?>" placeholder="common (or your Azure tenant id)" /></div>
						</div>
						<?php } ?>
						<?php if ($pid === 'apple') { ?>
						<div class="form-group">
							<label class="col-sm-3 control-label">Team ID</label>
							<div class="col-sm-9"><input type="text" name="oauth_apple_team_id" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h((string) ($info['extra']['team_id'] ?? '')); ?>" placeholder="10-char Team ID" /></div>
						</div>
						<div class="form-group">
							<label class="col-sm-3 control-label">Key ID</label>
							<div class="col-sm-9"><input type="text" name="oauth_apple_key_id" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h((string) ($info['extra']['key_id'] ?? '')); ?>" placeholder="10-char Key ID" /></div>
						</div>
						<div class="form-group">
							<label class="col-sm-3 control-label">.p8 private key</label>
							<div class="col-sm-9"><textarea name="oauth_apple_private_key" class="form-control input-sm" rows="3" placeholder="<?php echo !empty($info['extra']['private_key']) ? '•••••• (saved — leave blank to keep)' : '-----BEGIN PRIVATE KEY-----'; ?>"></textarea></div>
						</div>
						<?php } ?>
						<div class="form-group">
							<div class="col-sm-9 col-sm-offset-3">
								<div class="checkbox"><label><input type="checkbox" name="oauth_enabled" value="1"<?php echo $info['enabled'] ? ' checked' : ''; ?>> Enabled</label></div>
								<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save <?php echo epc_cp_auth_settings_h($info['label']); ?></button>
							</div>
						</div>
					</form>
				</div>
			</div>
			<?php } ?>
		</div>
		<p class="text-muted small">Registration via social login auto-accepts the storefront Terms when the buttons include is rendered with <code>require_terms</code>. Credentials are stored in platform table <code>epc_oauth_config</code>; per-user links live in tenant table <code>epc_oauth_identity</code>.</p>
		<?php } ?>

		<h4>Email OTP &amp; SMTP</h4>
		<p>Codes are stored hashed on platform DB table <code>epc_auth_otp_requests</code>. Rate limit: <strong>5 codes/hour/email</strong>.</p>
		<table class="table table-bordered table-condensed epc-scp-status-table">
			<tr><th>Config source</th><td><?php echo epc_cp_auth_settings_h($smtpDiag['source']); ?></td></tr>
			<tr><th><code>config.epc-smtp.php</code></th><td><?php echo $smtpConfigExists ? 'Present' : 'Missing — copy <code>config.epc-smtp.example.php</code>'; ?></td></tr>
			<tr><th>SMTP host</th><td><code><?php echo epc_cp_auth_settings_h($smtpDiag['host']); ?></code> :<?php echo epc_cp_auth_settings_h($smtpDiag['port']); ?> (<?php echo epc_cp_auth_settings_h($smtpDiag['encryption']); ?>)</td></tr>
			<tr><th>From</th><td><?php echo epc_cp_auth_settings_h($smtpDiag['from_name']); ?> &lt;<?php echo epc_cp_auth_settings_h($smtpDiag['from_email']); ?>&gt;</td></tr>
			<tr><th>Health</th><td><?php echo !empty($smtpDiag['ok']) ? '<span class="label label-success">OK</span>' : '<span class="label label-danger">Issues</span>'; ?>
				<?php if (empty($smtpDiag['ok']) && !empty($smtpDiag['issues'])) { ?>
				<ul class="small" style="margin:8px 0 0">
					<?php foreach ($smtpDiag['issues'] as $issue) { ?>
					<li><?php echo epc_cp_auth_settings_h($issue); ?></li>
					<?php } ?>
				</ul>
				<?php } ?>
			</td></tr>
		</table>

		<?php
			$smtpPresets = function_exists('epc_auth_smtp_recommended_presets') ? epc_auth_smtp_recommended_presets() : array();
			$smtpEnc = strtolower((string) ($smtpDiag['encryption'] ?? ''));
			$smtpModeOn = !empty($smtpDiag['smtp_mode']);
		?>
		<h5><i class="fa fa-cog"></i> SMTP credentials</h5>
		<p class="text-muted small">Saved to <code>config.epc-smtp.php</code> on the server (never committed to git). Leave the password blank to keep the one already stored.</p>
		<?php if (!empty($smtpPresets)) { ?>
		<p class="small"><strong>Quick fill:</strong>
			<?php foreach ($smtpPresets as $pk => $pv) { ?>
			<button type="button" class="btn btn-default btn-xs epc-smtp-preset"
				data-host="<?php echo epc_cp_auth_settings_h($pv['smtp_host']); ?>"
				data-port="<?php echo epc_cp_auth_settings_h($pv['smtp_port']); ?>"
				data-enc="<?php echo epc_cp_auth_settings_h($pv['smtp_encryption']); ?>"
				title="<?php echo epc_cp_auth_settings_h($pv['note']); ?>"><?php echo epc_cp_auth_settings_h($pv['label']); ?></button>
			<?php } ?>
		</p>
		<?php } ?>
		<form method="post" class="form-horizontal epc-scp-auth-settings__smtp-save" id="epc-smtp-form">
			<input type="hidden" name="epc_auth_action" value="save_smtp" />
			<div class="form-group">
				<div class="col-sm-9 col-sm-offset-3">
					<div class="checkbox"><label><input type="checkbox" name="smtp_mode" value="1"<?php echo $smtpModeOn ? ' checked' : ''; ?>> <strong>SMTP mode on</strong> (recommended)</label></div>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">SMTP host</label>
				<div class="col-sm-9"><input type="text" name="smtp_host" id="epc-smtp-host" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($smtpDiag['host']); ?>" placeholder="smtp.gmail.com" /></div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Port</label>
				<div class="col-sm-3"><input type="text" name="smtp_port" id="epc-smtp-port" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($smtpDiag['port']); ?>" placeholder="587" /></div>
				<label class="col-sm-2 control-label">Encryption</label>
				<div class="col-sm-4">
					<select name="smtp_encryption" id="epc-smtp-enc" class="form-control input-sm">
						<option value="tls"<?php echo $smtpEnc === 'tls' ? ' selected' : ''; ?>>TLS (587)</option>
						<option value="ssl"<?php echo $smtpEnc === 'ssl' ? ' selected' : ''; ?>>SSL (465)</option>
						<option value=""<?php echo ($smtpEnc !== 'tls' && $smtpEnc !== 'ssl') ? ' selected' : ''; ?>>None</option>
					</select>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Username</label>
				<div class="col-sm-9"><input type="text" name="smtp_username" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($smtpDiag['username']); ?>" placeholder="you@gmail.com (Gmail: same as From email)" autocomplete="off" /></div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Password / App Password</label>
				<div class="col-sm-9">
					<input type="password" name="smtp_password" class="form-control input-sm" autocomplete="new-password" placeholder="<?php echo ((int) ($smtpDiag['password_length'] ?? 0)) > 0 ? '•••••• (saved — leave blank to keep)' : 'paste 16-char Gmail App Password here'; ?>" />
					<span class="help-block small">Gmail: <a href="https://myaccount.google.com/apppasswords" target="_blank" rel="noopener">myaccount.google.com/apppasswords</a> → Security → 2-Step Verification → App passwords. Paste the 16-char code (no spaces).</span>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">From email</label>
				<div class="col-sm-9"><input type="email" name="from_email" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($smtpDiag['from_email']); ?>" placeholder="hello@ecomae.com" /></div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">From name</label>
				<div class="col-sm-9"><input type="text" name="from_name" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($smtpDiag['from_name']); ?>" placeholder="ECOM AE" /></div>
			</div>
			<div class="form-group">
				<div class="col-sm-9 col-sm-offset-3">
					<div class="checkbox"><label><input type="checkbox" name="allow_mail_fallback" value="1"<?php echo !empty($smtpDiag['allow_mail_fallback']) ? ' checked' : ''; ?>> Allow PHP <code>mail()</code> fallback if SMTP fails (uses the VPS local mailer — lets OTP work without an App Password, but lower deliverability)</label></div>
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save SMTP settings</button>
				</div>
			</div>
		</form>
		<script>
		(function () {
			var btns = document.querySelectorAll('.epc-smtp-preset');
			for (var i = 0; i < btns.length; i++) {
				btns[i].addEventListener('click', function () {
					var host = document.getElementById('epc-smtp-host');
					var port = document.getElementById('epc-smtp-port');
					var enc = document.getElementById('epc-smtp-enc');
					if (host) { host.value = this.getAttribute('data-host'); }
					if (port) { port.value = this.getAttribute('data-port'); }
					if (enc) { enc.value = this.getAttribute('data-enc'); }
				});
			}
		})();
		</script>

		<form method="post" class="form-inline epc-scp-auth-settings__smtp-test" style="margin-top:12px">
			<input type="hidden" name="epc_auth_action" value="smtp_test" />
			<label>Send test email to</label>
			<input type="email" name="smtp_test_to" class="form-control input-sm" value="<?php echo epc_cp_auth_settings_h($adminEmail); ?>" placeholder="you@example.com" required />
			<button type="submit" class="btn btn-primary btn-sm">Send test email</button>
		</form>
		<?php if (is_array($smtpTestResult)) { ?>
		<div class="alert alert-<?php echo !empty($smtpTestResult['ok']) ? 'success' : 'danger'; ?>" style="margin-top:10px">
			<strong><?php echo !empty($smtpTestResult['ok']) ? 'Sent' : 'Send failed'; ?>:</strong>
			<?php echo epc_cp_auth_settings_h((string) ($smtpTestResult['message'] ?? '')); ?>
			<?php if (empty($smtpTestResult['ok']) && trim((string) ($smtpTestResult['detail'] ?? '')) !== '' && (string) ($smtpTestResult['detail'] ?? '') !== 'precheck') { ?>
			<pre class="small" style="margin-top:8px;white-space:pre-wrap;max-height:220px;overflow:auto"><?php echo epc_cp_auth_settings_h((string) $smtpTestResult['detail']); ?></pre>
			<?php } ?>
		</div>
		<?php } ?>

		<h5>Demo OTP lookup (operators only)</h5>
		<form method="post" class="form-inline epc-scp-auth-settings__otp-lookup">
			<input type="hidden" name="epc_auth_action" value="otp_lookup" />
			<label>Email</label>
			<input type="email" name="otp_lookup_email" class="form-control input-sm" required />
			<button type="submit" class="btn btn-default btn-sm">View last logged OTP</button>
		</form>
		<?php if (is_array($otpLookup) && !empty($otpLookup['ok'])) { ?>
		<p class="alert alert-success epc-scp-auth-settings__otp-result">
			<strong><?php echo epc_cp_auth_settings_h($otpLookup['email']); ?></strong>
			code <code class="epc-scp-auth-settings__otp-code"><?php echo epc_cp_auth_settings_h($otpLookup['code']); ?></code>
		</p>
		<?php } ?>

		<h4>Operator checklist</h4>
		<ol class="small">
			<li>Create OAuth 2.0 Web client in Google Cloud Console.</li>
			<li>Add redirect URI: <code><?php echo epc_cp_auth_settings_h($callback); ?></code></li>
			<li>Deploy <code>config.epc-oauth.php</code> and <code>config.epc-smtp.php</code> on production.</li>
			<li>Save toggles above, then test on a demo CP login.</li>
		</ol>
		<p class="text-muted small">Guide: <code>docs/EPC-CP-AUTH-MODERN.md</code></p>
	</div>
</div>
</div>
				<?php
			}
		}

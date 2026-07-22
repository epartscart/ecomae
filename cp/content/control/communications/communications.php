<?php
// Communications — e-mail / SMS delivery status + test send
defined('_ASTEXE_') or die('No access');

if (isset($_POST['action'])) {
	// Reserved for future actions
} else {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();

	$backend = trim((string) $DP_Config->backend_dir, '/');
	$baseCp = '/' . $backend;
	$domain = rtrim((string) $DP_Config->domain_path, '/') . '/';

	$email_settings_pointed = (
		!empty($DP_Config->from_name)
		&& !empty($DP_Config->from_email)
		&& !empty($DP_Config->smtp_mode)
		&& !empty($DP_Config->smtp_encryption)
		&& !empty($DP_Config->smtp_host)
		&& !empty($DP_Config->smtp_port)
		&& !empty($DP_Config->smtp_username)
		&& !empty($DP_Config->smtp_password)
	);

	$email_debug = false;
	if ($email_settings_pointed) {
		$email_debug_query = $db_link->prepare('SELECT * FROM `debug_results` WHERE `name` = ?;');
		$email_debug_query->execute(array('email'));
		$email_debug = $email_debug_query->fetch();
	}

	$sms_settings_pointed = false;
	$sms_operator_name = '';
	$sms_sender = '';
	$check_sms_query = $db_link->prepare('SELECT `name`, `parameters_values` FROM `sms_api` WHERE `active` = ? LIMIT 1;');
	$check_sms_query->execute(array(1));
	$sms_active = $check_sms_query->fetch(PDO::FETCH_ASSOC);
	if ($sms_active) {
		$sms_settings_pointed = true;
		$sms_operator_name = (string) ($sms_active['name'] ?? '');
		$smsVals = json_decode((string) ($sms_active['parameters_values'] ?? ''), true);
		if (is_array($smsVals)) {
			foreach (array('sender_number', 'from', 'sender', 'sender_id') as $sk) {
				if (!empty($smsVals[$sk])) {
					$sms_sender = (string) $smsVals[$sk];
					break;
				}
			}
		}
	}

	$sms_debug = false;
	if ($sms_settings_pointed) {
		$sms_debug_query = $db_link->prepare('SELECT * FROM `debug_results` WHERE `name` = ?;');
		$sms_debug_query->execute(array('sms'));
		$sms_debug = $sms_debug_query->fetch();
	}

	$my_admin_profile = DP_User::getAdminProfile();
	$email_for_test_letter = '';
	if (!empty($my_admin_profile['email'])) {
		$email_for_test_letter = (string) $my_admin_profile['email'];
	} elseif (!empty($DP_Config->from_email)) {
		$email_for_test_letter = (string) $DP_Config->from_email;
	}
	$phone_for_test_sms = !empty($my_admin_profile['phone']) ? (string) $my_admin_profile['phone'] : '';

	$epc_cn_h = static function ($v): string {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};
	$epc_cn_t = static function ($id) use ($epc_cn_h): string {
		$v = function_exists('translate_str_by_id') ? translate_str_by_id($id) : $id;
		return $epc_cn_h($v === null || $v === false ? '' : $v);
	};

	$formatDebug = static function ($debug) use ($epc_cn_h, $epc_cn_t): array {
		if ($debug === false || !is_array($debug)) {
			return array(
				'pill' => 'idle',
				'label' => $epc_cn_t(2415),
				'meta' => 'No successful test recorded yet. Send a test below.',
			);
		}
		$when = date('d.m.Y H:i:s', (int) $debug['time']);
		$age = time() - (int) $debug['time'];
		if ((int) $debug['status'] === 1) {
			$ageClass = '';
			$ageNote = '';
			if ($age >= 86400 && $age < 604800) {
				$ageClass = 'is-stale';
				$ageNote = ' · ' . strip_tags($epc_cn_t(2416));
			} elseif ($age >= 604800) {
				$ageClass = 'is-old';
				$ageNote = ' · ' . strip_tags($epc_cn_t(2417));
			}
			return array(
				'pill' => 'ok',
				'label' => $epc_cn_t(2418),
				'meta' => $epc_cn_t(2419) . ' <span class="' . $ageClass . '">' . $epc_cn_h($when) . '</span>' . $epc_cn_h($ageNote),
			);
		}
		return array(
			'pill' => 'bad',
			'label' => $epc_cn_t(2420),
			'meta' => $epc_cn_t(2433) . ' ' . $epc_cn_h($when)
				. '<br>' . $epc_cn_t(2421) . ': <code>' . $epc_cn_h((string) $debug['debug_result']) . '</code>',
		);
	};

	$emailDbg = $formatDebug($email_debug);
	$smsDbg = $formatDebug($sms_debug);

	require_once 'content/control/actions_alert.php';
	?>
	<div class="col-lg-12 epc-cn" id="epc-cn-comms-root">
		<div class="epc-cn-hero">
			<h3>Communications</h3>
			<p>Check e-mail (SMTP) and SMS delivery, then send a test message. Configure templates under Notification settings, SMTP under Configuration, and the operator under SMS Operators.</p>
			<div class="epc-cn-hero__actions">
				<a class="btn btn-sm btn-primary" href="<?php echo $epc_cn_h($baseCp . '/control/config?need_config_group=3'); ?>"><i class="far fa-envelope"></i> E-mail settings</a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp . '/control/sms-operatory'); ?>"><i class="fas fa-mobile-alt"></i> SMS operators</a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp . '/control/notifications_settings'); ?>"><i class="fas fa-envelope-open-text"></i> Templates</a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp); ?>"><i class="fas fa-home"></i> <?php echo $epc_cn_t(2116); ?></a>
			</div>
		</div>

		<div class="epc-cn-quick">
			<a class="is-email" href="<?php echo $epc_cn_h($baseCp . '/control/config?need_config_group=3'); ?>">
				<span class="epc-cn-quick__icon"><i class="far fa-envelope"></i></span>
				<strong>SMTP / From address</strong>
				<span>Host, port, encryption, username, From name &amp; e-mail.</span>
			</a>
			<a class="is-sms" href="<?php echo $epc_cn_h($baseCp . '/control/sms-operatory'); ?>">
				<span class="epc-cn-quick__icon"><i class="fas fa-mobile-alt"></i></span>
				<strong>SMS operator</strong>
				<span>Activate Etisalat, du, Unifonic, or Pakistan — set sender number.</span>
			</a>
			<a class="is-templates" href="<?php echo $epc_cn_h($baseCp . '/control/notifications_settings'); ?>">
				<span class="epc-cn-quick__icon"><i class="fas fa-envelope-open-text"></i></span>
				<strong>Notification templates</strong>
				<span>Turn e-mail / SMS on per event and edit message bodies.</span>
			</a>
			<a href="<?php echo $epc_cn_h($baseCp . '/control/cp-guideline'); ?>">
				<span class="epc-cn-quick__icon"><i class="fas fa-book"></i></span>
				<strong>CP guideline</strong>
				<span>Full map of System → Communications workflows.</span>
			</a>
		</div>

		<div class="epc-cn-guide">
			<h4>How to get messaging working</h4>
			<ol class="epc-cn-steps">
				<li><strong>Configure e-mail</strong>Fill SMTP fields in Configuration → E-mail group, then return here to test.</li>
				<li><strong>Activate SMS</strong>Pick one operator, paste API credentials, confirm sender number (default +971567607011).</li>
				<li><strong>Edit templates</strong>Enable e-mail/SMS per event under Notification settings.</li>
				<li><strong>Test here</strong>Send a test to your inbox / mobile. Green status means the last test succeeded.</li>
			</ol>
		</div>

		<div class="epc-cn-grid">
			<div class="epc-cn-card is-email">
				<div class="epc-cn-card__head">
					<div>
						<h4><i class="far fa-envelope"></i> E-mail</h4>
						<p><?php echo $epc_cn_t(2410); ?></p>
					</div>
					<a class="btn btn-sm btn-success" href="<?php echo $epc_cn_h($baseCp . '/control/config?need_config_group=3'); ?>"><i class="far fa-envelope"></i> <?php echo $epc_cn_t(2423); ?></a>
				</div>
				<div class="epc-cn-card__body">
					<div class="epc-cn-status">
						<?php if ($email_settings_pointed) { ?>
							<span class="epc-cn-pill is-ok"><i class="fas fa-check-circle"></i> <?php echo $epc_cn_t(2412); ?></span>
						<?php } else { ?>
							<span class="epc-cn-pill is-bad"><i class="fas fa-exclamation-triangle"></i> <?php echo $epc_cn_t(2413); ?></span>
						<?php } ?>
						<span class="epc-cn-pill is-<?php echo $epc_cn_h($emailDbg['pill']); ?>"><i class="fas fa-vial"></i> <?php echo $emailDbg['label']; ?></span>
					</div>
					<div class="epc-cn-meta">
						<?php if ($email_settings_pointed) { ?>
							From <code><?php echo $epc_cn_h((string) $DP_Config->from_email); ?></code>
							<?php if (!empty($DP_Config->smtp_host)) { ?>
								· SMTP <code><?php echo $epc_cn_h((string) $DP_Config->smtp_host); ?>:<?php echo $epc_cn_h((string) $DP_Config->smtp_port); ?></code>
							<?php } ?>
							<br><?php echo $emailDbg['meta']; ?>
						<?php } else { ?>
							<?php echo $epc_cn_t(2422); ?>
						<?php } ?>
					</div>
					<?php if ($email_settings_pointed) { ?>
						<div class="epc-cn-test">
							<label for="email_for_test"><?php echo $epc_cn_t(2424); ?></label>
							<div class="input-group">
								<input type="text" class="form-control" placeholder="<?php echo $epc_cn_t(2427); ?>" value="<?php echo $epc_cn_h($email_for_test_letter); ?>" id="email_for_test" />
								<span class="input-group-btn">
									<button type="button" class="btn btn-primary" onclick="epcCnTestEmail();"><i class="far fa-envelope"></i> <?php echo $epc_cn_t(2426); ?></button>
								</span>
							</div>
							<p class="help"><?php echo $epc_cn_t(2425); ?></p>
						</div>
					<?php } ?>
				</div>
			</div>

			<div class="epc-cn-card is-sms">
				<div class="epc-cn-card__head">
					<div>
						<h4><i class="fas fa-mobile-alt"></i> SMS</h4>
						<p><?php echo $epc_cn_t(2431); ?></p>
					</div>
					<a class="btn btn-sm btn-success" href="<?php echo $epc_cn_h($baseCp . '/control/sms-operatory'); ?>"><i class="fas fa-mobile-alt"></i> <?php echo $epc_cn_t(2435); ?></a>
				</div>
				<div class="epc-cn-card__body">
					<div class="epc-cn-status">
						<?php if ($sms_settings_pointed) { ?>
							<span class="epc-cn-pill is-ok"><i class="fas fa-check-circle"></i> <?php echo $epc_cn_t(2412); ?></span>
						<?php } else { ?>
							<span class="epc-cn-pill is-bad"><i class="fas fa-exclamation-triangle"></i> <?php echo $epc_cn_t(2413); ?></span>
						<?php } ?>
						<span class="epc-cn-pill is-<?php echo $epc_cn_h($smsDbg['pill']); ?>"><i class="fas fa-vial"></i> <?php echo $smsDbg['label']; ?></span>
					</div>
					<div class="epc-cn-meta">
						<?php if ($sms_settings_pointed) { ?>
							Operator <code><?php echo $epc_cn_h($sms_operator_name !== '' ? $sms_operator_name : 'Active'); ?></code>
							<?php if ($sms_sender !== '') { ?>
								· From <code><?php echo $epc_cn_h($sms_sender); ?></code>
							<?php } ?>
							<br><?php echo $smsDbg['meta']; ?>
						<?php } else { ?>
							<?php echo $epc_cn_t(2434); ?>
						<?php } ?>
					</div>
					<?php if ($sms_settings_pointed) { ?>
						<div class="epc-cn-test">
							<label for="phone_for_test"><?php echo $epc_cn_t(2436); ?></label>
							<div class="input-group">
								<input type="text" class="form-control" placeholder="<?php echo $epc_cn_t(2438); ?>" value="<?php echo $epc_cn_h($phone_for_test_sms); ?>" id="phone_for_test" />
								<span class="input-group-btn">
									<button type="button" class="btn btn-primary" onclick="epcCnTestSms();"><i class="fas fa-mobile-alt"></i> <?php echo $epc_cn_t(2426); ?></button>
								</span>
							</div>
							<p class="help"><?php echo $epc_cn_t(2437); ?></p>
						</div>
					<?php } ?>
				</div>
			</div>
		</div>
	</div>

	<script>
	window.EPC_COMMS_NOTIFY = {
		page: 'communications',
		ajaxUrl: <?php echo json_encode($domain . $backend . '/content/control/communications/ajax_test_notification.php', JSON_UNESCAPED_SLASHES); ?>,
		reloadUrl: <?php echo json_encode($domain . $backend . '/control/communications', JSON_UNESCAPED_SLASHES); ?>,
		csrf: <?php echo json_encode((string) ($user_session['csrf_guard_key'] ?? ''), JSON_UNESCAPED_UNICODE); ?>,
		messages: {
			emailRequired: <?php echo json_encode((string) translate_str_by_id(2428), JSON_UNESCAPED_UNICODE); ?>,
			phoneRequired: <?php echo json_encode((string) translate_str_by_id(2439), JSON_UNESCAPED_UNICODE); ?>,
			badResponse: <?php echo json_encode((string) translate_str_by_id(2429), JSON_UNESCAPED_UNICODE); ?>,
			emailOk: <?php echo json_encode((string) translate_str_by_id(2430), JSON_UNESCAPED_UNICODE); ?>,
			smsOk: <?php echo json_encode((string) translate_str_by_id(2440), JSON_UNESCAPED_UNICODE); ?>
		}
	};
	</script>
	<?php
}
?>

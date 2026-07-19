<?php
/**
 * Frontend vendor self-registration — creates storefront user + vendor account (auto-approved).
 * URL: /vendor/register
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/vendor/epc_vendor_access.php';

global $DP_Config, $db_link;
$urls = epc_vendor_urls();
$uid = (int) DP_User::getUserId();
$errors = array();
$success = '';

if ($uid > 0 && isset($db_link) && $db_link instanceof PDO) {
	$existing = epc_vendor_get_account($db_link, $uid);
	if ($existing) {
		echo '<script>location = ' . json_encode($urls['home']) . ';</script>';
		echo '<p style="padding:24px;"><a href="' . htmlspecialchars($urls['home'], ENT_QUOTES, 'UTF-8') . '">Open vendor portal</a></p>';
		return;
	}
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($db_link) && $db_link instanceof PDO) {
	$email = strtolower(trim((string) ($_POST['email'] ?? '')));
	$password = (string) ($_POST['password'] ?? '');
	$password2 = (string) ($_POST['password2'] ?? '');
	$contact = trim((string) ($_POST['contact_name'] ?? ''));
	$phone = trim((string) ($_POST['phone'] ?? ''));
	$city = trim((string) ($_POST['city'] ?? ''));
	$vendorFull = trim((string) ($_POST['vendor_full'] ?? ''));
	$vendorShort = trim((string) ($_POST['vendor_short'] ?? ''));

	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		$errors[] = 'Enter a valid email address.';
	}
	if (strlen($password) < 6) {
		$errors[] = 'Password must be at least 6 characters.';
	}
	if ($password !== $password2) {
		$errors[] = 'Passwords do not match.';
	}
	if ($vendorFull === '') {
		$errors[] = 'Company / vendor full name is required.';
	}
	if ($vendorShort === '') {
		$errors[] = 'Vendor short code is required (shown on the storefront).';
	}
	$vendorShort = epc_multivendor_sanitize_short($vendorShort);
	$vendorFull = epc_multivendor_sanitize_full($vendorFull);
	if ($vendorShort === '') {
		$errors[] = 'Vendor short code is invalid.';
	}

	if (!$errors) {
		epc_vendor_ensure_schema($db_link);
		$dup = $db_link->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
		$dup->execute(array($email));
		if ((int) $dup->fetchColumn() > 0) {
			$errors[] = 'This email is already registered. Sign in instead.';
		}
		$dupS = $db_link->prepare('SELECT `id` FROM `epc_vendor_accounts` WHERE UPPER(`vendor_short`) = UPPER(?) LIMIT 1');
		$dupS->execute(array($vendorShort));
		if ((int) $dupS->fetchColumn() > 0) {
			$errors[] = 'That vendor short code is already taken. Choose another.';
		}
	}

	if (!$errors) {
		$regVariant = 1;
		try {
			$rv = $db_link->query('SELECT `id` FROM `reg_variants` ORDER BY `order` ASC LIMIT 1')->fetchColumn();
			if ($rv) {
				$regVariant = (int) $rv;
			}
		} catch (Exception $e) {
		}
		$hash = md5($password . $DP_Config->secret_succession);
		$ok = $db_link->prepare(
			'INSERT INTO `users` (`email`, `reg_variant`, `password`, `email_confirmed`, `time_registered`, `unlocked`) VALUES (?, ?, ?, 1, ?, 1)'
		)->execute(array($email, $regVariant, $hash, time()));
		if (!$ok) {
			$errors[] = 'Could not create account. Try again.';
		} else {
			$userId = (int) $db_link->lastInsertId();
			// Also bind to default registered customers group (storefront browsing).
			try {
				$g = $db_link->query('SELECT `id` FROM `groups` WHERE `for_registrated` = 1 LIMIT 1')->fetchColumn();
				if ($g) {
					$db_link->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)')
						->execute(array($userId, (int) $g));
				}
			} catch (Exception $e) {
			}
			$gid = epc_vendor_group_id($db_link);
			$db_link->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)')
				->execute(array($userId, $gid));

			$now = time();
			$db_link->prepare(
				'INSERT INTO `epc_vendor_accounts`
				(`user_id`, `storage_id`, `vendor_full`, `vendor_short`, `contact_name`, `phone`, `city`, `status`, `created_at`, `updated_at`)
				VALUES (?, 0, ?, ?, ?, ?, ?, \'pending\', ?, ?)'
			)->execute(array($userId, $vendorFull, $vendorShort, $contact, $phone, $city, $now, $now));
			$accountId = (int) $db_link->lastInsertId();
			// Auto-approve so vendors can upload without CP access.
			epc_vendor_approve_account($db_link, $accountId, 0);
			$success = 'Vendor account created for ' . $vendorShort . '. Sign in below to upload your price list.';
			// Prefer clean GET after POST (CMS often already sent headers — JS fallback).
			$goto = $urls['home'] . '?registered=1';
			echo '<script>location = ' . json_encode($goto) . ';</script>';
			echo '<noscript><meta http-equiv="refresh" content="0;url=' . htmlspecialchars($goto, ENT_QUOTES, 'UTF-8') . '"></noscript>';
			echo '<div class="epc-vp__alert epc-vp__alert--ok" style="max-width:520px;margin:40px auto;">'
				. htmlspecialchars($success, ENT_QUOTES, 'UTF-8')
				. ' <a href="' . htmlspecialchars($goto, ENT_QUOTES, 'UTF-8') . '">Open vendor portal</a></div>';
			return;
		}
	}
}
?>
<link rel="stylesheet" href="/content/shop/vendor/epc_vendor_portal.css?v=20260719vp1">
<section class="epc-vp">
	<div class="epc-vp__wrap">
		<div class="epc-vp__card">
			<div class="epc-vp__brand">eParts<span>Cart</span> Vendor</div>
			<h1>Register as a vendor</h1>
			<p class="epc-vp__lead">Sell from the storefront — register, then upload your price list. No control-panel login needed.</p>

			<?php if ($errors) { ?>
			<div class="epc-vp__alert epc-vp__alert--err">
				<?php foreach ($errors as $e) { echo '<div>' . htmlspecialchars($e, ENT_QUOTES, 'UTF-8') . '</div>'; } ?>
			</div>
			<?php } ?>
			<?php if ($success) { ?>
			<div class="epc-vp__alert epc-vp__alert--ok"><?php echo htmlspecialchars($success, ENT_QUOTES, 'UTF-8'); ?></div>
			<?php } ?>

			<form method="post" class="epc-vp__form" autocomplete="on">
				<label>Email *</label>
				<input type="email" name="email" required value="<?php echo htmlspecialchars((string) ($_POST['email'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />

				<div class="epc-vp__row">
					<div>
						<label>Password *</label>
						<input type="password" name="password" required minlength="6" />
					</div>
					<div>
						<label>Confirm password *</label>
						<input type="password" name="password2" required minlength="6" />
					</div>
				</div>

				<label>Contact name</label>
				<input type="text" name="contact_name" value="<?php echo htmlspecialchars((string) ($_POST['contact_name'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />

				<div class="epc-vp__row">
					<div>
						<label>Phone</label>
						<input type="text" name="phone" value="<?php echo htmlspecialchars((string) ($_POST['phone'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
					</div>
					<div>
						<label>City</label>
						<input type="text" name="city" value="<?php echo htmlspecialchars((string) ($_POST['city'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
					</div>
				</div>

				<label>Company / vendor full name * <span class="epc-vp__hint">(backend warehouse name)</span></label>
				<input type="text" name="vendor_full" required maxlength="255" value="<?php echo htmlspecialchars((string) ($_POST['vendor_full'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" placeholder="S-UAE Trading LLC" />

				<label>Vendor short code * <span class="epc-vp__hint">(shown to customers)</span></label>
				<input type="text" name="vendor_short" required maxlength="64" value="<?php echo htmlspecialchars((string) ($_POST['vendor_short'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" placeholder="S-UAE" />

				<button type="submit" class="epc-vp__btn">Create vendor account</button>
			</form>

			<p class="epc-vp__foot">
				Already a vendor? <a href="<?php echo htmlspecialchars($urls['home'], ENT_QUOTES, 'UTF-8'); ?>">Sign in to your portal</a>
			</p>
		</div>
	</div>
</section>

<?php
/**
 * Frontend vendor self-registration — UAE e-invoice compliant seller identity.
 * URL: /vendor/register
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/vendor/epc_vendor_access.php';

$einvoiceFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
$uaeTaxFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
if (is_file($uaeTaxFile)) {
	require_once $uaeTaxFile;
}
if (is_file($einvoiceFile)) {
	require_once $einvoiceFile;
}

global $DP_Config, $db_link;
$urls = epc_vendor_urls();
$uid = (int) DP_User::getUserId();
$errors = array();
$success = '';
$emirates = epc_vendor_uae_emirates();
$regTypes = epc_vendor_legal_reg_types();
if (isset($db_link) && $db_link instanceof PDO) {
	epc_vendor_ensure_schema($db_link);
}

$pv = function ($key, $default = '') {
	return htmlspecialchars((string) ($_POST[$key] ?? $default), ENT_QUOTES, 'UTF-8');
};

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
	$jobTitle = trim((string) ($_POST['contact_job_title'] ?? ''));
	$phone = trim((string) ($_POST['phone'] ?? ''));
	$billingEmail = strtolower(trim((string) ($_POST['billing_email'] ?? '')));
	$vendorFull = trim((string) ($_POST['vendor_full'] ?? ''));
	$vendorShort = trim((string) ($_POST['vendor_short'] ?? ''));
	$legalName = trim((string) ($_POST['legal_name'] ?? ''));
	$vatRegistered = !empty($_POST['vat_registered']) ? 1 : 0;
	$trn = preg_replace('/\D/', '', (string) ($_POST['trn'] ?? ''));
	$legalRegNo = trim((string) ($_POST['legal_reg_no'] ?? ''));
	$legalRegType = strtoupper(trim((string) ($_POST['legal_reg_type'] ?? 'TL')));
	$authority = trim((string) ($_POST['authority_name'] ?? ''));
	$address1 = trim((string) ($_POST['address_line1'] ?? ''));
	$address2 = trim((string) ($_POST['address_line2'] ?? ''));
	$city = trim((string) ($_POST['city'] ?? ''));
	$emirate = trim((string) ($_POST['emirate'] ?? ''));
	$postal = trim((string) ($_POST['postal_code'] ?? ''));
	$country = strtoupper(trim((string) ($_POST['country_code'] ?? 'AE')));
	if ($country === '') {
		$country = 'AE';
	}
	if (!isset($regTypes[$legalRegType])) {
		$legalRegType = 'TL';
	}
	if ($authority === '' && $emirate !== '') {
		$authority = epc_vendor_authority_for_emirate($emirate);
	}

	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		$errors[] = 'Enter a valid login email address.';
	}
	if (strlen($password) < 6) {
		$errors[] = 'Password must be at least 6 characters.';
	}
	if ($password !== $password2) {
		$errors[] = 'Passwords do not match.';
	}
	if ($contact === '') {
		$errors[] = 'Contact person name is required.';
	}
	if ($phone === '') {
		$errors[] = 'Phone / mobile is required.';
	}
	if ($billingEmail !== '' && !filter_var($billingEmail, FILTER_VALIDATE_EMAIL)) {
		$errors[] = 'Billing email is invalid.';
	}
	if ($vendorFull === '') {
		$errors[] = 'Trade / storefront name is required.';
	}
	if ($vendorShort === '') {
		$errors[] = 'Vendor short code is required (shown on the storefront).';
	}
	if ($legalName === '') {
		$errors[] = 'Legal entity name (as on trade licence) is required for UAE e-invoicing.';
	}
	if ($legalRegNo === '') {
		$errors[] = 'Trade licence / legal registration number is required.';
	}
	if ($address1 === '') {
		$errors[] = 'Business address line 1 is required.';
	}
	if ($city === '') {
		$errors[] = 'City is required.';
	}
	if ($country === 'AE') {
		if ($emirate === '' || !in_array($emirate, $emirates, true)) {
			$errors[] = 'Select a UAE emirate (country subdivision) for e-invoicing.';
		}
		if ($authority === '') {
			$errors[] = 'Licensing authority name is required.';
		}
		if ($vatRegistered) {
			if (!function_exists('epc_uae_validate_trn') || !epc_uae_validate_trn($trn, true)) {
				$errors[] = 'UAE TRN must be exactly 15 digits (FTA tax registration number).';
			}
		} elseif ($trn !== '' && function_exists('epc_uae_validate_trn') && !epc_uae_validate_trn($trn, false)) {
			$errors[] = 'If provided, TRN must be exactly 15 digits.';
		}
	} elseif ($trn !== '' && function_exists('epc_uae_validate_trn') && !epc_uae_validate_trn($trn, false)) {
		$errors[] = 'If provided, TRN must be exactly 15 digits.';
	}

	$vendorShort = epc_multivendor_sanitize_short($vendorShort);
	$vendorFull = epc_multivendor_sanitize_full($vendorFull);
	if ($vendorShort === '') {
		$errors[] = 'Vendor short code is invalid.';
	}

	$tin = '';
	$peppol = '';
	if ($trn !== '' && function_exists('epc_einvoice_tin_from_trn')) {
		$tin = epc_einvoice_tin_from_trn($trn);
		$peppol = function_exists('epc_einvoice_peppol_endpoint') ? epc_einvoice_peppol_endpoint($tin) : ('0235:' . $tin);
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
		if ($trn !== '') {
			$dupT = $db_link->prepare('SELECT `id` FROM `epc_vendor_accounts` WHERE `trn` = ? AND `trn` <> \'\' LIMIT 1');
			$dupT->execute(array($trn));
			if ((int) $dupT->fetchColumn() > 0) {
				$errors[] = 'This TRN is already registered to another vendor.';
			}
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
				(`user_id`, `storage_id`, `vendor_full`, `vendor_short`,
				 `legal_name`, `trn`, `vat_registered`, `tin`, `peppol_endpoint`,
				 `legal_reg_no`, `legal_reg_type`, `authority_name`,
				 `address_line1`, `address_line2`, `city`, `emirate`, `postal_code`, `country_code`,
				 `contact_name`, `contact_job_title`, `phone`, `billing_email`,
				 `status`, `created_at`, `updated_at`)
				VALUES (?, 0, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, \'pending\', ?, ?)'
			)->execute(array(
				$userId, $vendorFull, $vendorShort,
				$legalName, $trn, $vatRegistered, $tin, $peppol,
				$legalRegNo, $legalRegType, $authority,
				$address1, $address2, $city, $emirate, $postal, $country,
				$contact, $jobTitle, $phone, $billingEmail,
				$now, $now,
			));
			$accountId = (int) $db_link->lastInsertId();
			epc_vendor_approve_account($db_link, $accountId, 0);
			$success = 'Vendor account created for ' . $vendorShort . '. Sign in below to upload your price list.';
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

$selEmirate = (string) ($_POST['emirate'] ?? '');
$selRegType = (string) ($_POST['legal_reg_type'] ?? 'TL');
$vatChecked = !isset($_POST['email']) || !empty($_POST['vat_registered']);
?>
<link rel="stylesheet" href="/content/shop/vendor/epc_vendor_portal.css?v=20260719vpNav2">
<section class="epc-vp">
	<div class="epc-vp__wrap">
		<div class="epc-vp__card epc-vp__card--wide">
			<div class="epc-vp__brand">eParts<span>Cart</span> Vendor</div>
			<h1>Register as a vendor</h1>
			<p class="epc-vp__lead">Sell from the storefront with UAE e-invoice ready seller details (TRN, trade licence, address). No control-panel login needed.</p>

			<div class="epc-vp__notice">
				<strong>UAE e-invoicing (FTA / PINT-AE)</strong>
				Seller tax invoices require legal name, 15-digit TRN (when VAT-registered), trade licence identifier, Peppol electronic address, and full UAE address with emirate.
			</div>

			<?php if ($errors) { ?>
			<div class="epc-vp__alert epc-vp__alert--err">
				<?php foreach ($errors as $e) { echo '<div>' . htmlspecialchars($e, ENT_QUOTES, 'UTF-8') . '</div>'; } ?>
			</div>
			<?php } ?>
			<?php if ($success) { ?>
			<div class="epc-vp__alert epc-vp__alert--ok"><?php echo htmlspecialchars($success, ENT_QUOTES, 'UTF-8'); ?></div>
			<?php } ?>

			<form method="post" class="epc-vp__form" autocomplete="on" id="epc-vendor-register-form">
				<div class="epc-vp__top-actions">
					<button type="submit" class="epc-vp__btn">Create vendor account</button>
				</div>
				<h2 class="epc-vp__section">1. Account login</h2>
				<label>Login email *</label>
				<input type="email" name="email" required value="<?php echo $pv('email'); ?>" />

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

				<h2 class="epc-vp__section">2. Contact person</h2>
				<div class="epc-vp__row">
					<div>
						<label>Contact name *</label>
						<input type="text" name="contact_name" required maxlength="190" value="<?php echo $pv('contact_name'); ?>" />
					</div>
					<div>
						<label>Job title</label>
						<input type="text" name="contact_job_title" maxlength="120" value="<?php echo $pv('contact_job_title'); ?>" placeholder="Accounts / Sales" />
					</div>
				</div>
				<div class="epc-vp__row">
					<div>
						<label>Phone / mobile *</label>
						<input type="text" name="phone" required maxlength="64" value="<?php echo $pv('phone'); ?>" placeholder="+971…" />
					</div>
					<div>
						<label>Billing email <span class="epc-vp__hint">(if different)</span></label>
						<input type="email" name="billing_email" maxlength="190" value="<?php echo $pv('billing_email'); ?>" />
					</div>
				</div>

				<h2 class="epc-vp__section">3. Legal &amp; tax identity (UAE e-invoice)</h2>
				<label>Legal entity name * <span class="epc-vp__hint">(as on trade licence)</span></label>
				<input type="text" name="legal_name" required maxlength="255" value="<?php echo $pv('legal_name'); ?>" placeholder="S-UAE Trading L.L.C" />

				<label>Trade / storefront name * <span class="epc-vp__hint">(warehouse / customer-facing)</span></label>
				<input type="text" name="vendor_full" required maxlength="255" value="<?php echo $pv('vendor_full'); ?>" placeholder="S-UAE Trading" />

				<label class="epc-vp__check">
					<input type="checkbox" name="vat_registered" value="1" id="epc_vat_registered" <?php echo $vatChecked ? 'checked' : ''; ?> />
					VAT registered in the UAE
				</label>

				<label>Tax Registration Number (TRN) <span class="epc-vp__hint" id="epc_trn_req_hint">* 15 digits</span></label>
				<input type="text" name="trn" id="epc_vendor_trn" inputmode="numeric" maxlength="15" value="<?php echo $pv('trn'); ?>" placeholder="100123456700003" />
				<p class="epc-vp__field-help">FTA TRN is mandatory on tax invoices when VAT-registered. TIN / Peppol endpoint are derived automatically (scheme 0235).</p>

				<div class="epc-vp__row">
					<div>
						<label>Legal registration type *</label>
						<select name="legal_reg_type" required>
							<?php foreach ($regTypes as $code => $label) { ?>
							<option value="<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $selRegType === $code ? 'selected' : ''; ?>><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></option>
							<?php } ?>
						</select>
					</div>
					<div>
						<label>Trade licence / reg. no. *</label>
						<input type="text" name="legal_reg_no" required maxlength="64" value="<?php echo $pv('legal_reg_no'); ?>" placeholder="Commercial licence number" />
					</div>
				</div>

				<label>Licensing authority *</label>
				<input type="text" name="authority_name" id="epc_authority_name" required maxlength="255" value="<?php echo $pv('authority_name'); ?>" placeholder="Dubai Economy and Tourism" />

				<h2 class="epc-vp__section">4. Business address</h2>
				<label>Address line 1 *</label>
				<input type="text" name="address_line1" required maxlength="255" value="<?php echo $pv('address_line1'); ?>" placeholder="Building, street, area" />

				<label>Address line 2</label>
				<input type="text" name="address_line2" maxlength="255" value="<?php echo $pv('address_line2'); ?>" placeholder="Office / unit (optional)" />

				<div class="epc-vp__row">
					<div>
						<label>City *</label>
						<input type="text" name="city" required maxlength="120" value="<?php echo $pv('city'); ?>" placeholder="Dubai" />
					</div>
					<div>
						<label>Emirate * <span class="epc-vp__hint">(country subdivision)</span></label>
						<select name="emirate" id="epc_vendor_emirate" required>
							<option value="">Select emirate…</option>
							<?php foreach ($emirates as $em) { ?>
							<option value="<?php echo htmlspecialchars($em, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $selEmirate === $em ? 'selected' : ''; ?>><?php echo htmlspecialchars($em, ENT_QUOTES, 'UTF-8'); ?></option>
							<?php } ?>
						</select>
					</div>
				</div>
				<div class="epc-vp__row">
					<div>
						<label>Postal / P.O. Box</label>
						<input type="text" name="postal_code" maxlength="32" value="<?php echo $pv('postal_code'); ?>" />
					</div>
					<div>
						<label>Country code *</label>
						<input type="text" name="country_code" required maxlength="8" value="<?php echo $pv('country_code', 'AE'); ?>" />
					</div>
				</div>

				<h2 class="epc-vp__section">5. Storefront code</h2>
				<label>Vendor short code * <span class="epc-vp__hint">(shown to customers)</span></label>
				<input type="text" name="vendor_short" required maxlength="64" value="<?php echo $pv('vendor_short'); ?>" placeholder="S-UAE" />

				<div class="epc-vp__sticky-submit">
					<button type="submit" class="epc-vp__btn">Create vendor account</button>
				</div>
			</form>

			<p class="epc-vp__foot">
				Already a vendor? <a href="<?php echo htmlspecialchars($urls['home'], ENT_QUOTES, 'UTF-8'); ?>">Sign in to your portal</a>
			</p>
		</div>
	</div>
</section>
<script>
(function(){
	var authorities = <?php echo json_encode(array_combine($emirates, array_map('epc_vendor_authority_for_emirate', $emirates)), JSON_UNESCAPED_UNICODE); ?>;
	var em = document.getElementById('epc_vendor_emirate');
	var auth = document.getElementById('epc_authority_name');
	var vat = document.getElementById('epc_vat_registered');
	var trn = document.getElementById('epc_vendor_trn');
	var hint = document.getElementById('epc_trn_req_hint');
	function syncAuth(){
		if(!em || !auth) return;
		var next = authorities[em.value] || '';
		if(next && (!auth.value || Object.values(authorities).indexOf(auth.value) !== -1)){
			auth.value = next;
		}
	}
	function syncTrn(){
		var on = !!(vat && vat.checked);
		if(trn){
			trn.required = on;
			trn.setAttribute('aria-required', on ? 'true' : 'false');
		}
		if(hint) hint.textContent = on ? '* 15 digits' : '(optional if not VAT-registered)';
	}
	if(em) em.addEventListener('change', syncAuth);
	if(vat) vat.addEventListener('change', syncTrn);
	if(trn){
		trn.addEventListener('input', function(){
			trn.value = (trn.value || '').replace(/\D/g,'').slice(0,15);
		});
	}
	syncAuth();
	syncTrn();
})();
</script>

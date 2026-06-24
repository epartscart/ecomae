<?php
/**
 * MFA UI — enrollment form, verification form, settings panel.
 * Rendered inside the CP/ERP shell via ?epc_mfa=enroll|verify|settings
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

function epc_mfa_render_enroll_page(array $enrollment): string
{
	$secret = htmlspecialchars($enrollment['secret'] ?? '', ENT_QUOTES, 'UTF-8');
	$qrUri = $enrollment['qr_uri'] ?? '';
	$qrImg = epc_mfa_qr_data_uri($qrUri);
	$backupCodes = $enrollment['backup_codes'] ?? array();

	ob_start();
	?>
<div class="epc-mfa-enroll" style="max-width:520px;margin:30px auto;font-family:system-ui,-apple-system,sans-serif;">
	<h3 style="margin-bottom:6px;color:#1a1a2e;">Set Up Two-Factor Authentication</h3>
	<p style="color:#555;margin-bottom:20px;">Scan the QR code below with your authenticator app (Google Authenticator, Authy, Microsoft Authenticator).</p>

	<div style="text-align:center;margin:20px 0;">
		<img src="<?php echo htmlspecialchars($qrImg, ENT_QUOTES, 'UTF-8'); ?>" alt="TOTP QR Code" width="200" height="200" style="border:1px solid #e0e0e0;border-radius:8px;padding:8px;background:#fff;" />
	</div>

	<div style="background:#f8f9fa;border:1px solid #e0e0e0;border-radius:6px;padding:12px;margin:16px 0;font-family:monospace;font-size:13px;word-break:break-all;text-align:center;">
		<small style="color:#888;display:block;margin-bottom:4px;font-family:system-ui;">Manual entry key:</small>
		<?php echo $secret; ?>
	</div>

	<form id="epc-mfa-confirm-form" style="margin-top:20px;">
		<label for="epc-mfa-code" style="font-weight:600;color:#333;display:block;margin-bottom:6px;">Enter the 6-digit code from your app:</label>
		<div style="display:flex;gap:10px;">
			<input type="text" id="epc-mfa-code" name="code" maxlength="6" pattern="[0-9]{6}" placeholder="000000"
				style="flex:1;font-size:24px;text-align:center;letter-spacing:8px;padding:12px;border:2px solid #ddd;border-radius:8px;font-family:monospace;"
				autocomplete="one-time-code" inputmode="numeric" autofocus />
			<button type="submit" style="padding:12px 24px;background:#0d6efd;color:#fff;border:none;border-radius:8px;font-size:16px;font-weight:600;cursor:pointer;">Verify</button>
		</div>
		<div id="epc-mfa-confirm-result" style="margin-top:12px;"></div>
	</form>

	<?php if (!empty($backupCodes)): ?>
	<div style="margin-top:24px;background:#fff3cd;border:1px solid #ffc107;border-radius:8px;padding:16px;">
		<h4 style="margin:0 0 8px;color:#856404;">Backup Codes</h4>
		<p style="font-size:13px;color:#856404;margin-bottom:12px;">Save these codes somewhere safe. Each can be used once if you lose your authenticator.</p>
		<div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;font-family:monospace;font-size:14px;">
			<?php foreach ($backupCodes as $bc): ?>
			<div style="background:#fff;padding:6px 10px;border-radius:4px;border:1px solid #e0e0e0;"><?php echo htmlspecialchars($bc, ENT_QUOTES, 'UTF-8'); ?></div>
			<?php endforeach; ?>
		</div>
	</div>
	<?php endif; ?>
</div>

<script>
document.getElementById('epc-mfa-confirm-form').addEventListener('submit', function(e) {
	e.preventDefault();
	var code = document.getElementById('epc-mfa-code').value.trim();
	if (code.length !== 6) { return; }
	var xhr = new XMLHttpRequest();
	xhr.open('POST', window.location.pathname + '?epc_mfa_ajax=1');
	xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
	xhr.onload = function() {
		var result = document.getElementById('epc-mfa-confirm-result');
		try {
			var data = JSON.parse(xhr.responseText);
			if (data.ok) {
				result.innerHTML = '<div style="color:#198754;font-weight:600;padding:10px;background:#d1e7dd;border-radius:6px;">MFA enabled successfully! Redirecting...</div>';
				setTimeout(function() {
					var redirect = new URLSearchParams(window.location.search).get('redirect');
					window.location.href = redirect || '/cp/';
				}, 1500);
			} else {
				result.innerHTML = '<div style="color:#dc3545;padding:10px;background:#f8d7da;border-radius:6px;">' + (data.error || 'Invalid code') + '</div>';
			}
		} catch (ex) {
			result.innerHTML = '<div style="color:#dc3545;">Error processing response</div>';
		}
	};
	xhr.send('mfa_action=confirm&code=' + encodeURIComponent(code));
});
</script>
	<?php
	return ob_get_clean();
}

function epc_mfa_render_verify_page(): string
{
	ob_start();
	?>
<div class="epc-mfa-verify" style="max-width:420px;margin:60px auto;font-family:system-ui,-apple-system,sans-serif;text-align:center;">
	<div style="font-size:48px;margin-bottom:12px;">&#128274;</div>
	<h3 style="margin-bottom:6px;color:#1a1a2e;">Two-Factor Verification</h3>
	<p style="color:#555;margin-bottom:24px;">Enter the 6-digit code from your authenticator app, or a backup code.</p>

	<form id="epc-mfa-verify-form">
		<input type="text" id="epc-mfa-verify-code" name="code" maxlength="10" placeholder="000000"
			style="font-size:28px;text-align:center;letter-spacing:8px;padding:14px;border:2px solid #ddd;border-radius:10px;width:100%;box-sizing:border-box;font-family:monospace;"
			autocomplete="one-time-code" inputmode="numeric" autofocus />
		<button type="submit" style="margin-top:16px;width:100%;padding:14px;background:#0d6efd;color:#fff;border:none;border-radius:10px;font-size:17px;font-weight:600;cursor:pointer;">Verify</button>
		<div id="epc-mfa-verify-result" style="margin-top:12px;"></div>
	</form>

	<p style="margin-top:20px;font-size:13px;color:#888;">Lost your device? Enter a 10-character backup code instead.</p>
</div>

<script>
document.getElementById('epc-mfa-verify-form').addEventListener('submit', function(e) {
	e.preventDefault();
	var code = document.getElementById('epc-mfa-verify-code').value.trim();
	if (code.length < 6) { return; }
	var xhr = new XMLHttpRequest();
	xhr.open('POST', window.location.pathname + '?epc_mfa_ajax=1');
	xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
	xhr.onload = function() {
		var result = document.getElementById('epc-mfa-verify-result');
		try {
			var data = JSON.parse(xhr.responseText);
			if (data.ok) {
				result.innerHTML = '<div style="color:#198754;font-weight:600;padding:10px;background:#d1e7dd;border-radius:6px;">Verified! Redirecting...</div>';
				setTimeout(function() {
					var redirect = new URLSearchParams(window.location.search).get('redirect');
					window.location.href = redirect || '/cp/';
				}, 1000);
			} else {
				result.innerHTML = '<div style="color:#dc3545;padding:10px;background:#f8d7da;border-radius:6px;">' + (data.error || 'Invalid code') + '</div>';
			}
		} catch (ex) {
			result.innerHTML = '<div style="color:#dc3545;">Error processing response</div>';
		}
	};
	xhr.send('mfa_action=verify&code=' + encodeURIComponent(code));
});
</script>
	<?php
	return ob_get_clean();
}

function epc_mfa_render_settings_panel(array $status): string
{
	$enrolled = $status['enrolled'];
	$backupLeft = $status['backup_codes_left'];
	$verified = $status['session_verified'];

	ob_start();
	?>
<div class="epc-mfa-settings" style="max-width:600px;font-family:system-ui,-apple-system,sans-serif;">
	<h3 style="margin-bottom:16px;color:#1a1a2e;">
		<span style="font-size:20px;margin-right:8px;">&#128274;</span>
		Two-Factor Authentication (MFA)
	</h3>

	<div style="background:<?php echo $enrolled ? '#d1e7dd' : '#fff3cd'; ?>;border:1px solid <?php echo $enrolled ? '#198754' : '#ffc107'; ?>;border-radius:8px;padding:16px;margin-bottom:20px;">
		<strong style="color:<?php echo $enrolled ? '#198754' : '#856404'; ?>;">
			Status: <?php echo $enrolled ? 'Enabled' : 'Not Enabled'; ?>
		</strong>
		<?php if ($enrolled): ?>
			<span style="margin-left:12px;color:#198754;">&#10003; TOTP active</span>
			<?php if ($backupLeft > 0): ?>
				<span style="margin-left:12px;color:#666;"><?php echo $backupLeft; ?> backup codes remaining</span>
			<?php else: ?>
				<span style="margin-left:12px;color:#dc3545;">No backup codes! Generate new ones below.</span>
			<?php endif; ?>
		<?php else: ?>
			<p style="margin:8px 0 0;color:#856404;font-size:14px;">
				MFA is required for finance roles and Super CP access. Enable it now using your authenticator app.
			</p>
		<?php endif; ?>
	</div>

	<?php if (!$enrolled): ?>
	<button onclick="epcMfaStartEnroll()" style="padding:12px 24px;background:#0d6efd;color:#fff;border:none;border-radius:8px;font-size:15px;font-weight:600;cursor:pointer;">
		Enable Two-Factor Authentication
	</button>
	<?php else: ?>
	<div style="display:flex;gap:12px;flex-wrap:wrap;">
		<button onclick="epcMfaRegenBackup()" style="padding:10px 18px;background:#ffc107;color:#333;border:none;border-radius:6px;font-weight:600;cursor:pointer;">
			Regenerate Backup Codes
		</button>
		<button onclick="if(confirm('Disable MFA? Finance routes will require re-enrollment.')){epcMfaDisable();}" style="padding:10px 18px;background:#dc3545;color:#fff;border:none;border-radius:6px;font-weight:600;cursor:pointer;">
			Disable MFA
		</button>
	</div>
	<?php endif; ?>

	<div id="epc-mfa-settings-result" style="margin-top:16px;"></div>

	<?php if (!empty($status['methods'])): ?>
	<div style="margin-top:24px;">
		<h4 style="color:#555;margin-bottom:8px;">Enrolled Methods</h4>
		<table style="width:100%;border-collapse:collapse;font-size:14px;">
			<tr style="border-bottom:1px solid #e0e0e0;">
				<th style="text-align:left;padding:8px;color:#666;">Method</th>
				<th style="text-align:left;padding:8px;color:#666;">Label</th>
				<th style="text-align:left;padding:8px;color:#666;">Status</th>
				<th style="text-align:left;padding:8px;color:#666;">Last Used</th>
			</tr>
			<?php foreach ($status['methods'] as $m): ?>
			<tr style="border-bottom:1px solid #f0f0f0;">
				<td style="padding:8px;"><?php echo strtoupper(htmlspecialchars($m['method'], ENT_QUOTES, 'UTF-8')); ?></td>
				<td style="padding:8px;"><?php echo htmlspecialchars($m['label'], ENT_QUOTES, 'UTF-8'); ?></td>
				<td style="padding:8px;"><?php echo (int) $m['confirmed'] === 1 ? '<span style="color:#198754;">Active</span>' : '<span style="color:#ffc107;">Pending</span>'; ?></td>
				<td style="padding:8px;"><?php echo htmlspecialchars($m['last_used_at'] ?? 'Never', ENT_QUOTES, 'UTF-8'); ?></td>
			</tr>
			<?php endforeach; ?>
		</table>
	</div>
	<?php endif; ?>
</div>

<script>
function epcMfaAjax(params, cb) {
	var xhr = new XMLHttpRequest();
	xhr.open('POST', window.location.pathname + '?epc_mfa_ajax=1');
	xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
	xhr.onload = function() { cb(JSON.parse(xhr.responseText)); };
	xhr.send(params);
}
function epcMfaStartEnroll() {
	epcMfaAjax('mfa_action=enroll', function(data) {
		if (data.ok) { window.location.href = '?epc_mfa=enroll'; }
		else { document.getElementById('epc-mfa-settings-result').innerHTML = '<div style="color:#dc3545;">' + data.error + '</div>'; }
	});
}
function epcMfaRegenBackup() {
	epcMfaAjax('mfa_action=regenerate_backup', function(data) {
		if (data.ok && data.backup_codes) {
			var html = '<div style="background:#fff3cd;padding:16px;border-radius:8px;border:1px solid #ffc107;"><h4 style="color:#856404;">New Backup Codes</h4><div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;font-family:monospace;">';
			data.backup_codes.forEach(function(c) { html += '<div style="background:#fff;padding:6px 10px;border-radius:4px;border:1px solid #e0e0e0;">' + c + '</div>'; });
			html += '</div><p style="font-size:13px;color:#856404;margin-top:8px;">Save these codes. Old codes are invalidated.</p></div>';
			document.getElementById('epc-mfa-settings-result').innerHTML = html;
		}
	});
}
function epcMfaDisable() {
	epcMfaAjax('mfa_action=disable', function(data) {
		if (data.ok) { window.location.reload(); }
	});
}
</script>
	<?php
	return ob_get_clean();
}

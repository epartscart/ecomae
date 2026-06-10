<?php
/**
 * Platform SMTP for auth OTP (CP + storefront) — copy to config.epc-smtp.php (not in git).
 *
 * Overrides DP_Config / config.local.php for epc-auth-send-code.php only.
 * Same keys as config.php mail section.
 *
 * Hostinger mailbox example:
 *   smtp_host => smtp.hostinger.com
 *   smtp_port => 465
 *   smtp_encryption => ssl
 *   smtp_username => hello@ecomae.com
 *   smtp_password => YOUR_MAILBOX_PASSWORD
 *
 * Gmail example (recommended TLS):
 *   smtp_host => smtp.gmail.com
 *   smtp_port => 587
 *   smtp_encryption => tls
 *   smtp_username => you@gmail.com  (MUST match from_email)
 *   smtp_password => 16-char App Password (NOT your normal Gmail password)
 *
 * Generate a Gmail App Password:
 *   1. https://myaccount.google.com/security → turn on 2-Step Verification.
 *   2. https://myaccount.google.com/apppasswords → create app password ("Mail").
 *   3. Paste the 16 characters (spaces removed) as smtp_password.
 *
 * EASIEST: set these from Super CP → Modern auth settings → SMTP credentials,
 * which writes this file for you (password is preserved if left blank).
 */
return array(
	'smtp_mode' => '1',
	'smtp_host' => 'smtp.hostinger.com',
	'smtp_port' => '465',
	'smtp_encryption' => 'ssl',
	'smtp_username' => 'hello@ecomae.com',
	'smtp_password' => 'YOUR_MAILBOX_OR_APP_PASSWORD',
	'from_email' => 'hello@ecomae.com',
	'from_name' => 'ECOM AE',
	/** If 1: try PHP mail() when SMTP fails (demo / rescue only). */
	'allow_mail_fallback' => '0',
	/** Set 1 to disable demo OTP logging when SMTP fails. */
	'disable_demo_otp_fallback' => '0',
);

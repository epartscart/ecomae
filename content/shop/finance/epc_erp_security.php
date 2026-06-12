<?php
/**
 * BOS security headers (Phase-1 security hardening).
 *
 * Sends a conservative but meaningful set of HTTP security headers on the BOS
 * shell and its AJAX endpoint: HSTS, anti-clickjacking, MIME-sniff protection,
 * a referrer policy, a restrictive permissions policy and a Content-Security-
 * Policy. The CSP intentionally allows inline scripts/styles (the app relies on
 * them) but locks down object/base/frame-ancestors and upgrades insecure
 * requests, which closes the most common injection/clickjacking vectors without
 * breaking the existing UI.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_erp_send_security_headers')) {

	function epc_erp_send_security_headers($withCsp = true)
	{
		if (headers_sent()) {
			return;
		}
		$isHttps = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
			|| (isset($_SERVER['HTTP_X_FORWARDED_PROTO']) && $_SERVER['HTTP_X_FORWARDED_PROTO'] === 'https');
		if ($isHttps) {
			header('Strict-Transport-Security: max-age=31536000; includeSubDomains; preload');
		}
		header('X-Content-Type-Options: nosniff');
		header('X-Frame-Options: SAMEORIGIN');
		header('Referrer-Policy: strict-origin-when-cross-origin');
		header('Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=(self)');
		header('X-XSS-Protection: 0');
		header('Cross-Origin-Opener-Policy: same-origin');

		if ($withCsp) {
			$csp = array(
				"default-src 'self'",
				"script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://static.cloudflareinsights.com",
				"style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://fonts.googleapis.com",
				"font-src 'self' data: https://cdnjs.cloudflare.com https://fonts.gstatic.com",
				"img-src 'self' data: https:",
				"connect-src 'self' https://cloudflareinsights.com",
				"object-src 'none'",
				"base-uri 'self'",
				"form-action 'self'",
				"frame-ancestors 'self'",
				"upgrade-insecure-requests",
			);
			header('Content-Security-Policy: ' . implode('; ', $csp));
		}
	}
}

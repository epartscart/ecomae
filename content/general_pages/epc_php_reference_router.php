<?php
/**
 * PHP reference-only entry (protocol: aspnet-primary-php-reference).
 *
 * Product URLs (/ /cp /erp /bos) stay on ASP.NET. This router serves the PHP
 * shells ONLY when reached via:
 *   /php-reference/{home|cp|erp|bos|storefront}
 *   /php-reference/CP/…  /php-reference/ERP/…
 *   → /epc_php_reference_boot.php?epc_php_reference=… (never product /index.php)
 *
 * Never redirect into product /cp|/erp|/bos trees (those are ASP.NET-primary).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return string One of home|cp|erp|bos|storefront, or '' if not a reference request.
 */
function epc_php_reference_surface(): string
{
	$raw = isset($_GET['epc_php_reference']) ? strtolower(trim((string) $_GET['epc_php_reference'])) : '';
	if ($raw === '') {
		return '';
	}
	static $allowed = array('home' => 1, 'cp' => 1, 'erp' => 1, 'bos' => 1, 'storefront' => 1);
	return isset($allowed[$raw]) ? $raw : '';
}

/**
 * Restore the classic PHP REQUEST_URI for a deep compare (CP/ERP tab).
 * Query keeps module args (area/tab/price_id) and drops the reference boot keys.
 */
function epc_php_reference_apply_deep_uri(string $surface): void
{
	$path = isset($_GET['epc_php_ref_path']) ? (string) $_GET['epc_php_ref_path'] : '';
	unset($_GET['epc_php_reference'], $_GET['epc_php_ref_path']);
	$path = '/' . ltrim($path, '/');

	if ($surface === 'cp') {
		if (!preg_match('#^/(?:CP|cp)(?:/|\?|$)#', $path)) {
			$path = '/cp/';
		}
	} elseif ($surface === 'erp') {
		if (!preg_match('#^/(?:ERP|erp)(?:/|\?|$)#', $path)) {
			$path = '/erp';
		}
	} elseif ($surface === 'bos') {
		$path = '/bos/';
	}

	$qs = http_build_query($_GET);
	$_SERVER['REQUEST_URI'] = $path . ($qs !== '' ? ('?' . $qs) : '');
}

function epc_php_reference_is_super_cp_host(): bool
{
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$host = preg_replace('/:\d+$/', '', $host);
	$host = preg_replace('/^www\./', '', $host);
	return in_array($host, array('ecomae.com', 'cp.ecomae.com'), true);
}

/**
 * Early exit handler. Call from index.php after _ASTEXE_ + config when possible,
 * or with minimal bootstrap for cp/bos includes.
 *
 * @return bool true if the request was fully handled (caller must exit).
 */
function epc_php_reference_try_route(): bool
{
	$surface = epc_php_reference_surface();
	if ($surface === '') {
		return false;
	}

	// Ops pause for ASP.NET deep test — not PHP feature work / not source deletion.
	require_once __DIR__ . '/epc_php_serving_deactivate.php';
	if (function_exists('epc_php_serving_deactivated_maybe_exit')
		&& epc_php_serving_deactivated_maybe_exit('php-reference/' . $surface)) {
		return true;
	}

	header('X-EcomAE-Php-Reference: ' . $surface);
	header('X-EcomAE-Target-Runtime: php-reference');
	header('X-Robots-Tag: noindex, nofollow');

	switch ($surface) {
		case 'bos':
			if (!epc_php_reference_is_super_cp_host()) {
				http_response_code(404);
				header('Content-Type: text/plain; charset=utf-8');
				echo "PHP reference BOS is Super-CP only.\n";
				return true;
			}
			$bos = $_SERVER['DOCUMENT_ROOT'] . '/bos/index.php';
			if (!is_file($bos)) {
				http_response_code(503);
				header('Content-Type: text/plain; charset=utf-8');
				echo "BOS reference unavailable.\n";
				return true;
			}
			// Internal boot — do not Location: /bos (product URL is ASP.NET / 404 on tenants).
			epc_php_reference_apply_deep_uri('bos');
			$_SERVER['SCRIPT_NAME'] = '/bos/index.php';
			$_GET = array();
			require $bos;
			return true;

		case 'cp':
			$cp = $_SERVER['DOCUMENT_ROOT'] . '/cp/index.php';
			if (!is_file($cp)) {
				http_response_code(503);
				header('Content-Type: text/plain; charset=utf-8');
				echo "CP reference unavailable.\n";
				return true;
			}
			epc_php_reference_apply_deep_uri('cp');
			$_SERVER['SCRIPT_NAME'] = '/cp/index.php';
			require $cp;
			return true;

		case 'erp':
			// Boot standalone ERP portal in-process (product /erp is ASP.NET).
			epc_php_reference_apply_deep_uri('erp');
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal_router.php';
			if (function_exists('epc_erp_portal_try_route') && epc_erp_portal_try_route()) {
				return true;
			}
			http_response_code(503);
			header('Content-Type: text/plain; charset=utf-8');
			echo "ERP reference router did not handle /erp.\n";
			return true;

		case 'home':
		case 'storefront':
			// Continue normal index.php storefront/marketing bootstrap.
			// Mark so later redirects do not bounce into ASP.NET product trees.
			$GLOBALS['epc_php_reference_surface'] = $surface;
			return false;

		default:
			return false;
	}
}

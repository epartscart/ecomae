<?php
/**
 * Broken thin ASP.NET /storefront/* stubs → live PHP canonical pages.
 * Runs from index.php when nginx sends /storefront/search-app (etc.) to PHP-FPM
 * instead of Kestrel — prevents live 404s like:
 *   /storefront/search-app?article=1310154101
 */
declare(strict_types=1);

function epc_storefront_stub_redirect_maybe_exit(): void
{
	if (PHP_SAPI === 'cli' || headers_sent()) {
		return;
	}
	$method = strtoupper((string) ($_SERVER['REQUEST_METHOD'] ?? 'GET'));
	if ($method !== 'GET' && $method !== 'HEAD') {
		return;
	}

	$uri = (string) ($_SERVER['REQUEST_URI'] ?? '/');
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return;
	}
	$path = '/' . trim(str_replace('\\', '/', $path), '/');
	if ($path === '//') {
		$path = '/';
	}
	$pathLower = strtolower($path);

	// Home + logout stay on ASP.NET when those routes are installed.
	if ($pathLower === '/storefront' || $pathLower === '/storefront/app' || $pathLower === '/storefront/logout') {
		return;
	}

	if (strpos($pathLower, '/storefront/') !== 0) {
		return;
	}

	$query = [];
	$qs = parse_url($uri, PHP_URL_QUERY);
	if (is_string($qs) && $qs !== '') {
		parse_str($qs, $query);
	}
	$mode = strtolower(trim((string) ($query['mode'] ?? '')));
	unset($query['mode']);

	$target = null;
	if ($pathLower === '/storefront/search-app') {
		if ($mode === 'attr') {
			$target = '/en/shop/warehouse-search';
		} elseif ($mode === 'vin') {
			$target = '/en/katalog-laximo';
		} elseif ($mode === 'car' || $mode === 'engine') {
			$target = '/en/vehicle-catalog';
		} elseif ($mode === 'name') {
			$target = '/en/shop/search';
		} else {
			$target = '/en/shop/part_search';
		}
	} else {
		switch ($pathLower) {
			case '/storefront/cart-app':
				$target = '/en/shop/cart';
				break;
			case '/storefront/checkout-app':
				$target = '/en/shop/checkout';
				break;
			case '/storefront/orders-app':
				$target = '/en/shop/orders';
				break;
			case '/storefront/login':
				$target = '/en/users/login';
				break;
			case '/storefront/garage-app':
				$target = '/en/garage/login';
				break;
			case '/storefront/profile-app':
			case '/storefront/account-summary-app':
				$target = '/en/shop/orders';
				break;
			default:
				$target = null;
		}
	}

	if ($target === null) {
		return;
	}

	$suffix = $query !== [] ? ('?' . http_build_query($query)) : '';
	header('Location: ' . $target . $suffix, true, 302);
	header('X-EcomAE-Storefront-Stub-Redirect: php-edge');
	exit;
}

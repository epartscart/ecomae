<?php
/**
 * Ops switch only — temporary PHP HTTP serving pause for ASP.NET Core deep testing.
 * Not a feature surface. Does not delete PHP files. Does not flip ReadyToRemovePhp.
 *
 * Flag file (either path):
 *   {DOCUMENT_ROOT}/.epc_php_serving_deactivated
 *   /etc/ecomae-aspnet/php_serving_deactivated
 *
 * Operator:
 *   ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING=YES bash scripts/cloudpanel_temporarily_deactivate_php_serving.sh
 *   ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh
 */
declare(strict_types=1);

function epc_php_serving_is_temporarily_deactivated(): bool
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$roots = array();
	if (!empty($_SERVER['DOCUMENT_ROOT'])) {
		$roots[] = rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/') . '/.epc_php_serving_deactivated';
	}
	$roots[] = '/etc/ecomae-aspnet/php_serving_deactivated';
	foreach ($roots as $path) {
		if (is_file($path)) {
			$cached = true;
			return true;
		}
	}
	$cached = false;
	return false;
}

/**
 * @return bool true if response was sent (caller must exit).
 */
function epc_php_serving_deactivated_maybe_exit(string $reason = 'php-http-serving'): bool
{
	if (!epc_php_serving_is_temporarily_deactivated()) {
		return false;
	}
	if (PHP_SAPI === 'cli' || headers_sent()) {
		return false;
	}
	http_response_code(503);
	header('Content-Type: text/plain; charset=utf-8');
	header('X-EcomAE-Php-Serving: temporarily-deactivated');
	header('X-EcomAE-Target-Runtime: aspnet-only-deep-test');
	header('X-EcomAE-Keep-Php-Project: true');
	header('X-EcomAE-Cutover-Allowed: false');
	header('X-EcomAE-Ready-For-Php-Removal: false');
	header('Retry-After: 3600');
	echo "PHP HTTP serving is temporarily deactivated for ASP.NET Core deep testing ({$reason}).\n";
	echo "PHP project files remain on disk (KeepPhpProjectAvailable=true).\n";
	echo "cutoverAllowed=false · readyForPhpRemoval=false.\n";
	echo "Restore: ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh\n";
	return true;
}

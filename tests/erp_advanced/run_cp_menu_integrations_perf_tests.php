<?php
/**
 * Guard: CP sidebar must not re-run integrations schema ensure per menu item.
 */
declare(strict_types=1);

$root = dirname(__DIR__, 2);
$failed = 0;
function check(string $label, bool $ok): void
{
	global $failed;
	echo ($ok ? 'OK  ' : 'FAIL') . " $label\n";
	if (!$ok) {
		$failed++;
	}
}

$helpers = (string) file_get_contents($root . '/content/general_pages/epc_integrations_helpers.php');
$portalDb = (string) file_get_contents($root . '/content/general_pages/epc_portal_db.php');

check('integrations ensure_schema has static once guard', strpos($helpers, 'static $done = array()') !== false && strpos($helpers, 'spl_object_id($pdo)') !== false);
check('features_for_site uses single SELECT', strpos($helpers, 'SELECT `feature_key`, `enabled` FROM `epc_tenant_feature_flags` WHERE `site_key` = ?') !== false);
check('feature_enabled uses features_for_site', strpos($helpers, 'epc_integrations_features_for_site($siteKey, $platformPdo)') !== false);
check('menu_blocked loads flags once', strpos($helpers, '$flags = epc_integrations_features_for_site($siteKey);') !== false);
check('portal db_ensure has static once guard', strpos($portalDb, "function epc_portal_db_ensure(PDO \$pdo)\n{\n\tstatic \$done = array();") !== false
	|| preg_match('/function epc_portal_db_ensure\(PDO \$pdo\)\s*\{\s*static \$done/s', $portalDb) === 1);

$dpCore = (string) file_get_contents($root . '/core/dp_core.php');
check('dp_core skips script relocate on php content', strpos($dpCore, "content_type ?? '') !== 'php'") !== false
	|| strpos($dpCore, 'Never rewrite PHP page sources before eval') !== false);

exit($failed > 0 ? 1 : 0);

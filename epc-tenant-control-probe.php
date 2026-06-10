<?php
/**
 * Tenant control center row probe (deploy token).
 * GET ?token=...&site_key=asapcustom
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'asapcustom'))));
$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	echo json_encode(array('ok' => false, 'message' => 'Platform DB unavailable'));
	exit;
}

$row = epc_portal_tenant_control_get_row($pdo, $siteKey);
$list = epc_portal_tenant_control_list_all($pdo);
$listRow = null;
foreach ($list as $t) {
	if ((string) ($t['site_key'] ?? '') === $siteKey) {
		$listRow = $t;
		break;
	}
}

$intro = array();
if (is_array($row) && !empty($row['intro_json'])) {
	$decoded = json_decode((string) $row['intro_json'], true);
	if (is_array($decoded)) {
		$intro = $decoded;
	}
}

$pwd = is_array($row) ? trim((string) ($row['operator_temp_password'] ?? '')) : '';

echo json_encode(array(
	'ok' => is_array($row),
	'site_key' => $siteKey,
	'registry' => is_array($row) ? array(
		'from_email' => (string) ($row['from_email'] ?? ''),
		'operator_temp_password_set' => $pwd !== '',
		'operator_temp_password_len' => strlen($pwd),
		'intro_admin_cp_email' => (string) ($intro['admin_cp_email'] ?? ''),
		'intro_admin_email' => (string) ($intro['admin_email'] ?? ''),
		'intro_operator_login_email' => (string) ($intro['operator_login_email'] ?? ''),
	) : null,
	'resolved_admin_email' => is_array($row) ? epc_portal_tenant_control_resolve_admin_email($row) : '',
	'list_row' => $listRow ? array(
		'admin_email' => (string) ($listRow['admin_email'] ?? ''),
		'stored_password_set' => trim((string) ($listRow['stored_password'] ?? '')) !== '',
		'in_registry' => !empty($listRow['in_registry']),
		'tenant_type' => (string) ($listRow['tenant_type'] ?? ''),
	) : null,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);

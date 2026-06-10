<?php
/**
 * External Catalog API — authenticated proxy to UMAPI (umapi_proxy.php).
 * GET /api/v1/catalog.php?action=manufacturers&section=passenger
 * Header: X-API-Key: epc_catalog_…
 */
define('_ASTEXE_', 1);
define('EPC_API_CLIENT_CATALOG_ENTRY', 1);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_api_clients.php';
$action = strtolower(trim((string) ($_REQUEST['action'] ?? '')));
if ($action === '') {
	epc_api_clients_json_error(400, 'missing_action', 'Query param action is required (e.g. manufacturers, vin, status).');
}
epc_api_client_require_auth('catalog', $action);
$GLOBALS['epc_api_client_umapi_authed'] = true;

require $_SERVER['DOCUMENT_ROOT'] . '/api/umapi_proxy.php';

<?php
/**
 * Document Control — AJAX actions.
 */
defined('_ASTEXE_') or die('No access');

header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

function epc_dc_json($ok, $message, $extra = array())
{
	echo json_encode(array_merge(array('status' => (bool)$ok, 'message' => (string)$message), $extra));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	epc_dc_json(false, 'No database');
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	epc_dc_json(false, 'No action');
}

$action = (string)$_POST['action'];

try {
	epc_dc_ensure($db_link);

	switch ($action) {
		case 'save_company':
			epc_dc_save_company($db_link, $_POST);
			epc_dc_json(true, 'Company profile saved');

		case 'save_template':
			$code = trim((string)($_POST['code'] ?? ''));
			if ($code === '') {
				throw new Exception('Template code required');
			}
			epc_dc_save_template($db_link, $code, $_POST);
			epc_dc_json(true, 'Template saved');

		case 'upload_logo':
			if (empty($_FILES['logo'])) {
				throw new Exception('No logo file');
			}
			$f = $_FILES['logo'];
			$ext = strtolower(pathinfo((string)$f['name'], PATHINFO_EXTENSION));
			if (!in_array($ext, array('png', 'jpg', 'jpeg', 'webp', 'gif'), true)) {
				throw new Exception('Logo must be PNG, JPG, or WebP');
			}
			$dir = epc_dc_logo_dir();
			$name = 'logo.' . ($ext === 'jpeg' ? 'jpg' : $ext);
			$dest = $dir . '/' . $name;
			if (!move_uploaded_file($f['tmp_name'], $dest)) {
				throw new Exception('Logo upload failed');
			}
			$rel = '/content/files/epc_doc/' . $name . '?v=' . time();
			epc_dc_save_company($db_link, array('logo_path' => $rel));
			epc_dc_json(true, 'Logo uploaded', array('logo_path' => $rel));

		case 'upload_attachment':
			epc_dc_save_attachment($db_link, $_POST, $_FILES['file'] ?? array());
			epc_dc_json(true, 'Document attached');

		case 'delete_attachment':
			epc_dc_delete_attachment($db_link, (int)($_POST['id'] ?? 0));
			epc_dc_json(true, 'Attachment removed');

		case 'sync_einvoice_seller':
			epc_dc_sync_seller_from_einvoice($db_link);
			epc_dc_json(true, 'Imported seller details from E-Invoicing settings');

		default:
			epc_dc_json(false, 'Unknown action');
	}
} catch (Throwable $e) {
	epc_dc_json(false, $e->getMessage());
}

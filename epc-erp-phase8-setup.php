<?php
/**
 * ERP Phases 8–12 — schema, demo seed (super CP / ecomae).
 * Run: https://www.ecomae.com/epc-erp-phase8-setup.php?token=epartscart-deploy-2026
 *      https://www.epartscart.com/epc-erp-phase8-setup.php?token=epartscart-deploy-2026&seed=1
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_phase8.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_extended.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_audit.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_staff.php';
require_once __DIR__ . '/content/shop/finance/epc_crm_schema.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_full_ensure_schema($pdo);
epc_erp_phase8_ensure_schema($pdo);
epc_erp_extended_ensure_schema($pdo);
epc_erp_audit_ensure_schema($pdo);
epc_erp_staff_ensure_schema($pdo);
epc_erp_staff_seed_departments($pdo);
epc_crm_ensure_schema($pdo);
epc_erp_kb_seed_defaults($pdo);
epc_erp_notification_seed($pdo, 'ERP Phases 8–12 ready', 'Area navigation and new modules are available under /cp/shop/finance/erp', 'dashboard');

echo "OK — ERP Phases 8–12 schema\n";
echo "- Phase 8: contacts, RFQ, delivery notes, bank lines, documents, expense reports\n";
echo "- Phase 9: purchase orders, PO receipts (3-way match)\n";
echo "- Phase 10: payment batches, petty cash\n";
echo "- Phase 11: agenda, notifications\n";
echo "- Phase 12: KB articles, multi-entity settings, CRM quote_kind\n";

if (empty($_GET['seed'])) {
	echo "\nAdd &seed=1 to insert demo RFQ + contact for super CP.\n";
	exit;
}

$now = time();
$supId = (int)$pdo->query('SELECT `id` FROM `epc_erp_suppliers` WHERE `active` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
if ($supId > 0) {
	$chk = $pdo->prepare('SELECT `id` FROM `epc_erp_rfq` WHERE `title` = ? LIMIT 1');
	$chk->execute(array('Phase 8 demo RFQ'));
	if (!$chk->fetchColumn()) {
		epc_erp_rfq_save($pdo, array(
			'supplier_id' => $supId,
			'title' => 'Phase 8 demo RFQ',
			'description' => 'Seeded by epc-erp-phase8-setup.php',
			'amount_est' => 12500,
			'status' => 'sent',
			'due_date' => date('Y-m-d', $now + 86400 * 14),
		));
		echo "- Demo RFQ created\n";
	}
	epc_erp_po_save($pdo, array(
		'supplier_id' => $supId,
		'title' => 'Phase 9 demo PO',
		'amount_ex_vat' => 8500,
	));
	echo "- Demo PO created\n";
}
$chkC = $pdo->prepare('SELECT `id` FROM `epc_erp_contacts` WHERE `name` = ? LIMIT 1');
$chkC->execute(array('Phase 8 Demo Contact'));
if (!$chkC->fetchColumn()) {
	epc_erp_contact_save($pdo, array(
		'party_type' => 'both',
		'name' => 'Phase 8 Demo Contact',
		'company' => 'ECOM AE Demo',
		'email' => 'demo@ecomae.com',
		'currency_code' => 'AED',
		'country_code' => 'AE',
	));
	echo "- Demo contact created\n";
}
epc_erp_audit_log($pdo, 'setup_seed', 'system', 0, 'Phase 8–12 demo seed completed');
echo "Done.\n";

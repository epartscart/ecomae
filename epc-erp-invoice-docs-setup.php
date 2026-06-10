<?php
/**
 * ERP customer invoices (e-invoice) + document ECM schema.
 * Run: https://www.ecomae.com/epc-erp-invoice-docs-setup.php?token=epartscart-deploy-2026
 *      https://www.epartscart.com/epc-erp-invoice-docs-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_invoices.php';
require_once __DIR__ . '/content/shop/document_control/epc_document_control_schema.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_invoices_ensure_schema($pdo);
epc_doc_control_ensure_schema($pdo);

echo "OK — ERP invoices & document ECM schema\n";
echo "- epc_einvoice_documents (customer tax invoices / e-invoice headers)\n";
echo "- epc_einvoice_lines (invoice line items)\n";
echo "- epc_einvoice_events (transmission log)\n";
echo "- epc_erp_documents (ERP native ECM attachments + version_note)\n";
echo "- epc_document_attachments (Document Control CP attachments)\n";
echo "\nERP tabs:\n";
echo "- Sales → Invoices: /cp/shop/finance/erp?area=sales&tab=invoices\n";
echo "- Collaboration → Documents: /cp/shop/finance/erp?area=collaboration&tab=documents\n";

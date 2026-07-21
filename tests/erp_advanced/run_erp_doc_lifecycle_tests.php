<?php
/**
 * Smoke tests for ERP document lifecycle (edit / delete / void gates).
 */
$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
require $root . '/content/shop/finance/epc_erp_doc_lifecycle.php';

$fail = 0;
$ok = static function (bool $cond, string $msg) use (&$fail): void {
	if ($cond) {
		echo "OK  $msg\n";
	} else {
		echo "FAIL $msg\n";
		$fail++;
	}
};

$postedCash = array('id' => 1, 'active' => 1);
$ok(epc_erp_doc_can_void('cash', $postedCash), 'posted cash can void');
$ok(!epc_erp_doc_can_delete('cash', $postedCash), 'posted cash cannot delete');
$ok(epc_erp_doc_can_amend('cash', $postedCash), 'posted cash can amend narrative');
$ok(!epc_erp_doc_can_edit('cash', $postedCash), 'posted cash cannot full-edit amounts');

$voided = array('id' => 2, 'active' => 0, 'voided_at' => time());
$ok(!epc_erp_doc_can_void('cash', $voided), 'voided cash cannot void again');

$draftPi = array('id' => 3, 'active' => 1, 'status' => 'draft', 'gl_journal_id' => 0);
$ok(epc_erp_doc_can_delete('purchase', $draftPi), 'draft PI can delete');
$ok(epc_erp_doc_can_edit('purchase', $draftPi), 'draft PI can edit');

$postedPi = array('id' => 4, 'active' => 1, 'status' => 'confirmed', 'gl_journal_id' => 9);
$ok(epc_erp_doc_can_void('purchase', $postedPi), 'posted PI can void');
$ok(!epc_erp_doc_can_delete('purchase', $postedPi), 'posted PI cannot delete');

$so = array('id' => 5, 'active' => 1, 'status' => 'confirmed', 'sales_invoice_id' => 0);
$ok(epc_erp_doc_can_void('so', $so), 'confirmed SO can cancel');

$invSub = array('id' => 6, 'active' => 1, 'status' => 'submitted');
$ok(!epc_erp_doc_can_void('invoice', $invSub), 'submitted invoice cannot cancel (credit note path)');
$ok(!epc_erp_doc_can_edit('invoice', $invSub), 'submitted invoice cannot edit');

$html = epc_erp_doc_actions_html('cash', $postedCash, 'csrf', array('id_field' => 'entry_id'));
$ok(strpos($html, 'Void') !== false && strpos($html, 'Edit') !== false, 'cash actions render Edit+Void');

$ajax = (string) file_get_contents($root . '/cp/content/shop/finance/erp/ajax_erp.php');
foreach (array('cash_voucher_void', 'purchase_void', 'so_cancel', 'po_delete', 'invoice_cancel') as $a) {
	$ok(strpos($ajax, "case '$a'") !== false, "ajax has case $a");
}

exit($fail > 0 ? 1 : 0);

<?php
/**
 * Dual Trial Balance — Weight + Value reporting for jewellery tenants.
 * Appears under GL area when industry = jewellery.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
epc_jw_ensure_integration_schema($db_link);

erp_page_header(
	'<i class="fa fa-balance-scale"></i> Dual trial balance',
	'Jewellery industry: Weight-based + Value-based trial balance side by side.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Dual trial balance'),
	)
);
erp_d365_assets();

$wtTB = epc_jw_weight_trial_balance($db_link, $date_from, $date_to);
$valTB = epc_jw_value_trial_balance($db_link, $date_from, $date_to);

erp_tabstrip(array(
	array('label' => 'Weight trial balance', 'target' => '#jw_tb_weight', 'active' => true, 'icon' => 'fa-balance-scale'),
	array('label' => 'Value trial balance', 'target' => '#jw_tb_value', 'icon' => 'fa-money'),
	array('label' => 'Combined', 'target' => '#jw_tb_combined', 'icon' => 'fa-columns'),
), 'jw_tb_view');

// Weight trial balance
erp_tabpanel_open('jw_tb_weight', 'jw_tb_view', true);
ob_start();
if (empty($wtTB)) {
	erp_empty_state('No weight ledger entries yet. Transactions will appear after purchase/sale postings.', 'fa-balance-scale');
} else {
	$totalIn = $totalOut = $totalBal = 0;
	erp_table_open(array(
		array('label' => 'Account', 'sort' => 'text'),
		array('label' => 'Metal'),
		array('label' => 'Karat'),
		array('label' => 'Weight In (g)', 'class' => 'num'),
		array('label' => 'Weight Out (g)', 'class' => 'num'),
		array('label' => 'Balance (g)', 'class' => 'num'),
	));
	foreach ($wtTB as $row) {
		$totalIn += (float)$row['total_weight_in'];
		$totalOut += (float)$row['total_weight_out'];
		$totalBal += (float)$row['weight_balance'];
		echo '<tr>';
		echo '<td><strong>' . epc_erp_h($row['account_code']) . '</strong> ' . epc_erp_h($row['account_name']) . '</td>';
		echo '<td>' . epc_erp_h($row['metal_type'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($row['karat'] ?: '—') . '</td>';
		echo '<td class="num">' . number_format((float)$row['total_weight_in'], 3) . '</td>';
		echo '<td class="num">' . number_format((float)$row['total_weight_out'], 3) . '</td>';
		$bal = (float)$row['weight_balance'];
		echo '<td class="num" style="font-weight:bold;color:' . ($bal >= 0 ? '#16e0a3' : '#ff5d73') . '">' . number_format($bal, 3) . '</td>';
		echo '</tr>';
	}
	$foot = '<tr class="epc-d365-sumrow"><td colspan="3">Totals</td>'
		. '<td class="num">' . number_format($totalIn, 3) . '</td>'
		. '<td class="num">' . number_format($totalOut, 3) . '</td>'
		. '<td class="num"><strong>' . number_format($totalBal, 3) . '</strong></td></tr>';
	erp_table_close($foot);
}
erp_section_card('Weight trial balance (grams)', ob_get_clean(), array('icon' => 'fa-balance-scale'));
erp_tabpanel_close();

// Value trial balance
erp_tabpanel_open('jw_tb_value', 'jw_tb_view');
ob_start();
if (empty($valTB)) {
	erp_empty_state('No value ledger entries yet.');
} else {
	$totalDr = $totalCr = $totalBal2 = 0;
	erp_table_open(array(
		array('label' => 'Account', 'sort' => 'text'),
		array('label' => 'Debit (AED)', 'class' => 'num'),
		array('label' => 'Credit (AED)', 'class' => 'num'),
		array('label' => 'Balance (AED)', 'class' => 'num'),
	));
	foreach ($valTB as $row) {
		$totalDr += (float)$row['total_debit'];
		$totalCr += (float)$row['total_credit'];
		$totalBal2 += (float)$row['balance'];
		echo '<tr>';
		echo '<td><strong>' . epc_erp_h($row['account_code']) . '</strong> ' . epc_erp_h($row['account_name']) . '</td>';
		echo '<td class="num">' . epc_erp_money($row['total_debit']) . '</td>';
		echo '<td class="num">' . epc_erp_money($row['total_credit']) . '</td>';
		$bal2 = (float)$row['balance'];
		echo '<td class="num" style="font-weight:bold;color:' . ($bal2 >= 0 ? '#16e0a3' : '#ff5d73') . '">' . epc_erp_money($bal2) . '</td>';
		echo '</tr>';
	}
	$foot2 = '<tr class="epc-d365-sumrow"><td>Totals</td>'
		. '<td class="num">' . epc_erp_money($totalDr) . '</td>'
		. '<td class="num">' . epc_erp_money($totalCr) . '</td>'
		. '<td class="num"><strong>' . epc_erp_money($totalBal2) . '</strong></td></tr>';
	erp_table_close($foot2);
}
erp_section_card('Value trial balance (AED)', ob_get_clean(), array('icon' => 'fa-money'));
erp_tabpanel_close();

// Combined view — both weight and value side by side
erp_tabpanel_open('jw_tb_combined', 'jw_tb_view');
ob_start();
if (empty($wtTB)) {
	erp_empty_state('No dual ledger entries yet.');
} else {
	erp_table_open(array(
		array('label' => 'Account'),
		array('label' => 'Metal'),
		array('label' => 'Karat'),
		array('label' => 'Wt In (g)', 'class' => 'num'),
		array('label' => 'Wt Out (g)', 'class' => 'num'),
		array('label' => 'Wt Bal (g)', 'class' => 'num'),
		array('label' => 'Dr (AED)', 'class' => 'num'),
		array('label' => 'Cr (AED)', 'class' => 'num'),
		array('label' => 'Val Bal (AED)', 'class' => 'num'),
	));
	foreach ($wtTB as $row) {
		echo '<tr>';
		echo '<td><strong>' . epc_erp_h($row['account_code']) . '</strong> ' . epc_erp_h($row['account_name']) . '</td>';
		echo '<td>' . epc_erp_h($row['metal_type'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($row['karat'] ?: '—') . '</td>';
		echo '<td class="num">' . number_format((float)$row['total_weight_in'], 3) . '</td>';
		echo '<td class="num">' . number_format((float)$row['total_weight_out'], 3) . '</td>';
		$wb = (float)$row['weight_balance'];
		echo '<td class="num" style="font-weight:bold;color:' . ($wb >= 0 ? '#16e0a3' : '#ff5d73') . '">' . number_format($wb, 3) . '</td>';
		echo '<td class="num">' . epc_erp_money($row['total_debit']) . '</td>';
		echo '<td class="num">' . epc_erp_money($row['total_credit']) . '</td>';
		$vb = (float)$row['value_balance'];
		echo '<td class="num" style="font-weight:bold;color:' . ($vb >= 0 ? '#16e0a3' : '#ff5d73') . '">' . epc_erp_money($vb) . '</td>';
		echo '</tr>';
	}
	erp_table_close();
}
erp_section_card('Combined dual trial balance', ob_get_clean(), array('icon' => 'fa-columns'));
erp_tabpanel_close();

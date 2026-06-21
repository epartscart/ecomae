<?php
/**
 * Module: Consolidation.
 * Sub-modules: Customer / Vendor / Item consolidation by Business Unit,
 * All-combination reporting.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aging.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_consolidation.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'group';
$subs = array(
	'group' => 'Group consolidation',
	'intercompany' => 'Intercompany',
	'customer' => 'Customer (by BU)',
	'vendor' => 'Vendor (by BU)',
	'item' => 'Item (by BU)',
	'all' => 'All-combination report',
);
$csrfLocal = isset($csrf) ? $csrf : '';

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-sitemap"></i> Consolidation</h3>';
echo '<p class="text-muted">Consolidated customer, vendor and item balances across all business units in this tenant — with a combined all-in-one report. A single shared COA keeps every BU on the same accounts.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'consolidation_bu', 'insights', $date_from_str, $date_to_str, $subs, $view);

$buCount = 0;
try {
	$buCount = count(epc_erp_pm_list($db_link, 'epc_erp_pm_business_units', true));
} catch (Exception $e) {
}
echo '<p class="text-muted"><i class="fa fa-info-circle"></i> Business units configured: <strong>' . $buCount . '</strong>. Balances below are consolidated across all of them.</p>';

function epc_pm_cons_table(array $rows, string $nameLabel, string $valLabel)
{
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>' . epc_erp_h($nameLabel) . '</th><th style="text-align:right;">' . epc_erp_h($valLabel) . '</th></tr></thead><tbody>';
	$tot = 0.0;
	if (empty($rows)) {
		echo '<tr><td colspan="2" class="text-muted">No data.</td></tr>';
	}
	foreach ($rows as $r) {
		$tot += (float) $r['v'];
		echo '<tr><td>' . epc_erp_h((string) $r['n']) . '</td><td style="text-align:right;">' . epc_erp_money((float) $r['v']) . '</td></tr>';
	}
	echo '<tr><th style="text-align:right;">Total</th><th style="text-align:right;">' . epc_erp_money($tot) . '</th></tr>';
	echo '</tbody></table></div>';
	return $tot;
}

$arRows = $apRows = $invRows = array();
try {
	foreach (epc_erp_ar_aging($db_link)['rows'] as $r) {
		$arRows[] = array('n' => $r['name'], 'v' => $r['total']);
	}
} catch (Exception $e) {
}
try {
	foreach (epc_erp_ap_aging($db_link)['rows'] as $r) {
		$apRows[] = array('n' => $r['name'], 'v' => $r['total']);
	}
} catch (Exception $e) {
}
try {
	foreach (epc_erp_inventory_aging($db_link)['rows'] as $r) {
		$invRows[] = array('n' => $r['name'], 'v' => $r['total']);
	}
} catch (Exception $e) {
}

switch ($view) {
	case 'group':
		require __DIR__ . '/erp_tabs_consolidation_group.php';
		break;
	case 'intercompany':
		require __DIR__ . '/erp_tabs_consolidation_ic.php';
		break;
	case 'vendor':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-truck"></i> Vendor consolidation (payables)</h4>';
		epc_pm_cons_table($apRows, 'Vendor', 'Outstanding payable');
		echo '</div>';
		break;
	case 'item':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-cubes"></i> Item consolidation (stock value)</h4>';
		epc_pm_cons_table($invRows, 'Item', 'Stock value');
		echo '</div>';
		break;
	case 'all':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-table"></i> All-combination report</h4>';
		$arTot = array_sum(array_map(function ($r) {
			return (float) $r['v'];
		}, $arRows));
		$apTot = array_sum(array_map(function ($r) {
			return (float) $r['v'];
		}, $apRows));
		$invTot = array_sum(array_map(function ($r) {
			return (float) $r['v'];
		}, $invRows));
		echo '<div class="table-responsive"><table class="table table-bordered table-condensed"><thead><tr><th>Dimension</th><th style="text-align:right;">Count</th><th style="text-align:right;">Consolidated value</th></tr></thead><tbody>';
		echo '<tr><td>Customers (receivable)</td><td style="text-align:right;">' . count($arRows) . '</td><td style="text-align:right;">' . epc_erp_money($arTot) . '</td></tr>';
		echo '<tr><td>Vendors (payable)</td><td style="text-align:right;">' . count($apRows) . '</td><td style="text-align:right;">' . epc_erp_money($apTot) . '</td></tr>';
		echo '<tr><td>Items (stock value)</td><td style="text-align:right;">' . count($invRows) . '</td><td style="text-align:right;">' . epc_erp_money($invTot) . '</td></tr>';
		echo '<tr><td>Net working position (AR − AP)</td><td style="text-align:right;">—</td><td style="text-align:right;">' . epc_erp_money($arTot - $apTot) . '</td></tr>';
		echo '</tbody></table></div></div>';
		break;
	case 'customer':
	default:
		echo '<div class="epc-erp-section"><h4><i class="fa fa-users"></i> Customer consolidation (receivables)</h4>';
		epc_pm_cons_table($arRows, 'Customer', 'Outstanding receivable');
		echo '</div>';
		break;
}

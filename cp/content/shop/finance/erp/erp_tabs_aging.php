<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aging.php';

$agingView = isset($_GET['aging_view']) ? (string) $_GET['aging_view'] : 'ar';
if (!in_array($agingView, array('ar', 'ap', 'inventory'), true)) {
	$agingView = 'ar';
}

$viewMeta = array(
	'ar' => array('label' => 'Receivables aging', 'icon' => 'fa-users', 'fn' => 'epc_erp_ar_aging', 'entity' => 'Customer', 'overdue' => true),
	'ap' => array('label' => 'Payables aging', 'icon' => 'fa-truck', 'fn' => 'epc_erp_ap_aging', 'entity' => 'Supplier', 'overdue' => true),
	'inventory' => array('label' => 'Inventory aging', 'icon' => 'fa-cubes', 'fn' => 'epc_erp_inventory_aging', 'entity' => 'Item', 'overdue' => false),
);
$meta = $viewMeta[$agingView];

erp_page_header(
	'<i class="fa fa-hourglass-half"></i> Aging analysis',
	'Receivable, payable and inventory aging — current snapshot bucketed by age. Bucket sizes are configurable in Accounting setup.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Aging'),
	)
);

// Sub-view switcher
$baseUrl = epc_erp_tab_url($erpUrl, 'aging', $date_from_str, $date_to_str, 'finance');
echo '<ul class="nav nav-tabs" style="margin-bottom:16px;">';
foreach ($viewMeta as $key => $m) {
	$active = ($key === $agingView) ? ' class="active"' : '';
	echo '<li' . $active . '><a href="' . epc_erp_h($baseUrl . '&aging_view=' . $key) . '"><i class="fa ' . epc_erp_h($m['icon']) . '"></i> ' . epc_erp_h($m['label']) . '</a></li>';
}
echo '</ul>';

$data = call_user_func($meta['fn'], $db_link);
$labels = $data['labels'];
$totals = $data['totals'];
$grand = $data['grand'];
$rows = $data['rows'];

// Summary stat cards: grand total + each bucket
$cards = array(array('label' => 'Total outstanding', 'value' => epc_erp_money($grand)));
foreach ($labels as $i => $lbl) {
	$cards[] = array('label' => $lbl . ' days', 'value' => epc_erp_money($totals[$i]));
}
erp_stat_cards($cards);

// Visual bar: proportion of each bucket
ob_start();
if ($grand > 0) {
	$colors = array('#5cb85c', '#f0ad4e', '#ec971f', '#d9534f', '#a94442');
	echo '<div style="display:flex;height:28px;border-radius:4px;overflow:hidden;border:1px solid #ddd;margin-bottom:8px;">';
	foreach ($labels as $i => $lbl) {
		$pct = $grand > 0 ? round($totals[$i] / $grand * 100, 1) : 0;
		if ($pct <= 0) {
			continue;
		}
		echo '<div title="' . epc_erp_h($lbl . ' days: ' . epc_erp_money($totals[$i]) . ' (' . $pct . '%)') . '" style="width:' . $pct . '%;background:' . $colors[$i] . ';color:#fff;font-size:11px;line-height:28px;text-align:center;overflow:hidden;white-space:nowrap;">' . ($pct >= 7 ? $pct . '%' : '') . '</div>';
	}
	echo '</div>';
	echo '<div style="font-size:12px;color:#777;">';
	foreach ($labels as $i => $lbl) {
		echo '<span style="display:inline-block;margin-right:14px;"><span style="display:inline-block;width:10px;height:10px;background:' . $colors[$i] . ';margin-right:4px;border-radius:2px;"></span>' . epc_erp_h($lbl) . ' days</span>';
	}
	echo '</div>';
} else {
	erp_empty_state('No outstanding ' . strtolower($meta['entity']) . ' balances to age.', 'fa-check-circle');
}
erp_section_card($meta['label'] . ' — distribution', ob_get_clean(), array('icon' => 'fa-pie-chart'));

// Detail table
$boundNote = 'Buckets (days): ' . implode(' / ', $data['boundaries']) . '. ';
$boundNote .= $meta['overdue']
	? 'Aged from the document due date; "Not due" = not yet past due.'
	: 'Aged from the most recent inbound stock movement.';
ob_start();
echo '<p class="text-muted" style="margin-top:-4px;">' . epc_erp_h($boundNote) . '</p>';
if (empty($rows)) {
	erp_empty_state('Nothing outstanding.', 'fa-check-circle');
} else {
	$headers = array_merge(array($meta['entity']), array_map(function ($l) { return $l . ' days'; }, $labels), array('Total'));
	erp_table_open($headers);
	foreach ($rows as $r) {
		echo '<tr><td>' . epc_erp_h($r['name']) . '</td>';
		foreach ($r['buckets'] as $bv) {
			echo '<td style="text-align:right;">' . ($bv > 0.005 ? epc_erp_money($bv) : '<span class="text-muted">—</span>') . '</td>';
		}
		echo '<td style="text-align:right;"><strong>' . epc_erp_money($r['total']) . '</strong></td></tr>';
	}
	echo '<tr style="background:#f5f5f5;font-weight:bold;"><td>Total</td>';
	foreach ($totals as $tv) {
		echo '<td style="text-align:right;">' . epc_erp_money($tv) . '</td>';
	}
	echo '<td style="text-align:right;">' . epc_erp_money($grand) . '</td></tr>';
	erp_table_close();
}
erp_section_card($meta['label'] . ' detail', ob_get_clean(), array('icon' => 'fa-list'));

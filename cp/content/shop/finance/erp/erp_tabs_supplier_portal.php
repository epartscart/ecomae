<?php
defined('_ASTEXE_') or die('No access');
/**
 * Supplier Portal — supplier performance scorecards + per-supplier activity.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_supplier_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$spSupplier = (int) ($_GET['sp_supplier'] ?? 0);

erp_page_header(
	'<i class="fa fa-handshake-o"></i> Supplier portal',
	'Supplier performance scorecards — delivery, responsiveness, spend and payables — with per-supplier activity.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Supplier portal'),
	)
);

$tabBase = epc_erp_tab_url($erpUrl, 'supplier_portal', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';

function epc_sp_rating_label(string $rating): string
{
	$map = array('A' => 'success', 'B' => 'info', 'C' => 'warning', 'D' => 'danger');
	$cls = $map[$rating] ?? 'default';
	return '<span class="label label-' . $cls . '">' . epc_erp_h($rating) . '</span>';
}

function epc_sp_pct(?float $v): string
{
	return $v === null ? '<span class="text-muted">—</span>' : epc_erp_money($v) . '%';
}

if ($spSupplier > 0) {
	$d = epc_sp_supplier_detail($db_link, $spSupplier);
	$card = $d['card'];
	$backUrl = $tabBase;
	if ($card === null) {
		echo '<div class="alert alert-warning">Supplier not found. <a href="' . epc_erp_h($backUrl) . '">Back</a></div>';
		return;
	}
	?>
	<p><a href="<?php echo epc_erp_h($backUrl); ?>">&laquo; Back to suppliers</a></p>
	<h4 style="margin-top:0;"><i class="fa fa-truck"></i> <?php echo epc_erp_h($card['name']); ?>
		<?php echo epc_sp_rating_label($card['rating']); ?>
		<small class="text-muted"><?php echo epc_erp_h($card['email']); ?> <?php echo $card['phone'] !== '' ? '· ' . epc_erp_h($card['phone']) : ''; ?></small>
	</h4>
	<?php
	erp_stat_cards(array(
		array('label' => 'Performance score', 'value' => epc_erp_money($card['score']) . ' / 100'),
		array('label' => 'Purchase orders', 'value' => (string) $card['po_count']),
		array('label' => 'Total spend', 'value' => epc_erp_money($card['spend']) . ' AED'),
		array('label' => 'On-time delivery', 'value' => $card['ontime_pct'] === null ? '—' : epc_erp_money($card['ontime_pct']) . '%'),
		array('label' => 'Open payable', 'value' => epc_erp_money($card['balance']) . ' AED'),
	));
	?>
	<div class="row">
		<div class="col-md-6">
			<div class="well well-sm">
				<h5><i class="fa fa-tachometer"></i> Score breakdown</h5>
				<table class="table table-condensed">
					<tbody>
						<tr><th>On-time delivery</th><td class="text-right"><?php echo epc_sp_pct($card['ontime_pct']); ?></td></tr>
						<tr><th>Avg delivery lead time</th><td class="text-right"><?php echo epc_erp_money($card['avg_lead_days']); ?> days</td></tr>
						<tr><th>RFQ response rate</th><td class="text-right"><?php echo epc_sp_pct($card['response_pct']); ?></td></tr>
						<tr><th>RFQ win rate</th><td class="text-right"><?php echo epc_sp_pct($card['win_pct']); ?></td></tr>
						<tr><th>RFQs received</th><td class="text-right"><?php echo (int) $card['rfq_count']; ?></td></tr>
					</tbody>
				</table>
			</div>
		</div>
		<div class="col-md-6">
			<div class="well well-sm">
				<h5><i class="fa fa-envelope-o"></i> Recent RFQs</h5>
				<?php if (empty($d['rfqs'])): ?>
					<p class="text-muted">No RFQs.</p>
				<?php else: ?>
				<table class="table table-condensed">
					<thead><tr><th>RFQ</th><th>Title</th><th class="text-right">Est.</th><th>Status</th></tr></thead>
					<tbody>
					<?php foreach ($d['rfqs'] as $r): ?>
						<tr><td><?php echo epc_erp_h($r['rfq_no']); ?></td><td><small><?php echo epc_erp_h($r['title']); ?></small></td><td class="text-right"><?php echo epc_erp_money($r['amount_est']); ?></td><td><span class="label label-default"><?php echo epc_erp_h($r['status']); ?></span></td></tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<?php endif; ?>
			</div>
		</div>
	</div>
	<h5><i class="fa fa-clipboard"></i> Purchase orders</h5>
	<?php if (empty($d['pos'])): ?>
		<p class="text-muted">No purchase orders.</p>
	<?php else: ?>
	<div class="table-responsive">
	<table class="table table-bordered table-condensed table-hover">
		<thead><tr><th>PO</th><th>Title</th><th class="text-right">Total</th><th>Status</th><th>Approved</th><th>Received</th><th class="text-right">Lead (d)</th></tr></thead>
		<tbody>
		<?php foreach ($d['pos'] as $p):
			$lead = ((int) $p['received_at'] > 0 && (int) $p['approved_at'] > 0) ? round(((int) $p['received_at'] - (int) $p['approved_at']) / 86400.0, 1) : null; ?>
			<tr>
				<td><?php echo epc_erp_h($p['po_no']); ?></td>
				<td><small><?php echo epc_erp_h($p['title']); ?></small></td>
				<td class="text-right"><?php echo epc_erp_money($p['total_amount']); ?></td>
				<td><span class="label label-default"><?php echo epc_erp_h($p['status']); ?></span></td>
				<td><small><?php echo (int) $p['approved_at'] > 0 ? date('Y-m-d', (int) $p['approved_at']) : '—'; ?></small></td>
				<td><small><?php echo (int) $p['received_at'] > 0 ? date('Y-m-d', (int) $p['received_at']) : '—'; ?></small></td>
				<td class="text-right"><?php echo $lead === null ? '—' : epc_erp_money($lead); ?></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
	</div>
	<?php endif;
	return;
}

/* ---------- Supplier scorecard grid ---------- */
$cards = epc_sp_scorecards($db_link);
$totSpend = 0.0;
$totBal = 0.0;
$aCount = 0;
foreach ($cards as $c) {
	$totSpend += (float) $c['spend'];
	$totBal += (float) $c['balance'];
	if ($c['rating'] === 'A') {
		$aCount++;
	}
}
erp_stat_cards(array(
	array('label' => 'Suppliers', 'value' => (string) count($cards)),
	array('label' => 'A-rated suppliers', 'value' => (string) $aCount),
	array('label' => 'Total spend', 'value' => epc_erp_money($totSpend) . ' AED'),
	array('label' => 'Open payables', 'value' => epc_erp_money($totBal) . ' AED'),
));
?>
<p class="text-muted">Composite score weighs on-time delivery (40%), RFQ responsiveness (30%), purchasing activity (20%) and RFQ win rate (10%). Click a supplier for full activity.</p>
<?php if (empty($cards)): ?>
	<div class="alert alert-info">No active suppliers yet.</div>
<?php else: ?>
<div class="table-responsive">
<table class="table table-bordered table-condensed table-hover">
	<thead><tr>
		<th>Supplier</th><th class="text-center">Rating</th><th class="text-right">Score</th>
		<th class="text-right">POs</th><th class="text-right">Spend</th><th class="text-right">On-time</th>
		<th class="text-right">Avg lead (d)</th><th class="text-right">RFQ resp.</th><th class="text-right">Win</th>
		<th class="text-right">Open payable</th>
	</tr></thead>
	<tbody>
	<?php foreach ($cards as $c):
		$url = $tabBase . $sep . 'sp_supplier=' . (int) $c['id']; ?>
		<tr>
			<td><a href="<?php echo epc_erp_h($url); ?>"><strong><?php echo epc_erp_h($c['name']); ?></strong></a><?php echo $c['email'] !== '' ? '<br><small class="text-muted">' . epc_erp_h($c['email']) . '</small>' : ''; ?></td>
			<td class="text-center"><?php echo epc_sp_rating_label($c['rating']); ?></td>
			<td class="text-right"><strong><?php echo epc_erp_money($c['score']); ?></strong></td>
			<td class="text-right"><?php echo (int) $c['po_count']; ?></td>
			<td class="text-right"><?php echo epc_erp_money($c['spend']); ?></td>
			<td class="text-right"><?php echo epc_sp_pct($c['ontime_pct']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($c['avg_lead_days']); ?></td>
			<td class="text-right"><?php echo epc_sp_pct($c['response_pct']); ?></td>
			<td class="text-right"><?php echo epc_sp_pct($c['win_pct']); ?></td>
			<td class="text-right"><?php echo epc_erp_money($c['balance']); ?></td>
		</tr>
	<?php endforeach; ?>
	</tbody>
</table>
</div>
<?php endif; ?>

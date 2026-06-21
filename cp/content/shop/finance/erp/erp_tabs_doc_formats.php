<?php
/**
 * Module: Documents.
 * Sub-modules: GRN, Goods payment note, Receipt voucher format, Payment
 * voucher format, Invoice format — each rendered with the tenant letterhead
 * (logo / legal name / TRN), so documents carry the client's legal identity.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
epc_erp_pm_inline_assets();

$formats = array(
	'grn' => array('label' => 'Goods Receipt Note (GRN)', 'icon' => 'fa-inbox', 'prefix' => 'GRN', 'desc' => 'Records goods received against a purchase order.'),
	'gpn' => array('label' => 'Goods Payment Note', 'icon' => 'fa-money', 'prefix' => 'GPN', 'desc' => 'Acknowledges payment made for received goods.'),
	'receipt' => array('label' => 'Receipt Voucher', 'icon' => 'fa-arrow-down', 'prefix' => 'RV', 'desc' => 'Money received from a customer.'),
	'payment' => array('label' => 'Payment Voucher', 'icon' => 'fa-arrow-up', 'prefix' => 'PV', 'desc' => 'Money paid to a supplier / payee.'),
	'invoice' => array('label' => 'Tax Invoice', 'icon' => 'fa-file-text-o', 'prefix' => 'SI', 'desc' => 'Sales tax invoice with VAT / tax breakdown.'),
);
$view = isset($_GET['pm_view']) && isset($formats[$_GET['pm_view']]) ? (string) $_GET['pm_view'] : 'grn';
$subs = array();
foreach ($formats as $k => $f) {
	$subs[$k] = $f['label'];
}

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-files-o"></i> Documents</h3>';
echo '<p class="text-muted">Standard document formats — every format carries the tenant letterhead (logo, legal name, TRN, address). Configure the letterhead under Setup &amp; Data → Accounting setup / Company profile.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'doc_formats', 'collaboration', $date_from_str, $date_to_str, $subs, $view);

$co = array();
try {
	epc_co_ensure_schema($db_link);
	$co = epc_co_profile_get($db_link);
} catch (Exception $e) {
}
$legal = (string) ($co['legal_name'] ?? '') ?: 'Your Company LLC';
$trnLabel = (string) ($co['tax_label'] ?? 'TRN') ?: 'TRN';
$trn = (string) ($co['trn'] ?? '');
$addr = (string) ($co['address'] ?? '');
$city = (string) ($co['city'] ?? '');
$country = (string) ($co['country'] ?? '');
$logo = (string) ($co['logo_url'] ?? '');
$currency = (string) ($co['base_currency'] ?? 'AED') ?: 'AED';

$f = $formats[$view];
$docNo = $f['prefix'] . '-' . date('Y') . '-00001';
?>
<div class="epc-erp-section">
	<h4><i class="fa <?php echo epc_erp_h($f['icon']); ?>"></i> <?php echo epc_erp_h($f['label']); ?> — format preview</h4>
	<p class="text-muted"><?php echo epc_erp_h($f['desc']); ?> Document numbering follows the per-tenant voucher sequence (<?php echo epc_erp_h($f['prefix']); ?>-YYYY-NNNNN).</p>

	<div style="border:1px solid #cbd5e1;border-radius:8px;padding:0;max-width:760px;background:#fff;">
		<!-- Letterhead -->
		<div style="display:flex;justify-content:space-between;align-items:flex-start;padding:18px 22px;border-bottom:2px solid #1f3a52;">
			<div style="display:flex;gap:12px;align-items:center;">
				<?php if ($logo !== ''): ?><img src="<?php echo epc_erp_h($logo); ?>" alt="logo" style="width:54px;height:54px;border-radius:8px;object-fit:contain;"><?php else: ?><div style="width:54px;height:54px;border-radius:8px;background:#1f3a52;color:#fff;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:20px;"><?php echo epc_erp_h(strtoupper(substr($legal, 0, 1))); ?></div><?php endif; ?>
				<div>
					<div style="font-size:18px;font-weight:800;color:#1f3a52;"><?php echo epc_erp_h($legal); ?></div>
					<div style="font-size:12px;color:#64748b;"><?php echo epc_erp_h(trim($addr . ($city ? ', ' . $city : '') . ($country ? ', ' . $country : ''), ', ')); ?></div>
					<?php if ($trn !== ''): ?><div style="font-size:12px;color:#0f172a;"><strong><?php echo epc_erp_h($trnLabel); ?>:</strong> <?php echo epc_erp_h($trn); ?></div><?php endif; ?>
				</div>
			</div>
			<div style="text-align:right;">
				<div style="font-size:20px;font-weight:800;letter-spacing:1px;color:#1f3a52;"><?php echo epc_erp_h(strtoupper(str_replace(' format', '', $f['label']))); ?></div>
				<div style="font-size:12px;color:#64748b;">No: <strong><?php echo epc_erp_h($docNo); ?></strong></div>
				<div style="font-size:12px;color:#64748b;">Date: <?php echo epc_erp_h(date('d M Y')); ?></div>
			</div>
		</div>
		<!-- Body -->
		<div style="padding:18px 22px;">
			<table style="width:100%;font-size:13px;border-collapse:collapse;">
				<thead><tr style="background:#f1f5f9;">
					<th style="text-align:left;padding:6px 8px;border:1px solid #e2e8f0;">#</th>
					<th style="text-align:left;padding:6px 8px;border:1px solid #e2e8f0;">Description</th>
					<th style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">Qty</th>
					<th style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">Rate</th>
					<th style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">Amount</th>
				</tr></thead>
				<tbody>
					<tr><td style="padding:6px 8px;border:1px solid #e2e8f0;">1</td><td style="padding:6px 8px;border:1px solid #e2e8f0;">Sample line item</td><td style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">10</td><td style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">100.00</td><td style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">1,000.00</td></tr>
				</tbody>
				<tfoot>
					<tr><td colspan="4" style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">Subtotal</td><td style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">1,000.00</td></tr>
					<?php if ($view === 'invoice'): ?><tr><td colspan="4" style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">VAT 5%</td><td style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">50.00</td></tr><?php endif; ?>
					<tr style="font-weight:700;"><td colspan="4" style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;">Total (<?php echo epc_erp_h($currency); ?>)</td><td style="text-align:right;padding:6px 8px;border:1px solid #e2e8f0;"><?php echo $view === 'invoice' ? '1,050.00' : '1,000.00'; ?></td></tr>
				</tfoot>
			</table>
			<div style="display:flex;justify-content:space-between;margin-top:40px;font-size:12px;color:#475569;">
				<div>Prepared by ______________</div>
				<div>Authorised signatory ______________</div>
			</div>
		</div>
	</div>

	<div style="margin-top:12px;">
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'erp_setup', $date_from_str, $date_to_str, 'setup')); ?>"><i class="fa fa-cogs"></i> Edit letterhead / company profile</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'document_control', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-print"></i> Document control</a>
	</div>
</div>
<?php

<?php
/**
 * RFID Provision — radio-frequency identification for inventory tracking.
 * Tag items, bulk scan, real-time location, anti-theft, and audit.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-wifi"></i> RFID System',
	'RFID-based inventory management — tag products, bulk scan for stocktaking, real-time tracking, and anti-theft gates.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'RFID'),
	),
	array(array('label' => 'Scan mode', 'url' => '#', 'class' => 'btn-success', 'icon' => 'fa-wifi'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-tags"></i> RFID tag management</h4>
	<p class="text-muted">Each product gets a unique RFID tag (UHF or HF). Tags link to the product barcode/SKU in the system.</p>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#2563eb;">2,458</h3><small class="text-muted">Tagged items</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;">98.4%</h3><small class="text-muted">Scan accuracy</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#dc2626;">3</h3><small class="text-muted">Missing (alert)</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#7c3aed;">45 sec</h3><small class="text-muted">Last full scan</small></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-search"></i> RFID scanner</h4>
	<div class="panel panel-default" style="background:#f0f9ff;border:2px dashed #3b82f6;border-radius:8px;padding:30px;text-center;">
		<div class="text-center">
			<i class="fa fa-wifi fa-3x" style="color:#3b82f6;margin-bottom:12px;"></i>
			<h5>RFID bulk scan mode</h5>
			<p class="text-muted">Connect handheld reader or fixed gate. Scan results appear automatically.</p>
			<button class="btn btn-primary"><i class="fa fa-play"></i> Start scanning</button>
			<button class="btn btn-default"><i class="fa fa-list"></i> Last scan results</button>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-list"></i> Recent scans</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Date/time</th><th>Location</th><th>Scanned</th><th>Expected</th><th>Discrepancy</th><th>Duration</th><th></th></tr></thead>
		<tbody>
			<tr><td>2026-06-21 07:30</td><td>Main showroom</td><td>845</td><td>848</td><td><span class="text-danger">-3 items</span></td><td>42s</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i> Details</a></td></tr>
			<tr><td>2026-06-20 18:00</td><td>Vault / Safe</td><td>312</td><td>312</td><td><span class="text-success">0</span></td><td>28s</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-20 07:30</td><td>Main showroom</td><td>848</td><td>848</td><td><span class="text-success">0</span></td><td>45s</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> RFID configuration</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Tag type</label>
			<select class="form-control input-sm"><option>UHF (long range, 1-10m)</option><option>HF/NFC (short range, 1-10cm)</option></select>
		</div>
		<div class="pm-field"><label>Reader hardware</label>
			<select class="form-control input-sm"><option>Zebra FX7500</option><option>Impinj Speedway</option><option>ThingMagic M6e</option><option>Custom / Generic</option></select>
		</div>
		<div class="pm-field"><label>Anti-theft gate</label>
			<select class="form-control input-sm"><option value="1">Enabled — alarm on uncleared items</option><option value="0">Disabled</option></select>
		</div>
		<div class="pm-field"><label>Auto-scan schedule</label>
			<select class="form-control input-sm"><option>Morning open + Evening close</option><option>Every hour</option><option>Manual only</option></select>
		</div>
		<div class="pm-field"><label>Alert threshold</label>
			<select class="form-control input-sm"><option>Any discrepancy</option><option>2+ items</option><option>5+ items</option></select>
		</div>
	</div>
</div>
<?php
erp_section_card('RFID System', ob_get_clean(), array('icon' => 'fa-wifi'));

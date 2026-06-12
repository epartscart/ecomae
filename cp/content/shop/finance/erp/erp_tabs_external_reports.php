<?php
defined('_ASTEXE_') or die('No access');
/**
 * External Reporting — statutory / regulatory report centre.
 *
 * A country-aware catalogue of 26 categories / ~300 external report types. Every
 * report exposes the governing LAW, the official REPORTING FORMAT / filing
 * portal and (for financial reports) the IFRS standard link, plus a Fetch
 * button. Priority statutory reports (VAT, Corporate Tax, IFRS financial
 * statements + notes, WPS, UBO, Economic Substance ...) are built from live ERP
 * data on one click.
 *
 * WORLDWIDE RULE: everything resolves from the tenant's REGISTRATION COUNTRY
 * (company profile). The country selector is a preview / look-up only — generated
 * reports always use the registered country.
 *
 * Backed by epc_erp_external_reports.php + epc_erp_external_reports_build.php.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_external_reports.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_external_reports_build.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

// Tenant registration country (drives compliance + generated reports).
$erpCo = function_exists('epc_co_profile_get') ? epc_co_profile_get($db_link) : array();
$regCountry = !empty($erpCo['country']) ? strtoupper(substr((string) $erpCo['country'], 0, 2)) : 'AE';
$regProf = epc_country_profile($regCountry);
$regName = (string) ($regProf['name'] ?? $regCountry);

// Preview country (look-up only). Defaults to the registered country.
$prevCountry = isset($_GET['rep_country']) ? strtoupper(preg_replace('/[^A-Za-z]/', '', (string) $_GET['rep_country'])) : $regCountry;
if ($prevCountry === '') {
	$prevCountry = $regCountry;
}

$cats = epc_ext_reports_categories();
$registry = epc_ext_reports_registry();
$selCat = isset($_GET['cat']) ? preg_replace('/[^a-z]/', '', (string) $_GET['cat']) : '';
$selRep = isset($_GET['rep']) ? preg_replace('/[^a-z0-9_]/', '', (string) $_GET['rep']) : '';

$baseUrl = epc_erp_tab_url($erpUrl, 'ext_reports', $date_from_str, $date_to_str, 'regrep');
$sep = (strpos($baseUrl, '?') !== false) ? '&' : '?';
$repUrl = function (string $key) use ($baseUrl, $sep, $registry) {
	$cat = $registry[$key]['cat'] ?? '';
	return $baseUrl . $sep . 'cat=' . urlencode($cat) . '&rep=' . urlencode($key);
};

erp_page_header(
	'<i class="fa fa-file-text-o"></i> External Reporting',
	'Statutory &amp; regulatory reports — auto-formatted from your ERP data and localized to your registration country (' . epc_erp_h($regName) . ').',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'External Reporting'),
	)
);

// Country banner + preview selector.
$countryOptions = array('AE','SA','QA','OM','BH','KW','IN','PK','BD','LK','SG','MY','GB','US','DE','FR','AU','ZA','EG','TR');
?>
<div class="epc-erp-section" style="margin-bottom:14px;">
	<div style="display:flex;flex-wrap:wrap;gap:14px;align-items:center;justify-content:space-between;">
		<div>
			<span class="label label-primary" style="font-size:13px;"><i class="fa fa-globe"></i> Registration country: <?php echo epc_erp_h($regName . ' (' . $regCountry . ')'); ?></span>
			<?php if ($regCountry === 'AE'): ?>
				<span class="label label-success" style="font-size:12px;"><i class="fa fa-check-circle"></i> UAE statutory sub-layer active (FTA · MOHRE · goAML · MoEC)</span>
			<?php endif; ?>
			<div class="text-muted" style="margin-top:6px;font-size:12px;">All generated reports use your registered country. The selector below is a preview/look-up only.</div>
		</div>
		<form method="get" class="form-inline" style="margin:0;">
			<?php foreach (array('area' => 'regrep', 'tab' => 'ext_reports', 'from' => $date_from_str, 'to' => $date_to_str) as $k => $v): ?>
				<input type="hidden" name="<?php echo epc_erp_h($k); ?>" value="<?php echo epc_erp_h($v); ?>">
			<?php endforeach; ?>
			<?php if ($selCat !== ''): ?><input type="hidden" name="cat" value="<?php echo epc_erp_h($selCat); ?>"><?php endif; ?>
			<?php if ($selRep !== ''): ?><input type="hidden" name="rep" value="<?php echo epc_erp_h($selRep); ?>"><?php endif; ?>
			<label style="font-size:12px;margin-right:6px;">Preview jurisdiction</label>
			<select name="rep_country" class="form-control input-sm" onchange="this.form.submit()">
				<?php foreach ($countryOptions as $cc): $cp = epc_country_profile($cc); ?>
					<option value="<?php echo epc_erp_h($cc); ?>" <?php echo $cc === $prevCountry ? 'selected' : ''; ?>><?php echo epc_erp_h(($cp['name'] ?? $cc) . ' (' . $cc . ')'); ?></option>
				<?php endforeach; ?>
			</select>
		</form>
	</div>
</div>

<?php
if ($selRep !== '' && isset($registry[$selRep])) {
	// ---------------------------------------------------------------- single report
	// The report renders for the registered country by default. When the admin
	// previews another jurisdiction the report re-localizes to it (an explicit
	// look-up) — compliance of record still keys off the registered country.
	$def = $registry[$selRep];
	$repCountry = $prevCountry !== '' ? $prevCountry : $regCountry;
	$isPreview = strtoupper($repCountry) !== strtoupper($regCountry);
	$repCountryName = (string) (epc_country_profile($repCountry)['name'] ?? $repCountry);
	$links = epc_ext_report_links($selRep, $repCountry);
	$auth = $links['authority'];
	$ifrs = $links['ifrs'];
	$built = epc_ext_report_build($db_link, $selRep, $repCountry, $date_from, $date_to);
	$fetched = isset($_GET['fetch']);
	$fetchUrl = $repUrl($selRep) . '&rep_country=' . urlencode($repCountry) . '&fetch=' . time();
	$co = epc_co_profile_get($db_link);
	?>
	<?php if ($isPreview): ?>
	<div class="alert alert-info" style="padding:8px 12px;margin-bottom:10px;">
		<i class="fa fa-eye"></i> <strong>Preview — <?php echo epc_erp_h($repCountryName); ?>.</strong>
		Figures, rates, authority &amp; format are localized to <?php echo epc_erp_h($repCountry); ?> for evaluation.
		Your compliance country of record is <strong><?php echo epc_erp_h($regName); ?></strong> (set it in Setup → Company).
	</div>
	<?php endif; ?>
	<p style="margin-bottom:10px;">
		<a href="<?php echo epc_erp_h($baseUrl . $sep . 'cat=' . urlencode((string) $def['cat'])); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> Back to <?php echo epc_erp_h($cats[$def['cat']] ?? 'catalogue'); ?></a>
	</p>

	<div class="epc-erp-section" style="margin-bottom:14px;">
		<div style="display:flex;flex-wrap:wrap;gap:10px;align-items:center;">
			<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h($fetchUrl); ?>" title="Re-fetch ERP data and rebuild this report"><i class="fa fa-refresh"></i> Fetch &amp; build</a>
			<button type="button" class="btn btn-default btn-sm" onclick="epcExtPrint();"><i class="fa fa-print"></i> Print / PDF</button>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($auth['url']); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-university"></i> Official source — <?php echo epc_erp_h($auth['name']); ?></a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($auth['format']); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-file-o"></i> Official format / filing portal</a>
			<?php if ($ifrs): ?>
				<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($ifrs['url']); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-book"></i> <?php echo epc_erp_h($ifrs['label']); ?></a>
			<?php endif; ?>
			<?php if ($fetched): ?>
				<span class="text-success" style="margin-left:4px;"><i class="fa fa-check-circle"></i> Built from live ERP data · <?php echo date('d M Y H:i'); ?> — verify on the official source.</span>
			<?php endif; ?>
		</div>
		<div style="margin-top:8px;font-size:12px;" class="text-muted">
			<strong>Governing law:</strong> <?php echo epc_erp_h($auth['law']); ?>
			&nbsp;·&nbsp; <strong>Frequency:</strong> <?php echo epc_erp_h(epc_ext_report_frequency((string) $def['cat'])); ?>
			&nbsp;·&nbsp; <?php echo $built['live'] ? '<span class="label label-success">Live data</span>' : '<span class="label label-default">Formatted template</span>'; ?>
		</div>
	</div>

	<div id="epc_ext_doc" class="epc-erp-section" style="background:#fff;border:1px solid #e2e6ee;padding:26px;">
		<div style="display:flex;justify-content:space-between;border-bottom:3px solid #2b3a55;padding-bottom:12px;margin-bottom:16px;">
			<div>
				<div style="font-size:20px;font-weight:800;color:#1d2740;"><?php echo epc_erp_h((string) ($co['legal_name'] ?: 'Company')); ?></div>
				<div class="text-muted" style="font-size:12px;">
					<?php echo epc_erp_h((string) ($co['address'] ?? '')); ?>
					<?php if (!empty($co['trn'])): ?> · <?php echo epc_erp_h((string) ($co['tax_label'] ?? 'TRN')); ?>: <?php echo epc_erp_h((string) $co['trn']); ?><?php endif; ?>
				</div>
			</div>
			<div style="text-align:right;">
				<div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;color:#7a869a;">Statutory report</div>
				<div style="font-weight:700;"><?php echo epc_erp_h($repCountryName); ?></div>
				<div class="text-muted" style="font-size:12px;"><?php echo epc_erp_h($date_from_str . '  —  ' . $date_to_str); ?></div>
			</div>
		</div>

		<h3 style="margin-top:0;color:#1d2740;"><?php echo epc_erp_h($built['title']); ?></h3>
		<div class="text-muted" style="font-size:12px;margin-bottom:6px;">
			<strong>To:</strong> <?php echo epc_erp_h($auth['name']); ?> &nbsp;·&nbsp; <strong>Under:</strong> <?php echo epc_erp_h($auth['law']); ?>
		</div>

		<?php if (!empty($built['summary'])): ?>
			<div style="display:flex;flex-wrap:wrap;gap:10px;margin:12px 0;">
				<?php foreach ($built['summary'] as $k => $v): ?>
					<div style="background:#f5f7fa;border:1px solid #e2e6ee;border-radius:6px;padding:8px 14px;min-width:140px;">
						<div style="font-size:11px;color:#7a869a;text-transform:uppercase;letter-spacing:.5px;"><?php echo epc_erp_h((string) $k); ?></div>
						<div style="font-size:16px;font-weight:700;color:#1d2740;"><?php echo epc_erp_h((string) $v); ?></div>
					</div>
				<?php endforeach; ?>
			</div>
		<?php endif; ?>

		<?php echo $built['body']; ?>

		<div style="margin-top:28px;display:flex;justify-content:space-between;gap:30px;">
			<div style="flex:1;border-top:1px solid #aaa;padding-top:6px;font-size:12px;color:#555;">Prepared by &amp; date</div>
			<div style="flex:1;border-top:1px solid #aaa;padding-top:6px;font-size:12px;color:#555;">Authorised signatory &amp; stamp</div>
		</div>
		<p class="text-muted" style="font-size:11px;margin-top:18px;border-top:1px dashed #ccc;padding-top:8px;">
			Generated by Ecom BOS External Reporting on <?php echo date('d M Y H:i'); ?> from posted ERP data. Informational — verify figures and the latest format against the official source (<?php echo epc_erp_h($auth['name']); ?>) before filing.
		</p>
	</div>

	<script>
	function epcExtPrint(){
		var doc = document.getElementById('epc_ext_doc');
		if(!doc){ window.print(); return; }
		var w = window.open('', '_blank');
		w.document.write('<html><head><title><?php echo epc_erp_h(addslashes($built['title'])); ?></title>');
		w.document.write('<style>body{font-family:Arial,Helvetica,sans-serif;color:#222;padding:24px;} table{border-collapse:collapse;width:100%;} td,th{border:1px solid #bbb;padding:6px 8px;font-size:13px;} h3,h4{color:#1d2740;}</style>');
		w.document.write('</head><body>'+doc.innerHTML+'</body></html>');
		w.document.close();
		setTimeout(function(){ w.focus(); w.print(); }, 300);
	}
	</script>
	<?php
} elseif ($selCat !== '' && isset($cats[$selCat])) {
	// ---------------------------------------------------------------- one category
	?>
	<p style="margin-bottom:10px;"><a href="<?php echo epc_erp_h($baseUrl); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> All categories</a></p>
	<div class="epc-erp-section">
		<h4 style="margin-top:0;"><?php echo epc_erp_h($cats[$selCat]); ?></h4>
		<div class="row">
		<?php foreach ($registry as $key => $def): if ($def['cat'] !== $selCat) { continue; } ?>
			<div class="col-sm-6 col-md-4" style="margin-bottom:10px;">
				<a href="<?php echo epc_erp_h($repUrl($key)); ?>" class="epc-ext-card" style="display:block;border:1px solid #e2e6ee;border-radius:8px;padding:12px 14px;background:#fff;color:#1d2740;text-decoration:none;height:100%;">
					<div style="font-weight:700;"><i class="fa fa-file-text-o text-primary"></i> <?php echo epc_erp_h($def['name']); ?></div>
					<div style="margin-top:4px;">
						<?php if ($def['builder'] !== ''): ?><span class="label label-success" style="font-size:10px;">Live build</span><?php else: ?><span class="label label-default" style="font-size:10px;">Formatted template</span><?php endif; ?>
						<?php if ($def['std'] !== ''): ?><span class="label label-info" style="font-size:10px;">IFRS/Std</span><?php endif; ?>
					</div>
				</a>
			</div>
		<?php endforeach; ?>
		</div>
	</div>
	<?php
} else {
	// ---------------------------------------------------------------- catalogue (all categories)
	$total = count($registry);
	?>
	<div class="epc-erp-section" style="margin-bottom:12px;">
		<p class="text-muted" style="margin:0;"><strong><?php echo (int) count($cats); ?></strong> categories · <strong><?php echo (int) $total; ?></strong> report types. Priority statutory reports (VAT, Corporate Tax, IFRS financial statements, WPS, UBO, Economic Substance ...) build from live ERP data; the rest provide the formatted statutory structure with the correct authority, law &amp; format links for <?php echo epc_erp_h($regName); ?>.</p>
	</div>
	<div class="row">
	<?php foreach ($cats as $ck => $cl):
		$count = 0; $hasLive = false;
		foreach ($registry as $def) { if ($def['cat'] === $ck) { $count++; if ($def['builder'] !== '') { $hasLive = true; } } }
	?>
		<div class="col-sm-6 col-md-4" style="margin-bottom:12px;">
			<a href="<?php echo epc_erp_h($baseUrl . $sep . 'cat=' . urlencode($ck)); ?>" class="epc-ext-cat" style="display:block;border:1px solid #e2e6ee;border-radius:8px;padding:14px 16px;background:#fff;color:#1d2740;text-decoration:none;height:100%;">
				<div style="font-weight:700;font-size:14px;"><?php echo epc_erp_h($cl); ?></div>
				<div class="text-muted" style="font-size:12px;margin-top:6px;"><?php echo (int) $count; ?> report type<?php echo $count === 1 ? '' : 's'; ?>
					<?php if ($hasLive): ?> · <span class="text-success">live builds</span><?php endif; ?>
				</div>
			</a>
		</div>
	<?php endforeach; ?>
	</div>
	<?php
}
?>
<style>
.epc-ext-card:hover, .epc-ext-cat:hover { border-color:#2b6cb0; box-shadow:0 2px 8px rgba(43,108,176,.12); }
@media print { .epc-erp-sidebar, .epc-erp-content-toolbar, .btn, form { display:none !important; } }
</style>

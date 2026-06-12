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
$regName = epc_ext_country_name($regCountry);

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
				<?php foreach ($countryOptions as $cc): ?>
					<option value="<?php echo epc_erp_h($cc); ?>" <?php echo $cc === $prevCountry ? 'selected' : ''; ?>><?php echo epc_erp_h(epc_ext_country_name($cc) . ' (' . $cc . ')'); ?></option>
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
	$repCountryName = epc_ext_country_name($repCountry);
	$links = epc_ext_report_links($selRep, $repCountry);
	$auth = $links['authority'];
	$ifrs = $links['ifrs'];

	// Per-report reporting period (each return is scoped to its own statutory
	// period — VAT = tax quarter, CT = financial year, WPS = month, etc.).
	$periodType = epc_ext_report_period_type((string) $def['cat'], $selRep);
	$selPeriod = isset($_GET['period']) ? preg_replace('/[^0-9A-Za-z\-]/', '', (string) $_GET['period']) : '';
	$period = epc_ext_resolve_period($periodType, $selPeriod, $date_to ?: time());
	$repFrom = $period['from'];
	$repTo = $period['to'];

	$built = epc_ext_report_build($db_link, $selRep, $repCountry, $repFrom, $repTo);
	$fetched = isset($_GET['fetch']);
	$fetchUrl = $repUrl($selRep) . '&rep_country=' . urlencode($repCountry) . '&period=' . urlencode($period['token']) . '&fetch=' . time();
	$periodTypeLabel = array('month' => 'Monthly', 'quarter' => 'Tax quarter', 'year' => 'Annual (financial year)');
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
			<form method="get" class="form-inline" style="margin:0;display:inline-block;">
				<?php foreach (array('area' => 'regrep', 'tab' => 'ext_reports', 'from' => $date_from_str, 'to' => $date_to_str, 'cat' => (string) $def['cat'], 'rep' => $selRep, 'rep_country' => $repCountry) as $k => $v): ?>
					<input type="hidden" name="<?php echo epc_erp_h($k); ?>" value="<?php echo epc_erp_h((string) $v); ?>">
				<?php endforeach; ?>
				<label style="font-size:12px;margin:0 6px;"><i class="fa fa-calendar"></i> Reporting period</label>
				<select name="period" class="form-control input-sm" onchange="this.form.submit()">
					<?php foreach ($period['options'] as $tok => $lbl): ?>
						<option value="<?php echo epc_erp_h((string) $tok); ?>" <?php echo $tok === $period['token'] ? 'selected' : ''; ?>><?php echo epc_erp_h((string) $lbl); ?></option>
					<?php endforeach; ?>
				</select>
			</form>
			<?php if ($fetched): ?>
				<span class="text-success" style="margin-left:4px;"><i class="fa fa-check-circle"></i> Built from live ERP data · <?php echo date('d M Y H:i'); ?> — verify on the official source.</span>
			<?php endif; ?>
		</div>
		<div style="margin-top:8px;font-size:12px;" class="text-muted">
			<strong>Governing law:</strong> <?php echo epc_erp_h($auth['law']); ?>
			&nbsp;·&nbsp; <strong>Frequency:</strong> <?php echo epc_erp_h($periodTypeLabel[$periodType] ?? epc_ext_report_frequency((string) $def['cat'])); ?>
			&nbsp;·&nbsp; <strong>Period:</strong> <?php echo epc_erp_h($period['label'] . ' (' . date('d M Y', $repFrom) . ' — ' . date('d M Y', $repTo) . ')'); ?>
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
				<div style="font-size:12px;color:#1d2740;font-weight:600;">Reporting period: <?php echo epc_erp_h($period['label']); ?></div>
				<div class="text-muted" style="font-size:12px;"><?php echo epc_erp_h(date('d M Y', $repFrom) . '  —  ' . date('d M Y', $repTo)); ?></div>
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
		var clone = doc.cloneNode(true);
		/* expand every drill-down so the printed pack is complete */
		var ds = clone.querySelectorAll('details'); for(var i=0;i<ds.length;i++){ ds[i].setAttribute('open','open'); }
		var dr = clone.querySelectorAll('.epc-ct-drill'); for(var j=0;j<dr.length;j++){ dr[j].style.display='table-row'; }
		/* drop interactive-only controls from the print copy */
		var strip = clone.querySelectorAll('button, textarea, script, .btn'); for(var k=0;k<strip.length;k++){ if(strip[k].parentNode){ strip[k].parentNode.removeChild(strip[k]); } }
		/* the on-screen letterhead is the first child — the cover replaces it */
		if(clone.firstElementChild){ clone.firstElementChild.style.display='none'; }

		var co   = <?php echo json_encode((string) ($co['legal_name'] ?: 'Company')); ?>;
		var addr = <?php echo json_encode((string) ($co['address'] ?? '')); ?>;
		var trnL = <?php echo json_encode((string) ($co['tax_label'] ?? 'TRN')); ?>;
		var trn  = <?php echo json_encode((string) ($co['trn'] ?? '')); ?>;
		var ttl  = <?php echo json_encode((string) $built['title']); ?>;
		var juris= <?php echo json_encode((string) $repCountryName); ?>;
		var auth = <?php echo json_encode((string) $auth['name']); ?>;
		var law  = <?php echo json_encode((string) $auth['law']); ?>;
		var perL = <?php echo json_encode((string) $period['label']); ?>;
		var perR = <?php echo json_encode(date('d M Y', $repFrom) . '  —  ' . date('d M Y', $repTo)); ?>;
		var gen  = <?php echo json_encode(date('d M Y H:i')); ?>;
		var esc  = function(s){ return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); };
		var trnLine = trn ? (esc(trnL)+': '+esc(trn)) : '';

		var css =
		'@page{size:A4;margin:16mm 14mm;}'
		+'*{box-sizing:border-box;}'
		+'body{font-family:"Segoe UI",Arial,Helvetica,sans-serif;color:#1f2733;font-size:11.5px;line-height:1.45;margin:0;}'
		+'.mis-run{position:fixed;top:0;left:0;right:0;font-size:9px;color:#8a93a3;border-bottom:.5px solid #d7dce5;padding:2px 0;display:flex;justify-content:space-between;}'
		+'.mis-foot{position:fixed;bottom:0;left:0;right:0;font-size:9px;color:#8a93a3;border-top:.5px solid #d7dce5;padding:2px 0;display:flex;justify-content:space-between;}'
		+'.mis-body{padding-top:16px;}'
		+'.mis-cover{height:248mm;display:flex;flex-direction:column;justify-content:center;align-items:center;text-align:center;border:3px double #2b3a55;padding:40px;page-break-after:always;}'
		+'.mis-cover .badge{font-size:11px;letter-spacing:3px;text-transform:uppercase;color:#7a869a;}'
		+'.mis-cover .co{font-size:30px;font-weight:800;color:#1d2740;margin:10px 0 2px;}'
		+'.mis-cover .addr{font-size:12px;color:#5b6577;}'
		+'.mis-cover .ttl{font-size:23px;font-weight:700;color:#2b3a55;margin:46px 0 6px;}'
		+'.mis-cover .juris{font-size:14px;color:#1d2740;}'
		+'.mis-cover .period{font-size:16px;font-weight:700;color:#2b3a55;margin-top:26px;}'
		+'.mis-cover .perd{font-size:13px;color:#5b6577;}'
		+'.mis-cover .meta{margin-top:46px;font-size:12px;color:#5b6577;line-height:1.8;}'
		+'.mis-cover .rule{width:120px;border-top:2px solid #c2a14d;margin:24px auto;}'
		+'h3{font-size:16px;color:#1d2740;border-bottom:2px solid #2b3a55;padding-bottom:5px;margin:0 0 6px;page-break-after:avoid;}'
		+'h4{font-size:13px;color:#1d2740;margin:18px 0 6px;padding:4px 8px;background:#eef1f6;border-left:4px solid #2b3a55;page-break-after:avoid;}'
		+'table{border-collapse:collapse;width:100%;margin:6px 0;}'
		+'thead{display:table-header-group;}'
		+'tr{page-break-inside:avoid;}'
		+'td,th{border:1px solid #c7cedb;padding:5px 8px;font-size:11px;vertical-align:top;}'
		+'th{background:#2b3a55;color:#fff;text-align:left;}'
		+'details{display:block!important;}details>div{display:block!important;}'
		+'.label{display:inline-block;padding:1px 6px;border-radius:3px;font-size:9px;border:1px solid #b9c0cd;}'
		+'.label-success{background:#e7f6ec;color:#1a7f37;border-color:#bfe3cb;}'
		+'.label-warning{background:#fff5e0;color:#9a6700;border-color:#f0dca8;}'
		+'.label-danger{background:#fdecec;color:#b42318;border-color:#f3c3bd;}'
		+'.label-info{background:#e8f0fb;color:#1d4e94;border-color:#c4d6f3;}'
		+'.alert{padding:8px 12px;border-radius:5px;margin:8px 0;font-size:11px;border:1px solid #ddd;}'
		+'.alert-success{background:#e7f6ec;border-color:#bfe3cb;}'
		+'.alert-warning{background:#fff5e0;border-color:#f0dca8;}'
		+'.alert-danger{background:#fdecec;border-color:#f3c3bd;}'
		+'.text-muted{color:#7a869a;}'
		+'.mis-sign{margin-top:34px;display:flex;justify-content:space-between;gap:40px;page-break-inside:avoid;}'
		+'.mis-sign>div{flex:1;border-top:1px solid #7a869a;padding-top:6px;font-size:11px;color:#5b6577;}';

		var cover =
		'<div class="mis-cover">'
		+'<div class="badge">Statutory / Management Report</div>'
		+'<div class="co">'+esc(co)+'</div>'
		+'<div class="addr">'+esc(addr)+(trnLine?(' &nbsp;·&nbsp; '+trnLine):'')+'</div>'
		+'<div class="rule"></div>'
		+'<div class="ttl">'+esc(ttl)+'</div>'
		+'<div class="juris">Jurisdiction: '+esc(juris)+'</div>'
		+'<div class="period">Reporting period: '+esc(perL)+'</div>'
		+'<div class="perd">'+esc(perR)+'</div>'
		+'<div class="meta">Submitted to: '+esc(auth)+'<br>Governing law: '+esc(law)+'<br>Prepared on '+esc(gen)+' from posted ERP data</div>'
		+'</div>';

		var runHdr = '<div class="mis-run"><span>'+esc(co)+'</span><span>'+esc(ttl)+'</span></div>';
		var runFt  = '<div class="mis-foot"><span>'+esc(perL)+' · '+esc(juris)+'</span><span>Generated by Ecom BOS External Reporting · '+esc(gen)+'</span></div>';
		var sign   = '<div class="mis-sign"><div>Prepared by &amp; date</div><div>Reviewed by &amp; date</div><div>Authorised signatory &amp; stamp</div></div>';

		var w = window.open('', '_blank');
		w.document.write('<html><head><title>'+esc(ttl)+' — '+esc(co)+'</title><meta charset="utf-8"><style>'+css+'</style></head><body>'
			+runHdr+runFt+cover+'<div class="mis-body">'+clone.innerHTML+sign+'</div></body></html>');
		w.document.close();
		setTimeout(function(){ w.focus(); w.print(); }, 350);
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

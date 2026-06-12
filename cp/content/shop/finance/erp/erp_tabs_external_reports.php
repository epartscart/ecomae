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
$selTool = isset($_GET['tool']) ? preg_replace('/[^a-z]/', '', (string) $_GET['tool']) : '';

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
if ($selTool === 'import') {
	// ------------------------------------------------ off-system Excel/CSV import
	$impKindReq = (string) ($_GET['kind'] ?? ($_POST['imp_kind'] ?? ''));
	$impKind = in_array($impKindReq, array('vat', 'ct', 'fin'), true) ? $impKindReq : 'vat';
	$impCountry = $regCountry;
	$impCountryName = $regName;
	$impCcy = (string) ($regProf['currency'] ?? 'AED');
	if ($impKind === 'fin') {
		$impAuth = array('name' => 'IFRS Foundation (IASB)', 'law' => 'IFRS as issued by the IASB · ISA (IAASB)', 'url' => 'https://www.ifrs.org', 'format' => 'https://www.iaasb.org/standards-pronouncements');
	} elseif ($impKind === 'ct') {
		$impAuth = array('name' => 'Federal Tax Authority (FTA)', 'law' => 'Corporate Tax — Federal Decree-Law 47/2022', 'url' => 'https://tax.gov.ae', 'format' => 'https://eservices.tax.gov.ae');
	} else {
		$impAuth = array('name' => 'Federal Tax Authority (FTA)', 'law' => 'VAT — Federal Decree-Law 8/2017', 'url' => 'https://tax.gov.ae', 'format' => 'https://eservices.tax.gov.ae');
	}

	$impBuilt = null;
	$impError = '';
	if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['imp_file']) && is_array($_FILES['imp_file'])) {
		$file = $_FILES['imp_file'];
		if (($file['error'] ?? UPLOAD_ERR_NO_FILE) !== UPLOAD_ERR_OK) {
			$impError = 'Upload failed (error code ' . (int) ($file['error'] ?? -1) . '). Pick a .xlsx or .csv file under 64 MB.';
		} else {
			$rows = epc_ext_parse_all_rows((string) $file['tmp_name'], (string) $file['name']);
			if ($rows === null || count($rows) === 0) {
				$impError = 'Could not read the file. Save it as .xlsx or .csv using the provided template and try again.';
			} else {
				$map = epc_ext_import_map($rows);
				if ($impKind === 'fin') {
					if (empty($map['fin'])) {
						$impError = 'No financial-statement lines found. Use the IFRS Financials template (Code column: FIN_REVENUE, FIN_PPE, …).';
					} else {
						$impBuilt = epc_ext_b_fin_summary($map, $impCcy);
					}
				} elseif ($impKind === 'ct') {
					if (empty($map['values'])) {
						$impError = 'No CT computation lines found. Use the CT template (Code column: ACCT_PROFIT, FINES, …).';
					} else {
						$impBuilt = epc_ext_b_ct_summary($map, $impCcy);
					}
				} else {
					if (empty($map['vat'])) {
						$impError = 'No VAT boxes found. Use the VAT template (Code column: BOX1A, BOX9, …).';
					} else {
						$impBuilt = epc_ext_b_vat_summary($map, $impCcy);
					}
				}
			}
		}
	}
	$impDlBase = $baseUrl . $sep . 'tool=import';
	?>
	<p style="margin-bottom:10px;"><a href="<?php echo epc_erp_h($baseUrl); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> All categories</a></p>
	<div class="epc-erp-section" style="margin-bottom:14px;">
		<h3 style="margin-top:0;color:#1d2740;"><i class="fa fa-upload"></i> Import from Excel → Return (off-system)</h3>
		<p class="text-muted" style="font-size:12.5px;max-width:900px;">
			Prepare a VAT 201 or Corporate Tax return from an uploaded spreadsheet. The <strong>complete multi-sheet workbook</strong> carries the
			<strong>company &amp; TRN details</strong>, the box / line totals, invoice-wise detail sheets and a compliance checklist — the return is built from the totals
			(<strong>summary</strong>, no invoice rows in the output) and stamped with the company's own TRN / address from the file.
			This is completely <strong>outside the ERP</strong>: nothing is read from or written to your live data, so you can use it to check or report for other clients.
			The output renders in the proper UAE format with compliance checks and the same professional Print / PDF.
		</p>
		<div style="display:flex;flex-wrap:wrap;gap:18px;margin-top:10px;">
			<div style="flex:1;min-width:320px;border:1px solid #e2e6ee;border-radius:6px;padding:14px;">
				<div style="font-weight:700;color:#1d2740;margin-bottom:6px;">1 · Download a template</div>
				<p class="text-muted" style="font-size:12px;">Complete multi-sheet workbook — <strong>Company &amp; TRN</strong>, <strong>boxes / computation</strong>, <strong>invoice-wise detail</strong> and a <strong>compliance checklist</strong>. Keep the <code>Code</code> column unchanged; edit the values. Re-upload to build the return.</p>
				<div style="margin-bottom:6px;">
					<button type="button" class="btn btn-success btn-sm" onclick="epcDlB64('epcTplVatX','VAT201_import_template.xlsx')"><i class="fa fa-file-excel-o"></i> VAT 201 workbook (.xlsx)</button>
					<button type="button" class="btn btn-success btn-sm" onclick="epcDlB64('epcTplCtX','CT_return_import_template.xlsx')"><i class="fa fa-file-excel-o"></i> Corporate Tax workbook (.xlsx)</button>
					<button type="button" class="btn btn-success btn-sm" onclick="epcDlB64('epcTplFinX','IFRS_financials_import_template.xlsx')"><i class="fa fa-file-excel-o"></i> IFRS Financials workbook (.xlsx)</button>
				</div>
				<div>
					<button type="button" class="btn btn-default btn-xs" onclick="epcDlCsv('epcTplVat','VAT201_import_template.csv')"><i class="fa fa-file-text-o"></i> VAT CSV (summary only)</button>
					<button type="button" class="btn btn-default btn-xs" onclick="epcDlCsv('epcTplCt','CT_return_import_template.csv')"><i class="fa fa-file-text-o"></i> CT CSV (summary only)</button>
					<button type="button" class="btn btn-default btn-xs" onclick="epcDlCsv('epcTplFin','IFRS_financials_import_template.csv')"><i class="fa fa-file-text-o"></i> Financials CSV (summary only)</button>
				</div>
				<textarea id="epcTplVat" style="display:none;"><?php echo epc_erp_h(epc_ext_import_template_csv('vat')); ?></textarea>
				<textarea id="epcTplCt" style="display:none;"><?php echo epc_erp_h(epc_ext_import_template_csv('ct')); ?></textarea>
				<textarea id="epcTplFin" style="display:none;"><?php echo epc_erp_h(epc_ext_import_template_csv('fin')); ?></textarea>
				<textarea id="epcTplVatX" style="display:none;"><?php echo base64_encode(epc_ext_import_template_xlsx('vat')); ?></textarea>
				<textarea id="epcTplCtX" style="display:none;"><?php echo base64_encode(epc_ext_import_template_xlsx('ct')); ?></textarea>
				<textarea id="epcTplFinX" style="display:none;"><?php echo base64_encode(epc_ext_import_template_xlsx('fin')); ?></textarea>
				<script>
				function epcDlCsv(id,fn){var el=document.getElementById(id);if(!el)return;var t=(el.value!==undefined?el.value:el.textContent);var blob=new Blob([t],{type:"text/csv;charset=utf-8;"});var url=URL.createObjectURL(blob);var a=document.createElement("a");a.href=url;a.download=fn;document.body.appendChild(a);a.click();document.body.removeChild(a);URL.revokeObjectURL(url);}
				function epcDlB64(id,fn){var el=document.getElementById(id);if(!el)return;var b64=(el.value!==undefined?el.value:el.textContent).replace(/\s+/g,'');var bin=atob(b64);var len=bin.length;var bytes=new Uint8Array(len);for(var i=0;i<len;i++){bytes[i]=bin.charCodeAt(i);}var blob=new Blob([bytes],{type:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"});var url=URL.createObjectURL(blob);var a=document.createElement("a");a.href=url;a.download=fn;document.body.appendChild(a);a.click();document.body.removeChild(a);URL.revokeObjectURL(url);}
				</script>
			</div>
			<div style="flex:1;min-width:320px;border:1px solid #e2e6ee;border-radius:6px;padding:14px;">
				<div style="font-weight:700;color:#1d2740;margin-bottom:6px;">2 · Upload &amp; build</div>
				<form method="post" enctype="multipart/form-data" action="<?php echo epc_erp_h($impDlBase); ?>" class="form-inline" style="margin:0;">
					<select name="imp_kind" class="form-control input-sm" style="margin:4px 6px 4px 0;">
						<option value="vat" <?php echo $impKind === 'vat' ? 'selected' : ''; ?>>VAT Return (FTA VAT 201)</option>
						<option value="ct" <?php echo $impKind === 'ct' ? 'selected' : ''; ?>>Corporate Tax Return</option>
						<option value="fin" <?php echo $impKind === 'fin' ? 'selected' : ''; ?>>IFRS Financial Statements &amp; Audit Report</option>
					</select>
					<input type="file" name="imp_file" accept=".xlsx,.csv" class="form-control input-sm" style="margin:4px 6px 4px 0;" required>
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-cogs"></i> Build return</button>
				</form>
				<p class="text-muted" style="font-size:11px;margin-top:8px;">Accepts <code>.xlsx</code> or <code>.csv</code>. Off-system — your file is parsed in-memory and not stored.</p>
			</div>
		</div>
	</div>

	<?php if ($impError !== ''): ?>
		<div class="alert alert-danger"><i class="fa fa-exclamation-triangle"></i> <?php echo epc_erp_h($impError); ?></div>
	<?php endif; ?>

	<?php if ($impBuilt !== null):
		$impMeta = $impBuilt['meta'] ?? array();
		$impCo = $impMeta['META_LEGAL_NAME'] ?? 'Uploaded client';
		$impTrn = $impMeta['META_TRN'] ?? '';
		$impPerL = trim((string) ($impMeta['META_PERIOD_FROM'] ?? '')) !== '' ? (($impMeta['META_PERIOD_FROM'] ?? '') . '  —  ' . ($impMeta['META_PERIOD_TO'] ?? '')) : 'As uploaded';
		$impAddrParts = array_filter(array(
			trim((string) ($impMeta['META_ADDRESS'] ?? '')),
			trim((string) ($impMeta['META_EMIRATE'] ?? '')),
		), static function ($v) { return $v !== ''; });
		$impAddr = implode(', ', $impAddrParts);
		$impContact = array_filter(array(
			trim((string) ($impMeta['META_PHONE'] ?? '')),
			trim((string) ($impMeta['META_EMAIL'] ?? '')),
		), static function ($v) { return $v !== ''; });
		$impContactL = implode(' · ', $impContact);
		if ($impAddr === '') { $impAddr = $impCountryName; }
	?>
		<div class="epc-erp-section" style="margin-bottom:14px;">
			<div style="display:flex;flex-wrap:wrap;gap:10px;align-items:center;">
				<button type="button" class="btn btn-default btn-sm" onclick="epcExtPrint();"><i class="fa fa-print"></i> Print / PDF</button>
				<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($impAuth['url']); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-university"></i> Official source — <?php echo epc_erp_h($impAuth['name']); ?></a>
				<span class="label label-info" style="font-size:11px;"><i class="fa fa-upload"></i> Off-system data</span>
			</div>
		</div>
		<div id="epc_ext_doc" class="epc-erp-section" style="background:#fff;border:1px solid #e2e6ee;padding:26px;">
			<div style="display:flex;justify-content:space-between;border-bottom:3px solid #2b3a55;padding-bottom:12px;margin-bottom:16px;">
				<div>
					<div style="font-size:20px;font-weight:800;color:#1d2740;"><?php echo epc_erp_h((string) $impCo); ?></div>
					<div class="text-muted" style="font-size:12px;">
						<?php echo epc_erp_h((string) $impAddr); ?><?php if ($impTrn !== ''): ?> · TRN: <?php echo epc_erp_h((string) $impTrn); ?><?php endif; ?>
					</div>
					<?php if ($impContactL !== ''): ?><div class="text-muted" style="font-size:12px;"><?php echo epc_erp_h((string) $impContactL); ?></div><?php endif; ?>
				</div>
				<div style="text-align:right;">
					<div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;color:#7a869a;">Imported return</div>
					<div style="font-weight:700;"><?php echo epc_erp_h($impCountryName); ?></div>
					<div style="font-size:12px;color:#1d2740;font-weight:600;">Reporting period: <?php echo epc_erp_h((string) $impPerL); ?></div>
				</div>
			</div>
			<h3 style="margin-top:0;color:#1d2740;"><?php echo epc_erp_h((string) $impBuilt['title']); ?></h3>
			<div class="text-muted" style="font-size:12px;margin-bottom:6px;">
				<strong>To:</strong> <?php echo epc_erp_h($impAuth['name']); ?> &nbsp;·&nbsp; <strong>Under:</strong> <?php echo epc_erp_h($impAuth['law']); ?>
			</div>
			<?php if (!empty($impBuilt['summary'])): ?>
				<div style="display:flex;flex-wrap:wrap;gap:10px;margin:12px 0;">
					<?php foreach ($impBuilt['summary'] as $k => $v): ?>
						<div style="background:#f5f7fa;border:1px solid #e2e6ee;border-radius:6px;padding:8px 14px;min-width:140px;">
							<div style="font-size:11px;color:#7a869a;text-transform:uppercase;letter-spacing:.5px;"><?php echo epc_erp_h((string) $k); ?></div>
							<div style="font-size:16px;font-weight:700;color:#1d2740;"><?php echo epc_erp_h((string) $v); ?></div>
						</div>
					<?php endforeach; ?>
				</div>
			<?php endif; ?>
			<?php echo $impBuilt['body']; ?>
			<div style="margin-top:28px;display:flex;justify-content:space-between;gap:30px;">
				<div style="flex:1;border-top:1px solid #aaa;padding-top:6px;font-size:12px;color:#555;">Prepared by &amp; date</div>
				<div style="flex:1;border-top:1px solid #aaa;padding-top:6px;font-size:12px;color:#555;">Authorised signatory &amp; stamp</div>
			</div>
			<p class="text-muted" style="font-size:11px;margin-top:18px;border-top:1px dashed #ccc;padding-top:8px;">
				Built by Ecom BOS External Reporting on <?php echo date('d M Y H:i'); ?> from an <strong>uploaded file</strong> (off-system). Informational — verify figures and the latest format against the official source (<?php echo epc_erp_h($impAuth['name']); ?>) before filing.
			</p>
		</div>
		<?php
		echo epc_ext_print_ctx_js(array(
			'co'    => (string) $impCo,
			'addr'  => $impAddr . ($impContactL !== '' ? ' · ' . $impContactL : ''),
			'trnL'  => 'TRN',
			'trn'   => (string) $impTrn,
			'ttl'   => (string) $impBuilt['title'],
			'juris' => $impCountryName,
			'auth'  => (string) $impAuth['name'],
			'law'   => (string) $impAuth['law'],
			'perL'  => (string) $impPerL,
			'perR'  => (string) $impPerL,
			'gen'   => date('d M Y H:i'),
		));
		echo epc_ext_print_fn_js();
		?>
	<?php endif; ?>
	<?php
} elseif ($selRep !== '' && isset($registry[$selRep])) {
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
	// The basis is user-selectable (monthly/quarterly/annual/custom) so monthly
	// VAT filers or non-calendar CT years can pick the right cadence.
	$naturalType = epc_ext_report_period_type((string) $def['cat'], $selRep);
	$periodBases = epc_ext_period_bases($naturalType);
	$selBasis = isset($_GET['basis']) && in_array($_GET['basis'], $periodBases, true) ? (string) $_GET['basis'] : $naturalType;
	$selPeriod = isset($_GET['period']) ? preg_replace('/[^0-9A-Za-z\-]/', '', (string) $_GET['period']) : '';
	$cFrom = isset($_GET['pfrom']) && $_GET['pfrom'] !== '' ? (int) strtotime((string) $_GET['pfrom']) : 0;
	$cTo = isset($_GET['pto']) && $_GET['pto'] !== '' ? (int) strtotime((string) $_GET['pto']) : 0;
	$resolveType = $selBasis === 'custom' ? 'custom' : $selBasis;
	$period = epc_ext_resolve_period($resolveType, $selBasis === 'custom' ? 'custom' : $selPeriod, $date_to ?: time(), array('from' => $cFrom, 'to' => $cTo));
	$periodType = $period['type'];
	$repFrom = $period['from'];
	$repTo = $period['to'];

	$built = epc_ext_report_build($db_link, $selRep, $repCountry, $repFrom, $repTo);
	$fetched = isset($_GET['fetch']);
	$fetchUrl = $repUrl($selRep) . '&rep_country=' . urlencode($repCountry) . '&basis=' . urlencode($selBasis) . '&period=' . urlencode($period['token'])
		. ($selBasis === 'custom' ? '&pfrom=' . urlencode(date('Y-m-d', $repFrom)) . '&pto=' . urlencode(date('Y-m-d', $repTo)) : '') . '&fetch=' . time();
	$periodTypeLabel = array('month' => 'Monthly', 'quarter' => 'Quarterly (tax quarter)', 'year' => 'Annual (financial year)', 'custom' => 'Custom range');
	$basisLabel = array('month' => 'Monthly', 'quarter' => 'Quarterly', 'year' => 'Annual', 'custom' => 'Custom range…');
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
				<?php foreach (array('area' => 'regrep', 'tab' => 'ext_reports', 'from' => $date_from_str, 'to' => $date_to_str, 'cat' => (string) $def['cat'], 'rep' => $selRep, 'rep_country' => $repCountry, 'fetch' => '1') as $k => $v): ?>
					<input type="hidden" name="<?php echo epc_erp_h($k); ?>" value="<?php echo epc_erp_h((string) $v); ?>">
				<?php endforeach; ?>
				<label style="font-size:12px;margin:0 6px;"><i class="fa fa-calendar"></i> Reporting period</label>
				<select name="basis" class="form-control input-sm" title="Filing basis" onchange="this.form.submit()">
					<?php foreach ($periodBases as $b): ?>
						<option value="<?php echo epc_erp_h($b); ?>" <?php echo $b === $selBasis ? 'selected' : ''; ?>><?php echo epc_erp_h($basisLabel[$b] ?? $b); ?></option>
					<?php endforeach; ?>
				</select>
				<?php if ($selBasis === 'custom'): ?>
					<input type="date" name="pfrom" class="form-control input-sm" style="margin:0 4px;" value="<?php echo epc_erp_h(date('Y-m-d', $repFrom)); ?>" title="Period from">
					<input type="date" name="pto" class="form-control input-sm" style="margin:0 4px;" value="<?php echo epc_erp_h(date('Y-m-d', $repTo)); ?>" title="Period to">
				<?php else: ?>
					<select name="period" class="form-control input-sm" style="margin-left:4px;" onchange="this.form.submit()">
						<?php foreach ($period['options'] as $tok => $lbl): ?>
							<option value="<?php echo epc_erp_h((string) $tok); ?>" <?php echo $tok === $period['token'] ? 'selected' : ''; ?>><?php echo epc_erp_h((string) $lbl); ?></option>
						<?php endforeach; ?>
					</select>
				<?php endif; ?>
				<button type="submit" class="btn btn-success btn-sm" style="margin-left:6px;" title="Recalculate the return for the selected period"><i class="fa fa-calculator"></i> Run / Recalculate</button>
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

	<?php
	echo epc_ext_print_ctx_js(array(
		'co'    => (string) ($co['legal_name'] ?: 'Company'),
		'addr'  => (string) ($co['address'] ?? ''),
		'trnL'  => (string) ($co['tax_label'] ?? 'TRN'),
		'trn'   => (string) ($co['trn'] ?? ''),
		'ttl'   => (string) $built['title'],
		'juris' => (string) $repCountryName,
		'auth'  => (string) $auth['name'],
		'law'   => (string) $auth['law'],
		'perL'  => (string) $period['label'],
		'perR'  => date('d M Y', $repFrom) . '  —  ' . date('d M Y', $repTo),
		'gen'   => date('d M Y H:i'),
	));
	echo epc_ext_print_fn_js();
	?>
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
	<div class="epc-erp-section" style="margin-bottom:14px;background:#f5f8ff;border:1px solid #d6e4ff;border-radius:8px;display:flex;flex-wrap:wrap;gap:12px;align-items:center;justify-content:space-between;">
		<div>
			<div style="font-weight:700;color:#1d2740;"><i class="fa fa-upload"></i> Import from Excel → VAT / CT return (off-system)</div>
			<div class="text-muted" style="font-size:12px;margin-top:4px;">Build a VAT 201 or Corporate Tax return from an uploaded spreadsheet (summary figures only) — for checking / reporting other clients, outside your ERP data.</div>
		</div>
		<a href="<?php echo epc_erp_h($baseUrl . $sep . 'tool=import'); ?>" class="btn btn-primary btn-sm"><i class="fa fa-cogs"></i> Open import tool</a>
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

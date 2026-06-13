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
} elseif ($selTool === 'intake') {
	// ----------------------------- Guided IFRS intake: upload prior PDF → system
	// review → system-generated Trial Balance / data request → compliant report.
	// Off-system: the PDF is parsed in-memory (never stored); the scanned figures
	// flow forward as form fields. Country/law resolve from the registered tenant.
	$inCcy      = (string) ($regProf['currency'] ?? 'AED');
	$inCountry  = $regCountry;
	$inStage    = preg_replace('/[^a-z]/', '', (string) ($_POST['intake_stage'] ?? ''));
	$inTargetY  = (int) ($_POST['intake_target_y'] ?? (int) date('Y'));
	if ($inTargetY < 2000 || $inTargetY > 2100) { $inTargetY = (int) date('Y'); }
	$inPriorY   = $inTargetY - 1;
	$inEntity   = trim((string) ($_POST['intake_entity'] ?? (string) ($erpCo['legal_name'] ?? '')));
	$inUnitsRaw = trim((string) ($_POST['intake_units'] ?? ''));
	$inUnits    = array();
	foreach (explode(',', $inUnitsRaw) as $u) { $u = trim($u); if ($u !== '') { $inUnits[] = $u; } }
	$multiUnit  = count($inUnits) > 1;
	$inUnitBreak = array();
	$inErr      = '';
	$inNotice   = '';
	$scan       = array('figures' => array(), 'matched' => array(), 'found' => 0);
	$inHistory  = array();
	$inYears    = array();
	$inSources  = array();
	$review     = null;
	$reqRows    = array();
	$inBuilt    = null;
	$inExtras   = array();
	$inLegal    = epc_ext_intake_legal($inCountry);
	$nclean = static function ($v): float {
		$v = trim((string) $v);
		$neg = (strpos($v, '(') !== false);
		$v = str_replace(array(',', ' ', "\xC2\xA0", '(', ')'), '', $v);
		return is_numeric($v) ? ($neg ? -1 : 1) * (float) $v : 0.0;
	};

	if ($inStage === 'review' && $_SERVER['REQUEST_METHOD'] === 'POST') {
		// Accept one or several prior-year reports (e.g. 2024 + 2025) so the
		// system studies the latest reports together and builds a multi-year
		// history. The two most recent years become the new report's
		// comparative + prior-comparative.
		$pdfFiles = array();
		if (isset($_FILES['intake_pdf']) && is_array($_FILES['intake_pdf']['name'] ?? null)) {
			$n = count($_FILES['intake_pdf']['name']);
			for ($fi = 0; $fi < $n; $fi++) {
				if ((int) ($_FILES['intake_pdf']['error'][$fi] ?? UPLOAD_ERR_NO_FILE) !== UPLOAD_ERR_OK) { continue; }
				$pdfFiles[] = array('tmp' => (string) $_FILES['intake_pdf']['tmp_name'][$fi], 'name' => (string) $_FILES['intake_pdf']['name'][$fi]);
			}
		} elseif (isset($_FILES['intake_pdf']) && ((int) ($_FILES['intake_pdf']['error'] ?? UPLOAD_ERR_NO_FILE) === UPLOAD_ERR_OK)) {
			$pdfFiles[] = array('tmp' => (string) $_FILES['intake_pdf']['tmp_name'], 'name' => (string) $_FILES['intake_pdf']['name']);
		}
		if (count($pdfFiles) > 0) {
			$scansForMerge = array();
			foreach ($pdfFiles as $pf) {
				$text = epc_ext_pdf_to_text($pf['tmp']);
				$one  = epc_ext_pdf_scan($text);
				$yr   = epc_ext_pdf_year($text);
				if ($yr <= 0) { $yr = $inPriorY; }
				$scansForMerge[] = array('year' => $yr, 'scan' => $one);
				$inSources[] = array('name' => $pf['name'], 'year' => $yr, 'found' => (int) $one['found'], 'consolidated' => !empty($one['consolidated']), 'combined' => !empty($one['combined']));
			}
			$merged = epc_ext_intake_merge($scansForMerge);
			$scan = array(
				'figures' => $merged['figures'], 'matched' => array(), 'found' => (int) $merged['found'],
				'consolidated' => $merged['consolidated'], 'combined' => $merged['combined'], 'prior_auditor' => $merged['prior_auditor'],
			);
			$inHistory = $merged['history'];
			$inYears   = $merged['years'];
			if ($merged['found'] === 0) {
				$inNotice = 'The system could not auto-read figures from the uploaded PDF(s) (they may be scanned images or an unusual layout). No problem — the data-request form below is ready for you to complete manually.';
			} else {
				$yrTxt = count($inYears) > 1 ? (' across ' . count($inYears) . ' year(s) (' . implode(', ', array_map('intval', $inYears)) . ')') : '';
				$inNotice = 'The system studied ' . count($pdfFiles) . ' report(s) and read ' . (int) $merged['found'] . ' line item(s)' . $yrTxt . '. The latest year (FY' . (int) $merged['latest'] . ') is used as your comparative; confirm/adjust on the request form below.';
			}
		} else {
			$inNotice = 'No PDF was attached — continuing with a blank data-request form for manual entry.';
		}
		$review  = epc_ext_intake_review($scan['figures'], $inCountry);
		$reqRows = epc_ext_intake_request_rows($scan['figures']);
	} elseif ($inStage === 'build' && $_SERVER['REQUEST_METHOD'] === 'POST') {
		$curIn  = (array) ($_POST['cur'] ?? array());
		$priIn  = (array) ($_POST['pri'] ?? array());
		$unitIn = (array) ($_POST['unit'] ?? array());
		$elimIn = (array) ($_POST['elim'] ?? array());
		$fin = array();
		foreach (epc_ext_fin_line_spec() as $s) {
			$code = $s['code'];
			if ($multiUnit) {
				// New-period TB is entered per business unit/division across columns;
				// the system consolidates (sum of units + eliminations) to the report figure.
				$perUnit = array();
				$sum = 0.0;
				foreach ($inUnits as $ui => $uname) {
					$val = $nclean((string) ($unitIn[$code][$ui] ?? '0'));
					$perUnit[$ui] = $val;
					$sum += $val;
				}
				$elim = $nclean((string) ($elimIn[$code] ?? '0'));
				$cur = $sum + $elim;
				$inUnitBreak[$code] = array('units' => $perUnit, 'elim' => $elim, 'total' => $cur);
			} else {
				$cur = $nclean((string) ($curIn[$code] ?? '0'));
			}
			$fin[$code] = array('cur' => $cur, 'pri' => $nclean((string) ($priIn[$code] ?? '0')));
		}
		$exLbl = (array) ($_POST['extra_label'] ?? array());
		$exCur = (array) ($_POST['extra_cur'] ?? array());
		$exPri = (array) ($_POST['extra_pri'] ?? array());
		foreach ($exLbl as $i => $lbl) {
			$lbl = trim((string) $lbl);
			if ($lbl === '') { continue; }
			$inExtras[] = array('label' => $lbl, 'cur' => $nclean((string) ($exCur[$i] ?? '0')), 'pri' => $nclean((string) ($exPri[$i] ?? '0')));
		}
		$map = array(
			'meta' => array(
				'META_LEGAL_NAME'   => $inEntity !== '' ? $inEntity : 'Client entity',
				'META_CURRENCY'     => $inCcy,
				'META_PERIOD_FROM'  => $inTargetY . '-01-01',
				'META_PERIOD_TO'    => $inTargetY . '-12-31',
				'META_AUDITOR_AUTH' => (string) ($inLegal['Authority'] ?? ''),
			),
			'fin' => $fin, 'values' => array(), 'vat' => array(),
		);
		$inBuilt = epc_ext_b_fin_summary($map, $inCcy);
	}
	$inBase = $baseUrl . $sep . 'tool=intake';
	// step number for the indicator
	$inStep = ($inBuilt !== null) ? 4 : (($review !== null) ? 2 : 1);
	$stepName = array(1 => 'Upload prior financials', 2 => 'System review & data request', 3 => 'Provide new-year data', 4 => 'Compliant report');
	?>
	<p style="margin-bottom:10px;"><a href="<?php echo epc_erp_h($baseUrl); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> All categories</a></p>
	<div class="epc-erp-section" style="margin-bottom:14px;">
		<h3 style="margin-top:0;color:#b3122a;"><i class="fa fa-magic"></i> Guided IFRS report builder — upload your accounts, the system tells you what it needs</h3>
		<p class="text-muted" style="font-size:12.5px;max-width:980px;">
			A multi-step engagement: <strong>upload your latest financials (PDF)</strong> → the system <strong>reviews them against IFRS and your country's legal requirements</strong> →
			it then <strong>requests exactly the Trial Balance / data it needs</strong> to build the new period's report (prior figures pre-filled from your upload) →
			it <strong>generates a fully IFRS-compliant report</strong>, even if the uploaded accounts weren't. Off-system: your PDF is read in memory and not stored.
			Country &amp; law resolve from your registration: <strong><?php echo epc_erp_h($regName . ' (' . $regCountry . ')'); ?></strong>.
		</p>
		<div style="display:flex;flex-wrap:wrap;gap:8px;margin-top:8px;">
			<?php foreach (array(1, 2, 3, 4) as $sn): ?>
				<span class="label" style="font-size:12px;padding:6px 10px;<?php echo $sn <= $inStep ? 'background:#b3122a;color:#fff;' : 'background:#f1e3e6;color:#7a0c1c;'; ?>">
					<?php echo $sn; ?> · <?php echo epc_erp_h($stepName[$sn]); ?>
				</span>
			<?php endforeach; ?>
		</div>
	</div>

	<?php if ($inErr !== ''): ?>
		<div class="alert alert-danger"><i class="fa fa-exclamation-triangle"></i> <?php echo epc_erp_h($inErr); ?></div>
	<?php endif; ?>

	<?php if ($inBuilt === null && $review === null): // -------- Step 1: upload ?>
		<div class="epc-erp-section" style="margin-bottom:14px;">
			<form method="post" enctype="multipart/form-data" action="<?php echo epc_erp_h($inBase); ?>">
				<input type="hidden" name="intake_stage" value="review">
				<div style="display:flex;flex-wrap:wrap;gap:18px;">
					<div style="flex:1;min-width:300px;border:1px solid #e2e6ee;border-radius:6px;padding:14px;">
						<div style="font-weight:700;color:#b3122a;margin-bottom:8px;">1 · Your latest signed financials (PDF)</div>
						<p class="text-muted" style="font-size:12px;">Attach the most recent accounts (e.g. <strong>FY<?php echo (int) $inPriorY; ?></strong> with its comparative). You can attach <strong>several prior years at once</strong> (e.g. FY<?php echo (int) ($inPriorY - 1); ?> + FY<?php echo (int) $inPriorY; ?>) — the system studies them together, builds a multi-year history and uses the latest year as your comparative.</p>
						<input type="file" name="intake_pdf[]" accept="application/pdf,.pdf" class="form-control input-sm" multiple required>
						<div class="text-muted" style="font-size:11px;margin-top:3px;">Tip: select multiple PDFs (hold Ctrl/Cmd) to give the system the full history.</div>
					</div>
					<div style="flex:1;min-width:300px;border:1px solid #e2e6ee;border-radius:6px;padding:14px;">
						<div style="font-weight:700;color:#b3122a;margin-bottom:8px;">2 · The report you need</div>
						<label style="font-size:12px;display:block;margin-bottom:4px;">Entity legal name</label>
						<input type="text" name="intake_entity" value="<?php echo epc_erp_h($inEntity); ?>" class="form-control input-sm" style="margin-bottom:8px;" placeholder="e.g. Acme Trading LLC">
						<label style="font-size:12px;display:block;margin-bottom:4px;">Target reporting year</label>
						<select name="intake_target_y" class="form-control input-sm">
							<?php for ($yy = (int) date('Y') + 1; $yy >= (int) date('Y') - 4; $yy--): ?>
								<option value="<?php echo $yy; ?>" <?php echo $yy === $inTargetY ? 'selected' : ''; ?>>FY<?php echo $yy; ?> (comparative FY<?php echo $yy - 1; ?>)</option>
							<?php endfor; ?>
						</select>
						<label style="font-size:12px;display:block;margin:8px 0 4px;">Business units / divisions <span class="text-muted">(optional — comma-separated)</span></label>
						<input type="text" name="intake_units" value="<?php echo epc_erp_h($inUnitsRaw); ?>" class="form-control input-sm" placeholder="e.g. Trading, Contracting, Real Estate">
						<div class="text-muted" style="font-size:11px;margin-top:3px;">If your new trial balance is unit-wise, list the units — the request form gives you a column per unit and the system consolidates them (with an eliminations column) to the report figure.</div>
					</div>
				</div>
				<button type="submit" class="btn btn-primary btn-sm" style="margin-top:12px;background:#b3122a;border-color:#7a0c1c;"><i class="fa fa-search"></i> Upload &amp; review against IFRS</button>
			</form>
		</div>

	<?php elseif ($review !== null): // -------- Step 2 + 3: review + data request ?>
		<?php if ($inNotice !== ''): ?>
			<div class="alert <?php echo $scan['found'] > 0 ? 'alert-success' : 'alert-warning'; ?>" style="font-size:12.5px;"><i class="fa fa-info-circle"></i> <?php echo epc_erp_h($inNotice); ?></div>
		<?php endif; ?>
		<?php if (!empty($scan['consolidated']) || !empty($scan['combined']) || !empty($scan['prior_auditor'])): ?>
			<div class="alert alert-info" style="font-size:12.5px;">
				<i class="fa fa-sitemap"></i>
				<?php if (!empty($scan['consolidated'])): ?><span class="label label-primary" style="font-size:10px;">Consolidated report</span> <?php endif; ?>
				<?php if (!empty($scan['combined'])): ?><span class="label label-primary" style="font-size:10px;">Combined report</span> <?php endif; ?>
				The system detected a group report and read the <strong>consolidated/group column</strong> — these become your new report's comparatives.
				<?php if (!empty($scan['prior_auditor'])): ?> Prior auditor detected: <strong><?php echo epc_erp_h($scan['prior_auditor']); ?></strong>.<?php endif; ?>
				If the uploaded report had per-entity columns, confirm the figures below are the consolidated/group ones.
			</div>
		<?php endif; ?>
		<?php if (count($inSources) > 1 || count($inYears) > 1): ?>
			<div class="epc-erp-section" style="margin-bottom:14px;">
				<h4 style="margin-top:0;color:#b3122a;"><i class="fa fa-history"></i> Multi-year study — the system read your prior reports together</h4>
				<?php if (!empty($inSources)): ?>
					<p class="text-muted" style="font-size:12px;margin-bottom:6px;">Reports studied:
						<?php $sp = array(); foreach ($inSources as $sc) { $sp[] = epc_erp_h($sc['name']) . ' → FY' . (int) $sc['year'] . ' (' . (int) $sc['found'] . ' lines' . (!empty($sc['consolidated']) ? ', consolidated' : '') . ')'; } echo implode(' · ', $sp); ?>.
					</p>
				<?php endif; ?>
				<?php
					$histYears = $inYears; // already sorted desc
					$histYears = array_slice($histYears, 0, 4);
				?>
				<table class="table table-condensed" style="font-size:12px;">
					<thead><tr style="background:#7a0c1c;color:#fff;"><th>Line item</th><th>Std</th>
						<?php foreach ($histYears as $hy): ?><th style="text-align:right;">FY<?php echo (int) $hy; ?></th><?php endforeach; ?>
						<?php if (count($histYears) >= 2): ?><th style="text-align:right;">YoY %</th><?php endif; ?>
					</tr></thead>
					<tbody>
					<?php foreach (epc_ext_fin_line_spec() as $sp):
						$code = $sp['code'];
						if (empty($inHistory[$code])) { continue; }
						$y0 = $histYears[0] ?? 0; $y1 = $histYears[1] ?? 0;
						$v0 = isset($inHistory[$code][$y0]) ? (float) $inHistory[$code][$y0] : 0.0;
						$v1 = isset($inHistory[$code][$y1]) ? (float) $inHistory[$code][$y1] : 0.0;
						$yoy = (abs($v1) > 0.0001) ? (($v0 - $v1) / abs($v1)) * 100 : null;
					?>
						<tr>
							<td><?php echo epc_erp_h($sp['label']); ?></td>
							<td><span class="label label-info" style="font-size:9.5px;"><?php echo epc_erp_h($sp['std']); ?></span></td>
							<?php foreach ($histYears as $hy): ?>
								<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo isset($inHistory[$code][$hy]) ? epc_erp_h(epc_ext_m((float) $inHistory[$code][$hy], $inCcy)) : '—'; ?></td>
							<?php endforeach; ?>
							<?php if (count($histYears) >= 2): ?>
								<td style="text-align:right;font-variant-numeric:tabular-nums;color:<?php echo $yoy === null ? '#888' : ($yoy >= 0 ? '#1a7f37' : '#b3122a'); ?>;"><?php echo $yoy === null ? '—' : (($yoy >= 0 ? '+' : '') . number_format($yoy, 1) . '%'); ?></td>
							<?php endif; ?>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<div class="text-muted" style="font-size:11px;">The latest year becomes your FY<?php echo (int) $inTargetY; ?> comparative; the year-on-year movement feeds the financial-analysis commentary in the generated report.</div>
			</div>
		<?php endif; ?>
		<div class="epc-erp-section" style="margin-bottom:14px;">
			<h4 style="margin-top:0;color:#b3122a;"><i class="fa fa-check-square-o"></i> System review of your uploaded report</h4>
			<div style="display:flex;flex-wrap:wrap;gap:14px;margin:8px 0;">
				<div style="flex:1;min-width:180px;border-left:4px solid #b3122a;background:#fdeef0;padding:10px;border-radius:4px;">
					<div style="font-size:11px;color:#7a0c1c;">Line items detected</div>
					<div style="font-size:20px;font-weight:800;color:#b3122a;"><?php echo (int) $review['present']; ?> / <?php echo (int) $review['total']; ?></div>
				</div>
				<div style="flex:1;min-width:180px;border-left:4px solid <?php echo $review['balance']['diff'] ? '#9a6700' : '#1a7f37'; ?>;background:#f7faff;padding:10px;border-radius:4px;">
					<div style="font-size:11px;color:#555;">Position balances (A = E + L)</div>
					<div style="font-size:16px;font-weight:800;color:<?php echo $review['balance']['diff'] ? '#9a6700' : '#1a7f37'; ?>;">
						<?php echo $review['balance']['diff'] ? 'Review needed' : 'Balanced'; ?>
					</div>
					<div style="font-size:10px;color:#888;">Assets <?php echo epc_erp_h(epc_ext_m($review['balance']['assets'], $inCcy)); ?> · E+L <?php echo epc_erp_h(epc_ext_m($review['balance']['eqliab'], $inCcy)); ?></div>
				</div>
				<div style="flex:1;min-width:180px;border-left:4px solid #1d4e94;background:#e8f0fb;padding:10px;border-radius:4px;">
					<div style="font-size:11px;color:#1d4e94;">Standards seen / to collect</div>
					<div style="font-size:16px;font-weight:800;color:#1d4e94;"><?php echo count($review['standards']); ?> seen · <?php echo count($review['missing_std']); ?> to collect</div>
				</div>
			</div>
			<table class="table table-condensed" style="font-size:12px;">
				<thead><tr style="background:#7a0c1c;color:#fff;"><th>Line item</th><th>Group</th><th>Standard</th><th style="text-align:right;">Detected (FY<?php echo (int) $inPriorY; ?>)</th><th>Status</th></tr></thead>
				<tbody>
				<?php foreach ($review['lines'] as $ln): ?>
					<tr>
						<td><?php echo epc_erp_h($ln['label']); ?>
							<?php if (!empty($scan['matched'][$ln['code']])): ?><div class="text-muted" style="font-size:9.5px;">read: <?php echo epc_erp_h(mb_strimwidth($scan['matched'][$ln['code']], 0, 80, '…')); ?></div><?php endif; ?>
						</td>
						<td><?php echo epc_erp_h($ln['group']); ?></td>
						<td><span class="label label-info" style="font-size:10px;"><?php echo epc_erp_h($ln['std']); ?></span></td>
						<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo $ln['status'] === 'found' ? epc_erp_h(epc_ext_m($ln['value'], $inCcy)) : '—'; ?></td>
						<td><?php echo $ln['status'] === 'found' ? '<span class="label label-success" style="font-size:10px;">Found</span>' : '<span class="label label-warning" style="font-size:10px;">System will request</span>'; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<div style="margin-top:8px;font-size:12px;background:#f7faff;border:1px solid #e2e6ee;border-radius:5px;padding:10px;">
				<strong style="color:#7a0c1c;">Legal / framework requirements applied (from your registration country):</strong>
				<ul style="margin:6px 0 0;padding-left:18px;">
					<?php foreach ($inLegal as $k => $v): ?><li><strong><?php echo epc_erp_h($k); ?>:</strong> <?php echo epc_erp_h($v); ?></li><?php endforeach; ?>
				</ul>
			</div>
		</div>

		<div class="epc-erp-section" style="margin-bottom:14px;">
			<h4 style="margin-top:0;color:#b3122a;"><i class="fa fa-list-alt"></i> The data the system needs to build your FY<?php echo (int) $inTargetY; ?> report</h4>
			<p class="text-muted" style="font-size:12px;">This is the system's <strong>Trial Balance request</strong> — only the lines the IFRS report needs. The <strong>FY<?php echo (int) $inPriorY; ?> (comparative)</strong> column is pre-filled from your uploaded report (edit if needed); enter the <strong>FY<?php echo (int) $inTargetY; ?></strong> figures. Add any extra accounts your new report has at the bottom — the system will analyse and include them.</p>
			<?php if ($multiUnit): ?>
				<div class="alert alert-info" style="font-size:12px;"><i class="fa fa-sitemap"></i> Multi-unit mode: enter each line per business unit (<strong><?php echo epc_erp_h(implode(' · ', $inUnits)); ?></strong>). Use the <strong>Eliminations</strong> column for inter-unit balances (enter negatives). The <strong>Consolidated</strong> column totals live and is what drives the report.</div>
			<?php endif; ?>
			<form method="post" action="<?php echo epc_erp_h($inBase); ?>">
				<input type="hidden" name="intake_stage" value="build">
				<input type="hidden" name="intake_target_y" value="<?php echo (int) $inTargetY; ?>">
				<input type="hidden" name="intake_entity" value="<?php echo epc_erp_h($inEntity); ?>">
				<input type="hidden" name="intake_units" value="<?php echo epc_erp_h($inUnitsRaw); ?>">
				<?php $grp = ''; $grpOpen = false; foreach ($reqRows as $r): ?>
					<?php if ($r['group'] !== $grp): if ($grpOpen) { echo '</tbody></table>'; } $grp = $r['group']; $grpOpen = true; ?>
						<div style="font-weight:700;color:#7a0c1c;background:#fdeef0;padding:5px 10px;border-radius:3px;margin:10px 0 4px;"><?php echo epc_erp_h($grp); ?></div>
						<table class="table table-condensed" style="font-size:12px;margin-bottom:0;"><thead><tr style="background:#f1e3e6;">
							<th>Line item</th><th>Std</th>
							<?php if ($multiUnit): ?>
								<?php foreach ($inUnits as $uname): ?><th style="text-align:right;">FY<?php echo (int) $inTargetY; ?> · <?php echo epc_erp_h($uname); ?></th><?php endforeach; ?>
								<th style="text-align:right;">Eliminations</th>
								<th style="text-align:right;background:#fdeef0;">FY<?php echo (int) $inTargetY; ?> consolidated</th>
							<?php else: ?>
								<th style="text-align:right;">FY<?php echo (int) $inTargetY; ?> (new)</th>
							<?php endif; ?>
							<th style="text-align:right;">FY<?php echo (int) $inPriorY; ?> (comparative)</th>
						</tr></thead><tbody>
					<?php endif; ?>
						<tr data-code="<?php echo epc_erp_h($r['code']); ?>">
							<td><?php echo epc_erp_h($r['label']); ?></td>
							<td><span class="label label-info" style="font-size:10px;"><?php echo epc_erp_h($r['std']); ?></span></td>
							<?php if ($multiUnit): ?>
								<?php foreach ($inUnits as $ui => $uname): ?>
									<td style="text-align:right;"><input type="text" name="unit[<?php echo epc_erp_h($r['code']); ?>][<?php echo (int) $ui; ?>]" class="form-control input-sm epc-u" style="text-align:right;" placeholder="0.00" oninput="epcRowTotal(this)"></td>
								<?php endforeach; ?>
								<td style="text-align:right;"><input type="text" name="elim[<?php echo epc_erp_h($r['code']); ?>]" class="form-control input-sm epc-u" style="text-align:right;" placeholder="0.00" oninput="epcRowTotal(this)"></td>
								<td style="text-align:right;font-weight:700;background:#f4fbf6;font-variant-numeric:tabular-nums;"><span class="epc-tot">0.00</span></td>
							<?php else: ?>
								<td style="text-align:right;"><input type="text" name="cur[<?php echo epc_erp_h($r['code']); ?>]" class="form-control input-sm" style="text-align:right;" placeholder="0.00"></td>
							<?php endif; ?>
							<td style="text-align:right;"><input type="text" name="pri[<?php echo epc_erp_h($r['code']); ?>]" class="form-control input-sm" style="text-align:right;<?php echo $r['prefilled'] ? 'background:#f4fbf6;' : ''; ?>" value="<?php echo $r['prefilled'] ? epc_erp_h(number_format($r['prior'], 2, '.', '')) : ''; ?>"></td>
						</tr>
				<?php endforeach; ?>
				<?php if ($grpOpen) { echo '</tbody></table>'; } ?>
				<?php if ($multiUnit): ?>
				<script>
				function epcRowTotal(el){var tr=el.closest('tr');if(!tr)return;var ins=tr.querySelectorAll('input.epc-u');var s=0;for(var i=0;i<ins.length;i++){var raw=(ins[i].value||'').trim();var neg=/^\(/.test(raw);var v=parseFloat(raw.replace(/[(),\s]/g,''))||0;if(neg)v=-Math.abs(v);s+=v;}var t=tr.querySelector('.epc-tot');if(t)t.textContent=s.toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2});}
				</script>
				<?php endif; ?>
				<div style="font-weight:700;color:#7a0c1c;background:#fdeef0;padding:5px 10px;border-radius:3px;margin:12px 0 4px;">Extra accounts in the new report (optional)</div>
				<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f1e3e6;"><th style="width:46%;">Account / line description</th><th style="width:14%;">&nbsp;</th><th style="width:20%;text-align:right;">FY<?php echo (int) $inTargetY; ?></th><th style="width:20%;text-align:right;">FY<?php echo (int) $inPriorY; ?></th></tr></thead><tbody>
					<?php for ($x = 0; $x < 4; $x++): ?>
						<tr>
							<td><input type="text" name="extra_label[]" class="form-control input-sm" placeholder="e.g. Investment property"></td>
							<td class="text-muted" style="font-size:10px;">analysed by system</td>
							<td style="text-align:right;"><input type="text" name="extra_cur[]" class="form-control input-sm" style="text-align:right;" placeholder="0.00"></td>
							<td style="text-align:right;"><input type="text" name="extra_pri[]" class="form-control input-sm" style="text-align:right;" placeholder="0.00"></td>
						</tr>
					<?php endfor; ?>
				</tbody></table>
				<button type="submit" class="btn btn-primary btn-sm" style="margin-top:10px;background:#b3122a;border-color:#7a0c1c;"><i class="fa fa-cogs"></i> Generate IFRS-compliant FY<?php echo (int) $inTargetY; ?> report</button>
				<a href="<?php echo epc_erp_h($inBase); ?>" class="btn btn-default btn-sm" style="margin-top:10px;">Start over</a>
			</form>
		</div>

	<?php else: // -------- Step 4: generated report ?>
		<?php
		$inMeta   = $inBuilt['meta'] ?? array();
		$inCo     = $inMeta['META_LEGAL_NAME'] ?? 'Client entity';
		$inPerL   = 'FY' . (int) $inTargetY . '  (FY' . (int) $inPriorY . ' comparative)';
		$inDocName = preg_replace('/[^A-Za-z0-9]+/', '_', (string) $inCo) . '_IFRS_FY' . (int) $inTargetY;
		?>
		<div class="epc-erp-section" style="margin-bottom:14px;">
			<div style="display:flex;flex-wrap:wrap;gap:10px;align-items:center;">
				<button type="button" class="btn btn-default btn-sm" onclick="epcExtPrint();"><i class="fa fa-file-pdf-o"></i> Download PDF</button>
				<button type="button" class="btn btn-default btn-sm" onclick="epcExtWord('<?php echo epc_erp_h($inDocName); ?>');"><i class="fa fa-file-word-o"></i> Download Word</button>
				<a href="<?php echo epc_erp_h($inBase); ?>" class="btn btn-default btn-sm"><i class="fa fa-refresh"></i> New engagement</a>
				<span class="label label-success" style="font-size:11px;"><i class="fa fa-magic"></i> Built from guided intake · IFRS-compliant</span>
			</div>
		</div>
		<?php if (!empty($inExtras)): ?>
			<div class="alert alert-info" style="font-size:12.5px;">
				<strong><i class="fa fa-plus-circle"></i> Extra accounts analysed:</strong>
				<?php $ex = array(); foreach ($inExtras as $e) { $ex[] = epc_erp_h($e['label']) . ' (' . epc_erp_h(epc_ext_m($e['cur'], $inCcy)) . ')'; } echo implode(' · ', $ex); ?>.
				These were captured outside the standard chart — review whether each needs its own IFRS line/note in the final pack.
			</div>
		<?php endif; ?>
		<?php if ($multiUnit && !empty($inUnitBreak)):
			$assetCodes = array();
			foreach (epc_ext_fin_line_spec() as $sg) { if ($sg['group'] === 'Assets') { $assetCodes[] = $sg['code']; } }
			$rev = isset($inUnitBreak['FIN_REVENUE']) ? $inUnitBreak['FIN_REVENUE'] : array('units' => array(), 'elim' => 0.0, 'total' => 0.0);
			$assetUnit = array(); $assetElim = 0.0; $assetTot = 0.0;
			foreach ($assetCodes as $ac) { if (!isset($inUnitBreak[$ac])) { continue; } foreach ($inUnitBreak[$ac]['units'] as $ui => $v) { $assetUnit[$ui] = ($assetUnit[$ui] ?? 0.0) + $v; } $assetElim += $inUnitBreak[$ac]['elim']; $assetTot += $inUnitBreak[$ac]['total']; }
		?>
			<div class="epc-erp-section" style="margin-bottom:14px;">
				<h4 style="margin-top:0;color:#b3122a;"><i class="fa fa-sitemap"></i> Business-unit / segment breakdown (IFRS 8) — consolidated for the report</h4>
				<p class="text-muted" style="font-size:12px;">Your unit-wise trial balance was consolidated (units + eliminations) into the figures driving the report. Segment summary:</p>
				<table class="table table-condensed" style="font-size:12px;">
					<thead><tr style="background:#7a0c1c;color:#fff;"><th>Segment / unit</th><th style="text-align:right;">Revenue (IFRS 15)</th><th style="text-align:right;">Total assets</th></tr></thead>
					<tbody>
					<?php foreach ($inUnits as $ui => $uname): ?>
						<tr><td><?php echo epc_erp_h($uname); ?></td>
							<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo epc_erp_h(epc_ext_m((float) ($rev['units'][$ui] ?? 0.0), $inCcy)); ?></td>
							<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo epc_erp_h(epc_ext_m((float) ($assetUnit[$ui] ?? 0.0), $inCcy)); ?></td>
						</tr>
					<?php endforeach; ?>
						<tr style="color:#9a6700;"><td>Eliminations</td>
							<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo epc_erp_h(epc_ext_m((float) $rev['elim'], $inCcy)); ?></td>
							<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo epc_erp_h(epc_ext_m($assetElim, $inCcy)); ?></td>
						</tr>
						<tr style="font-weight:800;background:#fdeef0;color:#7a0c1c;"><td>Consolidated</td>
							<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo epc_erp_h(epc_ext_m((float) $rev['total'], $inCcy)); ?></td>
							<td style="text-align:right;font-variant-numeric:tabular-nums;"><?php echo epc_erp_h(epc_ext_m($assetTot, $inCcy)); ?></td>
						</tr>
					</tbody>
				</table>
			</div>
		<?php endif; ?>
		<div id="epc_ext_doc" class="epc-erp-section" style="background:#fff;border:1px solid #e2e6ee;padding:26px;">
			<div style="display:flex;justify-content:space-between;border-bottom:3px solid #b3122a;padding-bottom:12px;margin-bottom:16px;">
				<div>
					<div style="font-size:20px;font-weight:800;color:#7a0c1c;"><?php echo epc_erp_h((string) $inCo); ?></div>
					<div class="text-muted" style="font-size:12px;"><?php echo epc_erp_h($regName); ?> · <?php echo epc_erp_h($inPerL); ?></div>
				</div>
				<div style="text-align:right;font-size:12px;color:#7a0c1c;"><strong><?php echo epc_erp_h((string) $inBuilt['title']); ?></strong><br><?php echo date('d M Y H:i'); ?></div>
			</div>
			<?php echo $inBuilt['body']; ?>
			<p class="text-muted" style="font-size:11px;margin-top:18px;border-top:1px dashed #ccc;padding-top:8px;">
				Built by Ecom BOS guided IFRS intake on <?php echo date('d M Y H:i'); ?> from your uploaded prior accounts + the data you provided (off-system). Verify figures against the official source before filing.
			</p>
		</div>
		<?php
		echo epc_ext_print_ctx_js(array(
			'co'    => (string) $inCo,
			'addr'  => $regName,
			'trnL'  => 'TRN',
			'trn'   => (string) ($inMeta['META_TRN'] ?? ''),
			'ttl'   => (string) $inBuilt['title'],
			'juris' => $regName,
			'auth'  => (string) ($inLegal['Authority'] ?? ''),
			'law'   => (string) ($inLegal['Companies law'] ?? ''),
			'perL'  => $inPerL,
			'perR'  => $inPerL,
			'gen'   => date('d M Y H:i'),
			'theme' => 'red',
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
			<button type="button" class="btn btn-default btn-sm" onclick="epcExtPrint();"><i class="fa fa-file-pdf-o"></i> Download PDF</button>
			<?php $docName = preg_replace('/[^A-Za-z0-9]+/', '_', (string) $def['name']) . '_FY' . date('Y', $repFrom); ?>
			<button type="button" class="btn btn-default btn-sm" onclick="epcExtWord('<?php echo epc_erp_h($docName); ?>');" title="Download this report as an editable Microsoft Word document (same layout, theme and tables)"><i class="fa fa-file-word-o"></i> Download Word</button>
			<?php
			$isFinModelRep = in_array($selRep, array('fin__financial_model_forecast', 'fin__business_valuation_report'), true);
			if ($isFinModelRep):
				$finXlsxCcy = (string) ($regProf['currency'] ?? 'AED');
				$finXlsxName = ($selRep === 'fin__business_valuation_report' ? 'Business_Valuation_Model' : 'Financial_Model') . '_FY' . date('Y', $repFrom) . '.xlsx';
			?>
				<button type="button" class="btn btn-success btn-sm" onclick="epcDlB64('epcFinModelX', '<?php echo epc_erp_h($finXlsxName); ?>')" title="Download a linked Excel workbook — Assumptions / Calculations / Results with live formulas"><i class="fa fa-file-excel-o"></i> Download Excel (linked model)</button>
				<textarea id="epcFinModelX" style="display:none;"><?php echo base64_encode(epc_ext_finmodel_xlsx($db_link, $finXlsxCcy, $repFrom, $repTo)); ?></textarea>
				<script>
				if (typeof epcDlB64 !== 'function') {
					function epcDlB64(id,fn){var el=document.getElementById(id);if(!el)return;var b64=(el.value!==undefined?el.value:el.textContent).replace(/\s+/g,'');var bin=atob(b64);var len=bin.length;var bytes=new Uint8Array(len);for(var i=0;i<len;i++){bytes[i]=bin.charCodeAt(i);}var blob=new Blob([bytes],{type:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"});var url=URL.createObjectURL(blob);var a=document.createElement("a");a.href=url;a.download=fn;document.body.appendChild(a);a.click();document.body.removeChild(a);URL.revokeObjectURL(url);}
				}
				</script>
			<?php endif; ?>
			<?php if ($selRep === 'audit__external_audit_report' && class_exists('ZipArchive')):
				$audXlsxCcy = (string) ($regProf['currency'] ?? 'AED');
				$audXlsxName = 'External_Audit_Report_IFRS_FY' . date('Y', $repFrom) . '.xlsx';
			?>
				<button type="button" class="btn btn-success btn-sm" onclick="epcDlB64('epcAuditX', '<?php echo epc_erp_h($audXlsxName); ?>')" title="Download a linked Excel workbook — one sheet per element (Trial Balance, SOFP, P&amp;L &amp; OCI, Cash Flows, Changes in Equity, Notes) where every figure links back to the Trial Balance"><i class="fa fa-file-excel-o"></i> Download Excel (linked audit pack)</button>
				<textarea id="epcAuditX" style="display:none;"><?php echo base64_encode(epc_ext_audit_xlsx($db_link, $audXlsxCcy, $repFrom, $repTo)); ?></textarea>
				<script>
				if (typeof epcDlB64 !== 'function') {
					function epcDlB64(id,fn){var el=document.getElementById(id);if(!el)return;var b64=(el.value!==undefined?el.value:el.textContent).replace(/\s+/g,'');var bin=atob(b64);var len=bin.length;var bytes=new Uint8Array(len);for(var i=0;i<len;i++){bytes[i]=bin.charCodeAt(i);}var blob=new Blob([bytes],{type:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"});var url=URL.createObjectURL(blob);var a=document.createElement("a");a.href=url;a.download=fn;document.body.appendChild(a);a.click();document.body.removeChild(a);URL.revokeObjectURL(url);}
				}
				</script>
			<?php endif; ?>
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
					<?php
						$bv = is_array($v) ? ($v['val'] ?? '') : (string) $v;
						$bc = is_array($v) ? ($v['cmp'] ?? '') : '';
						$bn = is_array($v) ? ($v['note'] ?? '') : '';
						$bcol = is_array($v) && !empty($v['color']) ? (string) $v['color'] : '#1d2740';
					?>
					<div style="background:#fff;border:1px solid #e2e6ee;border-top:3px solid <?php echo epc_erp_h($bcol); ?>;border-radius:6px;padding:8px 14px;min-width:150px;">
						<div style="font-size:11px;color:#7a869a;text-transform:uppercase;letter-spacing:.5px;"><?php echo epc_erp_h((string) $k); ?></div>
						<div style="font-size:16px;font-weight:700;color:<?php echo epc_erp_h($bcol); ?>;"><?php echo epc_erp_h((string) $bv); ?></div>
						<?php if ($bc !== ''): ?><div style="font-size:11px;color:#5b6577;margin-top:2px;"><?php echo epc_erp_h((string) $bc); ?></div><?php endif; ?>
						<?php if ($bn !== ''): ?><div style="font-size:10px;color:#9aa3b2;margin-top:1px;"><?php echo epc_erp_h((string) $bn); ?></div><?php endif; ?>
					</div>
				<?php endforeach; ?>
			</div>
			<?php $hasCards = false; foreach ($built['summary'] as $sv) { if (is_array($sv)) { $hasCards = true; break; } } ?>
			<?php if ($hasCards): ?><div class="text-muted" style="font-size:11px;margin:-2px 0 10px;">Each card shows the current reporting period with the prior-year comparative beneath and the governing standard / source note.</div><?php endif; ?>
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
		'theme' => ($selRep === 'audit__external_audit_report' ? 'red' : ''),
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
	<div class="epc-erp-section" style="margin-bottom:14px;background:#fdeef0;border:1px solid #f0c6cd;border-radius:8px;display:flex;flex-wrap:wrap;gap:12px;align-items:center;justify-content:space-between;">
		<div>
			<div style="font-weight:700;color:#7a0c1c;"><i class="fa fa-magic"></i> Guided IFRS report builder (upload prior PDF → system reviews → requests data → builds report)</div>
			<div class="text-muted" style="font-size:12px;margin-top:4px;">Upload your latest accounts (PDF — incl. consolidated/combined). The system reviews them against IFRS &amp; <?php echo epc_erp_h($regName); ?> law, then asks for exactly the trial balance / data it needs (multi-business-unit supported) and builds a fully compliant report.</div>
		</div>
		<a href="<?php echo epc_erp_h($baseUrl . $sep . 'tool=intake'); ?>" class="btn btn-sm" style="background:#b3122a;border-color:#7a0c1c;color:#fff;"><i class="fa fa-magic"></i> Start guided intake</a>
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

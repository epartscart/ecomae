<?php
/**
 * ERP tab — Tenant Configuration Hub.
 *
 * Centralises every tenant-configurable setting so nothing is hardcoded.
 * Groups: company, industry, regional/tax, currency, numbering, defaults, UI.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';

$csrfLocal = isset($csrf) ? $csrf : '';
$dashUrl = epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str);

// Load current settings
$get = function(string $key, string $default = '') use ($db_link): string {
	if (!function_exists('epc_erp_adv_get_setting')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_advanced.php';
	}
	return (string)epc_erp_adv_get_setting($db_link, $key, $default);
};

$cfg = array(
	'company_name' => $get('erp_company_name', ''),
	'company_name_ar' => $get('erp_company_name_ar', ''),
	'company_trn' => $get('erp_company_trn', ''),
	'company_address' => $get('erp_company_address', ''),
	'company_phone' => $get('erp_company_phone', ''),
	'company_email' => $get('erp_company_email', ''),
	'company_country' => $get('erp_company_country', 'AE'),
	'company_city' => $get('erp_company_city', ''),
	'company_license_no' => $get('erp_company_license_no', ''),
	'industry_profile' => $get('erp_industry_profile', ''),
	'industry_pack' => $get('erp_industry_pack', ''),
	'default_currency' => $get('erp_default_currency', 'AED'),
	'fiscal_year_start' => $get('erp_fiscal_year_start', '01'),
	'vat_rate' => $get('erp_vat_rate', '5'),
	'date_format' => $get('erp_date_format', 'd/m/Y'),
	'number_format_decimals' => $get('erp_number_format_decimals', '2'),
	'weight_unit' => $get('erp_weight_unit', 'gram'),
	'po_prefix' => $get('erp_po_prefix', 'PO-'),
	'so_prefix' => $get('erp_so_prefix', 'SO-'),
	'inv_prefix' => $get('erp_inv_prefix', 'INV-'),
	'jv_prefix' => $get('erp_jv_prefix', 'JV-'),
	'pv_prefix' => $get('erp_pv_prefix', 'PV-'),
	'rv_prefix' => $get('erp_rv_prefix', 'RV-'),
	'dn_prefix' => $get('erp_dn_prefix', 'DN-'),
	'auto_number_vouchers' => $get('erp_auto_number_vouchers', '1'),
	'default_warehouse' => $get('erp_default_warehouse', ''),
	'default_payment_terms' => $get('erp_default_payment_terms', 'Net 30'),
	'bank_name' => $get('erp_bank_name', ''),
	'bank_account' => $get('erp_bank_account', ''),
	'bank_iban' => $get('erp_bank_iban', ''),
	'bank_swift' => $get('erp_bank_swift', ''),
	'ui_theme' => $get('erp_ui_theme', 'light'),
	'ui_density' => $get('erp_ui_density', 'comfortable'),
	'ui_grid_rows' => $get('erp_ui_grid_rows', '25'),
);

$countries = array('AE'=>'UAE','SA'=>'Saudi Arabia','BH'=>'Bahrain','OM'=>'Oman','QA'=>'Qatar','KW'=>'Kuwait','EG'=>'Egypt','JO'=>'Jordan','LB'=>'Lebanon','IN'=>'India','PK'=>'Pakistan','US'=>'United States','GB'=>'United Kingdom','DE'=>'Germany','FR'=>'France','IT'=>'Italy','ES'=>'Spain','JP'=>'Japan','CN'=>'China','SG'=>'Singapore','MY'=>'Malaysia','AU'=>'Australia','CA'=>'Canada','BR'=>'Brazil','ZA'=>'South Africa','NG'=>'Nigeria','KE'=>'Kenya','GH'=>'Ghana','TR'=>'Turkey','RU'=>'Russia');
$industries = array(''=>'General','auto_parts'=>'Auto Parts','electronics'=>'Electronics','fashion'=>'Fashion & Apparel','jewellery'=>'Jewellery & Watches','food_beverage'=>'Food & Beverage','construction'=>'Construction','healthcare'=>'Healthcare','real_estate'=>'Real Estate','hospitality'=>'Hospitality','education'=>'Education','logistics'=>'Logistics & Transport');
$currencies = array('AED','SAR','BHD','OMR','QAR','KWD','USD','EUR','GBP','INR','PKR','JPY','CNY','SGD','AUD','CAD','ZAR','EGP','JOD','TRY');
$weightUnits = array('gram'=>'Gram (g)','tola'=>'Tola','ounce'=>'Troy Ounce (ozt)','carat'=>'Carat (ct)','kg'=>'Kilogram (kg)');

erp_page_header(
	'<i class="fa fa-sliders"></i> Tenant configuration',
	'Centralised tenant settings — company, industry, regional, numbering, defaults, and UI preferences. No hardcoded values.',
	array(
		array('label' => 'ERP', 'url' => $dashUrl),
		array('label' => 'System administration'),
		array('label' => 'Tenant configuration'),
	)
);
?>
<form method="post" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" class="epc-erp-form">
	<input type="hidden" name="action" value="tenant_config_save">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">

	<div class="row">
		<div class="col-sm-6">
			<div class="ef-window">
				<div class="ef-title"><i class="fa fa-building"></i> Company Profile</div>
				<div class="ef-body">
					<table class="ef-grid">
						<tr><td style="width:140px"><label>Company name</label></td><td><input type="text" name="company_name" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['company_name']); ?>"></td></tr>
						<tr><td><label>Name (Arabic)</label></td><td><input type="text" name="company_name_ar" class="form-control input-sm" dir="rtl" value="<?php echo epc_erp_h($cfg['company_name_ar']); ?>"></td></tr>
						<tr><td><label>TRN / Tax ID</label></td><td><input type="text" name="company_trn" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['company_trn']); ?>"></td></tr>
						<tr><td><label>License No.</label></td><td><input type="text" name="company_license_no" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['company_license_no']); ?>"></td></tr>
						<tr><td><label>Address</label></td><td><textarea name="company_address" class="form-control input-sm" rows="2"><?php echo epc_erp_h($cfg['company_address']); ?></textarea></td></tr>
						<tr><td><label>City</label></td><td><input type="text" name="company_city" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['company_city']); ?>"></td></tr>
						<tr><td><label>Phone</label></td><td><input type="text" name="company_phone" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['company_phone']); ?>"></td></tr>
						<tr><td><label>Email</label></td><td><input type="email" name="company_email" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['company_email']); ?>"></td></tr>
					</table>
				</div>
			</div>
		</div>
		<div class="col-sm-6">
			<div class="ef-window">
				<div class="ef-title"><i class="fa fa-globe"></i> Regional & Industry</div>
				<div class="ef-body">
					<table class="ef-grid">
						<tr><td style="width:140px"><label>Country</label></td><td>
							<select name="company_country" class="form-control input-sm">
								<?php foreach ($countries as $ck => $cv): ?>
								<option value="<?php echo $ck; ?>"<?php echo $cfg['company_country'] === $ck ? ' selected' : ''; ?>><?php echo epc_erp_h($cv); ?></option>
								<?php endforeach; ?>
							</select>
							<small class="text-muted">Drives compliance, tax, and reporting</small>
						</td></tr>
						<tr><td><label>Industry</label></td><td>
							<select name="industry_profile" class="form-control input-sm">
								<?php foreach ($industries as $ik => $iv): ?>
								<option value="<?php echo epc_erp_h($ik); ?>"<?php echo $cfg['industry_profile'] === $ik ? ' selected' : ''; ?>><?php echo epc_erp_h($iv); ?></option>
								<?php endforeach; ?>
							</select>
							<small class="text-muted">Controls which module fields appear</small>
						</td></tr>
						<tr><td><label>Currency</label></td><td>
							<select name="default_currency" class="form-control input-sm">
								<?php foreach ($currencies as $c): ?>
								<option<?php echo $cfg['default_currency'] === $c ? ' selected' : ''; ?>><?php echo $c; ?></option>
								<?php endforeach; ?>
							</select>
						</td></tr>
						<tr><td><label>VAT rate (%)</label></td><td><input type="number" name="vat_rate" class="form-control input-sm" style="width:80px" value="<?php echo epc_erp_h($cfg['vat_rate']); ?>" step="0.01"></td></tr>
						<tr><td><label>Fiscal year start</label></td><td>
							<select name="fiscal_year_start" class="form-control input-sm">
								<?php foreach (array('01'=>'January','04'=>'April','07'=>'July','10'=>'October') as $mk=>$mv): ?>
								<option value="<?php echo $mk; ?>"<?php echo $cfg['fiscal_year_start'] === $mk ? ' selected' : ''; ?>><?php echo $mv; ?></option>
								<?php endforeach; ?>
							</select>
						</td></tr>
						<tr><td><label>Date format</label></td><td>
							<select name="date_format" class="form-control input-sm">
								<?php foreach (array('d/m/Y'=>'DD/MM/YYYY','m/d/Y'=>'MM/DD/YYYY','Y-m-d'=>'YYYY-MM-DD','d M Y'=>'DD Mon YYYY') as $dk=>$dv): ?>
								<option value="<?php echo $dk; ?>"<?php echo $cfg['date_format'] === $dk ? ' selected' : ''; ?>><?php echo $dv; ?></option>
								<?php endforeach; ?>
							</select>
						</td></tr>
						<tr><td><label>Weight unit</label></td><td>
							<select name="weight_unit" class="form-control input-sm">
								<?php foreach ($weightUnits as $wk=>$wv): ?>
								<option value="<?php echo $wk; ?>"<?php echo $cfg['weight_unit'] === $wk ? ' selected' : ''; ?>><?php echo epc_erp_h($wv); ?></option>
								<?php endforeach; ?>
							</select>
						</td></tr>
					</table>
				</div>
			</div>
		</div>
	</div>

	<div class="row" style="margin-top:10px">
		<div class="col-sm-6">
			<div class="ef-window">
				<div class="ef-title"><i class="fa fa-sort-numeric-asc"></i> Voucher Numbering</div>
				<div class="ef-body">
					<table class="ef-grid">
						<tr><td style="width:140px"><label>Auto-number</label></td><td><label><input type="checkbox" name="auto_number_vouchers" value="1"<?php echo $cfg['auto_number_vouchers'] === '1' ? ' checked' : ''; ?>> Auto-generate voucher numbers</label></td></tr>
						<tr><td><label>PO prefix</label></td><td><input type="text" name="po_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['po_prefix']); ?>"></td></tr>
						<tr><td><label>SO prefix</label></td><td><input type="text" name="so_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['so_prefix']); ?>"></td></tr>
						<tr><td><label>Invoice prefix</label></td><td><input type="text" name="inv_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['inv_prefix']); ?>"></td></tr>
						<tr><td><label>JV prefix</label></td><td><input type="text" name="jv_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['jv_prefix']); ?>"></td></tr>
						<tr><td><label>PV prefix</label></td><td><input type="text" name="pv_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['pv_prefix']); ?>"></td></tr>
						<tr><td><label>RV prefix</label></td><td><input type="text" name="rv_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['rv_prefix']); ?>"></td></tr>
						<tr><td><label>DN prefix</label></td><td><input type="text" name="dn_prefix" class="form-control input-sm" style="width:100px" value="<?php echo epc_erp_h($cfg['dn_prefix']); ?>"></td></tr>
					</table>
				</div>
			</div>
		</div>
		<div class="col-sm-6">
			<div class="ef-window">
				<div class="ef-title"><i class="fa fa-university"></i> Bank & Defaults</div>
				<div class="ef-body">
					<table class="ef-grid">
						<tr><td style="width:140px"><label>Bank name</label></td><td><input type="text" name="bank_name" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['bank_name']); ?>"></td></tr>
						<tr><td><label>Account no.</label></td><td><input type="text" name="bank_account" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['bank_account']); ?>"></td></tr>
						<tr><td><label>IBAN</label></td><td><input type="text" name="bank_iban" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['bank_iban']); ?>"></td></tr>
						<tr><td><label>SWIFT/BIC</label></td><td><input type="text" name="bank_swift" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['bank_swift']); ?>"></td></tr>
						<tr><td><label>Payment terms</label></td><td>
							<select name="default_payment_terms" class="form-control input-sm">
								<?php foreach (array('Net 30','Net 60','Net 90','Net 15','COD','Prepaid','Net 7') as $pt): ?>
								<option<?php echo $cfg['default_payment_terms'] === $pt ? ' selected' : ''; ?>><?php echo $pt; ?></option>
								<?php endforeach; ?>
							</select>
						</td></tr>
						<tr><td><label>Default warehouse</label></td><td><input type="text" name="default_warehouse" class="form-control input-sm" value="<?php echo epc_erp_h($cfg['default_warehouse']); ?>"></td></tr>
					</table>
				</div>
			</div>
		</div>
	</div>

	<div class="row" style="margin-top:10px">
		<div class="col-sm-6">
			<div class="ef-window">
				<div class="ef-title"><i class="fa fa-desktop"></i> UI Preferences</div>
				<div class="ef-body">
					<table class="ef-grid">
						<tr><td style="width:140px"><label>Theme</label></td><td>
							<select name="ui_theme" class="form-control input-sm">
								<option value="light"<?php echo $cfg['ui_theme'] === 'light' ? ' selected' : ''; ?>>Light</option>
								<option value="dark"<?php echo $cfg['ui_theme'] === 'dark' ? ' selected' : ''; ?>>Dark</option>
							</select>
						</td></tr>
						<tr><td><label>Density</label></td><td>
							<select name="ui_density" class="form-control input-sm">
								<option value="comfortable"<?php echo $cfg['ui_density'] === 'comfortable' ? ' selected' : ''; ?>>Comfortable</option>
								<option value="compact"<?php echo $cfg['ui_density'] === 'compact' ? ' selected' : ''; ?>>Compact</option>
								<option value="spacious"<?php echo $cfg['ui_density'] === 'spacious' ? ' selected' : ''; ?>>Spacious</option>
							</select>
						</td></tr>
						<tr><td><label>Grid rows</label></td><td><input type="number" name="ui_grid_rows" class="form-control input-sm" style="width:70px" value="<?php echo epc_erp_h($cfg['ui_grid_rows']); ?>"></td></tr>
						<tr><td><label>Decimal places</label></td><td><input type="number" name="number_format_decimals" class="form-control input-sm" style="width:70px" value="<?php echo epc_erp_h($cfg['number_format_decimals']); ?>"></td></tr>
					</table>
				</div>
			</div>
		</div>
	</div>

	<div style="margin-top:12px;text-align:right;">
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save all settings</button>
		<a href="<?php echo epc_erp_h($dashUrl); ?>" class="btn btn-default btn-sm">Cancel</a>
	</div>
</form>

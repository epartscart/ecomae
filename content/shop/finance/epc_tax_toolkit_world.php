<?php
/**
 * Worldwide tax kit catalog — ISO 3166 countries with default business-tax metadata.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../../users/epc_countries.php';

/** @return array<string, array{tax_type:string, rate:float, label:string, reg_label:string, currency:string, note?:string}> */
function epc_tax_toolkit_world_rate_overrides(): array
{
	return array(
		'AE' => array('tax_type' => 'vat', 'rate' => 5.0, 'label' => 'VAT', 'reg_label' => 'TRN', 'currency' => 'AED', 'delegate_uae_vat' => true),
		'SA' => array('tax_type' => 'vat', 'rate' => 15.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'SAR'),
		'OM' => array('tax_type' => 'vat', 'rate' => 5.0, 'label' => 'VAT', 'reg_label' => 'VAT TIN', 'currency' => 'OMR'),
		'BH' => array('tax_type' => 'vat', 'rate' => 10.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'BHD'),
		'KW' => array('tax_type' => 'none', 'rate' => 0.0, 'label' => 'Tax', 'reg_label' => 'Tax ID', 'currency' => 'KWD', 'note' => 'No VAT — monitor legislative updates'),
		'QA' => array('tax_type' => 'none', 'rate' => 0.0, 'label' => 'Tax', 'reg_label' => 'Tax ID', 'currency' => 'QAR'),
		'IN' => array('tax_type' => 'gst', 'rate' => 18.0, 'label' => 'GST', 'reg_label' => 'GSTIN', 'currency' => 'INR'),
		'PK' => array('tax_type' => 'gst', 'rate' => 18.0, 'label' => 'GST', 'reg_label' => 'NTN / STRN', 'currency' => 'PKR'),
		'GB' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'GBP'),
		'UK' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'GBP'),
		'US' => array('tax_type' => 'sales_tax', 'rate' => 0.0, 'label' => 'Sales tax', 'reg_label' => 'EIN / State ID', 'currency' => 'USD', 'note' => 'State and local sales tax — configure per state'),
		'CA' => array('tax_type' => 'gst', 'rate' => 5.0, 'label' => 'GST/HST', 'reg_label' => 'Business Number', 'currency' => 'CAD', 'note' => 'Provincial PST/HST varies'),
		'AU' => array('tax_type' => 'gst', 'rate' => 10.0, 'label' => 'GST', 'reg_label' => 'ABN', 'currency' => 'AUD'),
		'NZ' => array('tax_type' => 'gst', 'rate' => 15.0, 'label' => 'GST', 'reg_label' => 'GST Number', 'currency' => 'NZD'),
		'SG' => array('tax_type' => 'gst', 'rate' => 9.0, 'label' => 'GST', 'reg_label' => 'GST Reg No.', 'currency' => 'SGD'),
		'MY' => array('tax_type' => 'gst', 'rate' => 8.0, 'label' => 'SST', 'reg_label' => 'SST Number', 'currency' => 'MYR'),
		'ID' => array('tax_type' => 'vat', 'rate' => 11.0, 'label' => 'VAT (PPN)', 'reg_label' => 'NPWP', 'currency' => 'IDR'),
		'PH' => array('tax_type' => 'vat', 'rate' => 12.0, 'label' => 'VAT', 'reg_label' => 'TIN', 'currency' => 'PHP'),
		'TH' => array('tax_type' => 'vat', 'rate' => 7.0, 'label' => 'VAT', 'reg_label' => 'Tax ID', 'currency' => 'THB'),
		'VN' => array('tax_type' => 'vat', 'rate' => 10.0, 'label' => 'VAT', 'reg_label' => 'Tax Code', 'currency' => 'VND'),
		'CN' => array('tax_type' => 'vat', 'rate' => 13.0, 'label' => 'VAT', 'reg_label' => 'USCC', 'currency' => 'CNY'),
		'JP' => array('tax_type' => 'vat', 'rate' => 10.0, 'label' => 'Consumption tax', 'reg_label' => 'Corporate Number', 'currency' => 'JPY'),
		'KR' => array('tax_type' => 'vat', 'rate' => 10.0, 'label' => 'VAT', 'reg_label' => 'Business Reg No.', 'currency' => 'KRW'),
		'HK' => array('tax_type' => 'none', 'rate' => 0.0, 'label' => 'Tax', 'reg_label' => 'BR Number', 'currency' => 'HKD'),
		'CH' => array('tax_type' => 'vat', 'rate' => 8.1, 'label' => 'VAT', 'reg_label' => 'UID', 'currency' => 'CHF'),
		'NO' => array('tax_type' => 'vat', 'rate' => 25.0, 'label' => 'VAT', 'reg_label' => 'Org. Number', 'currency' => 'NOK'),
		'IS' => array('tax_type' => 'vat', 'rate' => 24.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'ISK'),
		'TR' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT (KDV)', 'reg_label' => 'VKN', 'currency' => 'TRY'),
		'RU' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'INN', 'currency' => 'RUB'),
		'UA' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'Tax ID', 'currency' => 'UAH'),
		'ZA' => array('tax_type' => 'vat', 'rate' => 15.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'ZAR'),
		'NG' => array('tax_type' => 'vat', 'rate' => 7.5, 'label' => 'VAT', 'reg_label' => 'TIN', 'currency' => 'NGN'),
		'KE' => array('tax_type' => 'vat', 'rate' => 16.0, 'label' => 'VAT', 'reg_label' => 'PIN', 'currency' => 'KES'),
		'EG' => array('tax_type' => 'vat', 'rate' => 14.0, 'label' => 'VAT', 'reg_label' => 'Tax ID', 'currency' => 'EGP'),
		'MA' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'Tax ID', 'currency' => 'MAD'),
		'BR' => array('tax_type' => 'vat', 'rate' => 17.0, 'label' => 'ICMS/IPI (avg)', 'reg_label' => 'CNPJ', 'currency' => 'BRL', 'note' => 'State ICMS varies — placeholder composite rate'),
		'MX' => array('tax_type' => 'vat', 'rate' => 16.0, 'label' => 'IVA', 'reg_label' => 'RFC', 'currency' => 'MXN'),
		'AR' => array('tax_type' => 'vat', 'rate' => 21.0, 'label' => 'IVA', 'reg_label' => 'CUIT', 'currency' => 'ARS'),
		'CL' => array('tax_type' => 'vat', 'rate' => 19.0, 'label' => 'IVA', 'reg_label' => 'RUT', 'currency' => 'CLP'),
		'CO' => array('tax_type' => 'vat', 'rate' => 19.0, 'label' => 'IVA', 'reg_label' => 'NIT', 'currency' => 'COP'),
		'DE' => array('tax_type' => 'vat', 'rate' => 19.0, 'label' => 'VAT', 'reg_label' => 'VAT ID (USt-IdNr)', 'currency' => 'EUR'),
		'FR' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'VAT ID', 'currency' => 'EUR'),
		'IT' => array('tax_type' => 'vat', 'rate' => 22.0, 'label' => 'VAT', 'reg_label' => 'Partita IVA', 'currency' => 'EUR'),
		'ES' => array('tax_type' => 'vat', 'rate' => 21.0, 'label' => 'VAT', 'reg_label' => 'NIF / VAT ID', 'currency' => 'EUR'),
		'NL' => array('tax_type' => 'vat', 'rate' => 21.0, 'label' => 'VAT', 'reg_label' => 'VAT ID', 'currency' => 'EUR'),
		'BE' => array('tax_type' => 'vat', 'rate' => 21.0, 'label' => 'VAT', 'reg_label' => 'VAT ID', 'currency' => 'EUR'),
		'AT' => array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'UID', 'currency' => 'EUR'),
		'IE' => array('tax_type' => 'vat', 'rate' => 23.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'EUR'),
		'PL' => array('tax_type' => 'vat', 'rate' => 23.0, 'label' => 'VAT', 'reg_label' => 'NIP', 'currency' => 'PLN'),
		'SE' => array('tax_type' => 'vat', 'rate' => 25.0, 'label' => 'VAT', 'reg_label' => 'VAT Number', 'currency' => 'SEK'),
		'DK' => array('tax_type' => 'vat', 'rate' => 25.0, 'label' => 'VAT', 'reg_label' => 'CVR', 'currency' => 'DKK'),
		'FI' => array('tax_type' => 'vat', 'rate' => 25.5, 'label' => 'VAT', 'reg_label' => 'Business ID', 'currency' => 'EUR'),
		'PT' => array('tax_type' => 'vat', 'rate' => 23.0, 'label' => 'VAT', 'reg_label' => 'NIF', 'currency' => 'EUR'),
		'GR' => array('tax_type' => 'vat', 'rate' => 24.0, 'label' => 'VAT', 'reg_label' => 'AFM', 'currency' => 'EUR'),
		'CZ' => array('tax_type' => 'vat', 'rate' => 21.0, 'label' => 'VAT', 'reg_label' => 'DIČ', 'currency' => 'CZK'),
		'RO' => array('tax_type' => 'vat', 'rate' => 19.0, 'label' => 'VAT', 'reg_label' => 'CUI', 'currency' => 'RON'),
		'HU' => array('tax_type' => 'vat', 'rate' => 27.0, 'label' => 'VAT', 'reg_label' => 'Tax Number', 'currency' => 'HUF'),
		'BD' => array('tax_type' => 'vat', 'rate' => 15.0, 'label' => 'VAT', 'reg_label' => 'BIN', 'currency' => 'BDT'),
		'LK' => array('tax_type' => 'vat', 'rate' => 18.0, 'label' => 'VAT', 'reg_label' => 'TIN', 'currency' => 'LKR'),
		'NP' => array('tax_type' => 'vat', 'rate' => 13.0, 'label' => 'VAT', 'reg_label' => 'PAN', 'currency' => 'NPR'),
	);
}

function epc_tax_toolkit_eu_vat_rates(): array
{
	return array(
		'AT' => 20.0, 'BE' => 21.0, 'BG' => 20.0, 'HR' => 25.0, 'CY' => 19.0, 'CZ' => 21.0,
		'DK' => 25.0, 'EE' => 22.0, 'FI' => 25.5, 'FR' => 20.0, 'DE' => 19.0, 'GR' => 24.0,
		'HU' => 27.0, 'IE' => 23.0, 'IT' => 22.0, 'LV' => 21.0, 'LT' => 21.0, 'LU' => 17.0,
		'MT' => 18.0, 'NL' => 21.0, 'PL' => 23.0, 'PT' => 23.0, 'RO' => 19.0, 'SK' => 23.0,
		'SI' => 22.0, 'ES' => 21.0, 'SE' => 25.0,
	);
}

function epc_tax_toolkit_none_country_codes(): array
{
	return array(
		'AI', 'AW', 'BM', 'VG', 'KY', 'FK', 'GI', 'GG', 'JE', 'IM', 'MO', 'MC', 'MS', 'NU', 'PN',
		'SH', 'TC', 'TV', 'WF', 'AQ', 'TF', 'IO', 'CX', 'CC', 'HM', 'UM',
	);
}

function epc_tax_toolkit_country_slug(string $name): string
{
	$slug = strtoupper(preg_replace('/[^A-Z0-9]+/', '-', preg_replace('/[^a-zA-Z0-9\s]/', '', $name)));
	$slug = trim($slug, '-');
	if (strlen($slug) > 24) {
		$slug = substr($slug, 0, 24);
		$slug = rtrim($slug, '-');
	}
	return $slug !== '' ? $slug : 'COUNTRY';
}

function epc_tax_toolkit_type_suffix(string $taxType): string
{
	if ($taxType === 'gst') {
		return 'GST';
	}
	if ($taxType === 'sales_tax') {
		return 'SALES-TAX';
	}
	if ($taxType === 'none') {
		return 'NONE';
	}
	return 'VAT';
}

function epc_tax_toolkit_kit_code_for_country(string $countryCode, string $countryName = ''): string
{
	$cc = strtoupper(trim($countryCode));
	if ($cc === '' || $cc === 'UAE') {
		$cc = 'AE';
	}
	$legacy = array(
		'AE' => 'AE-UAE-VAT', 'OM' => 'OM-OMAN-VAT', 'SA' => 'SA-KSA-VAT',
		'IN' => 'IN-INDIA-GST', 'PK' => 'PK-PAKISTAN-GST', 'GB' => 'GB-UK-VAT',
		'UK' => 'GB-UK-VAT', 'US' => 'US-SALES-TAX',
	);
	if (isset($legacy[$cc])) {
		return $legacy[$cc];
	}
	if ($countryName === '') {
		$countries = epc_countries_iso3166_alpha2();
		$countryName = $countries[$cc] ?? $cc;
	}
	$meta = epc_tax_toolkit_world_meta_for_country($cc, $countryName);
	return $cc . '-' . epc_tax_toolkit_country_slug($countryName) . '-' . epc_tax_toolkit_type_suffix($meta['tax_type']);
}

/** @return array{tax_type:string, rate:float, label:string, reg_label:string, currency:string, note?:string} */
function epc_tax_toolkit_world_meta_for_country(string $countryCode, string $countryName = ''): array
{
	$cc = strtoupper(trim($countryCode));
	$overrides = epc_tax_toolkit_world_rate_overrides();
	if (isset($overrides[$cc])) {
		return $overrides[$cc];
	}
	if (in_array($cc, epc_tax_toolkit_none_country_codes(), true)) {
		return array('tax_type' => 'none', 'rate' => 0.0, 'label' => 'Tax', 'reg_label' => 'Tax ID', 'currency' => 'USD', 'note' => 'No general business VAT/GST — verify local rules');
	}
	$euRates = epc_tax_toolkit_eu_vat_rates();
	if (isset($euRates[$cc])) {
		return array('tax_type' => 'vat', 'rate' => $euRates[$cc], 'label' => 'VAT', 'reg_label' => 'VAT ID', 'currency' => 'EUR');
	}
	if (in_array($cc, epc_tax_toolkit_eu_country_codes(), true)) {
		return array('tax_type' => 'vat', 'rate' => 20.0, 'label' => 'VAT', 'reg_label' => 'VAT ID', 'currency' => 'EUR', 'note' => 'EU VAT — verify member-state rate');
	}
	$gcc = array('AE', 'SA', 'OM', 'BH', 'KW', 'QA');
	if (in_array($cc, $gcc, true)) {
		return array('tax_type' => 'vat', 'rate' => 5.0, 'label' => 'VAT', 'reg_label' => 'Tax ID', 'currency' => 'AED');
	}
	return array('tax_type' => 'vat', 'rate' => 15.0, 'label' => 'VAT', 'reg_label' => 'Tax ID', 'currency' => 'USD', 'note' => 'Placeholder standard rate — verify locally');
}

/** @return array<string, array{rate:?float, threshold?:float, threshold_currency?:string, notes?:string}> */
function epc_tax_toolkit_world_cit_overrides(): array
{
	return array(
		'AE' => array('rate' => 9.0, 'threshold' => 375000, 'threshold_currency' => 'AED', 'notes' => 'UAE Federal Decree-Law on CT — 9% above AED 375k taxable income; free zone QFZP may qualify for 0%'),
		'SA' => array('rate' => 20.0, 'notes' => 'KSA corporate income tax — 20% standard; Zakat may apply to Saudi/GCC nationals'),
		'OM' => array('rate' => 15.0, 'notes' => 'Oman CT from 2023'),
		'BH' => array('rate' => 0.0, 'notes' => 'No corporate income tax — domestic companies; verify foreign branch rules'),
		'KW' => array('rate' => 15.0, 'notes' => 'Kuwait corporate tax on foreign entities'),
		'QA' => array('rate' => 10.0, 'notes' => 'Qatar CT on foreign-owned share'),
		'IN' => array('rate' => 30.0, 'notes' => 'India headline CIT ~30% (plus surcharge/cess) — effective rate varies; configure per entity'),
		'PK' => array('rate' => 29.0, 'notes' => 'Pakistan corporate tax — tiered; placeholder headline rate'),
		'GB' => array('rate' => 25.0, 'notes' => 'UK corporation tax — 25% main rate (small profits rate may apply)'),
		'UK' => array('rate' => 25.0, 'notes' => 'Same as GB'),
		'US' => array('rate' => 21.0, 'notes' => 'US federal CIT 21% — state income tax not included (phase 2)'),
		'CA' => array('rate' => 15.0, 'notes' => 'Canada federal + provincial — placeholder combined reference'),
		'AU' => array('rate' => 30.0, 'notes' => 'Australia company tax 30% (25% base rate entity may apply)'),
		'NZ' => array('rate' => 28.0, 'notes' => 'New Zealand company tax'),
		'SG' => array('rate' => 17.0, 'notes' => 'Singapore headline CIT 17%'),
		'MY' => array('rate' => 24.0, 'notes' => 'Malaysia corporate tax'),
		'ID' => array('rate' => 22.0, 'notes' => 'Indonesia corporate tax'),
		'PH' => array('rate' => 25.0, 'notes' => 'Philippines corporate income tax'),
		'TH' => array('rate' => 20.0, 'notes' => 'Thailand corporate tax'),
		'VN' => array('rate' => 20.0, 'notes' => 'Vietnam CIT — incentives may reduce effective rate'),
		'CN' => array('rate' => 25.0, 'notes' => 'China standard CIT 25%'),
		'JP' => array('rate' => 23.2, 'notes' => 'Japan effective corporate tax ~23.2%'),
		'KR' => array('rate' => 24.0, 'notes' => 'South Korea corporate tax'),
		'HK' => array('rate' => 16.5, 'notes' => 'Hong Kong profits tax 16.5%'),
		'CH' => array('rate' => 14.0, 'notes' => 'Switzerland combined federal/cantonal — placeholder'),
		'DE' => array('rate' => 15.0, 'notes' => 'Germany ~15% CIT + trade tax (combined ~30%)'),
		'FR' => array('rate' => 25.0, 'notes' => 'France IS 25%'),
		'IT' => array('rate' => 24.0, 'notes' => 'Italy IRES 24%'),
		'ES' => array('rate' => 25.0, 'notes' => 'Spain corporate tax'),
		'NL' => array('rate' => 25.8, 'notes' => 'Netherlands CIT'),
		'BE' => array('rate' => 25.0, 'notes' => 'Belgium corporate tax'),
		'IE' => array('rate' => 12.5, 'notes' => 'Ireland trading income 12.5%'),
		'PL' => array('rate' => 19.0, 'notes' => 'Poland CIT 19%'),
		'SE' => array('rate' => 20.6, 'notes' => 'Sweden corporate tax'),
		'NO' => array('rate' => 22.0, 'notes' => 'Norway corporate tax'),
		'DK' => array('rate' => 22.0, 'notes' => 'Denmark corporate tax'),
		'FI' => array('rate' => 20.0, 'notes' => 'Finland corporate tax'),
		'PT' => array('rate' => 21.0, 'notes' => 'Portugal IRC'),
		'GR' => array('rate' => 22.0, 'notes' => 'Greece corporate tax'),
		'TR' => array('rate' => 25.0, 'notes' => 'Turkey corporate tax'),
		'RU' => array('rate' => 20.0, 'notes' => 'Russia profit tax'),
		'UA' => array('rate' => 18.0, 'notes' => 'Ukraine corporate tax'),
		'ZA' => array('rate' => 27.0, 'notes' => 'South Africa CIT'),
		'NG' => array('rate' => 30.0, 'notes' => 'Nigeria companies income tax'),
		'KE' => array('rate' => 30.0, 'notes' => 'Kenya corporate tax'),
		'EG' => array('rate' => 22.5, 'notes' => 'Egypt corporate tax'),
		'MA' => array('rate' => 31.0, 'notes' => 'Morocco corporate tax'),
		'BR' => array('rate' => 34.0, 'notes' => 'Brazil combined IRPJ/CSLL reference'),
		'MX' => array('rate' => 30.0, 'notes' => 'Mexico corporate tax'),
		'AR' => array('rate' => 35.0, 'notes' => 'Argentina corporate tax'),
		'CL' => array('rate' => 27.0, 'notes' => 'Chile corporate tax'),
		'CO' => array('rate' => 35.0, 'notes' => 'Colombia corporate tax'),
		'BD' => array('rate' => 27.5, 'notes' => 'Bangladesh corporate tax'),
		'LK' => array('rate' => 30.0, 'notes' => 'Sri Lanka corporate tax'),
		'NP' => array('rate' => 25.0, 'notes' => 'Nepal corporate tax'),
	);
}

/** Major trading nations with DTT / FTC reference flags. */
function epc_tax_toolkit_world_dtt_major_codes(): array
{
	return array(
		'AE', 'SA', 'OM', 'BH', 'KW', 'QA', 'IN', 'PK', 'GB', 'UK', 'US', 'CA', 'AU', 'NZ', 'SG', 'MY',
		'DE', 'FR', 'IT', 'ES', 'NL', 'BE', 'CH', 'JP', 'KR', 'CN', 'HK', 'ZA', 'NG', 'KE', 'EG', 'BR', 'MX',
	);
}

function epc_tax_toolkit_world_cit_for_country(string $countryCode): array
{
	$cc = strtoupper(trim($countryCode));
	$overrides = epc_tax_toolkit_world_cit_overrides();
	if (isset($overrides[$cc])) {
		return $overrides[$cc];
	}
	if (in_array($cc, epc_tax_toolkit_eu_country_codes(), true)) {
		return array('rate' => 25.0, 'notes' => 'EU member-state CIT varies — verify national rate and incentives');
	}
	return array('rate' => null, 'notes' => 'Configure corporate tax manually for this jurisdiction');
}

function epc_tax_toolkit_world_import_duty_default(string $countryCode): ?float
{
	$cc = strtoupper(trim($countryCode));
	$map = array('AE' => 5.0, 'SA' => 5.0, 'OM' => 5.0, 'BH' => 5.0, 'IN' => 10.0, 'PK' => 20.0, 'GB' => 0.0, 'UK' => 0.0, 'US' => 0.0);
	return $map[$cc] ?? 5.0;
}

function epc_tax_toolkit_build_uae_comprehensive_rules(array $meta): array
{
	$rate = (float) ($meta['rate'] ?? 5.0);
	$rules = epc_tax_toolkit_build_comprehensive_rules('AE', $meta);
	$rules['indirect']['vat'] = array(
		'rate' => $rate,
		'label' => 'VAT',
		'reg_label' => 'TRN',
		'delegate_uae_vat' => true,
		'fta_sync' => true,
	);
	$rules['direct']['corporate_tax'] = array(
		'rate' => 9.0,
		'threshold_aed' => 375000,
		'threshold_currency' => 'AED',
		'free_zone_qfzp' => true,
		'notes' => 'Federal Decree-Law No. 47 of 2022 — 9% on taxable income exceeding AED 375,000; QFZP qualifying free zone persons may be 0%',
	);
	$rules['indirect']['excise'] = array(
		array('category' => 'tobacco', 'notes' => '100% excise — verify FTA excise rates'),
		array('category' => 'carbonated_drinks', 'rate' => 50.0, 'notes' => 'Excise on sweetened drinks'),
		array('category' => 'energy_drinks', 'rate' => 100.0, 'notes' => 'Excise on energy drinks'),
	);
	$rules['trade']['import_duty_default'] = 5.0;
	$rules['trade']['export_vat_treatment'] = 'zero_rated';
	$rules['trade']['customs_authority'] = 'UAE Federal Customs Authority / emirate ports';
	$rules['international']['dtt_countries'] = array('IN', 'PK', 'GB', 'US', 'CN', 'SG', 'DE', 'FR', 'SA', 'OM', 'BH', 'KW', 'QA');
	$rules['international']['ftc_available'] = true;
	$rules['international']['notes'] = 'UAE extensive DTT network — foreign tax credit under ordinary credit method; permanent establishment rules apply for cross-border services';
	$rules['double_taxation']['rules'] = 'Credit method for foreign tax on UAE-resident entities; treaty relief where DTT applies';
	$rules['double_taxation']['credit_method'] = 'ordinary_credit';
	$rules['withholding'] = array(
		array('type' => 'services', 'rate' => null, 'notes' => 'WHT may apply on payments to non-residents — verify treaty'),
		array('type' => 'dividends', 'rate' => 0.0, 'notes' => 'Generally no WHT on dividends to non-residents (verify treaty)'),
	);
	$rules['erp_hooks']['purchase_inventory'] = 'vat_on_purchase_recoverable';
	$rules['erp_hooks']['sales'] = 'vat_on_output';
	$rules['erp_hooks']['profit_cit'] = 'apply_above_threshold';
	$rules['erp_hooks']['import_duty_on_cost'] = true;
	$rules['delegate_uae_vat'] = true;
	$rules['fta_update_url'] = 'https://tax.gov.ae/en/legislation.aspx';
	$rules['name'] = 'UAE — Comprehensive Business Tax';
	return $rules;
}

function epc_tax_toolkit_build_comprehensive_rules(string $countryCode, array $meta): array
{
	$cc = strtoupper(trim($countryCode));
	$taxType = (string) ($meta['tax_type'] ?? 'vat');
	$rate = (float) ($meta['rate'] ?? 0);
	$label = (string) ($meta['label'] ?? 'Tax');
	$cit = epc_tax_toolkit_world_cit_for_country($cc);
	$isMajorDtt = in_array($cc, epc_tax_toolkit_world_dtt_major_codes(), true);
	$hasIndirect = $taxType !== 'none' && $rate >= 0;
	$indirectKey = $taxType === 'gst' ? 'gst' : ($taxType === 'sales_tax' ? 'sales_tax' : 'vat');

	$indirect = array();
	if ($hasIndirect) {
		$indirect[$indirectKey] = array(
			'rate' => $rate,
			'label' => $label,
			'reg_label' => (string) ($meta['reg_label'] ?? 'Tax ID'),
		);
	} else {
		$indirect['vat'] = array('rate' => 0.0, 'label' => $label, 'notes' => 'No general VAT/GST');
	}
	if (!empty($meta['delegate_uae_vat'])) {
		$indirect['vat']['delegate_uae_vat'] = true;
	}

	$direct = array(
		'corporate_tax' => array(
			'rate' => $cit['rate'],
			'threshold' => $cit['threshold'] ?? null,
			'threshold_currency' => $cit['threshold_currency'] ?? ($meta['currency'] ?? 'USD'),
			'notes' => $cit['notes'] ?? '',
		),
		'income_tax' => array(
			'rate' => null,
			'notes' => 'Personal/business income tax — configure if applicable; many GCC states have no personal income tax',
		),
	);

	$trade = array(
		'import_duty_default' => epc_tax_toolkit_world_import_duty_default($cc),
		'export_vat_treatment' => $hasIndirect ? 'zero_rated' : 'not_applicable',
		'reverse_charge_b2b' => ($taxType === 'vat'),
		'anti_dumping' => false,
		'notes' => $taxType === 'sales_tax'
			? 'US import duty + state sales tax — configure per state (phase 2)'
			: 'Customs duty on import; import VAT/GST may apply at border',
	);

	$withholding = array();
	if ($isMajorDtt) {
		$withholding[] = array('type' => 'services', 'rate' => null, 'notes' => 'WHT on cross-border services — verify treaty and domestic law');
		$withholding[] = array('type' => 'royalties', 'rate' => null, 'notes' => 'Treaty-reduced rates may apply');
	}

	$international = array(
		'dtt_countries' => $isMajorDtt ? array() : array(),
		'ftc_available' => $isMajorDtt,
		'permanent_establishment' => $isMajorDtt,
		'notes' => $isMajorDtt
			? 'Double taxation treaty network — foreign tax credit typically available; verify treaty with counterparty country'
			: 'Verify bilateral DTT and foreign tax credit eligibility with tax advisor',
	);

	$doubleTaxation = array(
		'rules' => $isMajorDtt
			? 'Treaty relief and foreign tax credit under domestic law — ordinary credit method typical'
			: 'Configure double taxation relief per bilateral treaties',
		'credit_method' => $isMajorDtt ? 'ordinary_credit' : null,
	);

	$erpHooks = array(
		'purchase_inventory' => $hasIndirect ? 'vat_on_purchase_recoverable' : 'no_indirect_recoverable',
		'sales' => $hasIndirect ? 'vat_on_output' : 'no_indirect_output',
		'profit_cit' => ($cit['rate'] !== null && $cit['rate'] > 0) ? 'apply_above_threshold' : 'not_applicable',
		'import_duty_on_cost' => true,
		'purchase_tax' => $hasIndirect ? 'recoverable_input_' . $indirectKey : null,
		'sale_tax' => $hasIndirect ? 'output_' . $indirectKey : null,
		'profit_tax_applicable' => ($cit['rate'] !== null && $cit['rate'] > 0),
	);

	$rules = array(
		'tax_label' => $label,
		'reg_number_label' => (string) ($meta['reg_label'] ?? 'Tax ID'),
		'standard_rate' => $rate,
		'pricing_mode' => 'exclusive',
		'rounding' => 'line',
		'direct_tax' => ($cit['rate'] !== null && $cit['rate'] > 0),
		'indirect_tax' => $hasIndirect,
		'import_rules' => array(
			'customs_duty_applies' => true,
			'reverse_charge_b2b' => ($taxType === 'vat'),
			'import_duty_rate' => epc_tax_toolkit_world_import_duty_default($cc),
		),
		'export_rules' => array(
			'zero_rate_export' => $hasIndirect,
			'non_domestic_buyer_zero' => false,
		),
		'trade_type_rates' => array('retail' => 'standard', 'wholesale' => 'standard'),
		'currency_default' => (string) ($meta['currency'] ?? 'USD'),
		'indirect' => $indirect,
		'direct' => $direct,
		'trade' => $trade,
		'withholding' => $withholding,
		'international' => $international,
		'double_taxation' => $doubleTaxation,
		'erp_hooks' => $erpHooks,
		'payroll' => array(
			'social_security' => array('rate' => null, 'notes' => 'Reference only — configure per entity payroll'),
			'pension' => array('rate' => null, 'notes' => 'End-of-service / pension per labour law'),
		),
		'special' => array(
			'tourism_tax' => null,
			'digital_services_tax' => null,
			'transfer_pricing' => $isMajorDtt ? 'document_if_cross_border_related_party' : null,
		),
		'last_updated' => date('Y-m-d'),
		'source' => 'seed',
	);
	if (!empty($meta['delegate_uae_vat'])) {
		$rules['delegate_uae_vat'] = true;
	}
	if (!empty($meta['note'])) {
		$rules['phase2_note'] = $meta['note'];
		$rules['limitations'] = array($meta['note']);
	}
	if ($taxType === 'sales_tax') {
		$rules['phase2_note'] = $meta['note'] ?? 'Jurisdiction-specific sales tax — US state+local needs phase 2';
		$rules['limitations'] = array('US state and local sales tax not included — configure per state in phase 2');
	}
	return $rules;
}

function epc_tax_toolkit_build_country_kit_def(string $countryCode, string $countryName): array
{
	$cc = strtoupper(trim($countryCode));
	$meta = epc_tax_toolkit_world_meta_for_country($cc, $countryName);
	$kitCode = epc_tax_toolkit_kit_code_for_country($cc, $countryName);
	$taxType = $meta['tax_type'];
	if ($cc === 'AE' && $kitCode === 'AE-UAE-VAT') {
		$rules = epc_tax_toolkit_build_uae_comprehensive_rules($meta);
	} else {
		$rules = epc_tax_toolkit_build_comprehensive_rules($cc, $meta);
	}
	return array(
		'kit_code' => $kitCode,
		'name' => ($cc === 'AE' && $kitCode === 'AE-UAE-VAT')
			? 'UAE — Business Tax (VAT + CT + Trade)'
			: ($countryName . ' — ' . $meta['label'] . ' + Business Tax'),
		'jurisdiction' => $countryName,
		'country_codes' => array($cc),
		'tax_type' => $taxType === 'none' ? 'none' : $taxType,
		'rules' => $rules,
	);
}

function epc_tax_toolkit_world_catalog_definitions(): array
{
	$countries = epc_countries_iso3166_alpha2();
	$defs = array();
	$seen = array();
	foreach ($countries as $cc => $name) {
		if ($cc === 'AN') {
			continue;
		}
		$def = epc_tax_toolkit_build_country_kit_def($cc, $name);
		if (isset($seen[$def['kit_code']])) {
			$defs[$seen[$def['kit_code']]]['country_codes'][] = $cc;
			continue;
		}
		$seen[$def['kit_code']] = count($defs);
		$defs[] = $def;
	}
	$legacy = array(
		epc_tax_toolkit_build_country_kit_def('AE', 'United Arab Emirates'),
	);
	$legacy[0]['kit_code'] = 'AE-UAE-VAT';
	$legacy[0]['name'] = 'UAE — Business Tax (VAT + CT + Trade)';
	$legacy[0]['rules'] = epc_tax_toolkit_build_uae_comprehensive_rules(
		epc_tax_toolkit_world_meta_for_country('AE', 'United Arab Emirates')
	);
	$legacy[0]['country_codes'] = array('AE', 'UAE');
	if (!isset($seen['AE-UAE-VAT'])) {
		array_unshift($defs, $legacy[0]);
	}
	return $defs;
}

function epc_tax_toolkit_country_name_to_iso(string $countryName): string
{
	$name = strtolower(trim($countryName));
	if ($name === '' || $name === 'uae' || strpos($name, 'emirates') !== false) {
		return 'AE';
	}
	if ($name === 'uk' || $name === 'united kingdom' || $name === 'great britain') {
		return 'GB';
	}
	if ($name === 'usa' || $name === 'united states' || $name === 'united states of america') {
		return 'US';
	}
	foreach (epc_countries_iso3166_alpha2() as $code => $label) {
		if (strtolower($label) === $name) {
			return $code;
		}
	}
	if (strlen($countryName) === 2) {
		return strtoupper($countryName);
	}
	return '';
}

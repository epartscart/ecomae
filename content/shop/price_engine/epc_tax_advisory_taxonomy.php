<?php
/**
 * EPC Tax Advisory / Professional Services taxonomy — consultancy, accounting, audit, tax.
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<int,array{slug:string,name:string,sort?:int,children?:array}>
 */
function epc_tax_advisory_seed_tree(): array
{
	return array(
		array('slug' => 'svc-accounting', 'name' => 'Accounting & bookkeeping', 'sort' => 10, 'children' => array(
			array('slug' => 'svc-accounting-monthly', 'name' => 'Monthly bookkeeping packages', 'children' => array(
				array('slug' => 'svc-accounting-monthly-basic', 'name' => 'Basic SME package'),
				array('slug' => 'svc-accounting-monthly-growth', 'name' => 'Growth / multi-entity package'),
				array('slug' => 'svc-accounting-monthly-enterprise', 'name' => 'Enterprise / group reporting'),
			)),
			array('slug' => 'svc-accounting-payroll', 'name' => 'Payroll & WPS compliance'),
			array('slug' => 'svc-accounting-management', 'name' => 'Management accounts & MIS'),
		)),
		array('slug' => 'svc-audit', 'name' => 'Audit & assurance', 'sort' => 20, 'children' => array(
			array('slug' => 'svc-audit-statutory', 'name' => 'Statutory audit (UAE mainland / free zone)'),
			array('slug' => 'svc-audit-internal', 'name' => 'Internal audit & controls review'),
			array('slug' => 'svc-audit-due-diligence', 'name' => 'Due diligence & transaction support'),
		)),
		array('slug' => 'svc-tax', 'name' => 'Tax advisory & filing', 'sort' => 30, 'children' => array(
			array('slug' => 'svc-tax-vat', 'name' => 'VAT registration & filing', 'children' => array(
				array('slug' => 'svc-tax-vat-registration', 'name' => 'VAT registration (FTA)'),
				array('slug' => 'svc-tax-vat-return', 'name' => 'VAT return preparation & filing'),
				array('slug' => 'svc-tax-vat-refund', 'name' => 'VAT refund & voluntary disclosure'),
			)),
			array('slug' => 'svc-tax-corporate', 'name' => 'Corporate tax (UAE CT)', 'children' => array(
				array('slug' => 'svc-tax-ct-registration', 'name' => 'Corporate tax registration'),
				array('slug' => 'svc-tax-ct-return', 'name' => 'Corporate tax return & transfer pricing'),
				array('slug' => 'svc-tax-ct-planning', 'name' => 'Tax planning & restructuring'),
			)),
			array('slug' => 'svc-tax-withholding', 'name' => 'Withholding tax & treaty advice'),
		)),
		array('slug' => 'svc-advisory', 'name' => 'Business advisory', 'sort' => 40, 'children' => array(
			array('slug' => 'svc-advisory-company-setup', 'name' => 'Company formation & licensing'),
			array('slug' => 'svc-advisory-cfo', 'name' => 'Virtual CFO & financial advisory'),
			array('slug' => 'svc-advisory-esr', 'name' => 'ESR / UBO / AML compliance support'),
			array('slug' => 'svc-advisory-immigration', 'name' => 'PRO & immigration (corporate)'),
		)),
		array('slug' => 'svc-consulting', 'name' => 'Consultancy packages', 'sort' => 50, 'children' => array(
			array('slug' => 'svc-consulting-retainer', 'name' => 'Retainer advisory hours'),
			array('slug' => 'svc-consulting-project', 'name' => 'Fixed-fee project engagements'),
			array('slug' => 'svc-consulting-training', 'name' => 'Tax & finance training workshops'),
		)),
	);
}

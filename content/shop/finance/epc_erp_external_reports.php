<?php
/**
 * External Reporting engine — statutory / regulatory report registry.
 *
 * A single, country-aware catalogue of external (regulatory, statutory, tax,
 * financial, audit, AML, ESG ...) report types. Every report carries:
 *   - the governing LAW / regulator authority for the tenant country,
 *   - the official REPORTING-FORMAT / filing-portal link,
 *   - the relevant IFRS / international standard link (financial reports),
 * and either a live builder (fetches ERP data and renders a formatted report)
 * or a structured formatted template.
 *
 * WORLDWIDE RULE (see knowledge note): everything is driven by the tenant's
 * REGISTRATION COUNTRY (company profile). The country dropdown on the tab is a
 * preview/look-up only; the resolved authority + format + compliance always key
 * off the registered country, with a UAE sub-layer and a safe generic fallback.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';

if (!function_exists('epc_ext_reports_categories')) {
    /**
     * The 26 external-reporting categories (key => label).
     *
     * @return array<string,string>
     */
    function epc_ext_reports_categories(): array
    {
        return array(
            'corp'      => 'Corporate Registration & Legal Reporting',
            'tax'       => 'Tax Reporting',
            'fin'       => 'Financial Reporting',
            'audit'     => 'External Audit & Assurance Reporting',
            'hr'        => 'Employment & HR Reporting',
            'aml'       => 'AML / Financial Crime Reporting',
            'bank'      => 'Banking Reporting',
            'ins'       => 'Insurance Reporting',
            'sec'       => 'Investment & Securities Reporting',
            'customs'   => 'Customs & International Trade Reporting',
            'esg'       => 'ESG & Sustainability Reporting',
            'env'       => 'Environmental Reporting',
            'hs'        => 'Health & Safety Reporting',
            'data'      => 'Data Privacy & Cybersecurity Reporting',
            're'        => 'Real Estate Reporting',
            'health'    => 'Healthcare Reporting',
            'pharma'    => 'Pharmaceutical Reporting',
            'telecom'   => 'Telecommunications Reporting',
            'energy'    => 'Energy & Utilities Reporting',
            'transport' => 'Transportation & Logistics Reporting',
            'mfg'       => 'Manufacturing Reporting',
            'consumer'  => 'Consumer Protection Reporting',
            'govt'      => 'Government Contract Reporting',
            'stats'     => 'Statistical & Economic Reporting',
            'crisis'    => 'Crisis & Incident Reporting',
            'sector'    => 'Sector-Specific Regulatory Reporting',
        );
    }
}

if (!function_exists('epc_ext_reports_registry')) {
    /**
     * Full report registry. Each entry:
     *   key => array(name, cat, builder, std)
     * - builder: live builder id (see epc_ext_report_build) or '' for template.
     * - std: IFRS / international standard ref id (for IFRS link) or ''.
     *
     * @return array<string,array<string,string>>
     */
    function epc_ext_reports_registry(): array
    {
        // Compact definition: cat => list of [name, builder, std].
        $defs = array(
            'corp' => array(
                array('Company Incorporation Filing', '', ''),
                array('Branch Registration Filing', '', ''),
                array('Foreign Company Registration', '', ''),
                array('Annual Return Filing', 'annual_return', ''),
                array('Trade License Renewal', 'trade_license', ''),
                array('Business Activity Amendment', '', ''),
                array('Registered Address Change', '', ''),
                array('Director Appointment Filing', '', ''),
                array('Director Resignation Filing', '', ''),
                array('Secretary Appointment Filing', '', ''),
                array('Shareholder Change Filing', '', ''),
                array('Capital Change Filing', '', ''),
                array('Company Dissolution Filing', '', ''),
                array('Liquidation Reporting', '', ''),
                array('Merger Filing', '', ''),
                array('Acquisition Filing', '', ''),
                array('Corporate Restructuring Filing', '', ''),
                array('Beneficial Ownership Filing', 'ubo', ''),
                array('UBO Reporting', 'ubo', ''),
                array('Corporate Governance Reporting', '', ''),
            ),
            'tax' => array(
                array('Corporate Income Tax Return', 'corporate_tax', ''),
                array('Corporate Tax Registration', '', ''),
                array('Corporate Tax Deregistration', '', ''),
                array('VAT Return', 'vat_return', ''),
                array('VAT Registration', '', ''),
                array('VAT Deregistration', '', ''),
                array('VAT Refund Claim', 'vat_refund', ''),
                array('Tourist VAT Refund Reporting', '', ''),
                array('GST Return', 'vat_return', ''),
                array('Sales Tax Return', 'vat_return', ''),
                array('Use Tax Return', '', ''),
                array('Excise Tax Return', 'excise', ''),
                array('Customs Duty Reporting', '', ''),
                array('Property Tax Reporting', '', ''),
                array('Payroll Tax Reporting', 'payroll_tax', ''),
                array('Withholding Tax Reporting', 'wht', ''),
                array('Dividend Tax Reporting', '', ''),
                array('Capital Gains Tax Reporting', '', ''),
                array('Transfer Pricing Disclosure', '', ''),
                array('Transfer Pricing Local File', '', ''),
                array('Transfer Pricing Master File', '', ''),
                array('Country-by-Country Reporting', 'cbcr', ''),
                array('Digital Services Tax Reporting', '', ''),
                array('Environmental Tax Reporting', '', ''),
                array('Carbon Tax Reporting', '', ''),
                array('Stamp Duty Reporting', '', ''),
                array('Municipal Tax Reporting', '', ''),
                array('Tax Audit Response Filing', '', ''),
            ),
            'fin' => array(
                array('Annual Financial Statements', 'afs', 'IFRS18'),
                array('Interim Financial Statements', 'interim', 'IAS34'),
                array('Consolidated Financial Statements', 'consolidated', 'IFRS10'),
                array('Statutory Accounts Filing', 'afs', 'IFRS18'),
                array('IFRS Reporting', 'afs', 'IFRS18'),
                array('GAAP Reporting', 'afs', 'IFRS18'),
                array('Audit Report Filing', 'audit_report', 'ISA700'),
                array('Qualified Audit Disclosure', '', 'ISA705'),
                array('Financial Model & Forecast', 'fin_model', 'IFRS'),
                array('Business Valuation Report', 'valuation', 'IVS'),
                array('Internal Control Reporting', '', ''),
                array('Financial Risk Reporting', '', 'IFRS7'),
                array('Treasury Reporting', '', ''),
                array('Capital Adequacy Reporting', '', ''),
                array('Liquidity Reporting', '', ''),
                array('Solvency Reporting', '', ''),
            ),
            'audit' => array(
                array('External Audit Report', 'audit_report', 'ISA700'),
                array('Internal Audit Report', '', ''),
                array('Compliance Audit Report', '', ''),
                array('Tax Audit Report', '', ''),
                array('Forensic Audit Report', '', ''),
                array('Operational Audit Report', '', ''),
                array('IT Audit Report', '', ''),
                array('Cybersecurity Audit Report', '', ''),
                array('ESG Assurance Report', '', 'ISSB'),
                array('Sustainability Assurance Report', '', 'ISSB'),
            ),
            'hr' => array(
                array('Payroll Reporting', 'payroll_tax', ''),
                array('Wage Protection Reporting', 'wps', ''),
                array('Employee Census Reporting', 'employee_census', ''),
                array('Labor Compliance Reporting', '', ''),
                array('Work Permit Reporting', '', ''),
                array('Visa Compliance Reporting', '', ''),
                array('Pension Reporting', '', ''),
                array('Social Security Reporting', '', ''),
                array('End-of-Service Reporting', 'eos', ''),
                array('Workforce Diversity Reporting', '', ''),
                array('Gender Pay Gap Reporting', '', ''),
                array('Occupational Safety Reporting', '', ''),
                array('Workplace Injury Reporting', '', ''),
                array('Workers Compensation Reporting', '', ''),
            ),
            'aml' => array(
                array('AML Compliance Reporting', '', ''),
                array('KYC Compliance Reporting', '', ''),
                array('Customer Due Diligence Reporting', '', ''),
                array('Enhanced Due Diligence Reporting', '', ''),
                array('Suspicious Activity Report (SAR)', 'aml', ''),
                array('Suspicious Transaction Report (STR)', 'aml', ''),
                array('Suspicious Fund Transfer Report', 'aml', ''),
                array('Terrorist Financing Report', '', ''),
                array('Sanctions Screening Report', '', ''),
                array('Politically Exposed Person Reporting', '', ''),
                array('Fraud Reporting', '', ''),
                array('Anti-Bribery Reporting', '', ''),
                array('Anti-Corruption Reporting', '', ''),
                array('Financial Crime Risk Reporting', '', ''),
            ),
            'bank' => array(
                array('Prudential Reporting', '', ''),
                array('Basel Reporting', '', ''),
                array('Capital Adequacy Reporting', '', ''),
                array('Liquidity Coverage Reporting', '', ''),
                array('Stress Testing Reporting', '', ''),
                array('Credit Risk Reporting', '', ''),
                array('Market Risk Reporting', '', ''),
                array('Operational Risk Reporting', '', ''),
                array('Large Exposure Reporting', '', ''),
                array('Loan Portfolio Reporting', '', ''),
                array('Deposit Reporting', '', ''),
                array('Central Bank Reporting', '', ''),
            ),
            'ins' => array(
                array('Solvency Reporting', '', ''),
                array('Claims Reporting', '', ''),
                array('Actuarial Reporting', '', ''),
                array('Reinsurance Reporting', '', ''),
                array('Insurance Reserve Reporting', '', ''),
                array('Regulatory Insurance Reporting', '', ''),
            ),
            'sec' => array(
                array('Prospectus Filing', '', ''),
                array('Securities Offering Reporting', '', ''),
                array('Insider Trading Reporting', '', ''),
                array('Market Abuse Reporting', '', ''),
                array('Shareholding Disclosure', '', ''),
                array('Fund Reporting', '', ''),
                array('Investment Position Reporting', '', ''),
                array('Asset Management Reporting', '', ''),
                array('Portfolio Reporting', '', ''),
                array('Derivatives Reporting', '', ''),
                array('Trade Repository Reporting', '', ''),
            ),
            'customs' => array(
                array('Import Declaration', '', ''),
                array('Export Declaration', '', ''),
                array('Customs Declaration', '', ''),
                array('Trade Statistics Reporting', '', ''),
                array('Free Zone Reporting', '', ''),
                array('Certificate of Origin Reporting', '', ''),
                array('Sanctions Trade Reporting', '', ''),
                array('Export Control Reporting', '', ''),
                array('Dual-Use Goods Reporting', '', ''),
            ),
            'esg' => array(
                array('ESG Reporting', '', 'ISSB'),
                array('Sustainability Reporting', '', 'ISSB'),
                array('Carbon Emissions Reporting', '', 'IFRS_S2'),
                array('Greenhouse Gas Reporting', '', 'IFRS_S2'),
                array('Climate Risk Reporting', '', 'IFRS_S2'),
                array('Energy Consumption Reporting', '', ''),
                array('Water Usage Reporting', '', ''),
                array('Waste Management Reporting', '', ''),
                array('Biodiversity Reporting', '', ''),
                array('Net-Zero Reporting', '', 'IFRS_S2'),
                array('Sustainable Finance Reporting', '', 'ISSB'),
            ),
            'env' => array(
                array('Environmental Impact Reporting', '', ''),
                array('Pollution Reporting', '', ''),
                array('Air Emissions Reporting', '', ''),
                array('Hazardous Waste Reporting', '', ''),
                array('Chemical Usage Reporting', '', ''),
                array('Environmental Incident Reporting', '', ''),
                array('Environmental Permit Reporting', '', ''),
            ),
            'hs' => array(
                array('Occupational Health Reporting', '', ''),
                array('Workplace Safety Reporting', '', ''),
                array('Accident Reporting', '', ''),
                array('Injury Reporting', '', ''),
                array('Fatality Reporting', '', ''),
                array('Hazard Reporting', '', ''),
                array('Safety Inspection Reporting', '', ''),
            ),
            'data' => array(
                array('Data Protection Reporting', '', ''),
                array('Personal Data Processing Reporting', '', ''),
                array('Data Breach Notification', '', ''),
                array('Cyber Incident Reporting', '', ''),
                array('Information Security Reporting', '', ''),
                array('Cyber Resilience Reporting', '', ''),
                array('Critical Infrastructure Reporting', '', ''),
            ),
            're' => array(
                array('Property Ownership Reporting', '', ''),
                array('Real Estate Transaction Reporting', '', ''),
                array('Escrow Reporting', '', ''),
                array('Rental Reporting', '', ''),
                array('Property Valuation Reporting', '', ''),
            ),
            'health' => array(
                array('Clinical Reporting', '', ''),
                array('Adverse Event Reporting', '', ''),
                array('Pharmacovigilance Reporting', '', ''),
                array('Patient Safety Reporting', '', ''),
                array('Medical Device Reporting', '', ''),
            ),
            'pharma' => array(
                array('Drug Safety Reporting', '', ''),
                array('Clinical Trial Reporting', '', ''),
                array('Manufacturing Compliance Reporting', '', ''),
                array('Product Recall Reporting', '', ''),
            ),
            'telecom' => array(
                array('Spectrum Usage Reporting', '', ''),
                array('Telecom Regulatory Reporting', '', ''),
                array('Service Quality Reporting', '', ''),
            ),
            'energy' => array(
                array('Energy Production Reporting', '', ''),
                array('Utility Compliance Reporting', '', ''),
                array('Grid Reporting', '', ''),
                array('Oil & Gas Production Reporting', '', ''),
                array('Reserves Reporting', '', ''),
            ),
            'transport' => array(
                array('Aviation Safety Reporting', '', ''),
                array('Maritime Compliance Reporting', '', ''),
                array('Port Reporting', '', ''),
                array('Fleet Reporting', '', ''),
                array('Transportation Safety Reporting', '', ''),
            ),
            'mfg' => array(
                array('Production Reporting', '', ''),
                array('Quality Compliance Reporting', '', ''),
                array('Product Safety Reporting', '', ''),
                array('Recall Reporting', '', ''),
            ),
            'consumer' => array(
                array('Consumer Complaint Reporting', '', ''),
                array('Product Defect Reporting', '', ''),
                array('Product Recall Reporting', '', ''),
            ),
            'govt' => array(
                array('Public Procurement Reporting', '', ''),
                array('Government Grant Reporting', '', ''),
                array('Subsidy Reporting', '', ''),
            ),
            'stats' => array(
                array('National Statistics Reporting', '', ''),
                array('Census Reporting', '', ''),
                array('Economic Survey Reporting', '', ''),
                array('Industry Survey Reporting', '', ''),
            ),
            'crisis' => array(
                array('Business Continuity Reporting', '', ''),
                array('Disaster Reporting', '', ''),
                array('Emergency Incident Reporting', '', ''),
                array('Crisis Management Reporting', '', ''),
            ),
            'sector' => array(
                array('Aviation Regulatory Reporting', '', ''),
                array('Maritime Regulatory Reporting', '', ''),
                array('Mining Regulatory Reporting', '', ''),
                array('Education Regulatory Reporting', '', ''),
                array('Defense Industry Reporting', '', ''),
                array('Food Safety Reporting', '', ''),
                array('Agriculture Reporting', '', ''),
                array('Hospitality Reporting', '', ''),
                array('Tourism Reporting', '', ''),
                array('Gaming/Gambling Reporting (where legal)', '', ''),
                array('Economic Substance Notification', 'esr_notify', ''),
                array('Economic Substance Report', 'esr', ''),
                array('AML goAML Reporting', 'aml', ''),
            ),
        );

        $out = array();
        foreach ($defs as $cat => $rows) {
            foreach ($rows as $r) {
                $key = epc_ext_report_key($cat, $r[0]);
                $out[$key] = array(
                    'key' => $key,
                    'name' => $r[0],
                    'cat' => $cat,
                    'builder' => $r[1],
                    'std' => $r[2],
                );
            }
        }
        return $out;
    }
}

if (!function_exists('epc_ext_report_key')) {
    function epc_ext_report_key(string $cat, string $name): string
    {
        $slug = strtolower($name);
        $slug = preg_replace('/[^a-z0-9]+/', '_', $slug);
        $slug = trim((string) $slug, '_');
        return $cat . '__' . $slug;
    }
}

if (!function_exists('epc_ext_report_get')) {
    /**
     * @return array<string,string>|null
     */
    function epc_ext_report_get(string $key): ?array
    {
        $reg = epc_ext_reports_registry();
        return $reg[$key] ?? null;
    }
}

/* -------------------------------------------------------------------------
 * Authority / law / format resolution — driven by tenant country + domain.
 * ---------------------------------------------------------------------- */

if (!function_exists('epc_ext_domain_for_category')) {
    /**
     * Map a report category to a regulator domain used for authority lookup.
     */
    function epc_ext_domain_for_category(string $cat): string
    {
        $map = array(
            'corp' => 'corp', 'tax' => 'tax', 'fin' => 'fin', 'audit' => 'audit',
            'hr' => 'hr', 'aml' => 'aml', 'bank' => 'bank', 'ins' => 'ins',
            'sec' => 'sec', 'customs' => 'customs', 'esg' => 'esg', 'env' => 'env',
            'hs' => 'hs', 'data' => 'data', 're' => 're', 'health' => 'health',
            'pharma' => 'pharma', 'telecom' => 'telecom', 'energy' => 'energy',
            'transport' => 'transport', 'mfg' => 'mfg', 'consumer' => 'consumer',
            'govt' => 'govt', 'stats' => 'stats', 'crisis' => 'crisis', 'sector' => 'sector',
        );
        return $map[$cat] ?? 'corp';
    }
}

if (!function_exists('epc_ext_authority')) {
    /**
     * Resolve the governing authority + law reference + official format/portal
     * link for a (country, domain). UAE is fully specified; other major
     * jurisdictions have their key regulators; everything else falls back to a
     * sensible global standard body + the country's e-government portal.
     *
     * @return array{name:string,law:string,url:string,format:string}
     */
    function epc_ext_authority(string $country, string $domain): array
    {
        $c = strtoupper(trim($country));

        // UAE — the tenant-country sub-layer with precise sources.
        $uae = array(
            'corp'   => array('Ministry of Economy (MoEC) / licensing authority', 'UAE Commercial Companies Law — Federal Decree-Law 32/2021', 'https://www.moec.gov.ae', 'https://www.moec.gov.ae/en/commercial-register'),
            'tax'    => array('Federal Tax Authority (FTA)', 'Corporate Tax — Federal Decree-Law 47/2022; VAT — Federal Decree-Law 8/2017 (as amended, incl. FDL 16/2025 Art. 74 5-year excess recoverable limit from 1 Jan 2026) + Exec. Regulations; Tax Procedures FDL 28/2022 (as amended by FDL 17/2025)', 'https://tax.gov.ae', 'https://eservices.tax.gov.ae'),
            'fin'    => array('Securities & Commodities Authority (SCA) / IFRS Foundation', 'IFRS as adopted in the UAE — IFRS 18 early applied for FY2026+ (mandatory periods beginning on/after 1 Jan 2027); IAS 1 superseded for presentation & disclosure', 'https://www.sca.gov.ae', 'https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/'),
            'audit'  => array('Ministry of Economy — Auditors Register / IAASB', 'International Standards on Auditing (ISA)', 'https://www.moec.gov.ae', 'https://www.iaasb.org/standards-pronouncements'),
            'hr'     => array('Ministry of Human Resources & Emiratisation (MOHRE)', 'UAE Labour Law — Federal Decree-Law 33/2021 (as amended by FDL 20/2023 & FDL 9/2024); WPS — Ministerial Resolution 340/2026 (from 1 Jun 2026; replaces MR 598/2022)', 'https://www.mohre.gov.ae', 'https://www.mohre.gov.ae/en/services.aspx'),
            'aml'    => array('UAE Financial Intelligence Unit (goAML) / EOCN', 'Anti-Money Laundering — Federal Decree-Law 20/2018', 'https://www.uaefiu.gov.ae', 'https://services.uaefiu.gov.ae'),
            'bank'   => array('Central Bank of the UAE (CBUAE)', 'Decretal Federal Law 14/2018', 'https://www.centralbank.ae', 'https://www.centralbank.ae/en/cbuae-regulation/'),
            'ins'    => array('Central Bank of the UAE — Insurance', 'Insurance Law — Federal Law 6/2007 (as amended)', 'https://www.centralbank.ae', 'https://www.centralbank.ae/en/our-operations/insurance/'),
            'sec'    => array('Securities & Commodities Authority (SCA)', 'Federal Law 4/2000 (as amended)', 'https://www.sca.gov.ae', 'https://www.sca.gov.ae/en/services.aspx'),
            'customs'=> array('Federal Customs Authority', 'GCC Common Customs Law', 'https://www.fca.gov.ae', 'https://www.dubaicustoms.gov.ae'),
            'esg'    => array('SCA / ISSB / market ESG guidance (DFM, ADX)', 'IFRS S1 & S2 sustainability standards', 'https://www.sca.gov.ae', 'https://www.ifrs.org/sustainability/'),
            'env'    => array('Ministry of Climate Change & Environment (MOCCAE)', 'Federal Law 24/1999 on environment protection', 'https://www.moccae.gov.ae', 'https://www.moccae.gov.ae/en/services.aspx'),
            'hs'     => array('MOHRE — Occupational Health & Safety', 'OSH provisions, Federal Decree-Law 33/2021', 'https://www.mohre.gov.ae', 'https://www.mohre.gov.ae/en/services.aspx'),
            'data'   => array('UAE Data Office / TDRA', 'Personal Data Protection — Federal Decree-Law 45/2021', 'https://www.tdra.gov.ae', 'https://u.ae/en/about-the-uae/digital-uae/data/data-protection-laws'),
            're'     => array('Land Department / RERA (emirate level)', 'Real-estate registration laws (emirate level)', 'https://dubailand.gov.ae', 'https://dubailand.gov.ae/en/eservices/'),
            'health' => array('Ministry of Health & Prevention (MOHAP) / DHA / DoH', 'Federal Law 4/2016 on medical liability', 'https://mohap.gov.ae', 'https://mohap.gov.ae/en/services'),
            'pharma' => array('MOHAP — Drug Department', 'Federal Law 8/2019 on medical products', 'https://mohap.gov.ae', 'https://mohap.gov.ae/en/services'),
            'telecom'=> array('Telecommunications & Digital Government Regulatory Authority (TDRA)', 'Federal Law by Decree 3/2003 (Telecom)', 'https://tdra.gov.ae', 'https://tdra.gov.ae/en/about-tdra'),
            'energy' => array('Ministry of Energy & Infrastructure (MOEI)', 'Energy & utilities regulations', 'https://www.moei.gov.ae', 'https://www.moei.gov.ae/en/services.aspx'),
            'transport' => array('GCAA (aviation) / Federal Transport Authority', 'Civil aviation & transport regulations', 'https://www.gcaa.gov.ae', 'https://www.gcaa.gov.ae'),
            'mfg'    => array('Ministry of Industry & Advanced Technology (MOIAT) / ESMA', 'UAE conformity & standards regulations', 'https://moiat.gov.ae', 'https://moiat.gov.ae/en/services'),
            'consumer' => array('Ministry of Economy — Consumer Protection', 'Federal Law 15/2020 on consumer protection', 'https://www.moec.gov.ae', 'https://www.consumerrights.ae'),
            'govt'   => array('Ministry of Finance — Federal procurement', 'Federal procurement regulations', 'https://www.mof.gov.ae', 'https://www.mof.gov.ae/en/resourcesAndBudget/Pages/procurement.aspx'),
            'stats'  => array('Federal Competitiveness & Statistics Centre (FCSC)', 'Federal statistics law', 'https://fcsc.gov.ae', 'https://fcsc.gov.ae/en-us'),
            'crisis' => array('National Emergency Crisis & Disasters Management Authority (NCEMA)', 'Federal Law 2/2011', 'https://www.ncema.gov.ae', 'https://www.ncema.gov.ae'),
            'sector' => array('Sector regulator / Ministry of Economy', 'Sector-specific UAE regulations', 'https://www.moec.gov.ae', 'https://u.ae/en/information-and-services'),
        );
        if ($c === 'AE' && isset($uae[$domain])) {
            $r = $uae[$domain];
            return array('name' => $r[0], 'law' => $r[1], 'url' => $r[2], 'format' => $r[3]);
        }

        // Worldwide: global standard bodies per domain (apply everywhere).
        $global = array(
            'fin'    => array('IFRS Foundation', 'IFRS Accounting Standards — IFRS 18 Presentation and Disclosure in Financial Statements (early application permitted; mandatory for periods beginning on/after 1 Jan 2027)', 'https://www.ifrs.org', 'https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/'),
            'audit'  => array('IAASB', 'International Standards on Auditing (ISA)', 'https://www.iaasb.org', 'https://www.iaasb.org/standards-pronouncements'),
            'tax'    => array('OECD / national tax authority', 'OECD model tax framework + local tax law', 'https://www.oecd.org/tax/', 'https://www.oecd.org/tax/forum-on-tax-administration/'),
            'aml'    => array('FATF / national FIU', 'FATF 40 Recommendations', 'https://www.fatf-gafi.org', 'https://www.fatf-gafi.org/en/topics/fatf-recommendations.html'),
            'esg'    => array('ISSB / GRI', 'IFRS S1 & S2; GRI Standards', 'https://www.ifrs.org/sustainability/', 'https://www.globalreporting.org/standards/'),
            'bank'   => array('Basel Committee (BIS) / central bank', 'Basel III framework', 'https://www.bis.org/bcbs/', 'https://www.bis.org/basel_framework/'),
            'ins'    => array('IAIS / national regulator', 'Insurance Core Principles (ICP)', 'https://www.iaisweb.org', 'https://www.iaisweb.org/icp-online-tool/'),
            'sec'    => array('IOSCO / national securities regulator', 'IOSCO principles', 'https://www.iosco.org', 'https://www.iosco.org/library/'),
            'customs'=> array('World Customs Organization / national customs', 'WCO / WTO trade framework', 'https://www.wcoomd.org', 'https://www.wcoomd.org'),
            'data'   => array('National data-protection authority', 'GDPR-style data-protection law', 'https://gdpr.eu', 'https://gdpr.eu'),
        );
        if (isset($global[$domain])) {
            $r = $global[$domain];
            // Prefix national context where we know the country name.
            $name = $r[0];
            return array('name' => $name, 'law' => $r[1], 'url' => $r[2], 'format' => $r[3]);
        }

        // Fallback: the country's e-government / standard reference.
        $prof = function_exists('epc_country_profile') ? epc_country_profile($c) : array('name' => $c);
        $cname = (string) ($prof['name'] ?? $c);
        return array(
            'name' => $cname . ' — national regulator',
            'law' => 'Applicable national legislation (' . $cname . ')',
            'url' => 'https://www.google.com/search?q=' . rawurlencode($cname . ' government ' . $domain . ' regulator official'),
            'format' => 'https://www.google.com/search?q=' . rawurlencode($cname . ' official reporting format ' . $domain),
        );
    }
}

if (!function_exists('epc_ext_ifrs_link')) {
    /**
     * IFRS / international-standard reference link for a standard id.
     *
     * @return array{label:string,url:string}|null
     */
    function epc_ext_ifrs_link(string $std): ?array
    {
        if ($std === '') {
            return null;
        }
        $map = array(
            'IFRS18' => array('IFRS 18 — Presentation and Disclosure in Financial Statements', 'https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/'),
            'IAS1'   => array('IAS 1 — Presentation of Financial Statements', 'https://www.ifrs.org/issued-standards/list-of-standards/ias-1-presentation-of-financial-statements/'),
            'IAS34'  => array('IAS 34 — Interim Financial Reporting', 'https://www.ifrs.org/issued-standards/list-of-standards/ias-34-interim-financial-reporting/'),
            'IFRS10' => array('IFRS 10 — Consolidated Financial Statements', 'https://www.ifrs.org/issued-standards/list-of-standards/ifrs-10-consolidated-financial-statements/'),
            'IFRS7'  => array('IFRS 7 — Financial Instruments: Disclosures', 'https://www.ifrs.org/issued-standards/list-of-standards/ifrs-7-financial-instruments-disclosures/'),
            'ISA700' => array('ISA 700 — Forming an Opinion & Reporting on Financial Statements', 'https://www.iaasb.org/publications/international-standard-auditing-isa-700-revised-forming-opinion-and-reporting-financial'),
            'ISA705' => array('ISA 705 — Modifications to the Opinion in the Auditor\'s Report', 'https://www.iaasb.org/publications/international-standard-auditing-isa-705-revised'),
            'ISSB'   => array('ISSB — IFRS S1 General Sustainability Disclosures', 'https://www.ifrs.org/issued-standards/ifrs-sustainability-standards-navigator/ifrs-s1-general-requirements/'),
            'IFRS_S2'=> array('IFRS S2 — Climate-related Disclosures', 'https://www.ifrs.org/issued-standards/ifrs-sustainability-standards-navigator/ifrs-s2-climate-related-disclosures/'),
        );
        if (!isset($map[$std])) {
            return null;
        }
        return array('label' => $map[$std][0], 'url' => $map[$std][1]);
    }
}

if (!function_exists('epc_ext_report_links')) {
    /**
     * The resolved law / format / IFRS links for a report under a country.
     *
     * @return array{authority:array,ifrs:?array}
     */
    function epc_ext_report_links(string $key, string $country): array
    {
        $def = epc_ext_report_get($key);
        $cat = $def['cat'] ?? 'corp';
        $std = $def['std'] ?? '';
        $domain = epc_ext_domain_for_category($cat);
        return array(
            'authority' => epc_ext_authority($country, $domain),
            'ifrs' => epc_ext_ifrs_link($std),
        );
    }
}

if (!function_exists('epc_ext_country_name')) {
    /**
     * Display name for an ISO-2 country code (independent of whether a full
     * localization pack exists), so preview labels read correctly worldwide.
     */
    function epc_ext_country_name(string $country): string
    {
        $c = strtoupper(trim($country));
        $names = array(
            'AE' => 'United Arab Emirates', 'SA' => 'Saudi Arabia', 'QA' => 'Qatar', 'OM' => 'Oman',
            'BH' => 'Bahrain', 'KW' => 'Kuwait', 'IN' => 'India', 'PK' => 'Pakistan',
            'BD' => 'Bangladesh', 'LK' => 'Sri Lanka', 'NP' => 'Nepal', 'SG' => 'Singapore',
            'MY' => 'Malaysia', 'ID' => 'Indonesia', 'PH' => 'Philippines', 'TH' => 'Thailand',
            'CN' => 'China', 'HK' => 'Hong Kong', 'JP' => 'Japan', 'KR' => 'South Korea',
            'GB' => 'United Kingdom', 'US' => 'United States', 'CA' => 'Canada', 'DE' => 'Germany',
            'FR' => 'France', 'NL' => 'Netherlands', 'IE' => 'Ireland', 'AU' => 'Australia',
            'NZ' => 'New Zealand', 'ZA' => 'South Africa', 'NG' => 'Nigeria', 'KE' => 'Kenya',
            'EG' => 'Egypt', 'JO' => 'Jordan', 'LB' => 'Lebanon', 'MA' => 'Morocco', 'TR' => 'Turkey',
        );
        if (isset($names[$c])) {
            return $names[$c];
        }
        $prof = function_exists('epc_country_profile') ? epc_country_profile($c) : array();
        $n = (string) ($prof['name'] ?? '');
        return ($n !== '' && strcasecmp($n, 'Generic') !== 0) ? $n : $c;
    }
}

if (!function_exists('epc_ext_report_frequency')) {
    function epc_ext_report_frequency(string $cat): string
    {
        $map = array(
            'tax' => 'Periodic (monthly / quarterly / annual per regime)',
            'fin' => 'Annual / interim',
            'audit' => 'Annual',
            'hr' => 'Monthly / annual',
            'aml' => 'Event-driven + periodic',
            'corp' => 'On event / annual return',
        );
        return $map[$cat] ?? 'Periodic / on event';
    }
}

if (!function_exists('epc_ext_report_period_type')) {
    /**
     * The reporting-period granularity for a given report — so each return is
     * scoped to its own statutory period (VAT = tax quarter, CT = financial
     * year, WPS/payroll = month, financial statements = year, etc.).
     *
     * @return string one of 'month' | 'quarter' | 'year'
     */
    function epc_ext_report_period_type(string $cat, string $key): string
    {
        $k = strtolower($key);
        // Key-level overrides (most specific first).
        if (strpos($k, 'vat_return') !== false || strpos($k, 'gst_return') !== false
            || strpos($k, 'sales_tax') !== false || strpos($k, 'use_tax') !== false) {
            return 'quarter';
        }
        if (strpos($k, 'corporate_income_tax') !== false || strpos($k, 'corporate_tax') !== false
            || strpos($k, 'transfer_pricing') !== false || strpos($k, 'country_by_country') !== false
            || strpos($k, 'capital_gains') !== false || strpos($k, 'annual_return') !== false
            || strpos($k, 'annual_financial') !== false || strpos($k, 'consolidated') !== false) {
            return 'year';
        }
        if (strpos($k, 'interim') !== false) {
            return 'quarter';
        }
        if (strpos($k, 'excise') !== false || strpos($k, 'withholding') !== false
            || strpos($k, 'wage_protection') !== false || strpos($k, 'wps') !== false
            || strpos($k, 'payroll') !== false || strpos($k, 'customs') !== false) {
            return 'month';
        }
        // Category defaults.
        $map = array(
            'tax' => 'quarter', 'fin' => 'year', 'audit' => 'year', 'hr' => 'month',
            'aml' => 'month', 'bank' => 'quarter', 'ins' => 'quarter', 'sec' => 'quarter',
            'customs' => 'month', 'esg' => 'year', 'env' => 'year', 'hs' => 'month',
            'data' => 'month', 're' => 'year', 'health' => 'month', 'pharma' => 'year',
            'telecom' => 'quarter', 'energy' => 'month', 'transport' => 'month',
            'mfg' => 'month', 'consumer' => 'month', 'govt' => 'year', 'stats' => 'year',
            'crisis' => 'month', 'sector' => 'year', 'corp' => 'year',
        );
        return $map[$cat] ?? 'year';
    }
}

if (!function_exists('epc_ext_period_bases')) {
    /**
     * The period bases a user may pick for a report, ordered with the report's
     * natural/statutory basis first. Lets filers choose a different cadence
     * (e.g. a monthly VAT filer, or a company with a non-calendar CT year)
     * without leaving the report. 'custom' is always available.
     *
     * @return array<int,string> ordered subset of month|quarter|year|custom
     */
    function epc_ext_period_bases(string $natural): array
    {
        $labels = array('month', 'quarter', 'year');
        $ordered = array($natural);
        foreach ($labels as $l) {
            if ($l !== $natural) {
                $ordered[] = $l;
            }
        }
        $ordered[] = 'custom';
        return $ordered;
    }
}

if (!function_exists('epc_ext_resolve_period')) {
    /**
     * Resolve a concrete reporting period (from/to timestamps + label + preset
     * options) for a period type. The selected token is validated; otherwise the
     * current period is used. So every report fetches only its own period's data.
     *
     * @return array{type:string,token:string,label:string,from:int,to:int,options:array<string,string>}
     */
    function epc_ext_resolve_period(string $type, ?string $sel, int $refTs = 0, array $custom = array()): array
    {
        $ref = $refTs > 0 ? $refTs : time();
        $y = (int) date('Y', $ref);
        $m = (int) date('n', $ref);

        $mk = function (int $fy, int $fm, int $fd, int $ty, int $tm, int $td): array {
            return array(mktime(0, 0, 0, $fm, $fd, $fy), mktime(23, 59, 59, $tm, $td, $ty));
        };

        // Custom range — any from/to the user picks (e.g. a non-calendar
        // financial year, a transitional first CT period, or an ad-hoc range).
        if ($type === 'custom') {
            $f = (int) ($custom['from'] ?? 0);
            $t = (int) ($custom['to'] ?? 0);
            if ($f <= 0 || $t <= 0 || $t < $f) {
                $last = (int) date('t', $ref);
                $f = mktime(0, 0, 0, $m, 1, $y);
                $t = mktime(23, 59, 59, $m, $last, $y);
            } else {
                $f = mktime(0, 0, 0, (int) date('n', $f), (int) date('j', $f), (int) date('Y', $f));
                $t = mktime(23, 59, 59, (int) date('n', $t), (int) date('j', $t), (int) date('Y', $t));
            }
            return array(
                'type' => 'custom',
                'token' => 'custom',
                'label' => date('d M Y', $f) . ' — ' . date('d M Y', $t),
                'from' => $f,
                'to' => $t,
                'options' => array('custom' => 'Custom range…'),
            );
        }

        $options = array();
        $build = function (string $tok) use ($type, $mk): array {
            if ($type === 'month') {
                $yy = (int) substr($tok, 0, 4);
                $mm = (int) substr($tok, 5, 2);
                $last = (int) date('t', mktime(0, 0, 0, $mm, 1, $yy));
                list($f, $t) = $mk($yy, $mm, 1, $yy, $mm, $last);
                return array('label' => date('M Y', $f), 'from' => $f, 'to' => $t);
            }
            if ($type === 'quarter') {
                $yy = (int) substr($tok, 0, 4);
                $q = (int) substr($tok, 6, 1);
                $fm = ($q - 1) * 3 + 1;
                $tm = $fm + 2;
                $last = (int) date('t', mktime(0, 0, 0, $tm, 1, $yy));
                list($f, $t) = $mk($yy, $fm, 1, $yy, $tm, $last);
                return array('label' => 'Q' . $q . ' ' . $yy, 'from' => $f, 'to' => $t);
            }
            $yy = (int) substr($tok, 0, 4);
            list($f, $t) = $mk($yy, 1, 1, $yy, 12, 31);
            return array('label' => 'FY' . $yy, 'from' => $f, 'to' => $t);
        };

        // Build the option list (current period first, then previous ones).
        if ($type === 'month') {
            for ($i = 0; $i < 12; $i++) {
                $tok = date('Y-m', mktime(0, 0, 0, $m - $i, 1, $y));
                $options[$tok] = $build($tok)['label'];
            }
            $cur = date('Y-m', $ref);
        } elseif ($type === 'quarter') {
            $cq = (int) ceil($m / 3);
            for ($i = 0; $i < 6; $i++) {
                $qq = $cq - $i;
                $yy = $y;
                while ($qq <= 0) { $qq += 4; $yy--; }
                $tok = $yy . '-Q' . $qq;
                $options[$tok] = $build($tok)['label'];
            }
            $cur = $y . '-Q' . $cq;
        } else {
            for ($i = 0; $i < 5; $i++) {
                $tok = (string) ($y - $i);
                $options[$tok] = $build($tok)['label'];
            }
            $cur = (string) $y;
        }

        $tok = ($sel !== null && $sel !== '' && isset($options[$sel])) ? $sel : $cur;
        if (!isset($options[$tok])) {
            $options = array($tok => $build($tok)['label']) + $options;
        }
        $r = $build($tok);
        return array(
            'type' => $type,
            'token' => $tok,
            'label' => $r['label'],
            'from' => $r['from'],
            'to' => $r['to'],
            'options' => $options,
        );
    }
}

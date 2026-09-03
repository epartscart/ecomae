namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_ext_reports_*</c> catalogue — categories, report keys, authority, country labels.</summary>
public static class ErpExternalReportingCatalog
{
    public sealed record Category(string Key, string Label);
    public sealed record Report(string Key, string Name, string Cat, string Builder, string Std);
    public sealed record Authority(string Name, string Law, string Url, string Format);
    public sealed record IfrsLink(string Label, string Url);
    public sealed record PeriodOption(string Token, string Label);
    public sealed record Period(string Type, string Token, string Label, DateTime From, DateTime To, IReadOnlyList<PeriodOption> Options);

    public static readonly IReadOnlyList<string> PreviewCountries =
        ["AE","SA","QA","OM","BH","KW","IN","PK","BD","LK","SG","MY","GB","US","DE","FR","AU","ZA","EG","TR"];

    public static readonly IReadOnlyList<Category> Categories =
    [
        new("corp", "Corporate Registration & Legal Reporting"),
        new("tax", "Tax Reporting"),
        new("fin", "Financial Reporting"),
        new("audit", "External Audit & Assurance Reporting"),
        new("hr", "Employment & HR Reporting"),
        new("aml", "AML / Financial Crime Reporting"),
        new("bank", "Banking Reporting"),
        new("ins", "Insurance Reporting"),
        new("sec", "Investment & Securities Reporting"),
        new("customs", "Customs & International Trade Reporting"),
        new("esg", "ESG & Sustainability Reporting"),
        new("env", "Environmental Reporting"),
        new("hs", "Health & Safety Reporting"),
        new("data", "Data Privacy & Cybersecurity Reporting"),
        new("re", "Real Estate Reporting"),
        new("health", "Healthcare Reporting"),
        new("pharma", "Pharmaceutical Reporting"),
        new("telecom", "Telecommunications Reporting"),
        new("energy", "Energy & Utilities Reporting"),
        new("transport", "Transportation & Logistics Reporting"),
        new("mfg", "Manufacturing Reporting"),
        new("consumer", "Consumer Protection Reporting"),
        new("govt", "Government Contract Reporting"),
        new("stats", "Statistical & Economic Reporting"),
        new("crisis", "Crisis & Incident Reporting"),
        new("sector", "Sector-Specific Regulatory Reporting"),
    ];

    public static readonly IReadOnlyList<Report> Reports =
    [
        new("corp__company_incorporation_filing", "Company Incorporation Filing", "corp", "", ""),
        new("corp__branch_registration_filing", "Branch Registration Filing", "corp", "", ""),
        new("corp__foreign_company_registration", "Foreign Company Registration", "corp", "", ""),
        new("corp__annual_return_filing", "Annual Return Filing", "corp", "annual_return", ""),
        new("corp__trade_license_renewal", "Trade License Renewal", "corp", "trade_license", ""),
        new("corp__business_activity_amendment", "Business Activity Amendment", "corp", "", ""),
        new("corp__registered_address_change", "Registered Address Change", "corp", "", ""),
        new("corp__director_appointment_filing", "Director Appointment Filing", "corp", "", ""),
        new("corp__director_resignation_filing", "Director Resignation Filing", "corp", "", ""),
        new("corp__secretary_appointment_filing", "Secretary Appointment Filing", "corp", "", ""),
        new("corp__shareholder_change_filing", "Shareholder Change Filing", "corp", "", ""),
        new("corp__capital_change_filing", "Capital Change Filing", "corp", "", ""),
        new("corp__company_dissolution_filing", "Company Dissolution Filing", "corp", "", ""),
        new("corp__liquidation_reporting", "Liquidation Reporting", "corp", "", ""),
        new("corp__merger_filing", "Merger Filing", "corp", "", ""),
        new("corp__acquisition_filing", "Acquisition Filing", "corp", "", ""),
        new("corp__corporate_restructuring_filing", "Corporate Restructuring Filing", "corp", "", ""),
        new("corp__beneficial_ownership_filing", "Beneficial Ownership Filing", "corp", "ubo", ""),
        new("corp__ubo_reporting", "UBO Reporting", "corp", "ubo", ""),
        new("corp__corporate_governance_reporting", "Corporate Governance Reporting", "corp", "", ""),
        new("tax__corporate_income_tax_return", "Corporate Income Tax Return", "tax", "corporate_tax", ""),
        new("tax__corporate_tax_registration", "Corporate Tax Registration", "tax", "", ""),
        new("tax__corporate_tax_deregistration", "Corporate Tax Deregistration", "tax", "", ""),
        new("tax__vat_return", "VAT Return", "tax", "vat_return", ""),
        new("tax__vat_registration", "VAT Registration", "tax", "", ""),
        new("tax__vat_deregistration", "VAT Deregistration", "tax", "", ""),
        new("tax__vat_refund_claim", "VAT Refund Claim", "tax", "vat_refund", ""),
        new("tax__tourist_vat_refund_reporting", "Tourist VAT Refund Reporting", "tax", "", ""),
        new("tax__gst_return", "GST Return", "tax", "vat_return", ""),
        new("tax__sales_tax_return", "Sales Tax Return", "tax", "vat_return", ""),
        new("tax__use_tax_return", "Use Tax Return", "tax", "", ""),
        new("tax__excise_tax_return", "Excise Tax Return", "tax", "excise", ""),
        new("tax__customs_duty_reporting", "Customs Duty Reporting", "tax", "", ""),
        new("tax__property_tax_reporting", "Property Tax Reporting", "tax", "", ""),
        new("tax__payroll_tax_reporting", "Payroll Tax Reporting", "tax", "payroll_tax", ""),
        new("tax__withholding_tax_reporting", "Withholding Tax Reporting", "tax", "wht", ""),
        new("tax__dividend_tax_reporting", "Dividend Tax Reporting", "tax", "", ""),
        new("tax__capital_gains_tax_reporting", "Capital Gains Tax Reporting", "tax", "", ""),
        new("tax__transfer_pricing_disclosure", "Transfer Pricing Disclosure", "tax", "", ""),
        new("tax__transfer_pricing_local_file", "Transfer Pricing Local File", "tax", "", ""),
        new("tax__transfer_pricing_master_file", "Transfer Pricing Master File", "tax", "", ""),
        new("tax__country_by_country_reporting", "Country-by-Country Reporting", "tax", "cbcr", ""),
        new("tax__digital_services_tax_reporting", "Digital Services Tax Reporting", "tax", "", ""),
        new("tax__environmental_tax_reporting", "Environmental Tax Reporting", "tax", "", ""),
        new("tax__carbon_tax_reporting", "Carbon Tax Reporting", "tax", "", ""),
        new("tax__stamp_duty_reporting", "Stamp Duty Reporting", "tax", "", ""),
        new("tax__municipal_tax_reporting", "Municipal Tax Reporting", "tax", "", ""),
        new("tax__tax_audit_response_filing", "Tax Audit Response Filing", "tax", "", ""),
        new("fin__annual_financial_statements", "Annual Financial Statements", "fin", "afs", "IFRS18"),
        new("fin__interim_financial_statements", "Interim Financial Statements", "fin", "interim", "IAS34"),
        new("fin__consolidated_financial_statements", "Consolidated Financial Statements", "fin", "consolidated", "IFRS10"),
        new("fin__statutory_accounts_filing", "Statutory Accounts Filing", "fin", "afs", "IFRS18"),
        new("fin__ifrs_reporting", "IFRS Reporting", "fin", "afs", "IFRS18"),
        new("fin__gaap_reporting", "GAAP Reporting", "fin", "afs", "IFRS18"),
        new("fin__audit_report_filing", "Audit Report Filing", "fin", "audit_report", "ISA700"),
        new("fin__qualified_audit_disclosure", "Qualified Audit Disclosure", "fin", "", "ISA705"),
        new("fin__financial_model_forecast", "Financial Model & Forecast", "fin", "fin_model", "IFRS"),
        new("fin__business_valuation_report", "Business Valuation Report", "fin", "valuation", "IVS"),
        new("fin__internal_control_reporting", "Internal Control Reporting", "fin", "", ""),
        new("fin__financial_risk_reporting", "Financial Risk Reporting", "fin", "", "IFRS7"),
        new("fin__treasury_reporting", "Treasury Reporting", "fin", "", ""),
        new("fin__capital_adequacy_reporting", "Capital Adequacy Reporting", "fin", "", ""),
        new("fin__liquidity_reporting", "Liquidity Reporting", "fin", "", ""),
        new("fin__solvency_reporting", "Solvency Reporting", "fin", "", ""),
        new("audit__external_audit_report", "External Audit Report", "audit", "audit_report", "ISA700"),
        new("audit__internal_audit_report", "Internal Audit Report", "audit", "", ""),
        new("audit__compliance_audit_report", "Compliance Audit Report", "audit", "", ""),
        new("audit__tax_audit_report", "Tax Audit Report", "audit", "", ""),
        new("audit__forensic_audit_report", "Forensic Audit Report", "audit", "", ""),
        new("audit__operational_audit_report", "Operational Audit Report", "audit", "", ""),
        new("audit__it_audit_report", "IT Audit Report", "audit", "", ""),
        new("audit__cybersecurity_audit_report", "Cybersecurity Audit Report", "audit", "", ""),
        new("audit__esg_assurance_report", "ESG Assurance Report", "audit", "", "ISSB"),
        new("audit__sustainability_assurance_report", "Sustainability Assurance Report", "audit", "", "ISSB"),
        new("hr__payroll_reporting", "Payroll Reporting", "hr", "payroll_tax", ""),
        new("hr__wage_protection_reporting", "Wage Protection Reporting", "hr", "wps", ""),
        new("hr__employee_census_reporting", "Employee Census Reporting", "hr", "employee_census", ""),
        new("hr__labor_compliance_reporting", "Labor Compliance Reporting", "hr", "", ""),
        new("hr__work_permit_reporting", "Work Permit Reporting", "hr", "", ""),
        new("hr__visa_compliance_reporting", "Visa Compliance Reporting", "hr", "", ""),
        new("hr__pension_reporting", "Pension Reporting", "hr", "", ""),
        new("hr__social_security_reporting", "Social Security Reporting", "hr", "", ""),
        new("hr__end_of_service_reporting", "End-of-Service Reporting", "hr", "eos", ""),
        new("hr__workforce_diversity_reporting", "Workforce Diversity Reporting", "hr", "", ""),
        new("hr__gender_pay_gap_reporting", "Gender Pay Gap Reporting", "hr", "", ""),
        new("hr__occupational_safety_reporting", "Occupational Safety Reporting", "hr", "", ""),
        new("hr__workplace_injury_reporting", "Workplace Injury Reporting", "hr", "", ""),
        new("hr__workers_compensation_reporting", "Workers Compensation Reporting", "hr", "", ""),
        new("aml__aml_compliance_reporting", "AML Compliance Reporting", "aml", "", ""),
        new("aml__kyc_compliance_reporting", "KYC Compliance Reporting", "aml", "", ""),
        new("aml__customer_due_diligence_reporting", "Customer Due Diligence Reporting", "aml", "", ""),
        new("aml__enhanced_due_diligence_reporting", "Enhanced Due Diligence Reporting", "aml", "", ""),
        new("aml__suspicious_activity_report_sar", "Suspicious Activity Report (SAR)", "aml", "aml", ""),
        new("aml__suspicious_transaction_report_str", "Suspicious Transaction Report (STR)", "aml", "aml", ""),
        new("aml__suspicious_fund_transfer_report", "Suspicious Fund Transfer Report", "aml", "aml", ""),
        new("aml__terrorist_financing_report", "Terrorist Financing Report", "aml", "", ""),
        new("aml__sanctions_screening_report", "Sanctions Screening Report", "aml", "", ""),
        new("aml__politically_exposed_person_reporting", "Politically Exposed Person Reporting", "aml", "", ""),
        new("aml__fraud_reporting", "Fraud Reporting", "aml", "", ""),
        new("aml__anti_bribery_reporting", "Anti-Bribery Reporting", "aml", "", ""),
        new("aml__anti_corruption_reporting", "Anti-Corruption Reporting", "aml", "", ""),
        new("aml__financial_crime_risk_reporting", "Financial Crime Risk Reporting", "aml", "", ""),
        new("bank__prudential_reporting", "Prudential Reporting", "bank", "", ""),
        new("bank__basel_reporting", "Basel Reporting", "bank", "", ""),
        new("bank__capital_adequacy_reporting", "Capital Adequacy Reporting", "bank", "", ""),
        new("bank__liquidity_coverage_reporting", "Liquidity Coverage Reporting", "bank", "", ""),
        new("bank__stress_testing_reporting", "Stress Testing Reporting", "bank", "", ""),
        new("bank__credit_risk_reporting", "Credit Risk Reporting", "bank", "", ""),
        new("bank__market_risk_reporting", "Market Risk Reporting", "bank", "", ""),
        new("bank__operational_risk_reporting", "Operational Risk Reporting", "bank", "", ""),
        new("bank__large_exposure_reporting", "Large Exposure Reporting", "bank", "", ""),
        new("bank__loan_portfolio_reporting", "Loan Portfolio Reporting", "bank", "", ""),
        new("bank__deposit_reporting", "Deposit Reporting", "bank", "", ""),
        new("bank__central_bank_reporting", "Central Bank Reporting", "bank", "", ""),
        new("ins__solvency_reporting", "Solvency Reporting", "ins", "", ""),
        new("ins__claims_reporting", "Claims Reporting", "ins", "", ""),
        new("ins__actuarial_reporting", "Actuarial Reporting", "ins", "", ""),
        new("ins__reinsurance_reporting", "Reinsurance Reporting", "ins", "", ""),
        new("ins__insurance_reserve_reporting", "Insurance Reserve Reporting", "ins", "", ""),
        new("ins__regulatory_insurance_reporting", "Regulatory Insurance Reporting", "ins", "", ""),
        new("sec__prospectus_filing", "Prospectus Filing", "sec", "", ""),
        new("sec__securities_offering_reporting", "Securities Offering Reporting", "sec", "", ""),
        new("sec__insider_trading_reporting", "Insider Trading Reporting", "sec", "", ""),
        new("sec__market_abuse_reporting", "Market Abuse Reporting", "sec", "", ""),
        new("sec__shareholding_disclosure", "Shareholding Disclosure", "sec", "", ""),
        new("sec__fund_reporting", "Fund Reporting", "sec", "", ""),
        new("sec__investment_position_reporting", "Investment Position Reporting", "sec", "", ""),
        new("sec__asset_management_reporting", "Asset Management Reporting", "sec", "", ""),
        new("sec__portfolio_reporting", "Portfolio Reporting", "sec", "", ""),
        new("sec__derivatives_reporting", "Derivatives Reporting", "sec", "", ""),
        new("sec__trade_repository_reporting", "Trade Repository Reporting", "sec", "", ""),
        new("customs__import_declaration", "Import Declaration", "customs", "", ""),
        new("customs__export_declaration", "Export Declaration", "customs", "", ""),
        new("customs__customs_declaration", "Customs Declaration", "customs", "", ""),
        new("customs__trade_statistics_reporting", "Trade Statistics Reporting", "customs", "", ""),
        new("customs__free_zone_reporting", "Free Zone Reporting", "customs", "", ""),
        new("customs__certificate_of_origin_reporting", "Certificate of Origin Reporting", "customs", "", ""),
        new("customs__sanctions_trade_reporting", "Sanctions Trade Reporting", "customs", "", ""),
        new("customs__export_control_reporting", "Export Control Reporting", "customs", "", ""),
        new("customs__dual_use_goods_reporting", "Dual-Use Goods Reporting", "customs", "", ""),
        new("esg__esg_reporting", "ESG Reporting", "esg", "", "ISSB"),
        new("esg__sustainability_reporting", "Sustainability Reporting", "esg", "", "ISSB"),
        new("esg__carbon_emissions_reporting", "Carbon Emissions Reporting", "esg", "", "IFRS_S2"),
        new("esg__greenhouse_gas_reporting", "Greenhouse Gas Reporting", "esg", "", "IFRS_S2"),
        new("esg__climate_risk_reporting", "Climate Risk Reporting", "esg", "", "IFRS_S2"),
        new("esg__energy_consumption_reporting", "Energy Consumption Reporting", "esg", "", ""),
        new("esg__water_usage_reporting", "Water Usage Reporting", "esg", "", ""),
        new("esg__waste_management_reporting", "Waste Management Reporting", "esg", "", ""),
        new("esg__biodiversity_reporting", "Biodiversity Reporting", "esg", "", ""),
        new("esg__net_zero_reporting", "Net-Zero Reporting", "esg", "", "IFRS_S2"),
        new("esg__sustainable_finance_reporting", "Sustainable Finance Reporting", "esg", "", "ISSB"),
        new("env__environmental_impact_reporting", "Environmental Impact Reporting", "env", "", ""),
        new("env__pollution_reporting", "Pollution Reporting", "env", "", ""),
        new("env__air_emissions_reporting", "Air Emissions Reporting", "env", "", ""),
        new("env__hazardous_waste_reporting", "Hazardous Waste Reporting", "env", "", ""),
        new("env__chemical_usage_reporting", "Chemical Usage Reporting", "env", "", ""),
        new("env__environmental_incident_reporting", "Environmental Incident Reporting", "env", "", ""),
        new("env__environmental_permit_reporting", "Environmental Permit Reporting", "env", "", ""),
        new("hs__occupational_health_reporting", "Occupational Health Reporting", "hs", "", ""),
        new("hs__workplace_safety_reporting", "Workplace Safety Reporting", "hs", "", ""),
        new("hs__accident_reporting", "Accident Reporting", "hs", "", ""),
        new("hs__injury_reporting", "Injury Reporting", "hs", "", ""),
        new("hs__fatality_reporting", "Fatality Reporting", "hs", "", ""),
        new("hs__hazard_reporting", "Hazard Reporting", "hs", "", ""),
        new("hs__safety_inspection_reporting", "Safety Inspection Reporting", "hs", "", ""),
        new("data__data_protection_reporting", "Data Protection Reporting", "data", "", ""),
        new("data__personal_data_processing_reporting", "Personal Data Processing Reporting", "data", "", ""),
        new("data__data_breach_notification", "Data Breach Notification", "data", "", ""),
        new("data__cyber_incident_reporting", "Cyber Incident Reporting", "data", "", ""),
        new("data__information_security_reporting", "Information Security Reporting", "data", "", ""),
        new("data__cyber_resilience_reporting", "Cyber Resilience Reporting", "data", "", ""),
        new("data__critical_infrastructure_reporting", "Critical Infrastructure Reporting", "data", "", ""),
        new("re__property_ownership_reporting", "Property Ownership Reporting", "re", "", ""),
        new("re__real_estate_transaction_reporting", "Real Estate Transaction Reporting", "re", "", ""),
        new("re__escrow_reporting", "Escrow Reporting", "re", "", ""),
        new("re__rental_reporting", "Rental Reporting", "re", "", ""),
        new("re__property_valuation_reporting", "Property Valuation Reporting", "re", "", ""),
        new("health__clinical_reporting", "Clinical Reporting", "health", "", ""),
        new("health__adverse_event_reporting", "Adverse Event Reporting", "health", "", ""),
        new("health__pharmacovigilance_reporting", "Pharmacovigilance Reporting", "health", "", ""),
        new("health__patient_safety_reporting", "Patient Safety Reporting", "health", "", ""),
        new("health__medical_device_reporting", "Medical Device Reporting", "health", "", ""),
        new("pharma__drug_safety_reporting", "Drug Safety Reporting", "pharma", "", ""),
        new("pharma__clinical_trial_reporting", "Clinical Trial Reporting", "pharma", "", ""),
        new("pharma__manufacturing_compliance_reporting", "Manufacturing Compliance Reporting", "pharma", "", ""),
        new("pharma__product_recall_reporting", "Product Recall Reporting", "pharma", "", ""),
        new("telecom__spectrum_usage_reporting", "Spectrum Usage Reporting", "telecom", "", ""),
        new("telecom__telecom_regulatory_reporting", "Telecom Regulatory Reporting", "telecom", "", ""),
        new("telecom__service_quality_reporting", "Service Quality Reporting", "telecom", "", ""),
        new("energy__energy_production_reporting", "Energy Production Reporting", "energy", "", ""),
        new("energy__utility_compliance_reporting", "Utility Compliance Reporting", "energy", "", ""),
        new("energy__grid_reporting", "Grid Reporting", "energy", "", ""),
        new("energy__oil_gas_production_reporting", "Oil & Gas Production Reporting", "energy", "", ""),
        new("energy__reserves_reporting", "Reserves Reporting", "energy", "", ""),
        new("transport__aviation_safety_reporting", "Aviation Safety Reporting", "transport", "", ""),
        new("transport__maritime_compliance_reporting", "Maritime Compliance Reporting", "transport", "", ""),
        new("transport__port_reporting", "Port Reporting", "transport", "", ""),
        new("transport__fleet_reporting", "Fleet Reporting", "transport", "", ""),
        new("transport__transportation_safety_reporting", "Transportation Safety Reporting", "transport", "", ""),
        new("mfg__production_reporting", "Production Reporting", "mfg", "", ""),
        new("mfg__quality_compliance_reporting", "Quality Compliance Reporting", "mfg", "", ""),
        new("mfg__product_safety_reporting", "Product Safety Reporting", "mfg", "", ""),
        new("mfg__recall_reporting", "Recall Reporting", "mfg", "", ""),
        new("consumer__consumer_complaint_reporting", "Consumer Complaint Reporting", "consumer", "", ""),
        new("consumer__product_defect_reporting", "Product Defect Reporting", "consumer", "", ""),
        new("consumer__product_recall_reporting", "Product Recall Reporting", "consumer", "", ""),
        new("govt__public_procurement_reporting", "Public Procurement Reporting", "govt", "", ""),
        new("govt__government_grant_reporting", "Government Grant Reporting", "govt", "", ""),
        new("govt__subsidy_reporting", "Subsidy Reporting", "govt", "", ""),
        new("stats__national_statistics_reporting", "National Statistics Reporting", "stats", "", ""),
        new("stats__census_reporting", "Census Reporting", "stats", "", ""),
        new("stats__economic_survey_reporting", "Economic Survey Reporting", "stats", "", ""),
        new("stats__industry_survey_reporting", "Industry Survey Reporting", "stats", "", ""),
        new("crisis__business_continuity_reporting", "Business Continuity Reporting", "crisis", "", ""),
        new("crisis__disaster_reporting", "Disaster Reporting", "crisis", "", ""),
        new("crisis__emergency_incident_reporting", "Emergency Incident Reporting", "crisis", "", ""),
        new("crisis__crisis_management_reporting", "Crisis Management Reporting", "crisis", "", ""),
        new("sector__aviation_regulatory_reporting", "Aviation Regulatory Reporting", "sector", "", ""),
        new("sector__maritime_regulatory_reporting", "Maritime Regulatory Reporting", "sector", "", ""),
        new("sector__mining_regulatory_reporting", "Mining Regulatory Reporting", "sector", "", ""),
        new("sector__education_regulatory_reporting", "Education Regulatory Reporting", "sector", "", ""),
        new("sector__defense_industry_reporting", "Defense Industry Reporting", "sector", "", ""),
        new("sector__food_safety_reporting", "Food Safety Reporting", "sector", "", ""),
        new("sector__agriculture_reporting", "Agriculture Reporting", "sector", "", ""),
        new("sector__hospitality_reporting", "Hospitality Reporting", "sector", "", ""),
        new("sector__tourism_reporting", "Tourism Reporting", "sector", "", ""),
        new("sector__gaming_gambling_reporting_where_legal", "Gaming/Gambling Reporting (where legal)", "sector", "", ""),
        new("sector__economic_substance_notification", "Economic Substance Notification", "sector", "esr_notify", ""),
        new("sector__economic_substance_report", "Economic Substance Report", "sector", "esr", ""),
        new("sector__aml_goaml_reporting", "AML goAML Reporting", "sector", "aml", ""),
    ];

    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"] = "United Arab Emirates",
        ["SA"] = "Saudi Arabia",
        ["QA"] = "Qatar",
        ["OM"] = "Oman",
        ["BH"] = "Bahrain",
        ["KW"] = "Kuwait",
        ["IN"] = "India",
        ["PK"] = "Pakistan",
        ["BD"] = "Bangladesh",
        ["LK"] = "Sri Lanka",
        ["NP"] = "Nepal",
        ["SG"] = "Singapore",
        ["MY"] = "Malaysia",
        ["ID"] = "Indonesia",
        ["PH"] = "Philippines",
        ["TH"] = "Thailand",
        ["CN"] = "China",
        ["HK"] = "Hong Kong",
        ["JP"] = "Japan",
        ["KR"] = "South Korea",
        ["GB"] = "United Kingdom",
        ["US"] = "United States",
        ["CA"] = "Canada",
        ["DE"] = "Germany",
        ["FR"] = "France",
        ["NL"] = "Netherlands",
        ["IE"] = "Ireland",
        ["AU"] = "Australia",
        ["NZ"] = "New Zealand",
        ["ZA"] = "South Africa",
        ["NG"] = "Nigeria",
        ["KE"] = "Kenya",
        ["EG"] = "Egypt",
        ["JO"] = "Jordan",
        ["LB"] = "Lebanon",
        ["MA"] = "Morocco",
        ["TR"] = "Turkey",
    };

    public static int ReportCount => Reports.Count;
    public static int CategoryCount => Categories.Count;
    public static bool Ifrs18Applies(int year) => year >= 2026;

    public static string CountryName(string? code)
    {
        var c = (code ?? "AE").Trim().ToUpperInvariant();
        if (c.Length > 2) c = c[..2];
        return CountryNames.TryGetValue(c, out var n) ? n : (c.Length == 0 ? "United Arab Emirates" : c);
    }

    public static string NormalizeCountry(string? code)
    {
        var c = new string((code ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant();
        if (c.Length == 0) return "AE";
        return c.Length > 2 ? c[..2] : c;
    }

    public static Report? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return Reports.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public static Category? FindCategory(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return Categories.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<Report> ReportsIn(string cat) =>
        Reports.Where(r => r.Cat.Equals(cat, StringComparison.OrdinalIgnoreCase)).ToList();

    public static (int Count, bool HasLive) CategoryStats(string cat)
    {
        var list = ReportsIn(cat);
        return (list.Count, list.Any(r => r.Builder.Length > 0));
    }

    public static string ReportKey(string cat, string name)
    {
        var slug = new string(name.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        while (slug.Contains("__", StringComparison.Ordinal)) slug = slug.Replace("__", "_", StringComparison.Ordinal);
        return cat + "__" + slug;
    }

    public static IfrsLink? Ifrs(string? std) => (std ?? string.Empty) switch
    {
        "IFRS18" => new("IFRS 18 — Presentation and Disclosure in Financial Statements", "https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/"),
        "IAS1" => new("IAS 1 — Presentation of Financial Statements", "https://www.ifrs.org/issued-standards/list-of-standards/ias-1-presentation-of-financial-statements/"),
        "IAS34" => new("IAS 34 — Interim Financial Reporting", "https://www.ifrs.org/issued-standards/list-of-standards/ias-34-interim-financial-reporting/"),
        "IFRS10" => new("IFRS 10 — Consolidated Financial Statements", "https://www.ifrs.org/issued-standards/list-of-standards/ifrs-10-consolidated-financial-statements/"),
        "IFRS7" => new("IFRS 7 — Financial Instruments: Disclosures", "https://www.ifrs.org/issued-standards/list-of-standards/ifrs-7-financial-instruments-disclosures/"),
        "ISA700" => new("ISA 700 — Forming an Opinion & Reporting on Financial Statements", "https://www.iaasb.org/publications/international-standard-auditing-isa-700-revised-forming-opinion-and-reporting-financial"),
        "ISA705" => new("ISA 705 — Modifications to the Opinion in the Auditor's Report", "https://www.iaasb.org/publications/international-standard-auditing-isa-705-revised"),
        "ISSB" => new("ISSB — IFRS S1 General Sustainability Disclosures", "https://www.ifrs.org/issued-standards/ifrs-sustainability-standards-navigator/ifrs-s1-general-requirements/"),
        "IFRS_S2" => new("IFRS S2 — Climate-related Disclosures", "https://www.ifrs.org/issued-standards/ifrs-sustainability-standards-navigator/ifrs-s2-climate-related-disclosures/"),
        _ => null,
    };

    public static Authority ResolveAuthority(string country, string cat)
    {
        var c = NormalizeCountry(country);
        var domain = cat;
        if (c == "AE")
        {
            return domain switch
            {
                "corp" => new("Ministry of Economy (MoEC) / licensing authority", "UAE Commercial Companies Law — Federal Decree-Law 32/2021", "https://www.moec.gov.ae", "https://www.moec.gov.ae/en/commercial-register"),
                "tax" => new("Federal Tax Authority (FTA)", "Corporate Tax — Federal Decree-Law 47/2022; VAT — Federal Decree-Law 8/2017 (as amended) + Exec. Regulations; Tax Procedures FDL 28/2022", "https://tax.gov.ae", "https://eservices.tax.gov.ae"),
                "fin" => new("Securities & Commodities Authority (SCA) / IFRS Foundation", "IFRS as adopted in the UAE — IFRS 18 early applied for FY2026+", "https://www.sca.gov.ae", "https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/"),
                "audit" => new("Ministry of Economy — Auditors Register / IAASB", "International Standards on Auditing (ISA)", "https://www.moec.gov.ae", "https://www.iaasb.org/standards-pronouncements"),
                "hr" => new("Ministry of Human Resources & Emiratisation (MOHRE)", "UAE Labour Law — Federal Decree-Law 33/2021; WPS — Ministerial Resolution 340/2026", "https://www.mohre.gov.ae", "https://www.mohre.gov.ae/en/services.aspx"),
                "aml" => new("UAE Financial Intelligence Unit (goAML) / EOCN", "Anti-Money Laundering — Federal Decree-Law 20/2018", "https://www.uaefiu.gov.ae", "https://services.uaefiu.gov.ae"),
                "bank" => new("Central Bank of the UAE (CBUAE)", "Decretal Federal Law 14/2018", "https://www.centralbank.ae", "https://www.centralbank.ae/en/cbuae-regulation/"),
                "ins" => new("Central Bank of the UAE — Insurance", "Insurance Law — Federal Law 6/2007 (as amended)", "https://www.centralbank.ae", "https://www.centralbank.ae/en/our-operations/insurance/"),
                "sec" => new("Securities & Commodities Authority (SCA)", "Federal Law 4/2000 (as amended)", "https://www.sca.gov.ae", "https://www.sca.gov.ae/en/services.aspx"),
                "customs" => new("Federal Customs Authority", "GCC Common Customs Law", "https://www.fca.gov.ae", "https://www.dubaicustoms.gov.ae"),
                "esg" => new("SCA / ISSB / market ESG guidance (DFM, ADX)", "IFRS S1 & S2 sustainability standards", "https://www.sca.gov.ae", "https://www.ifrs.org/sustainability/"),
                "env" => new("Ministry of Climate Change & Environment (MOCCAE)", "Federal Law 24/1999 on environment protection", "https://www.moccae.gov.ae", "https://www.moccae.gov.ae/en/services.aspx"),
                "hs" => new("MOHRE — Occupational Health & Safety", "OSH provisions, Federal Decree-Law 33/2021", "https://www.mohre.gov.ae", "https://www.mohre.gov.ae/en/services.aspx"),
                "data" => new("UAE Data Office / TDRA", "Personal Data Protection — Federal Decree-Law 45/2021", "https://www.tdra.gov.ae", "https://u.ae/en/about-the-uae/digital-uae/data/data-protection-laws"),
                "re" => new("Land Department / RERA (emirate level)", "Real-estate registration laws (emirate level)", "https://dubailand.gov.ae", "https://dubailand.gov.ae/en/eservices/"),
                "health" => new("Ministry of Health & Prevention (MOHAP) / DHA / DoH", "Federal Law 4/2016 on medical liability", "https://mohap.gov.ae", "https://mohap.gov.ae/en/services"),
                "pharma" => new("MOHAP — Drug Department", "Federal Law 8/2019 on medical products", "https://mohap.gov.ae", "https://mohap.gov.ae/en/services"),
                "telecom" => new("Telecommunications & Digital Government Regulatory Authority (TDRA)", "Federal Law by Decree 3/2003 (Telecom)", "https://tdra.gov.ae", "https://tdra.gov.ae/en/about-tdra"),
                "energy" => new("Ministry of Energy & Infrastructure (MOEI)", "Energy & utilities regulations", "https://www.moei.gov.ae", "https://www.moei.gov.ae/en/services.aspx"),
                "transport" => new("GCAA (aviation) / Federal Transport Authority", "Civil aviation & transport regulations", "https://www.gcaa.gov.ae", "https://www.gcaa.gov.ae"),
                "mfg" => new("Ministry of Industry & Advanced Technology (MOIAT) / ESMA", "UAE conformity & standards regulations", "https://moiat.gov.ae", "https://moiat.gov.ae/en/services"),
                "consumer" => new("Ministry of Economy — Consumer Protection", "Federal Law 15/2020 on consumer protection", "https://www.moec.gov.ae", "https://www.consumerrights.ae"),
                "govt" => new("Ministry of Finance — Federal procurement", "Federal procurement regulations", "https://www.mof.gov.ae", "https://www.mof.gov.ae/en/resourcesAndBudget/Pages/procurement.aspx"),
                "stats" => new("Federal Competitiveness & Statistics Centre (FCSC)", "Federal statistics law", "https://fcsc.gov.ae", "https://fcsc.gov.ae/en-us"),
                "crisis" => new("National Emergency Crisis & Disasters Management Authority (NCEMA)", "Federal Law 2/2011", "https://www.ncema.gov.ae", "https://www.ncema.gov.ae"),
                "sector" => new("Sector regulator / Ministry of Economy", "Sector-specific UAE regulations", "https://www.moec.gov.ae", "https://u.ae/en/information-and-services"),
                _ => new("Ministry of Economy (MoEC)", "Applicable UAE legislation", "https://www.moec.gov.ae", "https://u.ae/en/information-and-services"),
            };
        }
        return domain switch
        {
            "fin" => new("IFRS Foundation", "IFRS Accounting Standards — IFRS 18 Presentation and Disclosure in Financial Statements", "https://www.ifrs.org", "https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/"),
            "audit" => new("IAASB", "International Standards on Auditing (ISA)", "https://www.iaasb.org", "https://www.iaasb.org/standards-pronouncements"),
            "tax" => new("OECD / national tax authority", "OECD model tax framework + local tax law", "https://www.oecd.org/tax/", "https://www.oecd.org/tax/forum-on-tax-administration/"),
            "aml" => new("FATF / national FIU", "FATF 40 Recommendations", "https://www.fatf-gafi.org", "https://www.fatf-gafi.org/en/topics/fatf-recommendations.html"),
            "esg" => new("ISSB / GRI", "IFRS S1 & S2; GRI Standards", "https://www.ifrs.org/sustainability/", "https://www.globalreporting.org/standards/"),
            "bank" => new("Basel Committee (BIS) / central bank", "Basel III framework", "https://www.bis.org/bcbs/", "https://www.bis.org/basel_framework/"),
            "ins" => new("IAIS / national regulator", "Insurance Core Principles (ICP)", "https://www.iaisweb.org", "https://www.iaisweb.org/icp-online-tool/"),
            "sec" => new("IOSCO / national securities regulator", "IOSCO principles", "https://www.iosco.org", "https://www.iosco.org/library/"),
            "customs" => new("World Customs Organization / national customs", "WCO / WTO trade framework", "https://www.wcoomd.org", "https://www.wcoomd.org"),
            "data" => new("National data-protection authority", "GDPR-style data-protection law", "https://gdpr.eu", "https://gdpr.eu"),
            _ => new(CountryName(c) + " — national regulator", "Applicable national legislation (" + CountryName(c) + ")", "https://www.google.com/search?q=" + Uri.EscapeDataString(CountryName(c) + " government " + domain + " regulator official"), "https://www.google.com/search?q=" + Uri.EscapeDataString(CountryName(c) + " official reporting format " + domain)),
        };
    }

    public static string Frequency(string cat) => cat switch
    {
        "tax" => "Periodic (monthly / quarterly / annual per regime)",
        "fin" => "Annual / interim",
        "audit" => "Annual",
        "hr" => "Monthly / annual",
        "aml" => "Event-driven + periodic",
        "corp" => "On event / annual return",
        _ => "Periodic / on event",
    };

    public static string PeriodType(string cat, string key)
    {
        var k = key.ToLowerInvariant();
        if (k.Contains("vat_return", StringComparison.Ordinal) || k.Contains("gst_return", StringComparison.Ordinal)
            || k.Contains("sales_tax", StringComparison.Ordinal) || k.Contains("use_tax", StringComparison.Ordinal)
            || k.Contains("interim", StringComparison.Ordinal)) return "quarter";
        if (k.Contains("corporate_income_tax", StringComparison.Ordinal) || k.Contains("corporate_tax", StringComparison.Ordinal)
            || k.Contains("transfer_pricing", StringComparison.Ordinal) || k.Contains("country_by_country", StringComparison.Ordinal)
            || k.Contains("capital_gains", StringComparison.Ordinal) || k.Contains("annual_return", StringComparison.Ordinal)
            || k.Contains("annual_financial", StringComparison.Ordinal) || k.Contains("consolidated", StringComparison.Ordinal)) return "year";
        if (k.Contains("excise", StringComparison.Ordinal) || k.Contains("withholding", StringComparison.Ordinal)
            || k.Contains("wage_protection", StringComparison.Ordinal) || k.Contains("wps", StringComparison.Ordinal)
            || k.Contains("payroll", StringComparison.Ordinal) || k.Contains("customs", StringComparison.Ordinal)) return "month";
        return cat switch
        {
            "tax" => "quarter", "fin" => "year", "audit" => "year", "hr" => "month",
            "aml" => "month", "bank" => "quarter", "ins" => "quarter", "sec" => "quarter",
            "customs" => "month", "esg" => "year", "env" => "year", "hs" => "month",
            "data" => "month", "re" => "year", "health" => "month", "pharma" => "year",
            "telecom" => "quarter", "energy" => "month", "transport" => "month",
            "mfg" => "month", "consumer" => "month", "govt" => "year", "stats" => "year",
            "crisis" => "month", "sector" => "year", "corp" => "year",
            _ => "year",
        };
    }

    public static IReadOnlyList<string> PeriodBases(string natural)
    {
        var ordered = new List<string> { natural };
        foreach (var b in new[] { "month", "quarter", "year" })
        {
            if (!string.Equals(b, natural, StringComparison.Ordinal)) ordered.Add(b);
        }
        ordered.Add("custom");
        return ordered;
    }

    public static Period ResolvePeriod(string type, string? token, DateTime? customFrom = null, DateTime? customTo = null)
    {
        var now = DateTime.UtcNow.Date;
        if (type == "custom")
        {
            var f = customFrom ?? new DateTime(now.Year, now.Month, 1);
            var t = customTo ?? f.AddMonths(1).AddDays(-1);
            if (t < f) t = f;
            return new("custom", "custom", f.ToString("dd MMM yyyy") + " — " + t.ToString("dd MMM yyyy"), f, t, [new("custom", "Custom range…")]);
        }
        if (type == "month")
        {
            var opts = new List<PeriodOption>();
            Period? selected = null;
            for (var i = 0; i < 12; i++)
            {
                var start = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var end = start.AddMonths(1).AddDays(-1);
                var tok = start.ToString("yyyy-MM");
                var lbl = start.ToString("MMM yyyy");
                opts.Add(new(tok, lbl));
                if (selected is null && (string.IsNullOrWhiteSpace(token) || token == tok))
                    selected = new("month", tok, lbl, start, end, opts);
            }
            selected ??= new("month", opts[0].Token, opts[0].Label, new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1), opts);
            return selected with { Options = opts };
        }
        if (type == "quarter")
        {
            var opts = new List<PeriodOption>();
            Period? selected = null;
            var q = ((now.Month - 1) / 3) + 1;
            for (var i = 0; i < 8; i++)
            {
                var qi = q - i;
                var y = now.Year;
                while (qi <= 0) { qi += 4; y--; }
                var start = new DateTime(y, ((qi - 1) * 3) + 1, 1);
                var end = start.AddMonths(3).AddDays(-1);
                var tok = $"{y}-Q{qi}";
                var lbl = $"Q{qi} {y}";
                opts.Add(new(tok, lbl));
                if (selected is null && (string.IsNullOrWhiteSpace(token) || token == tok))
                    selected = new("quarter", tok, lbl, start, end, opts);
            }
            selected ??= new("quarter", opts[0].Token, opts[0].Label, now, now, opts);
            return selected with { Options = opts };
        }
        {
            var opts = new List<PeriodOption>();
            Period? selected = null;
            for (var i = 0; i < 6; i++)
            {
                var y = now.Year - i;
                var start = new DateTime(y, 1, 1);
                var end = new DateTime(y, 12, 31);
                var tok = y.ToString();
                var lbl = $"FY{y}";
                opts.Add(new(tok, lbl));
                if (selected is null && (string.IsNullOrWhiteSpace(token) || token == tok))
                    selected = new("year", tok, lbl, start, end, opts);
            }
            selected ??= new("year", opts[0].Token, opts[0].Label, now, now, opts);
            return selected with { Options = opts };
        }
    }

    public static string ImportTemplateCsv(string kind)
    {
        if (kind == "fin")
        {
            return """
Code,Description,Current year,Prior year
META_LEGAL_NAME,Legal name (reporting entity),Sample Client Trading LLC,
META_TRN,Tax / commercial registration number,100000000000003,
META_PERIOD_FROM,Reporting period from (YYYY-MM-DD),2026-01-01,
META_PERIOD_TO,Reporting period to (YYYY-MM-DD),2026-12-31,
FIN_REVENUE,Revenue (IFRS 15) — operating category,8400000,7500000
FIN_COGS,Cost of sales — operating category,4704000,4200000
FIN_PPE,Property plant & equipment (IAS 16),3528000,3150000
""";
        }
        if (kind == "ct")
        {
            return """
Code,Description,Amount
META_LEGAL_NAME,Legal name (taxable person),Sample Client Trading LLC
META_TRN,Corporate Tax registration number (TRN),100000000000003
ACCT_PROFIT,Accounting net profit per financial statements,1250000
REVENUE,Total revenue (for Small Business Relief test),8400000
FINES,Fines & administrative penalties (added back),15000
""";
        }
        return """
Code,Description,Amount,VAT,Adjustment
META_LEGAL_NAME,Legal name (taxable person),Sample Client Trading LLC,,
META_TRN,Tax Registration Number (TRN),100000000000003,,
BOX1A,Standard-rated supplies - Abu Dhabi,430500,21525,0
BOX1B,Standard-rated supplies - Dubai,645750,32287.50,0
BOX9,Standard-rated purchases,980000,49000,0
""";
    }
}

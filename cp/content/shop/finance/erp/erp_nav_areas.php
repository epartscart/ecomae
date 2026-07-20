<?php
/**
 * ERP area groups — left sidebar navigation (Dolibarr-inspired layout).
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_nav_areas_config()
{
	$epc_erp_nav_cfg = array(
		'overview' => array(
			'label' => 'Home',
			'icon' => 'fa-th-large',
			'desc' => 'Dashboards, workflow and approvals',
			'tabs' => array(
				'dashboard' => array('label' => 'Dashboard', 'icon' => 'fa-dashboard'),
				'workflow' => array('label' => 'Workflow', 'icon' => 'fa-tasks'),
				'processflow' => array('label' => 'Process flow', 'icon' => 'fa-sitemap'),
				'approvals' => array('label' => 'Approvals', 'icon' => 'fa-check-square-o'),
			),
			'groups' => array(
				'Workspaces' => array('dashboard', 'workflow', 'processflow'),
				'Governance' => array('approvals'),
			),
		),
		'ap' => array(
			'label' => 'Accounts payable',
			'icon' => 'fa-credit-card',
			'desc' => 'Vendor invoices, payments and AP setup',
			'tabs' => array(
				'payables' => array('label' => 'Payables', 'icon' => 'fa-truck'),
				'ap_setup' => array('label' => 'Vendor setup', 'icon' => 'fa-credit-card'),
			),
			'groups' => array(
				'Common' => array('payables'),
				'Setup' => array('ap_setup'),
			),
		),
		'ar' => array(
			'label' => 'Accounts receivable',
			'icon' => 'fa-users',
			'desc' => 'Customer invoices, settlements and AR setup',
			'tabs' => array(
				'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users'),
				'ar_setup' => array('label' => 'Customer setup', 'icon' => 'fa-handshake-o'),
			),
			'groups' => array(
				'Common' => array('receivables'),
				'Setup' => array('ar_setup'),
			),
		),
		'asset_mgmt' => array(
			'label' => 'Asset management',
			'icon' => 'fa-wrench',
			'desc' => 'Asset register, maintenance and lifecycle',
			'tabs' => array(
				'fixed_assets' => array('label' => 'Fixed assets', 'icon' => 'fa-building'),
			),
			'groups' => array(
				'Common' => array('fixed_assets'),
			),
		),
		'audit_wb' => array(
			'label' => 'Audit workbench',
			'icon' => 'fa-history',
			'desc' => 'Audit policies, cases and audit trail',
			'tabs' => array(
				'audit' => array('label' => 'Audit trail', 'icon' => 'fa-history'),
				'blockchain_proofs' => array('label' => 'Blockchain proofs', 'icon' => 'fa-link'),
			),
			'groups' => array(
				'Inquiries and reports' => array('audit', 'blockchain_proofs'),
			),
		),
		'budgeting' => array(
			'label' => 'Budgeting',
			'icon' => 'fa-pie-chart',
			'desc' => 'Budget registers, control and budget vs actual',
			'tabs' => array(
				'budgeting' => array('label' => 'Budgeting', 'icon' => 'fa-pie-chart'),
				'budget_planning' => array('label' => 'Budget planning', 'icon' => 'fa-line-chart'),
			),
			'groups' => array(
				'Common' => array('budgeting'),
				'Periodic' => array('budget_planning'),
			),
		),
		'banking' => array(
			'label' => 'Cash and bank management',
			'icon' => 'fa-money',
			'desc' => 'Bank accounts, cash, payments and reconciliation',
			'tabs' => array(
				'cash_bank' => array('label' => 'Cash & bank', 'icon' => 'fa-university'),
				'petty_cash' => array('label' => 'Petty cash', 'icon' => 'fa-money'),
				'payment_batches' => array('label' => 'Payment batches', 'icon' => 'fa-send'),
				'bank_recon' => array('label' => 'Bank recon', 'icon' => 'fa-check-square'),
				'cash_forecast' => array('label' => 'Cash flow forecast', 'icon' => 'fa-area-chart'),
				'bank_instruments' => array('label' => 'Bank instruments', 'icon' => 'fa-certificate'),
				'bank_setup' => array('label' => 'Bank accounts', 'icon' => 'fa-university'),
			),
			'groups' => array(
				'Common' => array('cash_bank', 'petty_cash', 'bank_instruments'),
				'Journals' => array('payment_batches'),
				'Inquiries and reports' => array('bank_recon', 'cash_forecast'),
				'Setup' => array('bank_setup'),
			),
		),
		'common' => array(
			'label' => 'Common',
			'icon' => 'fa-th',
			'desc' => 'Shared agenda, contacts, documents and knowledge',
			'tabs' => array(
				'agenda' => array('label' => 'Agenda', 'icon' => 'fa-calendar'),
				'contacts' => array('label' => 'Contacts', 'icon' => 'fa-address-book-o'),
				'documents' => array('label' => 'Documents', 'icon' => 'fa-folder-open-o'),
				'knowledge_base' => array('label' => 'Knowledge base', 'icon' => 'fa-book'),
				'ai_advisor' => array('label' => 'AI advisor', 'icon' => 'fa-magic'),
			),
			'groups' => array(
				'Common' => array('agenda', 'contacts', 'documents'),
				'Inquiries and reports' => array('knowledge_base', 'ai_advisor'),
			),
		),
		'risk' => array(
			'label' => 'Compliance',
			'icon' => 'fa-shield',
			'desc' => 'Insurance policies, claims and document expiry tracking',
			'tabs' => array(
				'insurance' => array('label' => 'Insurance', 'icon' => 'fa-shield'),
				'doc_expiry' => array('label' => 'Document expiry', 'icon' => 'fa-calendar-times-o'),
			),
			'groups' => array(
				'Common' => array('insurance', 'doc_expiry'),
			),
		),
		'consolidations' => array(
			'label' => 'Consolidations',
			'icon' => 'fa-sitemap',
			'desc' => 'Multi-entity consolidation and eliminations',
			'tabs' => array(
				'consolidation_bu' => array('label' => 'Consolidation', 'icon' => 'fa-sitemap'),
				'multi_entity' => array('label' => 'Multi-entity', 'icon' => 'fa-sitemap'),
			),
			'groups' => array(
				'Common' => array('consolidation_bu', 'multi_entity'),
			),
		),
		'cost_acct' => array(
			'label' => 'Cost accounting',
			'icon' => 'fa-calculator',
			'desc' => 'Cost allocations, accruals and cost analysis',
			'tabs' => array(
				'fin_advanced' => array('label' => 'Financial depth', 'icon' => 'fa-sliders'),
			),
			'groups' => array(
				'Periodic' => array('fin_advanced'),
			),
		),
		'cost_mgmt' => array(
			'label' => 'Cost management',
			'icon' => 'fa-balance-scale',
			'desc' => 'Inventory costing value models and analysis',
			'tabs' => array(
				'cost_models' => array('label' => 'Costing value-models', 'icon' => 'fa-balance-scale'),
			),
			'groups' => array(
				'Common' => array('cost_models'),
			),
		),
		'credit_coll' => array(
			'label' => 'Credit and collections',
			'icon' => 'fa-gavel',
			'desc' => 'Collection cases, dunning and credit holds',
			'tabs' => array(
				'collections' => array('label' => 'Collections', 'icon' => 'fa-gavel'),
			),
			'groups' => array(
				'Common' => array('collections'),
			),
		),
		'expense' => array(
			'label' => 'Expense management',
			'icon' => 'fa-credit-card',
			'desc' => 'Employee expense reports and reimbursements',
			'tabs' => array(
				'expense_reports' => array('label' => 'Expense reports', 'icon' => 'fa-credit-card'),
			),
			'groups' => array(
				'Common' => array('expense_reports'),
			),
		),
		'fixed_assets' => array(
			'label' => 'Fixed assets',
			'icon' => 'fa-building',
			'desc' => 'Asset register, depreciation and disposals',
			'tabs' => array(
				'fixed_assets' => array('label' => 'Fixed assets', 'icon' => 'fa-building'),
			),
			'groups' => array(
				'Common' => array('fixed_assets'),
			),
		),
		'finance' => array(
			'label' => 'General ledger',
			'icon' => 'fa-university',
			'desc' => 'Chart of accounts, journals, period close and financial statements',
			'tabs' => array(
				'gl' => array('label' => 'General ledger', 'icon' => 'fa-book'),
				'opening_balances' => array('label' => 'Opening balances', 'icon' => 'fa-flag-o'),
				'aging' => array('label' => 'Aging (AR/AP/Inv)', 'icon' => 'fa-hourglass-half'),
				'pl' => array('label' => 'Profit & loss', 'icon' => 'fa-bar-chart'),
				'balance_sheet' => array('label' => 'Balance sheet', 'icon' => 'fa-balance-scale'),
				'reports' => array('label' => 'Reports', 'icon' => 'fa-table'),
				'enterprise_reports' => array('label' => 'Trial balance / reports', 'icon' => 'fa-table'),
				'fin_advanced' => array('label' => 'Financial depth', 'icon' => 'fa-sliders'),
				'year_end' => array('label' => 'Year-end closing', 'icon' => 'fa-calendar-check-o'),
				'coa' => array('label' => 'Chart of accounts', 'icon' => 'fa-list'),
				'jw_trial_balance' => array('label' => 'Dual trial balance (wt+val)', 'icon' => 'fa-balance-scale', 'jw' => true),
				'jw_journal_voucher' => array('label' => 'Journal voucher (jewellery)', 'icon' => 'fa-book', 'jw' => true),
				'jw_petty_cash' => array('label' => 'Petty cash (jewellery)', 'icon' => 'fa-money', 'jw' => true),
			),
			'groups' => array(
				'Common' => array('gl'),
				'Journals' => array('opening_balances', 'jw_journal_voucher', 'jw_petty_cash'),
				'Inquiries and reports' => array('aging', 'pl', 'balance_sheet', 'reports', 'enterprise_reports', 'jw_trial_balance'),
				'Periodic' => array('fin_advanced', 'year_end'),
				'Setup' => array('coa'),
			),
		),
		'people' => array(
			'label' => 'Human resources',
			'icon' => 'fa-users',
			'desc' => 'Workers, organization and labour-law compliance',
			'tabs' => array(
				'staff' => array('label' => 'Workers', 'icon' => 'fa-id-badge'),
				'hr' => array('label' => 'HR', 'icon' => 'fa-user-circle'),
				'hr_ops' => array('label' => 'HR operations', 'icon' => 'fa-users'),
				'recruitment' => array('label' => 'Recruitment', 'icon' => 'fa-user-plus'),
				'performance' => array('label' => 'Performance management', 'icon' => 'fa-star-half-o'),
				'hr_law' => array('label' => 'Labour law & compliance', 'icon' => 'fa-gavel', 'raw' => true),
			),
			'groups' => array(
				'Common' => array('staff', 'hr', 'hr_ops'),
				'Talent' => array('recruitment', 'performance'),
				'Compliance' => array('hr_law'),
			),
		),
		'inventory_mgmt' => array(
			'label' => 'Inventory management',
			'icon' => 'fa-cubes',
			'desc' => 'Stock, item groups, order planning and barcodes',
			'tabs' => array(
				'inventory' => array('label' => 'Inventory', 'icon' => 'fa-cubes'),
				'inv_groups' => array('label' => 'Inventory (stock/groups)', 'icon' => 'fa-object-group'),
				'order_planning' => array('label' => 'Order planning', 'icon' => 'fa-cubes'),
				'retail_barcode' => array('label' => 'Retail barcode', 'icon' => 'fa-barcode'),
				'jw_karat' => array('label' => 'Karat master', 'icon' => 'fa-tachometer', 'jw' => true),
				'jw_rate_type' => array('label' => 'Rate type master', 'icon' => 'fa-line-chart', 'jw' => true),
				'jw_metal_stock' => array('label' => 'Metal stock master', 'icon' => 'fa-cubes', 'jw' => true),
				'jw_design' => array('label' => 'Design master', 'icon' => 'fa-paint-brush', 'jw' => true),
				'jw_diamond' => array('label' => 'Diamond master', 'icon' => 'fa-diamond', 'jw' => true),
				'jw_pearl' => array('label' => 'Pearl master', 'icon' => 'fa-circle-o', 'jw' => true),
				'jw_color_stone' => array('label' => 'Colour stone master', 'icon' => 'fa-star', 'jw' => true),
				'jw_stock_verification' => array('label' => 'Stock verification', 'icon' => 'fa-check-square-o', 'jw' => true),
				'jw_stock_balance' => array('label' => 'Stock balance (weight)', 'icon' => 'fa-bar-chart', 'jw' => true),
				'jw_barcode' => array('label' => 'Barcode (jewellery)', 'icon' => 'fa-barcode', 'jw' => true),
			),
			'groups' => array(
				'Common' => array('inventory', 'inv_groups'),
				'Periodic' => array('order_planning'),
				'Jewellery master data' => array('jw_karat', 'jw_rate_type', 'jw_metal_stock', 'jw_design', 'jw_diamond', 'jw_pearl', 'jw_color_stone'),
				'Jewellery stock' => array('jw_stock_verification', 'jw_stock_balance', 'jw_barcode'),
				'Setup' => array('retail_barcode'),
			),
		),
		'landed_cost_area' => array(
			'label' => 'Landed cost',
			'icon' => 'fa-ship',
			'desc' => 'Voyages, landed cost allocation and apportionment',
			'tabs' => array(
				'landed_cost' => array('label' => 'Landed cost', 'icon' => 'fa-ship'),
			),
			'groups' => array(
				'Common' => array('landed_cost'),
			),
		),
		'leave_abs' => array(
			'label' => 'Leave and absence',
			'icon' => 'fa-calendar-minus-o',
			'desc' => 'Leave plans, requests and absence tracking',
			'tabs' => array(
				'hr_ops' => array('label' => 'HR operations', 'icon' => 'fa-users'),
			),
			'groups' => array(
				'Common' => array('hr_ops'),
			),
		),
		'logistics' => array(
			'label' => 'Logistics',
			'icon' => 'fa-truck',
			'desc' => 'Customs, shipping and inbound/outbound logistics',
			'tabs' => array(
				'custom_shipping' => array('label' => 'Customs & shipping', 'icon' => 'fa-ship'),
				'procurement_link' => array('label' => 'Procurement CP', 'icon' => 'fa-external-link', 'external' => true),
			),
			'groups' => array(
				'Common' => array('custom_shipping'),
				'Setup' => array('procurement_link'),
			),
		),
		'master_planning_area' => array(
			'label' => 'Master planning',
			'icon' => 'fa-random',
			'desc' => 'Master scheduling and material requirements planning',
			'tabs' => array(
				'master_planning' => array('label' => 'Master planning', 'icon' => 'fa-random'),
			),
			'groups' => array(
				'Common' => array('master_planning'),
			),
		),
		'mhei' => array(
			'label' => 'Material handling equipment interface',
			'icon' => 'fa-cubes',
			'desc' => 'Material handling and warehouse equipment integration',
			'tabs' => array(
				'wms' => array('label' => 'Warehouse management', 'icon' => 'fa-cubes'),
			),
			'groups' => array(
				'Common' => array('wms'),
			),
		),
		'enterprise' => array(
			'label' => 'Organization administration',
			'icon' => 'fa-building-o',
			'desc' => 'Legal entities, business units, contracts and documents',
			'tabs' => array(
				'business_units' => array('label' => 'Business unit', 'icon' => 'fa-sitemap'),
				'org_admin' => array('label' => 'Organization administration', 'icon' => 'fa-sitemap'),
				'contracts' => array('label' => 'Contracts & e-sign', 'icon' => 'fa-file-text-o'),
				'doc_formats' => array('label' => 'Document formats', 'icon' => 'fa-files-o'),
				'listing' => array('label' => 'Listing', 'icon' => 'fa-list-alt'),
			),
			'groups' => array(
				'Organization' => array('business_units', 'org_admin'),
				'Documents' => array('contracts', 'doc_formats'),
				'Setup' => array('listing'),
			),
		),
		'payroll_area' => array(
			'label' => 'Payroll',
			'icon' => 'fa-money',
			'desc' => 'Payroll runs, earnings and statutory deductions',
			'tabs' => array(
				'payroll' => array('label' => 'Payroll', 'icon' => 'fa-money'),
			),
			'groups' => array(
				'Common' => array('payroll'),
			),
		),
		'purchasing' => array(
			'label' => 'Procurement and sourcing',
			'icon' => 'fa-shopping-basket',
			'desc' => 'Suppliers, RFQ, purchase orders and matching',
			'tabs' => array(
				'supplier_portal' => array('label' => 'Supplier portal', 'icon' => 'fa-handshake-o'),
				'purchase_requisitions' => array('label' => 'Purchase requisitions', 'icon' => 'fa-list-alt'),
				'rfq' => array('label' => 'RFQ', 'icon' => 'fa-envelope-o'),
				'purchase_orders' => array('label' => 'Purchase orders', 'icon' => 'fa-clipboard'),
				'purchases' => array('label' => 'Purchases', 'icon' => 'fa-file-text-o'),
				'three_way_match' => array('label' => '3-way match', 'icon' => 'fa-check-square-o'),
				'procurement_categories' => array('label' => 'Categories & policies', 'icon' => 'fa-sitemap'),
				'jw_metal_purchase' => array('label' => 'Metal purchase', 'icon' => 'fa-arrow-down', 'jw' => true),
				'jw_diamond_purchase' => array('label' => 'Diamond purchase', 'icon' => 'fa-diamond', 'jw' => true),
				'jw_purchase_fixing' => array('label' => 'Purchase fixing', 'icon' => 'fa-lock', 'jw' => true),
				'jw_purchase_window' => array('label' => 'Purchase window', 'icon' => 'fa-binoculars', 'jw' => true),
			),
			'groups' => array(
				'Common' => array('supplier_portal', 'purchase_requisitions'),
				'Orders' => array('rfq', 'purchase_orders', 'purchases', 'three_way_match'),
				'Jewellery purchase' => array('jw_metal_purchase', 'jw_diamond_purchase', 'jw_purchase_fixing', 'jw_purchase_window'),
				'Setup' => array('procurement_categories'),
			),
		),
		'pim' => array(
			'label' => 'Product information management',
			'icon' => 'fa-cube',
			'desc' => 'Products, dimensions and variant structures',
			'tabs' => array(
				'product_info' => array('label' => 'Product information', 'icon' => 'fa-cube'),
			),
			'groups' => array(
				'Common' => array('product_info'),
			),
		),
		'production' => array(
			'label' => 'Production control',
			'icon' => 'fa-cogs',
			'desc' => 'Production orders, routes, operations and quality',
			'tabs' => array(
				'manufacturing' => array('label' => 'Production', 'icon' => 'fa-cogs'),
				'mfg_planning' => array('label' => 'Production planning', 'icon' => 'fa-cogs'),
				'quality' => array('label' => 'Quality management', 'icon' => 'fa-check-circle'),
			),
			'groups' => array(
				'Common' => array('manufacturing'),
				'Periodic' => array('mfg_planning'),
				'Quality' => array('quality'),
			),
		),
		'projects' => array(
			'label' => 'Project management and accounting',
			'icon' => 'fa-tasks',
			'desc' => 'Projects, budgets, WIP and revenue recognition',
			'tabs' => array(
				'projects' => array('label' => 'Projects', 'icon' => 'fa-tasks'),
				'project_accounting' => array('label' => 'Project accounting', 'icon' => 'fa-pie-chart'),
			),
			'groups' => array(
				'Common' => array('projects'),
				'Periodic' => array('project_accounting'),
			),
		),
		'retail' => array(
			'label' => 'Retail and commerce',
			'icon' => 'fa-shopping-cart',
			'desc' => 'Channels, assortments, retail pricing, POS and statements',
			'tabs' => array(
				'retail_commerce' => array('label' => 'Retail & commerce', 'icon' => 'fa-shopping-cart', 'raw' => true),
			),
			'groups' => array(
				'Common' => array('retail_commerce'),
			),
		),
		'sales' => array(
			'label' => 'Sales and marketing',
			'icon' => 'fa-line-chart',
			'desc' => 'CRM, prospects, quotations, sales orders, delivery and invoicing',
			'tabs' => array(
				'crm' => array('label' => 'CRM', 'icon' => 'fa-handshake-o', 'raw' => true),
				'marketing' => array('label' => 'Marketing', 'icon' => 'fa-bullhorn'),
				'leads' => array('label' => 'Prospects & leads', 'icon' => 'fa-user-plus'),
				'opportunities' => array('label' => 'Opportunities', 'icon' => 'fa-filter'),
				'proposals' => array('label' => 'Sales quotations', 'icon' => 'fa-file-text'),
				'sales_orders' => array('label' => 'Sales orders', 'icon' => 'fa-shopping-cart'),
				'subscriptions' => array('label' => 'Subscriptions', 'icon' => 'fa-refresh'),
				'delivery_notes' => array('label' => 'Delivery notes', 'icon' => 'fa-truck'),
				'invoices' => array('label' => 'Invoices (e-invoice)', 'icon' => 'fa-file-text-o'),
				'revenue' => array('label' => 'Revenue', 'icon' => 'fa-money'),
				'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-random'),
				'jw_retail_sales' => array('label' => 'Retail sales (POS)', 'icon' => 'fa-shopping-cart', 'jw' => true),
				'jw_metal_sales' => array('label' => 'Metal sales', 'icon' => 'fa-arrow-up', 'jw' => true),
				'jw_sales_fixing' => array('label' => 'Sales fixing', 'icon' => 'fa-lock', 'jw' => true),
				'jw_sales_return' => array('label' => 'Sales return', 'icon' => 'fa-reply', 'jw' => true),
				'jw_pos_advance' => array('label' => 'POS advance', 'icon' => 'fa-money', 'jw' => true),
				'jw_sales_analysis' => array('label' => 'Sales analysis (weight)', 'icon' => 'fa-area-chart', 'jw' => true),
			),
			'groups' => array(
				'Common' => array('crm', 'marketing'),
				'Pipeline' => array('leads', 'opportunities'),
				'Orders' => array('proposals', 'sales_orders', 'subscriptions', 'delivery_notes', 'invoices'),
				'Jewellery sales' => array('jw_retail_sales', 'jw_metal_sales', 'jw_sales_fixing', 'jw_sales_return', 'jw_pos_advance'),
				'Inquiries and reports' => array('revenue', 'fulfilment', 'jw_sales_analysis'),
			),
		),
		'service_mgmt' => array(
			'label' => 'Service management',
			'icon' => 'fa-life-ring',
			'desc' => 'Service agreements, contracts and service delivery',
			'tabs' => array(
				'contracts' => array('label' => 'Contracts & e-sign', 'icon' => 'fa-file-text-o'),
				'aftersales' => array('label' => 'After-sales RMA', 'icon' => 'fa-undo'),
				'jw_repairs' => array('label' => 'Repair jobs', 'icon' => 'fa-wrench', 'jw' => true),
				'jw_repair_receipt' => array('label' => 'Repair receipt', 'icon' => 'fa-wrench', 'jw' => true),
				'jw_repair_transfer' => array('label' => 'Repair transfer', 'icon' => 'fa-truck', 'jw' => true),
				'jw_workshop_receive' => array('label' => 'Workshop receive', 'icon' => 'fa-inbox', 'jw' => true),
				'jw_repair_delivery' => array('label' => 'Repair delivery', 'icon' => 'fa-gift', 'jw' => true),
				'jw_repair_sale' => array('label' => 'Repair sale', 'icon' => 'fa-file-text-o', 'jw' => true),
				'jw_repair_register' => array('label' => 'Repair register', 'icon' => 'fa-list-alt', 'jw' => true),
				'jw_repair_search' => array('label' => 'Repair search', 'icon' => 'fa-search', 'jw' => true),
			),
			'groups' => array(
				'Common' => array('contracts', 'aftersales'),
				'Jewellery repair & workshop' => array('jw_repairs', 'jw_repair_receipt', 'jw_repair_transfer', 'jw_workshop_receive', 'jw_repair_delivery', 'jw_repair_sale', 'jw_repair_register', 'jw_repair_search'),
			),
		),
		'setup' => array(
			'label' => 'System administration',
			'icon' => 'fa-sliders',
			'desc' => 'Company setup, security, batch/platform services, data and integration',
			'tabs' => array(
				'erp_setup' => array('label' => 'Accounting setup', 'icon' => 'fa-cogs'),
				'security_roles' => array('label' => 'Security roles', 'icon' => 'fa-shield'),
				'platform' => array('label' => 'Platform services', 'icon' => 'fa-cogs'),
				'data_import' => array('label' => 'Data import', 'icon' => 'fa-upload'),
				'integration' => array('label' => 'Data & integration', 'icon' => 'fa-plug'),
				'jw_seed_data' => array('label' => 'Sample data (jewellery)', 'icon' => 'fa-database', 'jw' => true),
				'jw_currency' => array('label' => 'Currency master (jewellery)', 'icon' => 'fa-money', 'jw' => true),
				'ai_assistant' => array('label' => 'Devin AI assistant', 'icon' => 'fa-robot'),
				'tenant_config' => array('label' => 'Tenant configuration', 'icon' => 'fa-sliders'),
				'print_designer' => array('label' => 'Print designer', 'icon' => 'fa-paint-brush'),
				'workflow_automation' => array('label' => 'Workflow automation', 'icon' => 'fa-cogs'),
			),
			'groups' => array(
				'Setup' => array('erp_setup', 'tenant_config'),
				'Security' => array('security_roles'),
				'Platform' => array('platform', 'ai_assistant'),
				'Automation' => array('workflow_automation', 'print_designer'),
				'Data management' => array('data_import', 'integration', 'jw_seed_data'),
				'Jewellery setup' => array('jw_currency'),
			),
		),
		'tax' => array(
			'label' => 'Tax',
			'icon' => 'fa-percent',
			'desc' => 'VAT/tax returns, e-invoicing, compliance and statutory reporting',
			'tabs' => array(
				'vat_return' => array('label' => 'VAT return', 'icon' => 'fa-percent'),
				'tax_compliance' => array('label' => 'Tax compliance', 'icon' => 'fa-gavel'),
				'vat_refund' => array('label' => 'Tourist VAT refunds', 'icon' => 'fa-plane'),
				'einvoice' => array('label' => 'E-invoicing', 'icon' => 'fa-file-code-o'),
				'blockchain_proofs' => array('label' => 'Blockchain proofs', 'icon' => 'fa-link'),
				'withholding' => array('label' => 'Withholding tax', 'icon' => 'fa-scissors'),
				'compliance' => array('label' => 'Compliance center', 'icon' => 'fa-shield'),
				'ext_reports' => array('label' => 'External reporting', 'icon' => 'fa-file-text-o'),
				'elec_reporting' => array('label' => 'Electronic reporting', 'icon' => 'fa-file-code-o'),
				'document_control' => array('label' => 'Document control', 'icon' => 'fa-print'),
				'jw_tourist_vat' => array('label' => 'Tourist VAT refund (jewellery)', 'icon' => 'fa-plane', 'jw' => true),
			),
			'groups' => array(
				'Declarations' => array('vat_return', 'tax_compliance', 'vat_refund', 'withholding'),
				'Common' => array('einvoice', 'blockchain_proofs', 'compliance'),
				'Reports' => array('ext_reports', 'elec_reporting'),
				'Jewellery' => array('jw_tourist_vat'),
				'Setup' => array('document_control'),
			),
		),
		'warehouse' => array(
			'label' => 'Warehouse management',
			'icon' => 'fa-cubes',
			'desc' => 'Locations, license plates, waves, work and mobile RF',
			'tabs' => array(
				'wms' => array('label' => 'Warehouse management', 'icon' => 'fa-cubes'),
			),
			'groups' => array(
				'Common' => array('wms'),
			),
		),
	);
	return epc_erp_nav_inject_reports($epc_erp_nav_cfg);
}

/**
 * Filter nav areas based on tenant industry profile.
 * Tabs marked with 'jw' => true are only visible when industry = 'jewellery'.
 * This ensures jewellery-specific tabs are integrated into existing modules
 * but only shown for jewellery tenants — like Oracle/SAP industry configuration.
 */
function epc_erp_nav_filter_by_industry(array $areas, PDO $db): array
{
	$jwFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
	$isJewellery = false;
	if (is_file($jwFile)) {
		require_once $jwFile;
		if (function_exists('epc_jw_is_jewellery_tenant')) {
			$isJewellery = epc_jw_is_jewellery_tenant($db);
		}
	}
	if ($isJewellery) {
		return $areas;
	}
	foreach ($areas as $areaKey => &$area) {
		if (!isset($area['tabs']) || !is_array($area['tabs'])) continue;
		foreach ($area['tabs'] as $tabKey => $tabCfg) {
			if (!empty($tabCfg['jw'])) {
				unset($area['tabs'][$tabKey]);
			}
		}
		if (isset($area['groups']) && is_array($area['groups'])) {
			foreach ($area['groups'] as $grpKey => &$grpTabs) {
				$grpTabs = array_values(array_filter($grpTabs, function($t) use ($area) {
					return isset($area['tabs'][$t]);
				}));
				if (empty($grpTabs)) unset($area['groups'][$grpKey]);
			}
			unset($grpTabs);
		}
	}
	unset($area);
	return $areas;
}

/**
 * Auto-inject a "Reports & inquiries" tab into every module that has reports
 * registered in the report center, under an "Inquiries and reports" group.
 */
function epc_erp_nav_inject_reports(array $areas)
{
	$rc = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_report_center.php';
	if (!is_file($rc)) {
		return $areas;
	}
	require_once $rc;
	if (!function_exists('epc_rc_reports_for')) {
		return $areas;
	}
	foreach ($areas as $areaKey => &$area) {
		if ($areaKey === 'overview' || empty(epc_rc_reports_for($areaKey))) {
			continue;
		}
		$tabKey = 'rc_' . $areaKey;
		if (!isset($area['tabs'])) {
			$area['tabs'] = array();
		}
		if (isset($area['tabs'][$tabKey])) {
			continue;
		}
		$area['tabs'][$tabKey] = array('label' => 'Reports & inquiries', 'icon' => 'fa-table');
		if (!isset($area['groups']) || !is_array($area['groups'])) {
			$area['groups'] = array();
		}
		$grp = 'Inquiries and reports';
		if (!isset($area['groups'][$grp])) {
			$area['groups'][$grp] = array();
		}
		$area['groups'][$grp][] = $tabKey;
	}
	unset($area);
	return $areas;
}

/**
 * Process-category grouping for the ERP left sidebar.
 * Maps each top-level category to the area keys that belong to it.
 * Every area key from epc_erp_nav_areas_config() is assigned to exactly one
 * category; routing, area keys, tab keys, and URLs are unchanged.
 */
function epc_erp_nav_categories_config(): array
{
	return array(
		'home' => array(
			'label' => 'Home & workspace',
			'short' => 'Home',
			'icon'  => 'fa-th-large',
			'areas' => array('overview'),
		),
		'record_to_report' => array(
			'label' => 'Record to Report',
			'short' => 'R2R',
			'icon'  => 'fa-university',
			'areas' => array(
				'finance',        // General ledger
				'budgeting',      // Budgeting
				'consolidations', // Consolidations
				'cost_acct',      // Cost accounting
				'audit_wb',       // Audit workbench
				'fixed_assets',   // Fixed assets
			),
		),
		'procure_to_pay' => array(
			'label' => 'Procure to Pay',
			'short' => 'P2P',
			'icon'  => 'fa-shopping-basket',
			'areas' => array(
				'purchasing',         // Procurement & sourcing
				'ap',                 // Accounts payable
				'landed_cost_area',   // Landed cost
				'expense',            // Expense management
			),
		),
		'order_to_cash' => array(
			'label' => 'Order to Cash',
			'short' => 'O2C',
			'icon'  => 'fa-line-chart',
			'areas' => array(
				'sales',        // Sales & marketing
				'ar',           // Accounts receivable
				'credit_coll',  // Credit & collections
				'retail',       // Retail & commerce
				'service_mgmt', // Service management
			),
		),
		'cash_treasury' => array(
			'label' => 'Cash & Treasury',
			'short' => 'Cash',
			'icon'  => 'fa-money',
			'areas' => array(
				'banking', // Cash & bank management
			),
		),
		'inventory_fulfilment' => array(
			'label' => 'Inventory & Fulfilment',
			'short' => 'Stock',
			'icon'  => 'fa-cubes',
			'areas' => array(
				'inventory_mgmt',      // Inventory management
				'pim',                 // Product information management
				'cost_mgmt',           // Cost management
				'production',          // Production control
				'warehouse',           // Warehouse management
				'mhei',                // Material handling equipment interface
				'logistics',           // Logistics
				'master_planning_area',// Master planning
				'asset_mgmt',          // Asset management
			),
		),
		'hr_payroll' => array(
			'label' => 'HR & Payroll',
			'short' => 'HR',
			'icon'  => 'fa-users',
			'areas' => array(
				'people',      // Human resources
				'payroll_area',// Payroll
				'leave_abs',   // Leave & absence
				'projects',    // Project management & accounting
			),
		),
		'compliance_tax' => array(
			'label' => 'Compliance & Tax',
			'short' => 'Tax',
			'icon'  => 'fa-shield',
			'areas' => array(
				'tax',  // Tax
				'risk', // Compliance (insurance, doc expiry)
			),
		),
		'setup_admin' => array(
			'label' => 'Setup & Administration',
			'short' => 'Setup',
			'icon'  => 'fa-sliders',
			'areas' => array(
				'setup',      // System administration
				'enterprise', // Organisation administration
				'common',     // Common (agenda, contacts, docs, AI advisor)
			),
		),
	);
}

function epc_erp_nav_label_plain($label)
{
	return html_entity_decode(strip_tags((string) $label), ENT_QUOTES | ENT_HTML5, 'UTF-8');
}

function epc_erp_tab_to_area($tab)
{
	$tab = (string) $tab;
	if ($tab === 'bank_recon') {
		return 'banking';
	}
	// Some tabs are surfaced under more than one standard module (e.g. the asset
	// register appears under both Fixed assets and Asset management). Pin each to
	// its canonical owning module so breadcrumbs/highlighting stay stable when no
	// explicit area is supplied in the URL.
	static $canonical = array(
		'fixed_assets' => 'fixed_assets',
		'fin_advanced' => 'finance',
		'hr_ops' => 'people',
		'wms' => 'warehouse',
		'contracts' => 'enterprise',
	);
	if (isset($canonical[$tab])) {
		return $canonical[$tab];
	}
	foreach (epc_erp_nav_areas_config() as $areaKey => $area) {
		if (isset($area['tabs'][$tab])) {
			return $areaKey;
		}
	}
	return 'overview';
}

function epc_erp_area_default_tab($area)
{
	$cfg = epc_erp_nav_areas_config();
	if (!isset($cfg[$area]['tabs'])) {
		return 'dashboard';
	}
	$keys = array_keys($cfg[$area]['tabs']);
	return $keys[0] ?? 'dashboard';
}

function epc_erp_tab_url($base, $tab, $from, $to, $area = '')
{
	if ($area === '') {
		$area = epc_erp_tab_to_area($tab);
	}
	$q = 'area=' . urlencode($area) . '&tab=' . urlencode($tab)
		. '&from=' . urlencode($from) . '&to=' . urlencode($to);
	$url = $base . '?' . $q;
	if (!function_exists('epc_erp_shell_url_query')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	}
	$shellQ = epc_erp_shell_url_query();
	if ($shellQ !== '') {
		$url .= '&' . $shellQ;
	}
	return $url;
}

function epc_erp_nav_shell_link_attrs(): string
{
	if (!function_exists('epc_erp_shell_link_attrs')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	}
	return epc_erp_shell_link_attrs();
}

function epc_erp_procurement_url()
{
	global $DP_Config;
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
		&& function_exists('epc_portal_demo_cp_tenant_base')) {
		return epc_portal_demo_cp_tenant_base() . 'shop/procurement/procurement';
	}
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	return '/' . $backend . '/shop/procurement/procurement';
}

function epc_erp_nav_apply_commerce_filter(array $areas): array
{
	if (!function_exists('epc_erp_has_commerce_integration')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
	}
	if (epc_erp_has_commerce_integration()) {
		return $areas;
	}
	foreach ($areas as $areaKey => &$area) {
		if (!isset($area['tabs']) || !is_array($area['tabs'])) {
			continue;
		}
		foreach (epc_erp_commerce_tab_keys() as $tabKey) {
			unset($area['tabs'][$tabKey]);
		}
		if ($areaKey === 'sales') {
			$area['desc'] = 'CRM, sales orders, invoices and receivables';
		}
		if ($areaKey === 'purchasing') {
			$area['desc'] = 'Suppliers, RFQ, POs and payables';
		}
	}
	unset($area);
	return $areas;
}

function epc_erp_nav_tab_allowed($tabKey, array $allowedTabs)
{
	if (!function_exists('epc_erp_has_commerce_integration')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
	}
	if (in_array($tabKey, epc_erp_commerce_tab_keys(), true) && !epc_erp_has_commerce_integration()) {
		return false;
	}
	if ($tabKey === 'procurement_link') {
		return !empty($GLOBALS['epc_erp_cp_links']);
	}
	if ($tabKey === 'bank_recon') {
		return in_array('bank_recon', $allowedTabs, true) || in_array('cash_bank', $allowedTabs, true);
	}
	return in_array($tabKey, $allowedTabs, true);
}

function epc_erp_nav_area_visible_tabs($areaKey, array $allowedTabs)
{
	$areas = epc_erp_nav_areas_for_tenant();
	if (!isset($areas[$areaKey]['tabs'])) {
		return array();
	}
	$out = array();
	foreach ($areas[$areaKey]['tabs'] as $tabKey => $meta) {
		if (epc_erp_nav_tab_allowed($tabKey, $allowedTabs)) {
			$out[$tabKey] = $meta;
		}
	}
	return $out;
}

function epc_erp_nav_tab_label($areaKey, $tabKey)
{
	$areas = epc_erp_nav_areas_config();
	if (!isset($areas[$areaKey]['tabs'][$tabKey])) {
		return ucfirst(str_replace('_', ' ', $tabKey));
	}
	$meta = $areas[$areaKey]['tabs'][$tabKey];
	if (!empty($meta['raw'])) {
		return strip_tags($meta['label']);
	}
	return html_entity_decode(strip_tags($meta['label']), ENT_QUOTES, 'UTF-8');
}

function epc_erp_nav_areas_for_tenant()
{
	if (function_exists('epc_portal_erp_modules_full_access_context')
		&& epc_portal_erp_modules_full_access_context()) {
		$result = epc_erp_nav_apply_commerce_filter(epc_erp_nav_areas_config());
		if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
			$result = epc_erp_nav_filter_by_industry($result, $GLOBALS['db_link']);
		}
		return $result;
	}
	$areas = epc_erp_nav_areas_config();
	$modFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
	if (!is_file($modFile)) {
		$result = epc_erp_nav_apply_commerce_filter($areas);
		if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
			$result = epc_erp_nav_filter_by_industry($result, $GLOBALS['db_link']);
		}
		return $result;
	}
	require_once $modFile;
	if (!function_exists('epc_portal_erp_modules_enabled_areas')) {
		$result = epc_erp_nav_apply_commerce_filter($areas);
		if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
			$result = epc_erp_nav_filter_by_industry($result, $GLOBALS['db_link']);
		}
		return $result;
	}
	$enabledAreas = epc_portal_erp_modules_enabled_areas();
	if (empty($enabledAreas)) {
		$result = epc_erp_nav_apply_commerce_filter($areas);
		if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
			$result = epc_erp_nav_filter_by_industry($result, $GLOBALS['db_link']);
		}
		return $result;
	}
	$filtered = array();
	foreach ($areas as $key => $area) {
		if (in_array($key, $enabledAreas, true)) {
			$filtered[$key] = $area;
		}
	}
	$result = epc_erp_nav_apply_commerce_filter(!empty($filtered) ? $filtered : $areas);
	if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
		$result = epc_erp_nav_filter_by_industry($result, $GLOBALS['db_link']);
	}
	return $result;
}

/**
 * Ensure the favourites table exists.
 */
function epc_erp_ensure_favourites_table(PDO $db)
{
	static $done = false;
	if ($done) return;
	$done = true;
	// CREATE TABLE IF NOT EXISTS is a DDL statement — running it on every
	// single ERP page load (it was previously unconditional per-request)
	// adds real, avoidable overhead once the table already exists. Cache
	// the "already ensured" fact across requests via APCu when available
	// so we only touch the DB schema once per cache window.
	$apcuKey = 'epc_erp_favourites_table_ensured';
	if (function_exists('apcu_fetch')) {
		$hit = false;
		$cached = apcu_fetch($apcuKey, $hit);
		if ($hit && $cached) {
			return;
		}
	}
	try {
		$db->exec("
			CREATE TABLE IF NOT EXISTS `epc_erp_favourites` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`user_id` int(11) NOT NULL,
				`area_key` varchar(60) NOT NULL DEFAULT '',
				`tab_key` varchar(60) NOT NULL,
				`sort_order` int(11) NOT NULL DEFAULT 0,
				`created_at` int(11) NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				UNIQUE KEY `user_tab` (`user_id`, `tab_key`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
		");
		if (function_exists('apcu_store')) {
			apcu_store($apcuKey, true, 3600);
		}
	} catch (Throwable $e) {}
}

/**
 * Get the current user's favourite tab keys.
 */
function epc_erp_get_favourites(PDO $db): array
{
	epc_erp_ensure_favourites_table($db);
	$uid = 0;
	if (isset($_SESSION['user_id'])) $uid = (int)$_SESSION['user_id'];
	elseif (isset($_SESSION['admin_id'])) $uid = (int)$_SESSION['admin_id'];
	if ($uid <= 0) return array();
	try {
		$st = $db->prepare('SELECT area_key, tab_key FROM epc_erp_favourites WHERE user_id = ? ORDER BY sort_order, id');
		$st->execute(array($uid));
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	} catch (Throwable $e) { return array(); }
}

/**
 * Top process-area mega menu (Dynamics / NetSuite style).
 * Renders the same categories as the left sidebar across the full width so
 * modules are one hover/click away — faster than drilling three accordion levels.
 */
function epc_erp_render_top_nav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs)
{
	$areas = epc_erp_nav_areas_for_tenant();
	$categories = epc_erp_nav_categories_config();
	$activeCat = '';
	foreach ($categories as $catKey => $cat) {
		if (in_array($activeArea, (array) ($cat['areas'] ?? array()), true)) {
			$activeCat = $catKey;
			break;
		}
	}

	echo '<nav class="epc-erp-topnav" id="epc_erp_topnav" aria-label="ERP process areas">';
	echo '<div class="epc-erp-topnav-inner">';
	echo '<a class="epc-erp-topnav-brand" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'dashboard', $from, $to, 'overview')) . '"' . epc_erp_nav_shell_link_attrs() . '>';
	echo '<i class="fa fa-cubes" aria-hidden="true"></i><span>Ecom BOS</span></a>';
	echo '<ul class="epc-erp-topnav-list" role="menubar">';

	foreach ($categories as $catKey => $cat) {
		$catAreas = array();
		foreach ((array) ($cat['areas'] ?? array()) as $areaKey) {
			if (!isset($areas[$areaKey])) {
				continue;
			}
			$visibleTabs = epc_erp_nav_area_visible_tabs($areaKey, $allowedTabs);
			if (!empty($visibleTabs)) {
				$catAreas[$areaKey] = $visibleTabs;
			}
		}
		if (empty($catAreas)) {
			continue;
		}

		$isActive = ($catKey === $activeCat);
		$short = (string) ($cat['short'] ?? $cat['label']);
		$firstArea = array_key_first($catAreas);
		$firstTabs = $catAreas[$firstArea];
		$firstTab = array_key_first($firstTabs);
		$hubHref = epc_erp_tab_url($erpUrl, $firstTab, $from, $to, $firstArea);

		echo '<li class="epc-erp-topnav-item' . ($isActive ? ' is-active' : '') . '" data-category="' . epc_erp_h($catKey) . '" role="none">';
		echo '<button type="button" class="epc-erp-topnav-btn" role="menuitem" aria-haspopup="true" aria-expanded="false" data-topnav-toggle="' . epc_erp_h($catKey) . '">';
		echo '<i class="fa ' . epc_erp_h($cat['icon']) . '" aria-hidden="true"></i>';
		echo '<span class="epc-erp-topnav-label" data-full="' . epc_erp_h($cat['label']) . '" data-short="' . epc_erp_h($short) . '">' . epc_erp_h($cat['label']) . '</span>';
		echo '<i class="fa fa-angle-down epc-erp-topnav-caret" aria-hidden="true"></i>';
		echo '</button>';

		// Mega panel — columns of modules (areas) with their tabs.
		echo '<div class="epc-erp-topnav-panel" role="menu" hidden data-topnav-panel="' . epc_erp_h($catKey) . '">';
		echo '<div class="epc-erp-topnav-panel-hd">';
		echo '<div class="epc-erp-topnav-panel-title"><i class="fa ' . epc_erp_h($cat['icon']) . '"></i> ' . epc_erp_h($cat['label']) . '</div>';
		echo '<a class="epc-erp-topnav-panel-hub" href="' . epc_erp_h($hubHref) . '"' . epc_erp_nav_shell_link_attrs() . '>Open first module <i class="fa fa-arrow-right"></i></a>';
		echo '</div>';
		echo '<div class="epc-erp-topnav-cols">';
		foreach ($catAreas as $areaKey => $visibleTabs) {
			$area = $areas[$areaKey];
			$isAreaActive = ($areaKey === $activeArea);
			echo '<div class="epc-erp-topnav-col' . ($isAreaActive ? ' is-active-col' : '') . '">';
			echo '<div class="epc-erp-topnav-col-hd"><i class="fa ' . epc_erp_h($area['icon']) . '"></i> ' . epc_erp_h(epc_erp_nav_label_plain($area['label'])) . '</div>';
			echo '<ul class="epc-erp-topnav-links">';
			$n = 0;
			foreach ($visibleTabs as $tabKey => $meta) {
				if (!empty($meta['external']) || $tabKey === 'procurement_link') {
					echo '<li><a href="' . epc_erp_h(epc_erp_procurement_url()) . '" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> ' . epc_erp_h(epc_erp_nav_label_plain($meta['label'] ?? 'Procurement')) . '</a></li>';
					continue;
				}
				$hrefTab = ($tabKey === 'bank_recon') ? 'cash_bank' : $tabKey;
				$isTabActive = ($activeTab === $tabKey)
					|| ($tabKey === 'bank_recon' && $activeTab === 'cash_bank' && !empty($_GET['account_id']));
				// Cap dense columns so the mega panel stays scannable.
				if ($n >= 8) {
					$moreHref = epc_erp_tab_url($erpUrl, $hrefTab, $from, $to, $areaKey);
					echo '<li class="epc-erp-topnav-more"><a href="' . epc_erp_h($moreHref) . '"' . epc_erp_nav_shell_link_attrs() . '>More…</a></li>';
					break;
				}
				$lbl = epc_erp_nav_label_plain($meta['label'] ?? $tabKey);
				echo '<li' . ($isTabActive ? ' class="is-active"' : '') . '>';
				echo '<a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, $hrefTab, $from, $to, $areaKey)) . '"' . epc_erp_nav_shell_link_attrs() . '>';
				echo '<i class="fa ' . epc_erp_h($meta['icon'] ?? 'fa-circle-o') . '"></i> ' . epc_erp_h($lbl);
				echo '</a></li>';
				$n++;
			}
			echo '</ul></div>';
		}
		echo '</div></div>'; // panel cols + panel
		echo '</li>';
	}

	echo '</ul>'; // list
	// No side-rail "All" control — top mega-menu is the sole primary navigation.
	echo '</div></nav>';
}

/**
 * Left rail. When $contextual is true (top mega-menu is primary), only the
 * active process category is listed so the rail stays short and fast.
 */
function epc_erp_render_sidebar_nav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs, $contextual = false)
{
	$areas = epc_erp_nav_areas_for_tenant();

	// Favourites section at top of sidebar — unchanged
	$dbForFav = isset($GLOBALS['db_link']) ? $GLOBALS['db_link'] : null;
	$favItems = $dbForFav ? epc_erp_get_favourites($dbForFav) : array();
	echo '<nav class="epc-erp-sidebar-nav' . ($contextual ? ' epc-erp-sidebar-nav--contextual' : '') . '" aria-label="ERP modules">';
	if (!empty($favItems)) {
		echo '<div class="epc-erp-sidebar-favourites">';
		echo '<div class="epc-erp-sidebar-fav-head"><i class="fa fa-star" style="color:#f59e0b"></i> <span>Favourites</span></div>';
		echo '<ul class="epc-erp-sidebar-fav-list">';
		foreach ($favItems as $fav) {
			$fArea = $fav['area_key'];
			$fTab = $fav['tab_key'];
			$fMeta = array('label' => ucfirst(str_replace('_', ' ', $fTab)), 'icon' => 'fa-circle-o');
			if (isset($areas[$fArea]['tabs'][$fTab])) {
				$fMeta = $areas[$fArea]['tabs'][$fTab];
			} else {
				foreach ($areas as $aKey => $aData) {
					if (isset($aData['tabs'][$fTab])) { $fMeta = $aData['tabs'][$fTab]; $fArea = $aKey; break; }
				}
			}
			echo epc_erp_render_sidebar_item($erpUrl, $fArea, $fTab, $fMeta, $activeTab, $from, $to, true);
		}
		echo '</ul></div>';
	}

	// Render one <li> per process category, each collapsible.
	// Inside each category, render the existing area groups exactly as before.
	$categories = epc_erp_nav_categories_config();
	$hasActiveCategory = false;
	foreach ($categories as $cat) {
		if (in_array($activeArea, (array) ($cat['areas'] ?? array()), true)) {
			$hasActiveCategory = true;
			break;
		}
	}
	// If the current area is unknown, fall back to the full list.
	$filterToActive = ($contextual && $hasActiveCategory);
	if ($filterToActive) {
		echo '<div class="epc-erp-sidebar-rail-hint"><i class="fa fa-mouse-pointer"></i> <span>Modules for this area</span></div>';
	}
	echo '<ul class="epc-erp-sidebar-list">';
	foreach ($categories as $catKey => $cat) {
		// Collect all areas for this category that have at least one visible tab.
		$catAreas = array();
		foreach ($cat['areas'] as $areaKey) {
			if (!isset($areas[$areaKey])) {
				continue;
			}
			$visibleTabs = epc_erp_nav_area_visible_tabs($areaKey, $allowedTabs);
			if (!empty($visibleTabs)) {
				$catAreas[$areaKey] = $visibleTabs;
			}
		}
		if (empty($catAreas)) {
			continue;
		}
		$isCatActive = isset($catAreas[$activeArea]);
		// With top mega-menu as primary nav, only render the active category
		// so the left rail stays contextual and fast.
		if ($filterToActive && !$isCatActive) {
			continue;
		}
		$isCatOpen   = $isCatActive || $filterToActive;
		echo '<li class="epc-erp-sidebar-category' . ($isCatActive ? ' is-active-category' : '')
			. ($isCatOpen ? ' is-open' : '') . '" data-category="' . epc_erp_h($catKey) . '">';
		echo '<button type="button" class="epc-erp-sidebar-category-hd" aria-expanded="' . ($isCatOpen ? 'true' : 'false') . '">';
		echo '<i class="fa ' . epc_erp_h($cat['icon']) . ' epc-erp-sidebar-icon"></i>';
		echo '<span class="epc-erp-sidebar-label">' . epc_erp_h($cat['label']) . '</span>';
		echo '<i class="fa fa-chevron-right epc-erp-sidebar-chevron" aria-hidden="true"></i>';
		echo '</button>';
		// Areas inside this category
		echo '<ul class="epc-erp-sidebar-category-areas">';
		foreach ($catAreas as $areaKey => $visibleTabs) {
			$area = $areas[$areaKey];
			$isActiveArea = ($areaKey === $activeArea);
			$isOpen = $isActiveArea || ($filterToActive && count($catAreas) === 1);
			echo '<li class="epc-erp-sidebar-group' . ($isActiveArea ? ' is-active-area' : '')
				. ($isOpen ? ' is-open' : '') . '" data-area="' . epc_erp_h($areaKey) . '">';
			echo '<button type="button" class="epc-erp-sidebar-group-hd" aria-expanded="' . ($isOpen ? 'true' : 'false') . '">';
			echo '<i class="fa ' . epc_erp_h($area['icon']) . ' epc-erp-sidebar-icon"></i>';
			echo '<span class="epc-erp-sidebar-label">' . epc_erp_h(epc_erp_nav_label_plain($area['label'])) . '</span>';
			echo '<i class="fa fa-chevron-right epc-erp-sidebar-chevron" aria-hidden="true"></i>';
			echo '</button>';
			echo '<ul class="epc-erp-sidebar-sublist">';
			// Enterprise style: group sub-modules when the area defines a 'groups' map.
			if (!empty($area['groups']) && is_array($area['groups'])) {
				$rendered = array();
				foreach ($area['groups'] as $groupLabel => $groupTabKeys) {
					$groupItems = array();
					foreach ((array) $groupTabKeys as $tabKey) {
						if (isset($visibleTabs[$tabKey])) {
							$groupItems[$tabKey] = $visibleTabs[$tabKey];
						}
					}
					if (empty($groupItems)) {
						continue;
					}
					echo '<li class="epc-erp-sidebar-subhead" aria-hidden="true">' . epc_erp_h((string) $groupLabel) . '</li>';
					foreach ($groupItems as $tabKey => $meta) {
						echo epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, $meta, $activeTab, $from, $to);
						$rendered[$tabKey] = true;
					}
				}
				// Any visible tab not covered by a group falls under "More".
				$leftover = array_diff_key($visibleTabs, $rendered);
				if (!empty($leftover)) {
					echo '<li class="epc-erp-sidebar-subhead" aria-hidden="true">More</li>';
					foreach ($leftover as $tabKey => $meta) {
						echo epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, $meta, $activeTab, $from, $to);
					}
				}
			} else {
				foreach ($visibleTabs as $tabKey => $meta) {
					echo epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, $meta, $activeTab, $from, $to);
				}
			}
			echo '</ul></li>';
		}
		echo '</ul></li>';
	}
	echo '</ul></nav>';
}

/** Render a single sidebar sub-module <li>. */
function epc_erp_render_sidebar_item($erpUrl, $areaKey, $tabKey, array $meta, $activeTab, $from, $to, $inFavSection = false)
{
	if (!empty($meta['external']) || $tabKey === 'procurement_link') {
		return '<li class="epc-erp-sidebar-item epc-erp-sidebar-item--external">'
			. '<a href="' . epc_erp_h(epc_erp_procurement_url()) . '" target="_blank" rel="noopener">'
			. '<i class="fa fa-external-link"></i> ' . epc_erp_h(epc_erp_nav_label_plain($meta['label'] ?? 'Procurement')) . '</a></li>';
	}
	$hrefTab = ($tabKey === 'bank_recon') ? 'cash_bank' : $tabKey;
	$isActive = ($activeTab === $tabKey)
		|| ($tabKey === 'bank_recon' && $activeTab === 'cash_bank' && !empty($_GET['account_id']));
	$lbl = !empty($meta['raw']) ? $meta['label'] : epc_erp_h(epc_erp_nav_label_plain($meta['label']));
	$out = '<li class="epc-erp-sidebar-item' . ($isActive ? ' is-active' : '') . '">';
	$out .= '<a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, $hrefTab, $from, $to, $areaKey)) . '"' . epc_erp_nav_shell_link_attrs() . '>';
	if (empty($meta['raw'])) {
		$out .= '<i class="fa ' . epc_erp_h($meta['icon']) . '"></i> ';
	}
	$out .= $lbl . '</a>';
	if (!$inFavSection) {
		$out .= '<button type="button" class="epc-erp-fav-star" data-tab="' . epc_erp_h($tabKey) . '" data-area="' . epc_erp_h($areaKey) . '" title="Add to favourites"><i class="fa fa-star-o"></i></button>';
	} else {
		$out .= '<button type="button" class="epc-erp-fav-unstar" data-tab="' . epc_erp_h($tabKey) . '" title="Remove from favourites"><i class="fa fa-star" style="color:#f59e0b"></i></button>';
	}
	$out .= '</li>';
	return $out;
}

function epc_erp_render_content_header($erpUrl, $activeArea, $activeTab, $from, $to)
{
	$areas = epc_erp_nav_areas_for_tenant();
	$areaLabel = isset($areas[$activeArea]['label'])
		? epc_erp_nav_label_plain($areas[$activeArea]['label'])
		: ucfirst($activeArea);
	$areaDesc = isset($areas[$activeArea]['desc']) ? $areas[$activeArea]['desc'] : '';
	$tabLabel = epc_erp_nav_tab_label($activeArea, $activeTab);
	if ($activeTab === 'bank_recon' || ($activeTab === 'cash_bank' && !empty($_GET['account_id']))) {
		$tabLabel = 'Bank recon';
	}
	$tabIcon = 'fa-folder-open-o';
	if (isset($areas[$activeArea]['tabs'][$activeTab]['icon'])) {
		$tabIcon = $areas[$activeArea]['tabs'][$activeTab]['icon'];
	} elseif ($activeTab === 'bank_recon' || ($activeTab === 'cash_bank' && !empty($_GET['account_id']))) {
		$tabIcon = 'fa-check-square';
	}
	$dashUrl = epc_erp_h(epc_erp_tab_url($erpUrl, 'dashboard', $from, $to, 'overview'));
	echo '<div class="epc-erp-content-header">';
	// Company (legal-entity) picker, top-right of the header.
	$companyPicker = '';
	if (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
		if (!function_exists('epc_erp_company_picker_html')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
		}
		try {
			$companyPicker = epc_erp_company_picker_html($GLOBALS['db_link']);
		} catch (Throwable $e) {
			$companyPicker = '';
		}
	}
	if ($companyPicker !== '') {
		echo '<div class="epc-erp-company-scope" style="float:right;margin-top:2px;">' . $companyPicker . '</div>';
	}
	echo '<nav class="epc-erp-breadcrumb" aria-label="Breadcrumb">';
	echo '<a href="' . $dashUrl . '"' . epc_erp_nav_shell_link_attrs() . '>ERP</a>';
	echo ' <span class="sep">/</span> <span>' . epc_erp_h($areaLabel) . '</span>';
	echo ' <span class="sep">/</span> <span class="epc-erp-breadcrumb-current">' . epc_erp_h($tabLabel) . '</span>';
	echo '</nav>';
	echo '<h1 class="epc-erp-content-title"><i class="fa ' . epc_erp_h($tabIcon) . '"></i> ';
	echo epc_erp_h($tabLabel) . '</h1>';
	if ($areaDesc !== '') {
		echo '<p class="epc-erp-content-subtitle">' . epc_erp_h($areaDesc) . '</p>';
	}
	echo '</div>';
}

/**
 * Render a contextual "accounting chain" navigation strip.
 *
 * $chain is an ordered array of ['tab', 'area', 'label', 'icon'] steps.
 * The step matching $activeTab is highlighted as current.
 */
function epc_erp_render_chain_nav(array $chain, $erpUrl, $activeTab, $from, $to)
{
	echo '<div class="epc-erp-chain-nav" aria-label="Process chain">';
	echo '<span class="epc-erp-chain-nav__label"><i class="fa fa-link"></i> Chain:</span>';
	$count = count($chain);
	foreach ($chain as $i => $step) {
		$isCurrent = ($step['tab'] === $activeTab);
		$url = epc_erp_tab_url($erpUrl, $step['tab'], $from, $to, $step['area']);
		$cls = $isCurrent ? ' is-current' : '';
		echo '<a href="' . epc_erp_h($url) . '" class="' . ltrim($cls) . '">'
			. '<i class="fa ' . epc_erp_h($step['icon'] ?? 'fa-circle-o') . '"></i> '
			. epc_erp_h($step['label']) . '</a>';
		if ($i < $count - 1) {
			echo '<span class="epc-erp-chain-sep">›</span>';
		}
	}
	echo '</div>';
}

/**
 * AP (Procure-to-Pay) chain definition.
 */
function epc_erp_ap_chain()
{
	return array(
		array('tab' => 'rfq',            'area' => 'purchasing', 'label' => 'RFQ',            'icon' => 'fa-envelope-o'),
		array('tab' => 'purchase_orders','area' => 'purchasing', 'label' => 'Purchase orders', 'icon' => 'fa-clipboard'),
		array('tab' => 'purchases',      'area' => 'purchasing', 'label' => 'Bills',           'icon' => 'fa-file-text'),
		array('tab' => 'three_way_match','area' => 'purchasing', 'label' => '3-way match',     'icon' => 'fa-check-square-o'),
		array('tab' => 'payables',       'area' => 'ap',         'label' => 'Payables',        'icon' => 'fa-truck'),
		array('tab' => 'aging',          'area' => 'finance',    'label' => 'AP aging',        'icon' => 'fa-hourglass-half'),
	);
}

/**
 * AR (Order-to-Cash) chain definition.
 */
function epc_erp_ar_chain()
{
	return array(
		array('tab' => 'proposals',      'area' => 'sales',   'label' => 'Quotations',    'icon' => 'fa-file-text'),
		array('tab' => 'sales_orders',   'area' => 'sales',   'label' => 'Sales orders',  'icon' => 'fa-shopping-cart'),
		array('tab' => 'delivery_notes', 'area' => 'sales',   'label' => 'Delivery',      'icon' => 'fa-truck'),
		array('tab' => 'invoices',       'area' => 'sales',   'label' => 'Invoices',      'icon' => 'fa-file-text-o'),
		array('tab' => 'receivables',    'area' => 'ar',      'label' => 'Receivables',   'icon' => 'fa-users'),
		array('tab' => 'aging',          'area' => 'finance', 'label' => 'AR aging',      'icon' => 'fa-hourglass-half'),
	);
}

/**
 * GL / Record-to-Report chain definition.
 */
function epc_erp_gl_chain()
{
	return array(
		array('tab' => 'coa',                'area' => 'finance', 'label' => 'COA',           'icon' => 'fa-list'),
		array('tab' => 'gl',                 'area' => 'finance', 'label' => 'Journal / GL',  'icon' => 'fa-book'),
		array('tab' => 'enterprise_reports', 'area' => 'finance', 'label' => 'Trial balance', 'icon' => 'fa-table'),
		array('tab' => 'pl',                 'area' => 'finance', 'label' => 'P&L',           'icon' => 'fa-bar-chart'),
		array('tab' => 'balance_sheet',      'area' => 'finance', 'label' => 'Balance sheet', 'icon' => 'fa-balance-scale'),
	);
}

/** @deprecated Horizontal pills removed — use epc_erp_render_sidebar_nav */
function epc_erp_render_area_nav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs)
{
}

/** @deprecated Sub-nav merged into left sidebar */
function epc_erp_render_area_subnav($erpUrl, $activeArea, $activeTab, $from, $to, array $allowedTabs)
{
}

function epc_erp_render_notifications_stub(PDO $db)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
	$n = epc_erp_notifications_unread_count($db);
	echo '<div class="epc-erp-notify-stub dropdown">';
	echo '<button type="button" class="btn btn-default btn-xs dropdown-toggle" data-toggle="dropdown" title="Notifications">';
	echo '<i class="fa fa-bell"></i>';
	if ($n > 0) {
		echo ' <span class="badge">' . (int) $n . '</span>';
	}
	echo '</button>';
	echo '<ul class="dropdown-menu dropdown-menu-right epc-erp-notify-menu">';
	$items = epc_erp_notifications_list($db, 8);
	if (empty($items)) {
		echo '<li class="text-muted" style="padding:10px 14px;">No notifications yet.</li>';
	} else {
		foreach ($items as $it) {
			echo '<li><a href="#"><strong>' . epc_erp_h($it['title']) . '</strong><br><small>' . epc_erp_h($it['body']) . '</small></a></li>';
		}
	}
	echo '<li class="divider"></li><li class="text-center"><small class="text-muted">Notification centre (stub)</small></li>';
	echo '</ul></div>';
}

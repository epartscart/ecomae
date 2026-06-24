<?php
/**
 * Unified BOS kernel — session bridge, tenant context, sidebar engine.
 *
 * Entry: ecomae.com/bos/
 * Provider  → boc.* permission → sees fleet + tenant switcher
 * Tenant    → own site_key only → sees their CP + ERP
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_portal_db.php';
require_once __DIR__ . '/epc_portal_cp_menu.php';

/* ───────────────────── constants ───────────────────── */

define('EPC_BOS_VERSION', '1.4.0');
define('EPC_BOS_SESSION_KEY', 'epc_bos_context');

/* ───────────────────── session bridge ───────────────────── */

function epc_bos_session_start(): void
{
    if (session_status() === PHP_SESSION_NONE) {
        session_start();
    }
}

function epc_bos_context(): array
{
    epc_bos_session_start();
    return $_SESSION[EPC_BOS_SESSION_KEY] ?? array(
        'role'        => 'guest',
        'user_id'     => 0,
        'email'       => '',
        'tenant_key'  => '',
        'tenant_pdo'  => null,
    );
}

function epc_bos_set_context(array $ctx): void
{
    epc_bos_session_start();
    $_SESSION[EPC_BOS_SESSION_KEY] = $ctx;
}

function epc_bos_is_provider(): bool
{
    $ctx = epc_bos_context();
    return $ctx['role'] === 'provider';
}

function epc_bos_is_tenant_user(): bool
{
    $ctx = epc_bos_context();
    return $ctx['role'] === 'tenant';
}

function epc_bos_active_tenant_key(): string
{
    $ctx = epc_bos_context();
    return (string) ($ctx['tenant_key'] ?? '');
}

/* ───────────────────── tenant switcher data ───────────────────── */

function epc_bos_tenant_list(PDO $platformPdo): array
{
    require_once __DIR__ . '/epc_portal_tenant_control.php';
    epc_portal_tenant_control_ensure_schema($platformPdo);

    $st = $platformPdo->query(
        "SELECT `site_key`, `hostname`, `industry_code`, `status`, `trade_name`, `hub_name`,
                `hosted_on`, `erp_only_shared`, `is_active`, `db_name`
         FROM `epc_portal_tenants`
         WHERE `site_key` != ''
         ORDER BY `status` ASC, `trade_name` ASC"
    );
    $tenants = array();
    while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
        $industries = epc_portal_industries();
        $ind = $industries[$row['industry_code']] ?? null;
        $tenants[] = array(
            'site_key'      => $row['site_key'],
            'hostname'      => $row['hostname'],
            'industry'      => $row['industry_code'],
            'industry_name' => $ind ? $ind['name'] : ucfirst(str_replace('_', ' ', $row['industry_code'])),
            'industry_icon' => $ind ? $ind['icon'] : 'fa-building',
            'status'        => $row['status'],
            'trade_name'    => $row['trade_name'],
            'hub_name'      => $row['hub_name'],
            'hosted_on'     => $row['hosted_on'] ?? 'client',
            'erp_only'      => !empty($row['erp_only_shared']),
            'is_active'     => (int) ($row['is_active'] ?? 1),
            'has_db'        => trim($row['db_name'] ?? '') !== '',
            'type'          => epc_bos_resolve_tenant_type($row),
        );
    }
    return $tenants;
}

function epc_bos_resolve_tenant_type(array $row): string
{
    if (!empty($row['erp_only_shared'])) {
        return 'erp_only';
    }
    $status = (string) ($row['status'] ?? '');
    if ($status === 'demo' || strpos($row['site_key'] ?? '', 'demo_') === 0) {
        return 'demo';
    }
    $industry = (string) ($row['industry_code'] ?? '');
    if ($industry === 'platform_host') {
        return 'platform';
    }
    if ($industry === 'erp_standalone') {
        return 'erp_only';
    }
    return 'commerce';
}

/* ───────────────────── tenant DB connect ───────────────────── */

function epc_bos_tenant_connect(PDO $platformPdo, string $siteKey): array
{
    require_once __DIR__ . '/epc_portal_tenant_control.php';
    $row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
    if ($row === null) {
        return array('pdo' => null, 'error' => 'Tenant not found: ' . $siteKey, 'row' => null);
    }
    $result = epc_portal_tenant_control_tenant_pdo_connect($row);
    $result['row'] = $row;
    return $result;
}

/* ───────────────────── sidebar engine ───────────────────── */

function epc_bos_sidebar_sections(PDO $platformPdo, ?string $tenantKey = null): array
{
    $sections = array();

    // Section 1: BOS Fleet (provider only)
    $sections['fleet'] = array(
        'id'    => 'fleet',
        'label' => 'Fleet Command',
        'icon'  => 'fa-tachometer',
        'items' => epc_bos_fleet_items(),
        'provider_only' => true,
    );

    // Section 2: Tenant operations (provider only)
    $sections['tenants'] = array(
        'id'    => 'tenants',
        'label' => 'Tenant Operations',
        'icon'  => 'fa-sitemap',
        'items' => epc_bos_tenant_ops_items(),
        'provider_only' => true,
    );

    // Section 3: Active tenant CP modules (if a tenant is selected)
    if ($tenantKey !== null && $tenantKey !== '') {
        $tenantSettings = epc_bos_load_tenant_settings($platformPdo, $tenantKey);
        $packs = (array) ($tenantSettings['enabled_packs'] ?? array('core'));

        $sections['commerce'] = array(
            'id'    => 'commerce',
            'label' => 'Commerce',
            'icon'  => 'fa-shopping-cart',
            'items' => epc_bos_commerce_items($packs),
            'provider_only' => false,
            'requires_pack' => array('commerce', 'auto_parts', 'catalogue'),
        );

        $sections['catalogue'] = array(
            'id'    => 'catalogue',
            'label' => 'Catalogue',
            'icon'  => 'fa-th-large',
            'items' => epc_bos_catalogue_items($packs),
            'provider_only' => false,
            'requires_pack' => array('catalogue', 'auto_parts', 'commerce'),
        );

        $sections['logistics'] = array(
            'id'    => 'logistics',
            'label' => 'Logistics',
            'icon'  => 'fa-truck',
            'items' => epc_bos_logistics_items(),
            'provider_only' => false,
            'requires_pack' => array('logistics', 'auto_parts'),
        );

        $sections['marketing_cp'] = array(
            'id'    => 'marketing_cp',
            'label' => 'Marketing',
            'icon'  => 'fa-bullhorn',
            'items' => epc_bos_marketing_items(),
            'provider_only' => false,
            'requires_pack' => array('marketing', 'commerce'),
        );

        $sections['professional'] = array(
            'id'    => 'professional',
            'label' => 'Professional',
            'icon'  => 'fa-briefcase',
            'items' => epc_bos_professional_items(),
            'provider_only' => false,
            'requires_pack' => array('professional', 'erp'),
        );

        // ERP section (if erp pack is enabled)
        if (in_array('erp', $packs, true) || in_array('professional', $packs, true)) {
            $erpModules = epc_bos_resolve_erp_modules($tenantSettings);
            $sections['erp'] = array(
                'id'    => 'erp',
                'label' => 'ERP Finance',
                'icon'  => 'fa-university',
                'items' => epc_bos_erp_items($erpModules),
                'provider_only' => false,
                'requires_pack' => array('erp'),
            );
        }

        // Auto parts specialty (only for auto_parts industry)
        if (in_array('auto_parts', $packs, true)) {
            $sections['auto_parts'] = array(
                'id'    => 'auto_parts',
                'label' => 'Auto Parts',
                'icon'  => 'fa-car',
                'items' => epc_bos_auto_parts_items(),
                'provider_only' => false,
                'requires_pack' => array('auto_parts'),
            );
        }

        // Tax advisory (only for tax_advisory industry)
        if (in_array('tax_advisory', $packs, true)) {
            $sections['tax_advisory'] = array(
                'id'    => 'tax_advisory',
                'label' => 'Tax & Advisory',
                'icon'  => 'fa-balance-scale',
                'items' => epc_bos_tax_advisory_items(),
                'provider_only' => false,
                'requires_pack' => array('tax_advisory'),
            );
        }

        // Filter sections by tenant packs
        foreach ($sections as $key => &$section) {
            if (!empty($section['requires_pack'])) {
                $hasAny = false;
                foreach ($section['requires_pack'] as $rp) {
                    if (in_array($rp, $packs, true)) {
                        $hasAny = true;
                        break;
                    }
                }
                if (!$hasAny) {
                    unset($sections[$key]);
                }
            }
        }
        unset($section);
    }

    // Section N: Platform tools (always)
    $sections['platform'] = array(
        'id'    => 'platform',
        'label' => 'Platform',
        'icon'  => 'fa-cog',
        'items' => epc_bos_platform_items(),
        'provider_only' => false,
    );

    return $sections;
}

/* ───────────────────── fleet items (BOC areas) ───────────────────── */

function epc_bos_fleet_items(): array
{
    return array(
        array('id' => 'command_center',  'label' => 'Command Center',     'icon' => 'fa-tachometer',   'path' => 'control/portal/epc_boc_command_center'),
        array('id' => 'platform_health', 'label' => 'Platform Health',    'icon' => 'fa-heartbeat',    'path' => 'control/portal/epc_platform_health_checkup'),
        array('id' => 'governance',      'label' => 'Governance',         'icon' => 'fa-gavel',        'path' => 'control/portal/epc_platform_governance'),
        array('id' => 'audit_log',       'label' => 'Audit Log',          'icon' => 'fa-history',      'path' => 'control/portal/epc_boc_audit_log'),
        array('id' => 'failover',        'label' => 'Failover Runbook',   'icon' => 'fa-life-ring',    'path' => 'control/portal/epc_platform_failover_guide'),
        array('id' => 'isolation_audit', 'label' => 'Isolation Audit',   'icon' => 'fa-shield',       'path' => 'control/portal/epc_commerce_isolation_audit'),
        array('id' => 'mfa_management',  'label' => 'MFA / 2FA',          'icon' => 'fa-lock',         'path' => 'control/portal/epc_mfa_management'),
        array('id' => 'event_bus',       'label' => 'Event Bus',          'icon' => 'fa-bolt',         'path' => 'control/portal/epc_event_bus'),
        array('id' => 'webhooks',        'label' => 'Webhooks',           'icon' => 'fa-plug',         'path' => 'control/portal/epc_webhooks'),
        array('id' => 'readiness_score', 'label' => 'Readiness Score',  'icon' => 'fa-trophy',       'path' => 'control/portal/epc_readiness_score'),
        array('id' => 'notifications',  'label' => 'Notifications',     'icon' => 'fa-bell',         'path' => 'control/portal/epc_notifications'),
        array('id' => 'db_migrations',   'label' => 'DB Migrations',     'icon' => 'fa-database',     'path' => 'control/portal/epc_db_migrations'),
        array('id' => 'cp_role_home', 'label' => 'CP Roles',        'icon' => 'fa-users',        'path' => 'control/portal/epc_cp_role_home'),
        array('id' => 'credit_limit', 'label' => 'Credit Limits',   'icon' => 'fa-credit-card',  'path' => 'shop/finance/epc_credit_limit'),
        array('id' => 'order_erp_pipeline', 'label' => 'Order→ERP',       'icon' => 'fa-exchange',     'path' => 'shop/finance/epc_order_erp_pipeline'),
        array('id' => 'po_approval', 'label' => 'PO Approval',     'icon' => 'fa-check-square-o', 'path' => 'shop/finance/epc_po_approval'),
        array('id' => 'rest_api_v2', 'label' => 'API v2',          'icon' => 'fa-code',         'path' => 'control/portal/epc_rest_api_v2'),
        array('id' => 'fulfillment_queue', 'label' => 'Fulfillment',     'icon' => 'fa-truck',        'path' => 'shop/finance/epc_fulfillment_queue'),
        array('id' => 'bi_metrics', 'label' => 'BI Metrics',      'icon' => 'fa-bar-chart',    'path' => 'control/portal/epc_bi_metrics'),
        array('id' => 'ai_classification', 'label' => 'AI Classify',     'icon' => 'fa-magic',        'path' => 'control/portal/epc_ai_classification'),
        array('id' => 'tenant_config', 'label' => 'Tenant Config',   'icon' => 'fa-cog',          'path' => 'control/portal/epc_tenant_config'),
        array('id' => 'workflow_builder', 'label' => 'Workflows',       'icon' => 'fa-random',       'path' => 'control/portal/epc_workflow_builder'),
        array('id' => 'inventory_forecast', 'label' => 'Forecasting',     'icon' => 'fa-line-chart',   'path' => 'shop/finance/epc_inventory_forecast'),
        array('id' => 'multi_currency_gl', 'label' => 'Multi-Currency',  'icon' => 'fa-money',        'path' => 'shop/finance/epc_multi_currency_gl'),
        array('id' => 'sso_saml', 'label' => 'SSO / SAML',      'icon' => 'fa-key',          'path' => 'control/portal/epc_sso_saml'),
        array('id' => 'wps_payroll', 'label' => 'Payroll',         'icon' => 'fa-money',        'path' => 'shop/finance/epc_wps_payroll'),
        array('id' => 'collections_dunning', 'label' => 'Collections',     'icon' => 'fa-bell',         'path' => 'shop/finance/epc_collections_dunning'),
        array('id' => 'warranty_rma', 'label' => 'Warranty/RMA',    'icon' => 'fa-shield',       'path' => 'shop/finance/epc_warranty_rma'),
        array('id' => 'dealer_portal', 'label' => 'Dealer Portal',   'icon' => 'fa-handshake-o',  'path' => 'control/portal/epc_dealer_portal'),
        array('id' => 'ai_copilot', 'label' => 'AI Copilot',      'icon' => 'fa-commenting',   'path' => 'control/portal/epc_ai_copilot'),
        array('id' => 'nl_reporting', 'label' => 'NL Reports',      'icon' => 'fa-file-text',    'path' => 'control/portal/epc_nl_reporting'),
        array('id' => 'industry_packs', 'label' => 'Industry Packs',  'icon' => 'fa-industry',     'path' => 'control/portal/epc_industry_packs'),
        array('id' => 'multi_entity', 'label' => 'Multi-Entity',    'icon' => 'fa-building',     'path' => 'shop/finance/epc_multi_entity'),
        array('id' => 'promotions_engine', 'label' => 'Promotions',      'icon' => 'fa-tags',         'path' => 'control/portal/epc_promotions_engine'),
        array('id' => 'config_sandbox', 'label' => 'Sandbox',         'icon' => 'fa-flask',        'path' => 'control/portal/epc_config_sandbox'),
        array('id' => 'import_orchestrator', 'label' => 'Imports',         'icon' => 'fa-upload',       'path' => 'control/portal/epc_import_orchestrator'),
        array('id' => 'document_vault', 'label' => 'Doc Vault',       'icon' => 'fa-archive',      'path' => 'control/portal/epc_document_vault'),
        array('id' => 'subscription_billing', 'label' => 'Billing',         'icon' => 'fa-credit-card',  'path' => 'shop/finance/epc_subscription_billing'),
        array('id' => 'soc2_compliance', 'label' => 'SOC 2',           'icon' => 'fa-certificate',  'path' => 'control/portal/epc_soc2_compliance'),
        array('id' => 'config_sandbox', 'label' => 'Sandbox',         'icon' => 'fa-flask',        'path' => 'control/portal/epc_config_sandbox'),
    );
}

function epc_bos_tenant_ops_items(): array
{
    return array(
        array('id' => 'tenant_hub',      'label' => 'Tenant Hub',         'icon' => 'fa-sitemap',      'path' => 'shop/tenant_hub/tenant_hub'),
        array('id' => 'tenant_control',  'label' => 'Tenant Control',     'icon' => 'fa-sliders',      'path' => 'control/portal/epc_tenant_control_center'),
        array('id' => 'tenant_features', 'label' => 'Feature Matrix',     'icon' => 'fa-th',           'path' => 'control/portal/epc_tenant_features'),
        array('id' => 'demo_tenants',    'label' => 'Demo Tenants',       'icon' => 'fa-flask',        'path' => 'control/portal/epc_demo_tenants_manage'),
        array('id' => 'industry_packs',  'label' => 'Industry / ERP Packs', 'icon' => 'fa-cubes',     'path' => 'control/portal/industry_settings'),
        array('id' => 'customer_board',  'label' => 'Customer Board',     'icon' => 'fa-users',        'path' => 'control/portal/epc_super_cp_customer_board'),
        array('id' => 'integrations',    'label' => 'Integrations Hub',   'icon' => 'fa-plug',         'path' => 'control/portal/epc_integrations_hub'),
        array('id' => 'design_tokens',  'label' => 'Design Tokens',     'icon' => 'fa-paint-brush',  'path' => 'control/portal/epc_design_tokens'),
    );
}

/* ───────────────────── CP module items ───────────────────── */

function epc_bos_commerce_items(array $packs): array
{
    $items = array(
        array('id' => 'orders',       'label' => 'Orders',            'icon' => 'fa-shopping-cart',  'path' => 'shop/sao/robot'),
        array('id' => 'customers',    'label' => 'Customers',         'icon' => 'fa-users',          'path' => 'shop/customer_mgmt/customer_mgmt'),
        array('id' => 'payments',     'label' => 'Payments',          'icon' => 'fa-credit-card',    'path' => 'shop/payments/payments_main'),
        array('id' => 'returns',      'label' => 'Returns',           'icon' => 'fa-undo',           'path' => 'shop/returns/returns_main'),
        array('id' => 'quotes',       'label' => 'Quote Requests',    'icon' => 'fa-file-text',      'path' => 'shop/quote_requests/quote_requests_main'),
        array('id' => 'channels',     'label' => 'Channels',          'icon' => 'fa-share-alt',      'path' => 'shop/channels/channels_main'),
        array('id' => 'pos',          'label' => 'POS Terminal',      'icon' => 'fa-calculator',     'path' => 'shop/pos/epc_pos_hub_page'),
        array('id' => 'statistics',   'label' => 'Statistics',        'icon' => 'fa-bar-chart',      'path' => 'shop/statistics/statistics'),
    );
    return $items;
}

function epc_bos_catalogue_items(array $packs): array
{
    return array(
        array('id' => 'products',     'label' => 'Products',          'icon' => 'fa-cube',           'path' => 'shop/catalogue/catalogue'),
        array('id' => 'prices_edit',  'label' => 'Prices Edit',       'icon' => 'fa-tag',            'path' => 'shop/prices_edit/prices_edit'),
        array('id' => 'prices_upload','label' => 'Prices Upload',     'icon' => 'fa-upload',         'path' => 'shop/prices_upload/prices_manager'),
        array('id' => 'prices_send',  'label' => 'Prices Send',       'icon' => 'fa-paper-plane',    'path' => 'shop/prices_send/prices_send'),
        array('id' => 'pricing',      'label' => 'Pricing Rules',     'icon' => 'fa-percent',        'path' => 'shop/pricing/pricing_main'),
    );
}

function epc_bos_logistics_items(): array
{
    return array(
        array('id' => 'logistics',    'label' => 'Delivery & Shipping','icon' => 'fa-truck',         'path' => 'shop/logistics/logistics'),
        array('id' => 'procurement',  'label' => 'Procurement',       'icon' => 'fa-shopping-basket','path' => 'shop/procurement/procurement_main'),
    );
}

function epc_bos_marketing_items(): array
{
    return array(
        array('id' => 'marketing',    'label' => 'Campaigns',         'icon' => 'fa-bullhorn',       'path' => 'shop/marketing/marketing_main'),
        array('id' => 'broadcast',    'label' => 'Broadcast',         'icon' => 'fa-envelope',       'path' => 'control/portal/epc_marketing_broadcast'),
        array('id' => 'social',       'label' => 'Social Media',      'icon' => 'fa-share-alt',      'path' => 'control/portal/epc_social_media_hub'),
        array('id' => 'seo',          'label' => 'SEO',               'icon' => 'fa-search',         'path' => 'shop/marketing/seo_main'),
    );
}

function epc_bos_professional_items(): array
{
    return array(
        array('id' => 'crm',          'label' => 'CRM',               'icon' => 'fa-handshake-o',    'path' => 'shop/crm/crm_main'),
        array('id' => 'documents',    'label' => 'Documents',         'icon' => 'fa-folder-open',    'path' => 'shop/document_control/document_control'),
        array('id' => 'parts_agent',  'label' => 'Parts Agent',       'icon' => 'fa-comments',       'path' => 'shop/parts_agent/parts_agent_chats'),
    );
}

/* ───────────────────── ERP items ───────────────────── */

function epc_bos_resolve_erp_modules(?array $settings): array
{
    if ($settings === null) {
        $settings = array();
    }
    require_once __DIR__ . '/epc_portal_erp_modules.php';
    return epc_portal_erp_modules_enabled($settings);
}

function epc_bos_erp_items(array $enabledModules): array
{
    $all = array(
        array('id' => 'erp_home',        'label' => 'ERP Home',          'icon' => 'fa-th-large',       'path' => 'shop/finance/erp?epc_erp_shell=1&area=overview',        'module' => 'erp_overview'),
        array('id' => 'erp_gl',          'label' => 'General Ledger',    'icon' => 'fa-book',           'path' => 'shop/finance/erp?epc_erp_shell=1&area=gl',              'module' => 'erp_finance'),
        array('id' => 'erp_ap',          'label' => 'Accounts Payable',  'icon' => 'fa-credit-card',    'path' => 'shop/finance/erp?epc_erp_shell=1&area=ap',              'module' => 'erp_finance'),
        array('id' => 'erp_ar',          'label' => 'Accounts Receivable','icon' => 'fa-users',         'path' => 'shop/finance/erp?epc_erp_shell=1&area=ar',              'module' => 'erp_finance'),
        array('id' => 'erp_cash',        'label' => 'Cash & Bank',       'icon' => 'fa-university',     'path' => 'shop/finance/erp?epc_erp_shell=1&area=cash_bank',       'module' => 'erp_finance'),
        array('id' => 'erp_tax',         'label' => 'Tax & Compliance',  'icon' => 'fa-balance-scale',  'path' => 'shop/finance/erp?epc_erp_shell=1&area=tax',             'module' => 'erp_finance'),
        array('id' => 'erp_sales',       'label' => 'Sales & CRM',       'icon' => 'fa-line-chart',     'path' => 'shop/finance/erp?epc_erp_shell=1&area=sales',           'module' => 'erp_sales'),
        array('id' => 'erp_purchasing',  'label' => 'Purchasing',        'icon' => 'fa-shopping-basket','path' => 'shop/finance/erp?epc_erp_shell=1&area=purchasing',      'module' => 'erp_purchasing'),
        array('id' => 'erp_inventory',   'label' => 'Inventory',         'icon' => 'fa-cubes',          'path' => 'shop/finance/erp?epc_erp_shell=1&area=inventory_mgmt',  'module' => 'erp_operations'),
        array('id' => 'erp_hr',          'label' => 'Human Resources',   'icon' => 'fa-id-card',        'path' => 'shop/finance/erp?epc_erp_shell=1&area=people',          'module' => 'erp_people'),
        array('id' => 'erp_payroll',     'label' => 'Payroll',           'icon' => 'fa-money',          'path' => 'shop/finance/erp?epc_erp_shell=1&area=payroll',          'module' => 'erp_people'),
        array('id' => 'erp_production',  'label' => 'Production',        'icon' => 'fa-industry',       'path' => 'shop/finance/erp?epc_erp_shell=1&area=production',       'module' => 'erp_operations'),
        array('id' => 'erp_projects',    'label' => 'Projects',          'icon' => 'fa-tasks',          'path' => 'shop/finance/erp?epc_erp_shell=1&area=projects',         'module' => 'erp_operations'),
        array('id' => 'erp_warehouse',   'label' => 'Warehouse',         'icon' => 'fa-building-o',     'path' => 'shop/finance/erp?epc_erp_shell=1&area=warehouse',        'module' => 'erp_operations'),
        array('id' => 'erp_fixed_assets','label' => 'Fixed Assets',      'icon' => 'fa-building',       'path' => 'shop/finance/erp?epc_erp_shell=1&area=fixed_assets',     'module' => 'erp_finance'),
        array('id' => 'erp_budgeting',   'label' => 'Budgeting',         'icon' => 'fa-pie-chart',      'path' => 'shop/finance/erp?epc_erp_shell=1&area=budgeting',        'module' => 'erp_finance'),
    );
    return array_values(array_filter($all, function ($item) use ($enabledModules) {
        return in_array($item['module'], $enabledModules, true);
    }));
}

/* ───────────────────── industry-specific items ───────────────────── */

function epc_bos_auto_parts_items(): array
{
    return array(
        array('id' => 'crosses',       'label' => 'Crosses',           'icon' => 'fa-exchange',       'path' => 'shop/crosses/crosses'),
        array('id' => 'demand',        'label' => 'Demand Countries',  'icon' => 'fa-globe',          'path' => 'shop/demand_countries/demand_countries'),
        array('id' => 'auto_price',    'label' => 'Auto Price AI',     'icon' => 'fa-magic',          'path' => 'control/portal/epc_auto_price_engine'),
        array('id' => 'synonyms',      'label' => 'Brand Synonyms',    'icon' => 'fa-random',         'path' => 'shop/manufacturers_synonyms/manufacturers_synonyms'),
    );
}

function epc_bos_tax_advisory_items(): array
{
    return array(
        array('id' => 'tax_toolkit',   'label' => 'Tax Toolkit',       'icon' => 'fa-calculator',     'path' => 'control/portal/epc_tax_toolkit_manage'),
        array('id' => 'free_tools',    'label' => 'Free Tools Admin',  'icon' => 'fa-wrench',         'path' => 'control/portal/epc_free_tools_admin'),
    );
}

/* ───────────────────── platform items ───────────────────── */

function epc_bos_platform_items(): array
{
    return array(
        array('id' => 'portal_settings', 'label' => 'Portal Settings', 'icon' => 'fa-cog',           'path' => 'control/portal/portal'),
        array('id' => 'modern_auth',     'label' => 'Auth Settings',   'icon' => 'fa-sign-in',       'path' => 'control/portal/epc_cp_auth_settings'),
        array('id' => 'communication',   'label' => 'Communication',   'icon' => 'fa-comments',      'path' => 'control/portal/epc_super_cp_communication'),
        array('id' => 'data_policy',     'label' => 'Data Policy',     'icon' => 'fa-lock',          'path' => 'control/portal/epc_tenant_data_policy'),
        array('id' => 'api_docs',        'label' => 'API Docs',        'icon' => 'fa-file-code-o',   'path' => 'control/portal/epc_api_documentation_guide'),
        array('id' => 'operator_guide',  'label' => 'Operator Guide',  'icon' => 'fa-book',          'path' => 'control/portal/epc_super_cp_operator_guide'),
    );
}

/* ───────────────────── helpers ───────────────────── */

function epc_bos_load_tenant_settings(PDO $platformPdo, string $siteKey): ?array
{
    require_once __DIR__ . '/epc_portal_tenant_control.php';
    $row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
    if ($row === null) {
        return null;
    }
    $hostname = trim((string) ($row['hostname'] ?? ''));
    $settings = array();
    if ($hostname !== '') {
        $host = (strpos($hostname, 'www.') === 0) ? $hostname : 'www.' . $hostname;
        $settings = epc_portal_load_site_settings_for_host($platformPdo, $host);
        if (!is_array($settings)) {
            $settings = array();
        }
    }
    $settings['industry_code'] = $settings['industry_code'] ?? ($row['industry_code'] ?? 'auto_parts');
    $settings['trade_name'] = $row['trade_name'] ?? '';
    $settings['hostname'] = $hostname;
    if (!isset($settings['enabled_packs']) || !is_array($settings['enabled_packs'])) {
        $ind = epc_portal_industries();
        $ic = $settings['industry_code'];
        $settings['enabled_packs'] = isset($ind[$ic]['cp_packs']) ? $ind[$ic]['cp_packs'] : array('core');
    }
    return $settings;
}

function epc_bos_h(string $str): string
{
    return htmlspecialchars($str, ENT_QUOTES, 'UTF-8');
}

function epc_bos_tenant_badge_class(string $type): string
{
    $map = array(
        'commerce'  => 'bos-badge--commerce',
        'erp_only'  => 'bos-badge--erp',
        'demo'      => 'bos-badge--demo',
        'platform'  => 'bos-badge--platform',
    );
    return $map[$type] ?? 'bos-badge--default';
}

function epc_bos_tenant_type_label(string $type): string
{
    $map = array(
        'commerce'  => 'Commerce',
        'erp_only'  => 'ERP Only',
        'demo'      => 'Demo',
        'platform'  => 'Platform',
    );
    return $map[$type] ?? 'Tenant';
}

/**
 * Industry color for tenant cards.
 */
function epc_bos_industry_color(string $industryCode): string
{
    $map = array(
        'auto_parts'        => '#ef4444',
        'tax_advisory'      => '#f97316',
        'fashion_apparel'   => '#ec4899',
        'electronics'       => '#6366f1',
        'jewellery'         => '#eab308',
        'medical_supplies'  => '#14b8a6',
        'health_wellness'   => '#10b981',
        'consultancy'       => '#8b5cf6',
        'erp_only'          => '#3b82f6',
        'platform_host'     => '#64748b',
        'rental_leasing'    => '#06b6d4',
    );
    return $map[$industryCode] ?? '#0ea5e9';
}

/**
 * Section color for module view header icons.
 */
function epc_bos_module_color(string $sectionLabel): string
{
    $map = array(
        'Fleet Command'      => '#0ea5e9',
        'Tenant Operations'  => '#8b5cf6',
        'Commerce'           => '#10b981',
        'Catalogue'          => '#f59e0b',
        'Logistics'          => '#6366f1',
        'Marketing'          => '#ec4899',
        'Professional'       => '#14b8a6',
        'ERP Finance'        => '#3b82f6',
        'Auto Parts'         => '#ef4444',
        'Tax & Advisory'     => '#f97316',
        'Platform'           => '#64748b',
    );
    return $map[$sectionLabel] ?? '#64748b';
}

/**
 * Resolve active module info (label, icon, path, section) from sidebar sections.
 */
function epc_bos_resolve_module(array $sections, string $moduleId): ?array
{
    foreach ($sections as $section) {
        foreach ($section['items'] as $item) {
            if ($item['id'] === $moduleId) {
                return array(
                    'id'      => $item['id'],
                    'label'   => $item['label'],
                    'icon'    => $item['icon'],
                    'path'    => $item['path'],
                    'section' => $section['label'],
                    'section_icon' => $section['icon'],
                );
            }
        }
    }
    return null;
}

/**
 * Build the iframe URL for a CP module.
 * Fleet/platform modules use the platform host; tenant modules use the tenant host.
 */
function epc_bos_module_cp_url(string $hostname, string $path): string
{
    $host = $hostname ?: 'www.ecomae.com';
    if (strpos($path, 'shop/finance/erp') === 0) {
        $qs = '';
        $qPos = strpos($path, '?');
        if ($qPos !== false) {
            $qs = substr($path, $qPos);
            $path = substr($path, 0, $qPos);
        }
        return 'https://' . $host . '/cp/content/' . $path . '.php' . $qs . '&bos_embed=1';
    }
    return 'https://' . $host . '/cp/content/' . $path . '.php?bos_embed=1';
}

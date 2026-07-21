<?php
/**
 * BOC kernel — Business Operation Control shared spine.
 *
 * One place for: branding, the declarative area registry (single source of
 * truth for nav + permissions, so the auth-gate and dashboard never drift),
 * RBAC over the existing governance engine, an immutable operator audit log,
 * and the cross-tenant "fleet" aggregation that powers the BOS <-> BOC flow
 * (commerce tenants + ERP-only tenants + demo sandboxes in one controlled view).
 *
 * Pure where possible: registry/branding/summary functions take no globals and
 * are unit-testable; DB functions take an explicit PDO.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_boc_brand')) {
    /**
     * Platform-control branding. Routes/URLs keep the legacy "Super CP" paths;
     * the operator-facing name is Business Operation Control (BOC).
     *
     * @return array{name:string,short:string,legacy:string,tagline:string}
     */
    function epc_boc_brand(): array
    {
        return array(
            'name'    => 'Business Operation System',
            'short'   => 'BOS',
            'control' => 'Control',
            'legacy'  => 'Super CP',
            'tagline' => 'One BOS for every tenant, ERP-only client and demo — operators control the whole fleet, each tenant sees only their own area.',
        );
    }
}

if (!function_exists('epc_boc_nav_for_user')) {
    /**
     * Nav filtered to the areas a given user may access (role-scoped). This is
     * what makes BOS "one console, many scopes": a platform operator with the
     * wildcard role sees every group; a scoped/tenant user sees only their
     * permitted areas. Groups with no visible areas are dropped.
     *
     * @return array<string,array{group:array{label:string,icon:string,blurb:string},areas:array<string,mixed>}>
     */
    function epc_boc_nav_for_user(PDO $db, int $userId): array
    {
        $nav = epc_boc_nav();
        $out = array();
        foreach ($nav as $gid => $g) {
            $areas = array();
            foreach ($g['areas'] as $id => $area) {
                if (epc_boc_can($db, $userId, $id)) {
                    $areas[$id] = $area;
                }
            }
            if ($areas) {
                $out[$gid] = array('group' => $g['group'], 'areas' => $areas);
            }
        }
        return $out;
    }
}

if (!function_exists('epc_boc_h')) {
    function epc_boc_h($v): string
    {
        return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
    }
}

if (!function_exists('epc_boc_groups')) {
    /**
     * Operation domains for Super CP top mega-menu (full CP + ERP coverage).
     *
     * @return array<string,array{label:string,icon:string,blurb:string}>
     */
    function epc_boc_groups(): array
    {
        return array(
            'command'      => array('label' => 'Command', 'icon' => 'fa-tachometer', 'blurb' => 'Fleet health & KPIs'),
            'lifecycle'    => array('label' => 'Tenants', 'icon' => 'fa-rocket', 'blurb' => 'Onboard, control, demos'),
            'reliability'  => array('label' => 'Ops', 'icon' => 'fa-heartbeat', 'blurb' => 'Health, governance, incidents'),
            'supply'       => array('label' => 'Supply', 'icon' => 'fa-cubes', 'blurb' => 'Vendors, warehouses, channels'),
            'commerce'     => array('label' => 'Pricing', 'icon' => 'fa-tags', 'blurb' => 'Pricing AI, API, POS'),
            'shop'         => array('label' => 'Commerce', 'icon' => 'fa-shopping-cart', 'blurb' => 'Orders, customers, payments — CP'),
            'catalogue'    => array('label' => 'Catalogue', 'icon' => 'fa-th-large', 'blurb' => 'Products, prices, SKU media'),
            'logistics'    => array('label' => 'Logistics', 'icon' => 'fa-truck', 'blurb' => 'Delivery, procurement, stock'),
            'erp'          => array('label' => 'ERP', 'icon' => 'fa-university', 'blurb' => 'Finance, GL, AR/AP, inventory'),
            'growth'       => array('label' => 'Growth', 'icon' => 'fa-bullhorn', 'blurb' => 'Marketing, CMS, mobile'),
            'professional' => array('label' => 'Pro', 'icon' => 'fa-briefcase', 'blurb' => 'CRM, documents, parts agent'),
            'finance'      => array('label' => 'Compliance', 'icon' => 'fa-balance-scale', 'blurb' => 'Tax & compliance kits'),
            'identity'     => array('label' => 'Integrations', 'icon' => 'fa-plug', 'blurb' => 'Auth, email, connectors'),
            'platform'     => array('label' => 'Platform', 'icon' => 'fa-cog', 'blurb' => 'BI, AI, workflows, security'),
            'knowledge'    => array('label' => 'Guides', 'icon' => 'fa-book', 'blurb' => 'Operator & product docs'),
        );
    }
}

if (!function_exists('epc_boc_areas')) {
    /**
     * Declarative registry of every Super CP / BOC area — full CP + ERP panels.
     *
     * @return array<string,array{label:string,group:string,icon:string,path:string,tone:string,perm:string,scope:array<int,string>,hint:string}>
     */
    function epc_boc_areas(): array
    {
        $a = static function (string $label, string $group, string $icon, string $path, string $tone, string $perm, array $scope, string $hint = ''): array {
            return array('label' => $label, 'group' => $group, 'icon' => $icon, 'path' => $path, 'tone' => $tone, 'perm' => $perm, 'scope' => $scope, 'hint' => $hint);
        };
        $ALL = array('commerce', 'erp_only', 'demo');
        $COM = array('commerce', 'demo');
        $ERP = array('erp_only', 'commerce');

        return array(
            // Command
            'command_center'    => $a('Command Center', 'command', 'fa-tachometer', 'control/portal/epc_boc_command_center', 'platform', 'boc.command.view', $ALL, 'Live fleet health'),
            'fleet_cp'          => $a('CP fleet dashboard', 'command', 'fa-th', 'control/portal/epc_super_cp_fleet_dashboard', 'platform', 'boc.command.view', $ALL, 'Cross-tenant CP KPIs'),
            'fleet_erp'         => $a('ERP fleet dashboard', 'command', 'fa-university', 'control/portal/epc_super_erp_fleet_dashboard', 'finance', 'boc.finance.view', $ERP, 'Cross-tenant ERP KPIs'),
            'insights_erp'      => $a('Insights hub', 'command', 'fa-lightbulb-o', 'shop/finance/erp?epc_erp_shell=1&area=overview&tab=dashboard#epc-insights', 'finance', 'boc.finance.view', $ERP, 'Financial / business / CP insights'),

            // Tenants
            'tenant_hub'        => $a('Tenant hub / onboard', 'lifecycle', 'fa-sitemap', 'shop/tenant_hub/tenant_hub', 'platform', 'boc.tenants.manage', $ALL, 'Provision tenants'),
            'tenant_control'    => $a('Tenant control', 'lifecycle', 'fa-sliders', 'control/portal/epc_tenant_control_center', 'platform', 'boc.tenants.manage', $ALL, 'Credentials & on/off'),
            'tenant_features'   => $a('Feature matrix', 'lifecycle', 'fa-th', 'control/portal/epc_tenant_features', 'platform', 'boc.tenants.manage', $ALL, 'Per-tenant flags'),
            'demo_tenants'      => $a('Demo tenants', 'lifecycle', 'fa-flask', 'control/portal/epc_demo_tenants_manage', 'platform', 'boc.demo.manage', array('demo'), 'Sandbox lifecycle'),
            'industry_settings' => $a('Industry / ERP packs', 'lifecycle', 'fa-cubes', 'control/portal/industry_settings', 'platform', 'boc.tenants.manage', $ERP, 'Module presets'),
            'industry_consol'   => $a('Industry consolidation', 'lifecycle', 'fa-sitemap', 'control/portal/industry_consolidation', 'platform', 'boc.tenants.manage', $ALL, 'Industry roll-up'),
            'license_trends'    => $a('License trends', 'lifecycle', 'fa-line-chart', 'control/portal/epc_industry_license_trends', 'platform', 'boc.tenants.view', $ALL, 'License usage trends'),
            'customer_board'    => $a('Customer board', 'lifecycle', 'fa-users', 'control/portal/epc_super_cp_customer_board', 'clients', 'boc.tenants.view', $ALL, 'Cross-tenant search'),
            'erp_only_guide'    => $a('ERP-only onboard', 'lifecycle', 'fa-rocket', 'control/portal/epc_erp_only_onboard_guide', 'finance', 'boc.tenants.manage', $ERP, 'ERP-only client guide'),
            'tenant_config'     => $a('Tenant config', 'lifecycle', 'fa-cog', 'control/portal/epc_tenant_config', 'platform', 'boc.tenants.manage', $ALL, 'Tenant configuration'),
            'design_tokens'     => $a('Design tokens', 'lifecycle', 'fa-paint-brush', 'control/portal/epc_design_tokens', 'platform', 'boc.tenants.manage', $ALL, 'Brand tokens'),

            // Ops / reliability
            'platform_health'   => $a('Platform health', 'reliability', 'fa-heartbeat', 'control/portal/epc_platform_health_checkup', 'health', 'boc.ops.view', $ALL, 'SSL/DB/nginx probes'),
            'governance'        => $a('Governance', 'reliability', 'fa-gavel', 'control/portal/epc_platform_governance', 'governance', 'boc.ops.manage', $ALL, 'Policies & rules'),
            'audit_log'         => $a('Audit log', 'reliability', 'fa-history', 'control/portal/epc_boc_audit_log', 'governance', 'boc.ops.view', $ALL, 'Who did what'),
            'failover'          => $a('Failover runbook', 'reliability', 'fa-life-ring', 'control/portal/epc_platform_failover_guide', 'governance', 'boc.ops.view', $ALL, 'Incident steps'),
            'isolation_audit'   => $a('Isolation audit', 'reliability', 'fa-shield', 'control/portal/epc_commerce_isolation_audit', 'governance', 'boc.ops.view', $ALL, 'Data isolation'),
            'readiness_score'   => $a('Readiness score', 'reliability', 'fa-trophy', 'control/portal/epc_readiness_score', 'health', 'boc.ops.view', $ALL, 'Go-live readiness'),
            'notifications'     => $a('Notifications', 'reliability', 'fa-bell', 'control/portal/epc_notifications', 'platform', 'boc.ops.view', $ALL, 'Platform alerts'),
            'db_migrations'     => $a('DB migrations', 'reliability', 'fa-database', 'control/portal/epc_db_migrations', 'platform', 'boc.ops.manage', $ALL, 'Schema migrations'),
            'soc2_compliance'   => $a('SOC 2', 'reliability', 'fa-certificate', 'control/portal/epc_soc2_compliance', 'governance', 'boc.ops.view', $ALL, 'Compliance checklist'),

            // Supply fleet
            'vendor_control'    => $a('Vendor & sourcing', 'supply', 'fa-truck', 'control/portal/epc_boc_vendor_control', 'orders', 'boc.supply.view', $ERP, 'Fleet suppliers'),
            'warehouse_control' => $a('Warehouse & inventory', 'supply', 'fa-cubes', 'control/portal/epc_boc_warehouse_control', 'orders', 'boc.supply.view', $ERP, 'Stock risk'),
            'channel_control'   => $a('Channels & OMS', 'supply', 'fa-sitemap', 'control/portal/epc_boc_channel_control', 'orders', 'boc.supply.view', $COM, 'Web/POS/API'),
            'fulfillment_queue' => $a('Fulfilment queue', 'supply', 'fa-truck', 'shop/finance/epc_fulfillment_queue', 'orders', 'boc.supply.view', $COM, 'Ship queue'),

            // Pricing / commerce platform
            'auto_price'        => $a('Auto Price AI', 'commerce', 'fa-chart-line', 'control/portal/epc_auto_price_engine', 'prices', 'boc.commerce.manage', $COM, 'Multi-source compare'),
            'price_configs'     => $a('Price configs', 'commerce', 'fa-percent', 'control/portal/epc_super_cp_price_configs', 'prices', 'boc.commerce.manage', $COM, 'Markup rules'),
            'api_clients'       => $a('API clients', 'commerce', 'fa-key', 'control/portal/epc_api_clients_manage', 'platform', 'boc.commerce.manage', $ALL, 'Keys & quotas'),
            'pos_overview'      => $a('POS overview', 'commerce', 'fa-credit-card', 'control/portal/epc_pos_tenant_manage', 'orders', 'boc.commerce.view', $COM, 'POS per tenant'),
            'promotions'        => $a('Promotions', 'commerce', 'fa-tags', 'control/portal/epc_promotions_engine', 'prices', 'boc.commerce.manage', $COM, 'Promo engine'),
            'marketplace'       => $a('Marketplace', 'commerce', 'fa-shopping-cart', 'control/portal/epc_marketplace', 'orders', 'boc.commerce.manage', $COM, 'Marketplace hub'),

            // Tenant CP — Commerce (shop)
            'cp_orders'         => $a('OMS · Orders', 'shop', 'fa-shopping-cart', 'shop/orders/orders', 'orders', 'boc.commerce.view', $COM, 'Order management'),
            'cp_customers'      => $a('Customers', 'shop', 'fa-users', 'shop/customer_mgmt/customer_mgmt', 'clients', 'boc.commerce.view', $COM, 'Customer management'),
            'cp_payments'       => $a('Payments', 'shop', 'fa-credit-card', 'shop/payments/payments', 'orders', 'boc.commerce.view', $COM, 'Payment gateways'),
            'cp_returns'        => $a('Returns', 'shop', 'fa-undo', 'shop/returns-manager', 'orders', 'boc.commerce.view', $COM, 'Returns manager'),
            'cp_quotes'         => $a('Quote requests', 'shop', 'fa-file-text', 'shop/quote_requests', 'orders', 'boc.commerce.view', $COM, 'RFQs'),
            'cp_channels'       => $a('Channels', 'shop', 'fa-share-alt', 'shop/channels/channels', 'orders', 'boc.commerce.view', $COM, 'Sales channels'),
            'cp_pos'            => $a('POS terminal', 'shop', 'fa-calculator', 'shop/pos/terminal', 'orders', 'boc.commerce.view', $COM, 'Point of sale'),
            'cp_statistics'     => $a('Statistics', 'shop', 'fa-bar-chart', 'shop/statistics/statistics', 'orders', 'boc.commerce.view', $COM, 'Commerce stats'),
            'cp_web_tracker'    => $a('Web tracker (shop)', 'shop', 'fa-line-chart', 'shop/statistics/web_tracker', 'orders', 'boc.commerce.view', $COM, 'Shop traffic'),
            'portal_web_tracker'=> $a('Web tracker (portal)', 'shop', 'fa-area-chart', 'control/portal/epc_web_tracker', 'orders', 'boc.commerce.view', $COM, 'Platform web tracker'),

            // Catalogue / prices
            'cp_products'       => $a('Products', 'catalogue', 'fa-cube', 'shop/catalogue/products', 'prices', 'boc.commerce.view', $COM, 'Product catalogue'),
            'cp_sku_media'      => $a('SKU photos & specs', 'catalogue', 'fa-picture-o', 'shop/catalogue/sku_media', 'prices', 'boc.commerce.view', $COM, 'Media & specs'),
            'cp_prices'         => $a('Price lists', 'catalogue', 'fa-upload', 'shop/prices', 'prices', 'boc.commerce.view', $COM, 'Price list manager'),
            'cp_multivendor'    => $a('Multivendor upload', 'catalogue', 'fa-handshake-o', 'shop/prices/multivendor', 'prices', 'boc.commerce.manage', $COM, 'One-file vendor prices'),
            'cp_prices_guide'   => $a('Price upload guide', 'catalogue', 'fa-book', 'shop/prices/guide', 'prices', 'boc.commerce.view', $COM, 'Upload guide'),
            'cp_prices_edit'    => $a('Prices edit', 'catalogue', 'fa-tag', 'shop/prices/prices_edit', 'prices', 'boc.commerce.manage', $COM, 'Inline price edit'),
            'cp_prices_send'    => $a('Prices send', 'catalogue', 'fa-paper-plane', 'shop/prices_send', 'prices', 'boc.commerce.manage', $COM, 'Send price lists'),
            'cp_price_mgmt'     => $a('Pricing rules', 'catalogue', 'fa-percent', 'shop/price-management', 'prices', 'boc.commerce.manage', $COM, 'Pricing rules'),
            'cp_crosses'        => $a('Crosses', 'catalogue', 'fa-exchange', 'shop/crosses', 'prices', 'boc.commerce.view', $COM, 'OEM crosses'),
            'cp_synonyms'       => $a('Brand synonyms', 'catalogue', 'fa-random', 'shop/manufacturers_synonyms', 'prices', 'boc.commerce.view', $COM, 'Brand synonyms'),
            'cp_accessories'    => $a('Accessories', 'catalogue', 'fa-puzzle-piece', 'shop/accessories', 'prices', 'boc.commerce.view', $COM, 'Accessory links'),

            // Logistics
            'cp_logistics'      => $a('Delivery & shipping', 'logistics', 'fa-truck', 'shop/logistics/logistics', 'orders', 'boc.supply.view', $COM, 'Logistics hub'),
            'cp_storages'       => $a('Warehouses', 'logistics', 'fa-building', 'shop/logistics/storages', 'orders', 'boc.supply.view', $COM, 'Warehouse list'),
            'cp_stock'          => $a('Stock', 'logistics', 'fa-cubes', 'shop/logistics/stock', 'orders', 'boc.supply.view', $COM, 'Stock levels'),
            'cp_procurement'    => $a('Procurement', 'logistics', 'fa-shopping-basket', 'shop/procurement/procurement', 'orders', 'boc.supply.view', $ERP, 'Purchasing hub'),
            'cp_custom_ship'    => $a('Custom & shipping', 'logistics', 'fa-ship', 'shop/logistics/custom_shipping', 'orders', 'boc.supply.view', $COM, 'Customs & freight'),

            // ERP deep links
            'erp_home'          => $a('ERP home', 'erp', 'fa-th-large', 'shop/finance/erp?epc_erp_shell=1&area=overview&tab=dashboard', 'finance', 'boc.finance.view', $ERP, 'ERP command centre'),
            'erp_gl'            => $a('General ledger', 'erp', 'fa-book', 'shop/finance/erp?epc_erp_shell=1&area=gl', 'finance', 'boc.finance.view', $ERP, 'GL journals'),
            'erp_ap'            => $a('Accounts payable', 'erp', 'fa-credit-card', 'shop/finance/erp?epc_erp_shell=1&area=ap', 'finance', 'boc.finance.view', $ERP, 'AP'),
            'erp_ar'            => $a('Accounts receivable', 'erp', 'fa-users', 'shop/finance/erp?epc_erp_shell=1&area=ar', 'finance', 'boc.finance.view', $ERP, 'AR'),
            'erp_cash'          => $a('Cash & bank', 'erp', 'fa-university', 'shop/finance/erp?epc_erp_shell=1&area=banking&tab=cash_bank', 'finance', 'boc.finance.view', $ERP, 'Treasury'),
            'erp_tax'           => $a('Tax & VAT', 'erp', 'fa-balance-scale', 'shop/finance/erp?epc_erp_shell=1&area=tax', 'finance', 'boc.finance.view', $ERP, 'Tax compliance'),
            'erp_sales'         => $a('Sales & CRM', 'erp', 'fa-line-chart', 'shop/finance/erp?epc_erp_shell=1&area=sales', 'finance', 'boc.finance.view', $ERP, 'Sales orders'),
            'erp_purchasing'    => $a('Purchasing', 'erp', 'fa-shopping-basket', 'shop/finance/erp?epc_erp_shell=1&area=purchasing', 'finance', 'boc.finance.view', $ERP, 'POs'),
            'erp_inventory'     => $a('Inventory', 'erp', 'fa-cubes', 'shop/finance/erp?epc_erp_shell=1&area=inventory_mgmt', 'finance', 'boc.finance.view', $ERP, 'Stock & WMS'),
            'erp_hr'            => $a('Human resources', 'erp', 'fa-id-card', 'shop/finance/erp?epc_erp_shell=1&area=people', 'finance', 'boc.finance.view', $ERP, 'HR'),
            'erp_payroll'       => $a('Payroll', 'erp', 'fa-money', 'shop/finance/erp?epc_erp_shell=1&area=payroll', 'finance', 'boc.finance.view', $ERP, 'Payroll'),
            'erp_pl'            => $a('Profit & loss', 'erp', 'fa-line-chart', 'shop/finance/erp?epc_erp_shell=1&area=finance&tab=pl', 'finance', 'boc.finance.view', $ERP, 'P&L'),
            'erp_aging'         => $a('AR/AP aging', 'erp', 'fa-hourglass-half', 'shop/finance/erp?epc_erp_shell=1&area=finance&tab=aging', 'finance', 'boc.finance.view', $ERP, 'Aging'),
            'erp_guide'         => $a('ERP guide', 'erp', 'fa-book', 'shop/finance/erp?epc_erp_shell=1&area=overview&tab=guide', 'finance', 'boc.finance.view', $ERP, 'Operator guide'),

            // Growth
            'marketing'         => $a('Marketing broadcast', 'growth', 'fa-envelope', 'control/portal/epc_marketing_broadcast', 'auth', 'boc.growth.manage', $ALL, 'Bulk email'),
            'social'            => $a('Social media hub', 'growth', 'fa-share-alt', 'control/portal/epc_social_media_hub', 'platform', 'boc.growth.manage', $COM, 'Social + AI'),
            'info_blocks'       => $a('Info blocks (CMS)', 'growth', 'fa-newspaper-o', 'control/portal/epc_super_cp_info_blocks', 'docs', 'boc.growth.manage', $ALL, 'CMS blocks'),
            'visual_editor'     => $a('Visual editor', 'growth', 'fa-magic', 'control/portal/epc_visual_page_editor', 'platform', 'boc.growth.manage', $ALL, 'Page layouts'),
            'mobile_apps'       => $a('Mobile apps', 'growth', 'fa-mobile', 'control/portal/epc_mobile_apps', 'platform', 'boc.growth.manage', $COM, 'PWA / apps'),
            'free_tools'        => $a('Free Tools', 'growth', 'fa-wrench', 'control/portal/epc_free_tools_admin', 'platform', 'boc.growth.manage', $ALL, 'Public tools'),
            'cp_marketing'      => $a('Campaigns', 'growth', 'fa-bullhorn', 'shop/marketing/marketing', 'auth', 'boc.growth.manage', $COM, 'CP marketing'),
            'cp_seo'            => $a('SEO', 'growth', 'fa-search', 'shop/marketing/seo', 'auth', 'boc.growth.manage', $COM, 'SEO tools'),

            // Professional
            'cp_crm'            => $a('CRM', 'professional', 'fa-handshake-o', 'shop/crm/crm', 'clients', 'boc.commerce.view', $COM, 'CRM pipeline'),
            'cp_documents'      => $a('Documents', 'professional', 'fa-folder-open', 'shop/document_control/document_control', 'docs', 'boc.commerce.view', $ALL, 'Document control'),
            'cp_parts_agent'    => $a('Parts agent', 'professional', 'fa-comments', 'shop/parts_agent_chats', 'orders', 'boc.commerce.view', $COM, 'AI parts chats'),
            'dealer_portal'     => $a('Dealer portal', 'professional', 'fa-handshake-o', 'control/portal/epc_dealer_portal', 'clients', 'boc.commerce.view', $COM, 'Dealer access'),

            // Compliance
            'tax_toolkit'       => $a('Tax Toolkit', 'finance', 'fa-balance-scale', 'control/portal/epc_tax_toolkit_manage', 'finance', 'boc.finance.manage', $ALL, 'VAT/GST kits'),
            'erp_finance'       => $a('ERP & finance shell', 'finance', 'fa-university', 'shop/finance/erp?epc_erp_shell=1', 'finance', 'boc.finance.view', $ERP, 'Full ERP'),
            'uae_tax'           => $a('UAE tax compliance', 'finance', 'fa-gavel', 'shop/finance/erp/uae-tax-compliance?epc_erp_shell=1', 'finance', 'boc.finance.manage', $ERP, 'UAE VAT'),

            // Identity / integrations
            'integrations'      => $a('Integrations hub', 'identity', 'fa-plug', 'control/portal/epc_integrations_hub', 'platform', 'boc.identity.manage', $ALL, 'Connectors'),
            'modern_auth'       => $a('Modern auth', 'identity', 'fa-sign-in', 'control/portal/epc_cp_auth_settings', 'auth', 'boc.identity.manage', $ALL, 'OAuth / OTP / MFA'),
            'communication'     => $a('Communication', 'identity', 'fa-comments', 'control/portal/epc_super_cp_communication', 'auth', 'boc.identity.manage', $ALL, 'Email policy'),
            'tenant_email'      => $a('Tenant email / SMTP', 'identity', 'fa-envelope', 'control/portal/epc_tenant_email_settings', 'auth', 'boc.identity.manage', $ALL, 'Per-tenant SMTP'),
            'mfa_management'    => $a('MFA / 2FA', 'identity', 'fa-lock', 'control/portal/epc_mfa_management', 'auth', 'boc.identity.manage', $ALL, '2FA admin'),
            'sso_saml'          => $a('SSO / SAML', 'identity', 'fa-key', 'control/portal/epc_sso_saml', 'auth', 'boc.identity.manage', $ALL, 'Enterprise SSO'),
            'event_bus'         => $a('Event bus', 'identity', 'fa-bolt', 'control/portal/epc_event_bus', 'platform', 'boc.identity.manage', $ALL, 'Events'),
            'webhooks'          => $a('Webhooks', 'identity', 'fa-plug', 'control/portal/epc_webhooks', 'platform', 'boc.identity.manage', $ALL, 'Outbound hooks'),
            'rest_api_v2'       => $a('API v2', 'identity', 'fa-code', 'control/portal/epc_rest_api_v2', 'platform', 'boc.identity.manage', $ALL, 'REST API'),

            // Platform tools
            'bi_metrics'        => $a('BI metrics', 'platform', 'fa-bar-chart', 'control/portal/epc_bi_metrics', 'platform', 'boc.ops.view', $ALL, 'Fleet BI'),
            'power_bi'          => $a('Power BI', 'platform', 'fa-bar-chart', 'control/portal/epc_power_bi', 'platform', 'boc.ops.view', $ALL, 'Power BI embed'),
            'power_bi_guide'    => $a('Power BI guide', 'platform', 'fa-book', 'control/portal/epc_power_bi_guide', 'docs', 'boc.knowledge.view', $ALL, 'BI guide'),
            'ai_copilot'        => $a('AI Copilot', 'platform', 'fa-commenting', 'control/portal/epc_ai_copilot', 'platform', 'boc.ops.view', $ALL, 'AI assistant'),
            'ai_classify'       => $a('AI classify', 'platform', 'fa-magic', 'control/portal/epc_ai_classification', 'platform', 'boc.ops.view', $ALL, 'AI classification'),
            'nl_reporting'      => $a('NL reports', 'platform', 'fa-file-text', 'control/portal/epc_nl_reporting', 'platform', 'boc.ops.view', $ALL, 'Natural-language reports'),
            'workflow_builder'  => $a('Workflows', 'platform', 'fa-random', 'control/portal/epc_workflow_builder', 'platform', 'boc.ops.manage', $ALL, 'Workflow builder'),
            'import_orch'       => $a('Imports', 'platform', 'fa-upload', 'control/portal/epc_import_orchestrator', 'platform', 'boc.ops.manage', $ALL, 'Import jobs'),
            'doc_vault'         => $a('Doc vault', 'platform', 'fa-archive', 'control/portal/epc_document_vault', 'docs', 'boc.ops.view', $ALL, 'Document vault'),
            'cp_roles'          => $a('CP roles', 'platform', 'fa-users', 'control/portal/epc_cp_role_home', 'auth', 'boc.ops.manage', $ALL, 'Role homes'),
            'config_sandbox'    => $a('Sandbox', 'platform', 'fa-flask', 'control/portal/epc_config_sandbox', 'platform', 'boc.ops.manage', $ALL, 'Config sandbox'),
            'industry_packs'    => $a('Industry packs', 'platform', 'fa-industry', 'control/portal/epc_industry_packs', 'platform', 'boc.tenants.manage', $ALL, 'Industry packs'),
            'portal_settings'   => $a('Portal settings', 'platform', 'fa-cog', 'control/portal/portal', 'platform', 'boc.ops.manage', $ALL, 'Portal config'),
            'data_policy'       => $a('Data policy', 'platform', 'fa-lock', 'control/portal/epc_tenant_data_policy', 'governance', 'boc.ops.manage', $ALL, 'Data policy'),
            'config_edit'       => $a('Site config', 'platform', 'fa-wrench', 'control/config_edit', 'platform', 'boc.ops.manage', $ALL, 'DP config editor'),
            'sms_turning'       => $a('SMS settings', 'platform', 'fa-mobile', 'control/sms_turning', 'auth', 'boc.ops.manage', $ALL, 'SMS gateway'),

            // Knowledge
            'operator_guide'    => $a('Operator guide', 'knowledge', 'fa-book', 'control/portal/epc_super_cp_operator_guide', 'docs', 'boc.knowledge.view', $ALL, 'Who uses what'),
            'cp_brochure'       => $a('Full CP brochure', 'knowledge', 'fa-book', 'control/cp_brochure', 'docs', 'boc.knowledge.view', $ALL, 'Every CP function'),
            'product_brochure'  => $a('Product brochure', 'knowledge', 'fa-file-text-o', 'control/portal/epc_boc_product_brochure', 'docs', 'boc.knowledge.view', $ALL, 'Marketing brochure'),
            'api_docs'          => $a('API docs', 'knowledge', 'fa-file-code-o', 'control/portal/epc_api_documentation_guide', 'docs', 'boc.knowledge.view', $ALL, 'API guide'),
            'cp_guideline'      => $a('CP guideline', 'knowledge', 'fa-list-alt', 'control/cp-guideline', 'docs', 'boc.knowledge.view', $ALL, 'CP UX guideline'),
            'auto_price_guide'  => $a('Auto Price guide', 'knowledge', 'fa-book', 'control/portal/epc_auto_price_guide', 'docs', 'boc.knowledge.view', $COM, 'Pricing AI guide'),
            'custom_ship_guide' => $a('Custom & shipping guide', 'knowledge', 'fa-ship', 'control/portal/epc_custom_shipping_guide', 'docs', 'boc.knowledge.view', $COM, 'Customs guide'),
            'workshop_guide'    => $a('Autoworkshop guide', 'knowledge', 'fa-wrench', 'control/portal/epc_autoworkshop_guide', 'docs', 'boc.knowledge.view', $COM, 'Workshop vertical'),
        );
    }
}

if (!function_exists('epc_boc_area_path_resolvable')) {
    /**
     * Whether a BOC area path points at a real CP content PHP file.
     * Non-portal shop/ERP URLs are treated as resolvable (routed via content table).
     * Portal stubs without a PHP module are hidden from the top menu.
     */
    function epc_boc_area_path_resolvable(string $path): bool
    {
        $path = trim($path);
        if ($path === '') {
            return false;
        }
        // Strip query/hash for filesystem checks.
        $bare = preg_replace('/[?#].*$/', '', $path) ?: $path;
        $bare = trim($bare, '/');
        if ($bare === '') {
            return false;
        }
        // Portal modules must exist on disk so operators never hit empty stubs.
        if (strpos($bare, 'control/portal/') === 0) {
            $root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
            if ($root === '') {
                $root = rtrim((string) (dirname(__DIR__, 2) ?? ''), '/\\');
            }
            $file = $root . '/cp/content/' . $bare . '.php';
            return is_file($file);
        }
        // Classic control pages under cp/content/control/*.php
        if (preg_match('#^control/([a-z0-9_\-]+)$#i', $bare, $m)) {
            $root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
            $alias = str_replace('-', '_', strtolower($m[1]));
            $candidates = array(
                $root . '/cp/content/control/' . $m[1] . '.php',
                $root . '/cp/content/control/' . $alias . '.php',
                $root . '/cp/content/control/' . $alias . '_page.php',
            );
            foreach ($candidates as $cand) {
                if (is_file($cand)) {
                    return true;
                }
            }
            // Known content aliases that are registered in DB but file names differ.
            if (in_array(strtolower($m[1]), array('cp_brochure', 'cp-guideline', 'cp_guideline'), true)) {
                return true;
            }
        }
        // Shop / ERP / tenant_hub routes are content-table driven.
        return true;
    }
}

if (!function_exists('epc_boc_nav')) {
    /**
     * Areas grouped by domain, in group order, for rendering the nav.
     * Portal stubs without a PHP module are omitted so every top-menu link works.
     *
     * @return array<string,array{group:array{label:string,icon:string,blurb:string},areas:array<string,mixed>}>
     */
    function epc_boc_nav(): array
    {
        $groups = epc_boc_groups();
        $areas = epc_boc_areas();
        $out = array();
        foreach ($groups as $gid => $g) {
            $out[$gid] = array('group' => $g, 'areas' => array());
        }
        foreach ($areas as $id => $area) {
            $path = (string) ($area['path'] ?? '');
            if (!epc_boc_area_path_resolvable($path)) {
                continue;
            }
            $gid = $area['group'];
            if (!isset($out[$gid])) {
                $out[$gid] = array('group' => array('label' => $gid, 'icon' => 'fa-folder', 'blurb' => ''), 'areas' => array());
            }
            $out[$gid]['areas'][$id] = $area;
        }
        // Drop empty groups so the top bar only shows populated panels.
        foreach ($out as $gid => $g) {
            if (empty($g['areas'])) {
                unset($out[$gid]);
            }
        }
        return $out;
    }
}

if (!function_exists('epc_boc_area_perm')) {
    /** Permission string required for an area id ('' if unknown). */
    function epc_boc_area_perm(string $areaId): string
    {
        $areas = epc_boc_areas();
        return isset($areas[$areaId]['perm']) ? (string) $areas[$areaId]['perm'] : '';
    }
}

if (!function_exists('epc_boc_can')) {
    /**
     * RBAC check for a BOC area, layered over the governance engine. A platform
     * operator with the wildcard role ('*') passes everything. If governance has
     * no roles configured for the user yet, operators default to allow (legacy
     * behaviour) so the rebrand never locks anyone out; tighten via roles.
     */
    function epc_boc_can(PDO $db, int $userId, string $areaId): bool
    {
        $perm = epc_boc_area_perm($areaId);
        if ($perm === '') {
            return false;
        }
        if (!function_exists('epc_gov_can') || !function_exists('epc_gov_user_permissions')) {
            return true;
        }
        $perms = epc_gov_user_permissions($db, $userId);
        if (empty($perms)) {
            return true; // no roles assigned yet -> legacy operator access
        }
        if (in_array('boc.*', $perms, true)) {
            return true;
        }
        return epc_gov_can($db, $userId, $perm);
    }
}

/* ----------------------------- Audit log ----------------------------- */

if (!function_exists('epc_boc_audit_ensure_schema')) {
    function epc_boc_audit_ensure_schema(PDO $db): void
    {
        $db->exec(
            'CREATE TABLE IF NOT EXISTS `epc_boc_audit` (' .
            '`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,' .
            '`ts` INT UNSIGNED NOT NULL,' .
            '`user_id` INT NOT NULL DEFAULT 0,' .
            '`actor` VARCHAR(190) NOT NULL DEFAULT \'\',' .
            '`area` VARCHAR(64) NOT NULL DEFAULT \'\',' .
            '`action` VARCHAR(96) NOT NULL DEFAULT \'\',' .
            '`target` VARCHAR(190) NOT NULL DEFAULT \'\',' .
            '`meta` TEXT NULL,' .
            '`ip` VARCHAR(45) NOT NULL DEFAULT \'\',' .
            'PRIMARY KEY (`id`),' .
            'KEY `ts` (`ts`),' .
            'KEY `area` (`area`),' .
            'KEY `user_id` (`user_id`)' .
            ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
        );
    }
}

if (!function_exists('epc_boc_audit_log')) {
    /**
     * Append an immutable audit entry for a privileged operator action.
     *
     * @param array<string,mixed> $meta extra context (no secrets)
     * @return int new row id (0 on failure)
     */
    function epc_boc_audit_log(PDO $db, int $userId, string $area, string $action, string $target = '', array $meta = array(), string $actor = '', string $ip = ''): int
    {
        try {
            epc_boc_audit_ensure_schema($db);
            if ($ip === '') {
                $ip = isset($_SERVER['HTTP_CF_CONNECTING_IP']) ? (string) $_SERVER['HTTP_CF_CONNECTING_IP']
                    : (isset($_SERVER['REMOTE_ADDR']) ? (string) $_SERVER['REMOTE_ADDR'] : '');
            }
            $st = $db->prepare('INSERT INTO `epc_boc_audit` (`ts`,`user_id`,`actor`,`area`,`action`,`target`,`meta`,`ip`) VALUES (?,?,?,?,?,?,?,?)');
            $st->execute(array(
                time(), $userId, substr($actor, 0, 190), substr($area, 0, 64),
                substr($action, 0, 96), substr($target, 0, 190),
                $meta ? json_encode($meta) : null, substr($ip, 0, 45),
            ));
            return (int) $db->lastInsertId();
        } catch (Throwable $e) {
            return 0;
        }
    }
}

if (!function_exists('epc_boc_audit_recent')) {
    /**
     * Recent audit entries, newest first.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_boc_audit_recent(PDO $db, int $limit = 100, string $area = ''): array
    {
        try {
            epc_boc_audit_ensure_schema($db);
            $limit = max(1, min(1000, $limit));
            if ($area !== '') {
                $st = $db->prepare('SELECT * FROM `epc_boc_audit` WHERE `area` = ? ORDER BY `id` DESC LIMIT ' . $limit);
                $st->execute(array($area));
            } else {
                $st = $db->query('SELECT * FROM `epc_boc_audit` ORDER BY `id` DESC LIMIT ' . $limit);
            }
            $rows = $st->fetchAll(PDO::FETCH_ASSOC);
            return is_array($rows) ? $rows : array();
        } catch (Throwable $e) {
            return array();
        }
    }
}

/* ------------------------- Tenant fleet (BOS<->BOC) ------------------------- */

if (!function_exists('epc_boc_classify_tenant')) {
    /**
     * Normalize a tenant row into a BOC type: 'demo', 'erp_only', or 'commerce'.
     */
    function epc_boc_classify_tenant(array $t): string
    {
        if (!empty($t['is_demo']) || !empty($t['is_demo_tenant'])) {
            return 'demo';
        }
        $ind = strtolower((string) ($t['industry_code'] ?? $t['industry'] ?? ''));
        if ($ind === 'erp_standalone' || $ind === 'erp_only' || strpos($ind, 'erp') === 0) {
            return 'erp_only';
        }
        return 'commerce';
    }
}

if (!function_exists('epc_boc_tenant_health')) {
    /**
     * Derive a RAG health for a tenant row from already-collected signals
     * (DB connectivity, live status). Pure — no network calls here.
     *
     * @return array{rag:string,reasons:array<int,string>}
     */
    function epc_boc_tenant_health(array $t): array
    {
        $reasons = array();
        $rag = 'green';
        $status = (string) ($t['status'] ?? '');
        if (isset($t['db_connect_ok']) && !$t['db_connect_ok']) {
            $rag = 'red';
            $reasons[] = 'Tenant DB unreachable';
        }
        if ($status === 'dns_pending') {
            if ($rag !== 'red') { $rag = 'amber'; }
            $reasons[] = 'Awaiting DNS / go-live';
        }
        if (!empty($t['access_blocked'])) {
            $rag = 'red';
            $reasons[] = 'Access blocked';
        }
        if ($status !== '' && $status !== 'live' && $status !== 'dns_pending' && $rag === 'green') {
            $rag = 'amber';
            $reasons[] = 'Status: ' . $status;
        }
        return array('rag' => $rag, 'reasons' => $reasons);
    }
}

if (!function_exists('epc_boc_fleet')) {
    /**
     * The unified fleet: every controllable unit (commerce tenant, ERP-only
     * client, demo sandbox) normalized into one shape with type + health. This
     * is the BOS <-> BOC information flow surfaced as one controlled list.
     *
     * @return array<int,array{site_key:string,label:string,type:string,status:string,health:string,reasons:array<int,string>,urls:array<string,string>}>
     */
    function epc_boc_fleet(PDO $db): array
    {
        $rows = array();
        if (function_exists('epc_th_list_tenants')) {
            try {
                $rows = epc_th_list_tenants($db);
            } catch (Throwable $e) {
                $rows = array();
            }
        }
        $fleet = array();
        foreach ($rows as $t) {
            $type = epc_boc_classify_tenant($t);
            $health = epc_boc_tenant_health($t);
            $fleet[] = array(
                'site_key' => (string) ($t['site_key'] ?? ''),
                'label'    => (string) ($t['trade_name'] ?? $t['system_name'] ?? $t['site_key'] ?? ''),
                'type'     => $type,
                'status'   => (string) ($t['status'] ?? ''),
                'industry' => (string) ($t['industry_name'] ?? $t['industry_code'] ?? ''),
                'health'   => $health['rag'],
                'reasons'  => $health['reasons'],
                'urls'     => array(
                    'storefront' => (string) ($t['storefront_url'] ?? ''),
                    'cp'         => (string) ($t['cp_url'] ?? ''),
                    'erp'        => (string) ($t['erp_url'] ?? ''),
                ),
            );
        }
        return $fleet;
    }
}

if (!function_exists('epc_boc_fleet_summary')) {
    /**
     * Aggregate counts for the command center — by type and by health. Pure.
     *
     * @param array<int,array<string,mixed>> $fleet
     * @return array{total:int,by_type:array<string,int>,by_health:array<string,int>}
     */
    function epc_boc_fleet_summary(array $fleet): array
    {
        $byType = array('commerce' => 0, 'erp_only' => 0, 'demo' => 0);
        $byHealth = array('green' => 0, 'amber' => 0, 'red' => 0);
        foreach ($fleet as $f) {
            $type = (string) ($f['type'] ?? 'commerce');
            if (!isset($byType[$type])) { $byType[$type] = 0; }
            $byType[$type]++;
            $health = (string) ($f['health'] ?? 'green');
            if (!isset($byHealth[$health])) { $byHealth[$health] = 0; }
            $byHealth[$health]++;
        }
        return array('total' => count($fleet), 'by_type' => $byType, 'by_health' => $byHealth);
    }
}

if (!function_exists('epc_boc_type_label')) {
    function epc_boc_type_label(string $type): string
    {
        switch ($type) {
            case 'demo': return 'Demo';
            case 'erp_only': return 'ERP-only';
            case 'commerce': return 'Commerce';
            default: return ucfirst($type);
        }
    }
}

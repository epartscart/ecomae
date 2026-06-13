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
            'name'    => 'Business Operation Control',
            'short'   => 'BOC',
            'legacy'  => 'Super CP',
            'tagline' => 'Unified control over every tenant, ERP-only client and demo — one operations spine.',
        );
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
     * The 8 operation domains, in display order. Each area belongs to one group.
     *
     * @return array<string,array{label:string,icon:string,blurb:string}>
     */
    function epc_boc_groups(): array
    {
        return array(
            'command'      => array('label' => 'Command Center', 'icon' => 'fa-tachometer', 'blurb' => 'Live fleet health, KPIs, alerts'),
            'lifecycle'    => array('label' => 'Tenant Lifecycle', 'icon' => 'fa-rocket', 'blurb' => 'Onboard, control, features, demos'),
            'reliability'  => array('label' => 'Operations & Reliability', 'icon' => 'fa-heartbeat', 'blurb' => 'Health, governance, incidents'),
            'commerce'     => array('label' => 'Commerce & Pricing', 'icon' => 'fa-tags', 'blurb' => 'Pricing AI, API clients, POS'),
            'growth'       => array('label' => 'Growth & Marketing', 'icon' => 'fa-bullhorn', 'blurb' => 'Broadcast, social, CMS'),
            'finance'      => array('label' => 'Finance & Compliance', 'icon' => 'fa-balance-scale', 'blurb' => 'Tax, ERP — country-driven'),
            'identity'     => array('label' => 'Integrations & Identity', 'icon' => 'fa-plug', 'blurb' => 'Integrations, auth, email'),
            'knowledge'    => array('label' => 'Knowledge', 'icon' => 'fa-book', 'blurb' => 'Operator guides, parity with site'),
        );
    }
}

if (!function_exists('epc_boc_areas')) {
    /**
     * Declarative registry of every BOC area. This is the single source of
     * truth: the dashboard nav renders from it and the access permission for
     * each route is derived from it (no hand-maintained prefix lists).
     *
     * Each area: id => {label, group, icon, path (under /<cp>/), tone, perm,
     * scope (which tenant types it controls), hint}.
     *
     * @return array<string,array{label:string,group:string,icon:string,path:string,tone:string,perm:string,scope:array<int,string>,hint:string}>
     */
    function epc_boc_areas(): array
    {
        $a = static function (string $label, string $group, string $icon, string $path, string $tone, string $perm, array $scope, string $hint = ''): array {
            return array('label' => $label, 'group' => $group, 'icon' => $icon, 'path' => $path, 'tone' => $tone, 'perm' => $perm, 'scope' => $scope, 'hint' => $hint);
        };
        $ALL = array('commerce', 'erp_only', 'demo');
        return array(
            'command_center'    => $a('Command Center', 'command', 'fa-tachometer', 'control/portal/epc_boc_command_center', 'platform', 'boc.command.view', $ALL, 'Live fleet health across all tenant types'),
            'tenant_hub'        => $a('Tenant hub / onboard', 'lifecycle', 'fa-sitemap', 'shop/tenant_hub/tenant_hub', 'platform', 'boc.tenants.manage', $ALL, 'Provision & onboard tenants'),
            'tenant_control'    => $a('Tenant control', 'lifecycle', 'fa-sliders', 'control/portal/epc_tenant_control_center', 'platform', 'boc.tenants.manage', $ALL, 'Credentials & on/off'),
            'tenant_features'   => $a('Feature matrix', 'lifecycle', 'fa-th', 'control/portal/epc_tenant_features', 'platform', 'boc.tenants.manage', $ALL, 'Per-tenant feature flags'),
            'demo_tenants'      => $a('Demo tenants', 'lifecycle', 'fa-flask', 'control/portal/epc_demo_tenants_manage', 'platform', 'boc.demo.manage', array('demo'), 'Sandbox extend / convert / delete'),
            'industry_settings' => $a('Industry / ERP packs', 'lifecycle', 'fa-cubes', 'control/portal/industry_settings', 'platform', 'boc.tenants.manage', array('erp_only', 'commerce'), 'ERP module presets'),
            'customer_board'    => $a('Customer board', 'lifecycle', 'fa-users', 'control/portal/epc_super_cp_customer_board', 'clients', 'boc.tenants.view', $ALL, 'Cross-tenant CRM/ERP search'),

            'platform_health'   => $a('Platform health', 'reliability', 'fa-heartbeat', 'control/portal/epc_platform_health_checkup', 'health', 'boc.ops.view', $ALL, 'SSL/DB/nginx/backup probes'),
            'governance'        => $a('Governance', 'reliability', 'fa-gavel', 'control/portal/epc_platform_governance', 'governance', 'boc.ops.manage', $ALL, 'Policies & rules'),
            'audit_log'         => $a('Audit log', 'reliability', 'fa-history', 'control/portal/epc_boc_audit_log', 'governance', 'boc.ops.view', $ALL, 'Who did what, when'),
            'failover'          => $a('Failover runbook', 'reliability', 'fa-life-ring', 'control/portal/epc_platform_failover_guide', 'governance', 'boc.ops.view', $ALL, 'Incident steps'),

            'auto_price'        => $a('Auto Price AI', 'commerce', 'fa-chart-line', 'control/portal/epc_auto_price_engine', 'prices', 'boc.commerce.manage', array('commerce'), 'Multi-source compare, cross-list'),
            'price_configs'     => $a('Price configs', 'commerce', 'fa-percent', 'control/portal/epc_super_cp_price_configs', 'prices', 'boc.commerce.manage', array('commerce'), 'Markup rules'),
            'api_clients'       => $a('API clients', 'commerce', 'fa-key', 'control/portal/epc_api_clients_manage', 'platform', 'boc.commerce.manage', $ALL, 'Keys / scopes / quotas'),
            'pos_overview'      => $a('POS overview', 'commerce', 'fa-credit-card', 'control/portal/epc_pos_tenant_manage', 'orders', 'boc.commerce.view', array('commerce'), 'POS per tenant'),

            'marketing'         => $a('Marketing broadcast', 'growth', 'fa-envelope', 'control/portal/epc_marketing_broadcast', 'auth', 'boc.growth.manage', $ALL, 'Bulk email / WhatsApp'),
            'social'            => $a('Social media hub', 'growth', 'fa-share-alt', 'control/portal/epc_social_media_hub', 'platform', 'boc.growth.manage', array('commerce'), 'Content + AI advisor'),
            'info_blocks'       => $a('Info blocks (CMS)', 'growth', 'fa-newspaper-o', 'control/portal/epc_super_cp_info_blocks', 'docs', 'boc.growth.manage', $ALL, 'Storefront & CP blocks'),
            'visual_editor'     => $a('Visual editor', 'growth', 'fa-magic', 'control/portal/epc_visual_page_editor', 'platform', 'boc.growth.manage', $ALL, 'Block layout & preview'),
            'mobile_apps'       => $a('Mobile apps', 'growth', 'fa-mobile', 'control/portal/epc_mobile_apps', 'platform', 'boc.growth.manage', array('commerce'), 'Android/iOS/PWA'),

            'tax_toolkit'       => $a('Tax Toolkit', 'finance', 'fa-balance-scale', 'control/portal/epc_tax_toolkit_manage', 'finance', 'boc.finance.manage', $ALL, 'Country-driven VAT/GST kits'),
            'erp_finance'       => $a('ERP & finance', 'finance', 'fa-university', 'shop/finance/erp?epc_erp_shell=1', 'finance', 'boc.finance.view', array('erp_only', 'commerce'), 'Platform ERP'),

            'integrations'      => $a('Integrations hub', 'identity', 'fa-plug', 'control/portal/epc_integrations_hub', 'platform', 'boc.identity.manage', $ALL, 'Connection health & sync'),
            'modern_auth'       => $a('Modern auth', 'identity', 'fa-sign-in', 'control/portal/epc_cp_auth_settings', 'auth', 'boc.identity.manage', $ALL, 'OAuth + SMTP / OTP + operator MFA'),
            'communication'     => $a('Communication', 'identity', 'fa-comments', 'control/portal/epc_super_cp_communication', 'auth', 'boc.identity.manage', $ALL, 'Email policy & tasks'),

            'operator_guide'    => $a('Operator guide', 'knowledge', 'fa-book', 'control/portal/epc_super_cp_operator_guide', 'docs', 'boc.knowledge.view', $ALL, 'Who uses what'),
            'api_docs'          => $a('API docs', 'knowledge', 'fa-file-code-o', 'control/portal/epc_api_documentation_guide', 'docs', 'boc.knowledge.view', $ALL, 'Key management guide'),
        );
    }
}

if (!function_exists('epc_boc_nav')) {
    /**
     * Areas grouped by domain, in group order, for rendering the nav.
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
            $gid = $area['group'];
            if (!isset($out[$gid])) {
                $out[$gid] = array('group' => array('label' => $gid, 'icon' => 'fa-folder', 'blurb' => ''), 'areas' => array());
            }
            $out[$gid]['areas'][$id] = $area;
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

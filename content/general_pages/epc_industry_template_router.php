<?php
/**
 * Industry Template Router
 *
 * Routes tenants to their consolidated group template instead of requiring
 * 1154 individual template files. Each group (28 groups) has one shared
 * frontend template that adapts based on the tenant's active sub-areas.
 *
 * Usage in storefront rendering:
 *   $template = epc_industry_route_template($industryCode);
 *   require $template; // loads the shared group template
 *
 * The template itself checks which sub-areas are active and only renders
 * relevant sections (categories, hero imagery, feature blocks).
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_industry_consolidation.php';

/**
 * Get the filesystem path to the correct frontend template for a tenant.
 *
 * @param string $industryCode Tenant's industry code
 * @param string $layoutVariant Optional layout variant (hero, grid, minimal, editorial)
 * @return string Absolute path to the template file
 */
function epc_industry_route_template(string $industryCode, string $layoutVariant = ''): string
{
    $group = epc_industry_get_group($industryCode);
    $templateKey = $group['template_key'] ?? 'retail';
    $root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
    $templateDir = $root . '/content/general_pages/industry_templates';

    if ($layoutVariant !== '') {
        $variantFile = $templateDir . '/' . $templateKey . '_' . $layoutVariant . '.php';
        if (is_file($variantFile)) {
            return $variantFile;
        }
    }

    $mainFile = $templateDir . '/' . $templateKey . '.php';
    if (is_file($mainFile)) {
        return $mainFile;
    }

    // Fallback to generic commerce template
    $fallback = $root . '/content/general_pages/epc_generic_commerce_package.php';
    if (is_file($fallback)) {
        return $fallback;
    }

    return $mainDir . '/retail.php';
}

/**
 * Get the hero configuration for a consolidated group.
 *
 * @param string $industryCode Tenant's industry code
 * @param array $tenantProfile Tenant site profile (optional overrides)
 * @return array Hero config: style, colors, title_template, etc.
 */
function epc_industry_hero_config(string $industryCode, array $tenantProfile = array()): array
{
    $group = epc_industry_get_group($industryCode);
    $colors = $group['color_scheme'] ?? array('primary' => '#3b82f6', 'accent' => '#60a5fa', 'bg_from' => '#1e3a5f', 'bg_to' => '#1e40af');

    // Allow tenant overrides from their brand settings
    if (!empty($tenantProfile['theme']['primary'])) {
        $colors['primary'] = $tenantProfile['theme']['primary'];
    }
    if (!empty($tenantProfile['theme']['accent'])) {
        $colors['accent'] = $tenantProfile['theme']['accent'];
    }

    return array(
        'style' => $group['hero_style'] ?? 'parallax_dark',
        'colors' => $colors,
        'icon' => $group['icon'] ?? 'fa-shopping-cart',
        'group_label' => $group['label'] ?? 'Business',
        'template_key' => $group['template_key'] ?? 'retail',
    );
}

/**
 * Get filtered category list based on active sub-areas.
 *
 * When a tenant has only certain sub-areas active, this filters the
 * category/product display to only show relevant items.
 *
 * @param PDO $pdo Database connection
 * @param string $siteKey Tenant site_key
 * @param string $industryCode Tenant industry code
 * @param array $allCategories Full category list
 * @return array Filtered categories (only those in active sub-areas)
 */
function epc_industry_filter_categories_by_sub_areas(PDO $pdo, string $siteKey, string $industryCode, array $allCategories): array
{
    $activeAreas = epc_industry_tenant_sub_areas($pdo, $siteKey, $industryCode);
    $activeKeys = array_keys(array_filter($activeAreas));

    if (empty($activeKeys)) {
        return $allCategories; // No filter if nothing active (show all)
    }

    // If categories have a sub_area tag, filter by it
    $filtered = array();
    foreach ($allCategories as $cat) {
        $catArea = $cat['sub_area'] ?? '';
        if ($catArea === '' || in_array($catArea, $activeKeys, true)) {
            $filtered[] = $cat;
        }
    }

    return $filtered;
}

/**
 * Get ERP module visibility based on active sub-areas.
 *
 * Maps sub-area keys to ERP sidebar modules that should be visible.
 *
 * @param array<string, bool> $activeAreas Active sub-area toggles
 * @param string $groupKey Consolidated group key
 * @return array<string> List of ERP module identifiers to show
 */
function epc_industry_erp_modules_for_sub_areas(array $activeAreas, string $groupKey): array
{
    // Base modules that all groups get
    $modules = array('dashboard', 'gl', 'ar', 'ap', 'inventory', 'reports', 'setup');

    // Group-specific module mappings
    $groupModules = array(
        'automotive' => array(
            'parts_catalog' => array('catalog', 'cross_reference', 'vin_lookup'),
            'workshop' => array('job_card', 'labour', 'vehicle_history'),
            'dealership' => array('vehicle_sales', 'test_drive', 'trade_in'),
            'rental' => array('fleet_booking', 'availability', 'contracts'),
        ),
        'healthcare_medical' => array(
            'clinic' => array('patients', 'appointments', 'prescriptions'),
            'pharmacy' => array('drug_catalog', 'batch_tracking', 'dispensing'),
            'equipment' => array('asset_register', 'maintenance', 'calibration'),
            'laboratory' => array('samples', 'tests', 'results'),
        ),
        'food_beverage' => array(
            'restaurant' => array('menu', 'table_service', 'kitchen_display'),
            'pos' => array('pos_terminal', 'shift_close', 'tender'),
            'kitchen' => array('recipes', 'ingredients', 'wastage'),
            'delivery' => array('delivery_zones', 'riders', 'orders'),
        ),
        'jewellery_luxury' => array(
            'retail' => array('tag_system', 'showcase', 'certification'),
            'gold' => array('gold_rate', 'weight_tracking', 'purity'),
            'custom_design' => array('making_charges', 'job_work', 'design_catalog'),
            'wholesale' => array('consignment', 'memo', 'bulk_pricing'),
        ),
        'construction_realestate' => array(
            'contracting' => array('boq', 'progress_billing', 'subcontractors'),
            'materials' => array('procurement', 'stock_yard', 'delivery_challan'),
            'real_estate' => array('units', 'payment_plans', 'handover'),
        ),
        'manufacturing_industrial' => array(
            'production' => array('work_orders', 'bom', 'routing'),
            'quality' => array('qc_checks', 'non_conformance', 'capa'),
            'inventory' => array('raw_materials', 'wip', 'finished_goods'),
        ),
    );

    $mapping = $groupModules[$groupKey] ?? array();

    foreach ($activeAreas as $areaKey => $isActive) {
        if ($isActive && isset($mapping[$areaKey])) {
            $modules = array_merge($modules, $mapping[$areaKey]);
        }
    }

    return array_unique($modules);
}

/**
 * Get CP menu items filtered by active sub-areas.
 *
 * @param array<string, bool> $activeAreas Active sub-area toggles
 * @param string $groupKey Consolidated group key
 * @return array<string> CP menu section identifiers to show
 */
function epc_industry_cp_sections_for_sub_areas(array $activeAreas, string $groupKey): array
{
    // Base CP sections
    $sections = array('dashboard', 'settings', 'users', 'content', 'analytics');

    // All active sub-areas add their respective CP section
    foreach ($activeAreas as $areaKey => $isActive) {
        if ($isActive) {
            $sections[] = 'section_' . $areaKey;
        }
    }

    return array_unique($sections);
}

/**
 * Calculate server load reduction from consolidation.
 *
 * Estimates savings based on number of industries sharing templates
 * vs individual templates per industry.
 *
 * @return array Stats: templates_before, templates_after, reduction_pct, etc.
 */
function epc_industry_consolidation_savings(): array
{
    $groups = epc_industry_groups();
    $totalIndustries = 1154; // Current count on platform

    $templatesBefore = $totalIndustries; // Worst case: 1 per industry
    $templatesAfter = count($groups); // Consolidated: 1 per group
    $reductionPct = round((1 - $templatesAfter / $templatesBefore) * 100, 1);

    // Estimated resource savings
    $phpFilesBefore = $totalIndustries * 3; // home + catalog + config per industry
    $phpFilesAfter = count($groups) * 3;
    $cssFilesBefore = $totalIndustries; // 1 custom CSS per industry
    $cssFilesAfter = count($groups);
    $dbQueriesSaved = ($totalIndustries - count($groups)) * 2; // 2 queries per eliminated template

    return array(
        'total_industries' => $totalIndustries,
        'groups_count' => count($groups),
        'templates_before' => $templatesBefore,
        'templates_after' => $templatesAfter,
        'reduction_pct' => $reductionPct,
        'php_files_saved' => $phpFilesBefore - $phpFilesAfter,
        'css_files_saved' => $cssFilesBefore - $cssFilesAfter,
        'db_queries_saved_per_load' => $dbQueriesSaved,
        'memory_reduction_est' => '~' . round(($totalIndustries - count($groups)) * 0.5, 0) . ' MB less opcache',
    );
}

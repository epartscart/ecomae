<?php
/**
 * P2 #32 — Industry Vertical Packs
 *
 * Pre-configured feature sets for auto-parts, fashion, electronics, jewellery.
 * Each pack defines: modules, GL chart template, product attributes, tax rules,
 * report templates, and UI theme presets.
 * Schema: epc_industry_packs, epc_tenant_pack_assignments
 */

if (!defined('EPC_INDUSTRY_PACKS_VERSION')) {
    define('EPC_INDUSTRY_PACKS_VERSION', '1.0.0');
}

function epc_industry_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_industry_packs` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `pack_key`        VARCHAR(32)    NOT NULL UNIQUE,
            `name`            VARCHAR(64)    NOT NULL,
            `description`     TEXT           NOT NULL DEFAULT '',
            `icon`            VARCHAR(32)    NOT NULL DEFAULT 'fa-industry',
            `modules`         JSON           NOT NULL,
            `gl_template`     JSON           NOT NULL,
            `product_attrs`   JSON           NOT NULL,
            `tax_rules`       JSON           NOT NULL,
            `theme`           JSON           NOT NULL,
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_tenant_pack_assignments` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `pack_key`        VARCHAR(32)    NOT NULL,
            `applied_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `applied_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            UNIQUE KEY `uk_assignment` (`site_key`, `pack_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_industry_builtin_packs(): array
{
    return array(
        'auto_parts' => array(
            'name' => 'Auto Parts & Accessories',
            'icon' => 'fa-car',
            'description' => 'Full auto-parts vertical: OEM/aftermarket, VIN lookup, fitment data, core deposit tracking.',
            'modules' => array('catalog','inventory','orders','fulfillment','warranty_rma','dealer_portal','credit_limit','po_approval','inventory_forecast'),
            'gl_template' => array(
                array('code' => '1000', 'name' => 'Cash & Bank', 'type' => 'asset'),
                array('code' => '1200', 'name' => 'Accounts Receivable', 'type' => 'asset'),
                array('code' => '1300', 'name' => 'Inventory - Parts', 'type' => 'asset'),
                array('code' => '1310', 'name' => 'Inventory - Core Deposits', 'type' => 'asset'),
                array('code' => '2000', 'name' => 'Accounts Payable', 'type' => 'liability'),
                array('code' => '2100', 'name' => 'Core Deposit Liability', 'type' => 'liability'),
                array('code' => '4000', 'name' => 'Parts Revenue', 'type' => 'revenue'),
                array('code' => '5000', 'name' => 'Cost of Goods - Parts', 'type' => 'expense'),
                array('code' => '5100', 'name' => 'Freight & Shipping', 'type' => 'expense'),
                array('code' => '6000', 'name' => 'Warranty Claims Expense', 'type' => 'expense'),
            ),
            'product_attrs' => array('oem_number','brand','fitment','vehicle_make','vehicle_model','year_range','core_charge','weight_kg','condition'),
            'tax_rules' => array('vat_rate' => 5, 'exempt_categories' => array()),
            'theme' => array('primary' => '#1565C0', 'accent' => '#FF6F00', 'industry_badge' => 'Auto Parts'),
        ),
        'fashion' => array(
            'name' => 'Fashion & Apparel',
            'icon' => 'fa-shopping-bag',
            'description' => 'Fashion vertical: size/color matrix, seasonal collections, lookbooks, returns management.',
            'modules' => array('catalog','inventory','orders','fulfillment','warranty_rma','promotions','collections_dunning'),
            'gl_template' => array(
                array('code' => '1000', 'name' => 'Cash & Bank', 'type' => 'asset'),
                array('code' => '1200', 'name' => 'Accounts Receivable', 'type' => 'asset'),
                array('code' => '1300', 'name' => 'Inventory - Apparel', 'type' => 'asset'),
                array('code' => '2000', 'name' => 'Accounts Payable', 'type' => 'liability'),
                array('code' => '4000', 'name' => 'Apparel Revenue', 'type' => 'revenue'),
                array('code' => '4100', 'name' => 'Returns & Allowances', 'type' => 'revenue'),
                array('code' => '5000', 'name' => 'Cost of Goods - Apparel', 'type' => 'expense'),
            ),
            'product_attrs' => array('size','color','material','collection','season','gender','style_code','care_instructions'),
            'tax_rules' => array('vat_rate' => 5, 'exempt_categories' => array('children_clothing')),
            'theme' => array('primary' => '#AD1457', 'accent' => '#F06292', 'industry_badge' => 'Fashion'),
        ),
        'electronics' => array(
            'name' => 'Electronics & Technology',
            'icon' => 'fa-microchip',
            'description' => 'Electronics vertical: serial tracking, warranty registration, RMA, compatibility matrix.',
            'modules' => array('catalog','inventory','orders','fulfillment','warranty_rma','ai_classification','credit_limit','dealer_portal'),
            'gl_template' => array(
                array('code' => '1000', 'name' => 'Cash & Bank', 'type' => 'asset'),
                array('code' => '1200', 'name' => 'Accounts Receivable', 'type' => 'asset'),
                array('code' => '1300', 'name' => 'Inventory - Electronics', 'type' => 'asset'),
                array('code' => '2000', 'name' => 'Accounts Payable', 'type' => 'liability'),
                array('code' => '4000', 'name' => 'Electronics Revenue', 'type' => 'revenue'),
                array('code' => '5000', 'name' => 'Cost of Goods - Electronics', 'type' => 'expense'),
                array('code' => '5200', 'name' => 'Extended Warranty Revenue', 'type' => 'revenue'),
            ),
            'product_attrs' => array('model_number','brand','serial_number','ean','warranty_months','voltage','wattage','connectivity','compatibility'),
            'tax_rules' => array('vat_rate' => 5, 'exempt_categories' => array()),
            'theme' => array('primary' => '#0D47A1', 'accent' => '#00BCD4', 'industry_badge' => 'Electronics'),
        ),
        'jewellery' => array(
            'name' => 'Jewellery & Precious Metals',
            'icon' => 'fa-diamond',
            'description' => 'Jewellery vertical: karat/purity tracking, hallmark, stone certification, gold rate integration.',
            'modules' => array('catalog','inventory','orders','warranty_rma','multi_currency_gl','collections_dunning'),
            'gl_template' => array(
                array('code' => '1000', 'name' => 'Cash & Bank', 'type' => 'asset'),
                array('code' => '1200', 'name' => 'Accounts Receivable', 'type' => 'asset'),
                array('code' => '1300', 'name' => 'Inventory - Gold', 'type' => 'asset'),
                array('code' => '1310', 'name' => 'Inventory - Diamonds', 'type' => 'asset'),
                array('code' => '1320', 'name' => 'Inventory - Silver', 'type' => 'asset'),
                array('code' => '2000', 'name' => 'Accounts Payable', 'type' => 'liability'),
                array('code' => '4000', 'name' => 'Jewellery Sales', 'type' => 'revenue'),
                array('code' => '5000', 'name' => 'Cost of Gold', 'type' => 'expense'),
                array('code' => '5100', 'name' => 'Cost of Stones', 'type' => 'expense'),
                array('code' => '5200', 'name' => 'Making Charges', 'type' => 'expense'),
            ),
            'product_attrs' => array('karat','purity','weight_gm','stone_type','stone_carat','hallmark_number','certificate_number','metal_type','making_charge'),
            'tax_rules' => array('vat_rate' => 5, 'exempt_categories' => array('investment_gold')),
            'theme' => array('primary' => '#BF360C', 'accent' => '#FFD600', 'industry_badge' => 'Jewellery'),
        ),
    );
}

function epc_industry_seed_packs(PDO $pdo): int
{
    epc_industry_ensure_schema($pdo);
    $packs = epc_industry_builtin_packs();
    $inserted = 0;
    foreach ($packs as $key => $pack) {
        $st = $pdo->prepare("SELECT COUNT(*) FROM `epc_industry_packs` WHERE `pack_key` = ?");
        $st->execute(array($key));
        if ((int)$st->fetchColumn() === 0) {
            $pdo->prepare("INSERT INTO `epc_industry_packs` (`pack_key`,`name`,`description`,`icon`,`modules`,`gl_template`,`product_attrs`,`tax_rules`,`theme`) VALUES (?,?,?,?,?,?,?,?,?)")
                ->execute(array($key, $pack['name'], $pack['description'], $pack['icon'], json_encode($pack['modules']), json_encode($pack['gl_template']), json_encode($pack['product_attrs']), json_encode($pack['tax_rules']), json_encode($pack['theme'])));
            $inserted++;
        }
    }
    return $inserted;
}

function epc_industry_assign_pack(PDO $pdo, string $siteKey, string $packKey, int $userId = 0): array
{
    epc_industry_ensure_schema($pdo);
    $st = $pdo->prepare("INSERT INTO `epc_tenant_pack_assignments` (`site_key`,`pack_key`,`applied_by`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `applied_at` = NOW()");
    $st->execute(array($siteKey, $packKey, $userId));
    return array('ok' => true, 'site_key' => $siteKey, 'pack_key' => $packKey);
}

function epc_industry_tenant_packs(PDO $pdo, string $siteKey): array
{
    $st = $pdo->prepare("SELECT a.*, p.`name`, p.`icon`, p.`description` FROM `epc_tenant_pack_assignments` a JOIN `epc_industry_packs` p ON a.`pack_key` = p.`pack_key` WHERE a.`site_key` = ?");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_industry_fleet_stats(PDO $pdo): array
{
    epc_industry_ensure_schema($pdo);
    $st = $pdo->query("SELECT `pack_key`, COUNT(*) AS `tenants` FROM `epc_tenant_pack_assignments` GROUP BY `pack_key` ORDER BY `tenants` DESC");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

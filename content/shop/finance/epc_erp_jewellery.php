<?php
/**
 * Jewellery ERP — complete business management for jewellery tenants.
 *
 * Covers the full lifecycle:
 *   Master data   → Metal stock, design, diamond, pearl, color stone, karat, rate type
 *   Purchase      → Metal purchase, diamond purchase, fixing, purchase window
 *   Sales         → Retail (POS), metal sales, fixing, returns, advance
 *   Repair        → Receipt, transfer jobs, workshop receive, customer delivery, repair sale
 *   Stock         → Verification, balance, analysis, barcode generation
 *   Finance       → Petty cash, JV, tourist VAT refund
 *
 * Multi-division: G = Gold, S = Silver, T = Platinum, D = Diamond, P = Pearl
 * Multi-business-unit: tenant can have 3+ business units (branches).
 * Country-driven compliance via epc_country_profile().
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';

/* ────────────────────────────────────────────
 * Schema bootstrap
 * ──────────────────────────────────────────── */

function epc_jewel_ensure_schema(PDO $db): void
{
    static $done = false;
    if ($done) return;
    $done = true;

    $stmts = array(
        "CREATE TABLE IF NOT EXISTS `epc_jewel_karat_master` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `karat_code` VARCHAR(10) NOT NULL,
            `description` VARCHAR(60) NOT NULL DEFAULT '',
            `std_purity` DECIMAL(7,6) NOT NULL DEFAULT 0,
            `range_from` DECIMAL(7,6) NOT NULL DEFAULT 0,
            `range_to` DECIMAL(7,6) NOT NULL DEFAULT 0,
            `sp_gravity` DECIMAL(7,4) NOT NULL DEFAULT 0,
            `pos_rate_min_max` DECIMAL(12,2) NOT NULL DEFAULT 0,
            `division` CHAR(2) NOT NULL DEFAULT 'G',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_karat` (`company_id`, `karat_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_rate_type` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `metal` CHAR(2) NOT NULL DEFAULT 'G',
            `rate_type` VARCHAR(10) NOT NULL,
            `conv_factor` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `conv_factor_oz` DECIMAL(12,4) NOT NULL DEFAULT 31.1035,
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `curr_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `rate_variance_pct` DECIMAL(6,2) NOT NULL DEFAULT 50,
            `pos_margin_min` DECIMAL(6,2) NOT NULL DEFAULT 1,
            `pos_margin_max` DECIMAL(6,2) NOT NULL DEFAULT 50,
            `status` VARCHAR(10) NOT NULL DEFAULT 'MULTIPLY',
            `is_default` TINYINT(1) NOT NULL DEFAULT 0,
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_rt` (`company_id`, `metal`, `rate_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_currency` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `curr_code` VARCHAR(5) NOT NULL,
            `description` VARCHAR(60) NOT NULL DEFAULT '',
            `fraction` VARCHAR(20) NOT NULL DEFAULT '',
            `symbol` VARCHAR(5) NOT NULL DEFAULT '',
            `conv_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `min_conv_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `max_conv_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `status` VARCHAR(10) NOT NULL DEFAULT 'MULTIPLY',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_curr` (`company_id`, `curr_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_metal_stock` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `metal` CHAR(2) NOT NULL DEFAULT 'G',
            `item_code` VARCHAR(20) NOT NULL,
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `cc_making` VARCHAR(20) NOT NULL DEFAULT '',
            `cc_metal` VARCHAR(20) NOT NULL DEFAULT '',
            `karat` VARCHAR(10) NOT NULL DEFAULT '',
            `purity` DECIMAL(7,6) NOT NULL DEFAULT 0,
            `type` VARCHAR(30) NOT NULL DEFAULT '',
            `brand` VARCHAR(60) NOT NULL DEFAULT '',
            `category` VARCHAR(30) NOT NULL DEFAULT '',
            `sub_category` VARCHAR(30) NOT NULL DEFAULT '',
            `vendor` VARCHAR(20) NOT NULL DEFAULT '',
            `vendor_ref` VARCHAR(20) NOT NULL DEFAULT '',
            `country` VARCHAR(30) NOT NULL DEFAULT '',
            `hs_code` VARCHAR(20) NOT NULL DEFAULT '',
            `mc_unit` VARCHAR(10) NOT NULL DEFAULT 'GMS',
            `include_stone_weight` TINYINT(1) NOT NULL DEFAULT 0,
            `pass_purity_diff` TINYINT(1) NOT NULL DEFAULT 0,
            `in_pieces` TINYINT(1) NOT NULL DEFAULT 1,
            `pc_weight_gms` DECIMAL(12,5) NOT NULL DEFAULT 0,
            `pc_weight_oz` DECIMAL(12,5) NOT NULL DEFAULT 0,
            `create_barcodes` TINYINT(1) NOT NULL DEFAULT 0,
            `barcode_prefix` VARCHAR(10) NOT NULL DEFAULT '',
            `block_gross_wt_sales` TINYINT(1) NOT NULL DEFAULT 0,
            `ask_supplier` TINYINT(1) NOT NULL DEFAULT 0,
            `ask_wastage` TINYINT(1) NOT NULL DEFAULT 0,
            `exclude_gst_trn` TINYINT(1) NOT NULL DEFAULT 0,
            `gst_trn_on_making_stone` TINYINT(1) NOT NULL DEFAULT 1,
            `allow_negative_stock` TINYINT(1) NOT NULL DEFAULT 0,
            `allow_less_than_cost` TINYINT(1) NOT NULL DEFAULT 0,
            `conv_factor_oz` DECIMAL(12,5) NOT NULL DEFAULT 31.10347,
            `abc_code` CHAR(1) NOT NULL DEFAULT '',
            `loyalty_item` TINYINT(1) NOT NULL DEFAULT 0,
            `pop_stock_filter` TINYINT(1) NOT NULL DEFAULT 0,
            `gst_trn_making_only` TINYINT(1) NOT NULL DEFAULT 0,
            `price1_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price1_label` VARCHAR(20) NOT NULL DEFAULT 'General',
            `price2_code` VARCHAR(5) NOT NULL DEFAULT '',
            `price2_label` VARCHAR(20) NOT NULL DEFAULT '',
            `price3_code` VARCHAR(5) NOT NULL DEFAULT '',
            `price4_code` VARCHAR(5) NOT NULL DEFAULT '',
            `price5_code` VARCHAR(5) NOT NULL DEFAULT '',
            `std_cost` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `discount_pct` DECIMAL(6,2) NOT NULL DEFAULT 0,
            `min_qty` DECIMAL(12,2) NOT NULL DEFAULT 0,
            `max_qty` DECIMAL(12,2) NOT NULL DEFAULT 0,
            `reorder_level` DECIMAL(12,2) NOT NULL DEFAULT 0,
            `reorder_qty` DECIMAL(12,2) NOT NULL DEFAULT 0,
            `pur_cost_gms` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `sale_price_gms` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `stock_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `stock_pcs` INT NOT NULL DEFAULT 0,
            `stock_gms` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `stock_value` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_item` (`company_id`, `item_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_design` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `design_code` VARCHAR(20) NOT NULL,
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `currency_rate` DECIMAL(12,5) NOT NULL DEFAULT 1,
            `cost_centre` VARCHAR(20) NOT NULL DEFAULT '',
            `metal_and_stone` TINYINT(1) NOT NULL DEFAULT 1,
            `type` VARCHAR(30) NOT NULL DEFAULT '',
            `set_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `category` VARCHAR(30) NOT NULL DEFAULT '',
            `sub_category` VARCHAR(30) NOT NULL DEFAULT '',
            `brand` VARCHAR(60) NOT NULL DEFAULT '',
            `color` VARCHAR(30) NOT NULL DEFAULT '',
            `country` VARCHAR(30) NOT NULL DEFAULT '',
            `pair_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `vendor` VARCHAR(20) NOT NULL DEFAULT '',
            `vendor_ref` VARCHAR(20) NOT NULL DEFAULT '',
            `cost_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price1_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price1_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_code` VARCHAR(5) NOT NULL DEFAULT '',
            `price2_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price2_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price3_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price4_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price5_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `metal_total_qty` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `stone_total_qty` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `tag_details` TEXT,
            `image_path` VARCHAR(255) NOT NULL DEFAULT '',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_design` (`company_id`, `design_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_design_metals` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `design_id` INT UNSIGNED NOT NULL,
            `line_no` SMALLINT UNSIGNED NOT NULL DEFAULT 1,
            `division` CHAR(2) NOT NULL DEFAULT 'G',
            `karat` VARCHAR(10) NOT NULL DEFAULT '',
            `gross_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `rate_type` VARCHAR(10) NOT NULL DEFAULT 'GMS',
            `metal_rate` DECIMAL(14,5) NOT NULL DEFAULT 0,
            `amount_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `amount_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            KEY `fk_design` (`design_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_design_stones` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `design_id` INT UNSIGNED NOT NULL,
            `line_no` SMALLINT UNSIGNED NOT NULL DEFAULT 1,
            `stone_type` VARCHAR(30) NOT NULL DEFAULT '',
            `shape` VARCHAR(20) NOT NULL DEFAULT '',
            `size` VARCHAR(20) NOT NULL DEFAULT '',
            `color` VARCHAR(20) NOT NULL DEFAULT '',
            `clarity` VARCHAR(20) NOT NULL DEFAULT '',
            `pcs` INT NOT NULL DEFAULT 0,
            `carat` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `rate` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `amount_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `amount_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            KEY `fk_design_s` (`design_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_diamond_master` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `item_code` VARCHAR(20) NOT NULL,
            `rfid` VARCHAR(30) NOT NULL DEFAULT '',
            `design` VARCHAR(30) NOT NULL DEFAULT '',
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `promotional` TINYINT(1) NOT NULL DEFAULT 0,
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `currency_rate` DECIMAL(12,5) NOT NULL DEFAULT 1,
            `cost_centre` VARCHAR(20) NOT NULL DEFAULT '',
            `category` VARCHAR(30) NOT NULL DEFAULT '',
            `sub_category` VARCHAR(30) NOT NULL DEFAULT '',
            `color` VARCHAR(20) NOT NULL DEFAULT '',
            `clarity` VARCHAR(20) NOT NULL DEFAULT '',
            `type` VARCHAR(30) NOT NULL DEFAULT '',
            `brand` VARCHAR(60) NOT NULL DEFAULT '',
            `country` VARCHAR(30) NOT NULL DEFAULT '',
            `fluorescence` VARCHAR(20) NOT NULL DEFAULT '',
            `style` VARCHAR(20) NOT NULL DEFAULT '',
            `set_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `vendor` VARCHAR(20) NOT NULL DEFAULT '',
            `vendor_ref` VARCHAR(20) NOT NULL DEFAULT '',
            `item_gr_wt` DECIMAL(10,4) NOT NULL DEFAULT 0,
            `cost_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_code` VARCHAR(5) NOT NULL DEFAULT 'TAG',
            `price1_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price1_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price2_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price2_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price3_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price4_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price5_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `landed_cost` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `foreign_cost` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `cost_difference` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `certificate_no` VARCHAR(40) NOT NULL DEFAULT '',
            `certificate_date` DATE DEFAULT NULL,
            `certificate_by` VARCHAR(40) NOT NULL DEFAULT '',
            `certificate_no_1` VARCHAR(40) NOT NULL DEFAULT '',
            `certificate_date_1` DATE DEFAULT NULL,
            `no_of_certificates` INT NOT NULL DEFAULT 0,
            `setting_charge` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `polishing_charge` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `rhodium_charge` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `labour_charge` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `misc_charge` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `exclude_gst_metal` TINYINT(1) NOT NULL DEFAULT 0,
            `pure_wt` DECIMAL(10,4) NOT NULL DEFAULT 0,
            `trn_on_margin` TINYINT(1) NOT NULL DEFAULT 0,
            `uae_trn_item` TINYINT(1) NOT NULL DEFAULT 0,
            `last_purchase_voc` VARCHAR(20) NOT NULL DEFAULT '',
            `last_purchase_party` VARCHAR(20) NOT NULL DEFAULT '',
            `last_purchase_date` DATE DEFAULT NULL,
            `last_purchase_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `last_purchase_branch` VARCHAR(10) NOT NULL DEFAULT '',
            `last_sale_voc` VARCHAR(20) NOT NULL DEFAULT '',
            `last_sale_party` VARCHAR(20) NOT NULL DEFAULT '',
            `last_sale_date` DATE DEFAULT NULL,
            `last_sale_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `last_sale_branch` VARCHAR(10) NOT NULL DEFAULT '',
            `cust_sku` VARCHAR(30) NOT NULL DEFAULT '',
            `ageing_date` DATE DEFAULT NULL,
            `tag_details` TEXT,
            `image_path` VARCHAR(255) NOT NULL DEFAULT '',
            `certificate_image` VARCHAR(255) NOT NULL DEFAULT '',
            `metal_qty` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `stone_qty` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `labour_amount_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `labour_amount_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_ditem` (`company_id`, `item_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_pearl_master` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `code` VARCHAR(20) NOT NULL,
            `nature` VARCHAR(20) NOT NULL DEFAULT 'Cultured',
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `design` VARCHAR(30) NOT NULL DEFAULT '',
            `type` VARCHAR(30) NOT NULL DEFAULT '',
            `cost_centre` VARCHAR(20) NOT NULL DEFAULT '',
            `category` VARCHAR(30) NOT NULL DEFAULT '',
            `color` VARCHAR(20) NOT NULL DEFAULT '',
            `vendor` VARCHAR(20) NOT NULL DEFAULT '',
            `vendor_ref` VARCHAR(20) NOT NULL DEFAULT '',
            `luster` VARCHAR(20) NOT NULL DEFAULT '',
            `shape` VARCHAR(20) NOT NULL DEFAULT '',
            `size` VARCHAR(20) NOT NULL DEFAULT '',
            `brand` VARCHAR(60) NOT NULL DEFAULT '',
            `country` VARCHAR(30) NOT NULL DEFAULT '',
            `style` VARCHAR(20) NOT NULL DEFAULT '',
            `set_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `grade` VARCHAR(20) NOT NULL DEFAULT '',
            `sub_category` VARCHAR(30) NOT NULL DEFAULT '',
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `currency_rate` DECIMAL(12,5) NOT NULL DEFAULT 1,
            `cost_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price1_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price1_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price2_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price2_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price3_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price4_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price5_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `landed_cost` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `foreign_cost` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `pieces_wise_carat` DECIMAL(10,3) NOT NULL DEFAULT 0,
            `tag_details` TEXT,
            `promotional` TINYINT(1) NOT NULL DEFAULT 0,
            `ageing_date` DATE DEFAULT NULL,
            `image_path` VARCHAR(255) NOT NULL DEFAULT '',
            `certificate_image` VARCHAR(255) NOT NULL DEFAULT '',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_pearl` (`company_id`, `code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_color_stone_master` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `code` VARCHAR(20) NOT NULL,
            `short_id` VARCHAR(20) NOT NULL DEFAULT '',
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `cost_centre` VARCHAR(20) NOT NULL DEFAULT '',
            `shape` VARCHAR(20) NOT NULL DEFAULT '',
            `finish` VARCHAR(20) NOT NULL DEFAULT '',
            `size` VARCHAR(20) NOT NULL DEFAULT '',
            `color` VARCHAR(20) NOT NULL DEFAULT '',
            `clarity` VARCHAR(20) NOT NULL DEFAULT '',
            `sieve` VARCHAR(20) NOT NULL DEFAULT '',
            `brand` VARCHAR(60) NOT NULL DEFAULT '',
            `country` VARCHAR(30) NOT NULL DEFAULT '',
            `style` VARCHAR(20) NOT NULL DEFAULT '',
            `set_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `fluorescence` VARCHAR(20) NOT NULL DEFAULT '',
            `category` VARCHAR(30) NOT NULL DEFAULT '',
            `sub_category` VARCHAR(30) NOT NULL DEFAULT '',
            `vendor` VARCHAR(20) NOT NULL DEFAULT '',
            `vendor_ref` VARCHAR(20) NOT NULL DEFAULT '',
            `grade` VARCHAR(20) NOT NULL DEFAULT '',
            `design` VARCHAR(30) NOT NULL DEFAULT '',
            `pieces_wise_carat` DECIMAL(10,3) NOT NULL DEFAULT 0,
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `currency_rate` DECIMAL(12,5) NOT NULL DEFAULT 1,
            `cost_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price1_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price1_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price1_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_code` VARCHAR(5) NOT NULL DEFAULT 'GEN',
            `price2_pct` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `price2_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price2_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price3_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price4_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `price5_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `weighted_avg` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `promotional` TINYINT(1) NOT NULL DEFAULT 0,
            `tag_details` TEXT,
            `ageing_date` DATE DEFAULT NULL,
            `proportion_girdle` VARCHAR(30) NOT NULL DEFAULT '',
            `proportion_culet` VARCHAR(30) NOT NULL DEFAULT '',
            `table_width` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `crown_height` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `pavilion_depth` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `overall` VARCHAR(20) NOT NULL DEFAULT '',
            `measurement_length` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `measurement_width` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `measurement_depth` DECIMAL(8,2) NOT NULL DEFAULT 0,
            `certificate_no` VARCHAR(40) NOT NULL DEFAULT '',
            `certificate_date` DATE DEFAULT NULL,
            `certificate_by` VARCHAR(40) NOT NULL DEFAULT '',
            `cut` VARCHAR(20) NOT NULL DEFAULT '',
            `polish` VARCHAR(20) NOT NULL DEFAULT '',
            `symmetry` VARCHAR(20) NOT NULL DEFAULT '',
            `exclude_vat` TINYINT(1) NOT NULL DEFAULT 0,
            `image_path` VARCHAR(255) NOT NULL DEFAULT '',
            `certificate_path` VARCHAR(255) NOT NULL DEFAULT '',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_co_cs` (`company_id`, `code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_voucher` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL,
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `party_code` VARCHAR(20) NOT NULL DEFAULT '',
            `party_name` VARCHAR(120) NOT NULL DEFAULT '',
            `party_curr` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `party_curr_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `customer_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `customer_name` VARCHAR(120) NOT NULL DEFAULT '',
            `mobile` VARCHAR(20) NOT NULL DEFAULT '',
            `tel1` VARCHAR(20) NOT NULL DEFAULT '',
            `email` VARCHAR(100) NOT NULL DEFAULT '',
            `nationality` VARCHAR(30) NOT NULL DEFAULT '',
            `type_resi` VARCHAR(10) NOT NULL DEFAULT '',
            `state` VARCHAR(30) NOT NULL DEFAULT '',
            `religion` VARCHAR(30) NOT NULL DEFAULT '',
            `city` VARCHAR(30) NOT NULL DEFAULT '',
            `po_box` VARCHAR(20) NOT NULL DEFAULT '',
            `salesman` VARCHAR(20) NOT NULL DEFAULT '',
            `remarks` TEXT,
            `item_curr` VARCHAR(5) NOT NULL DEFAULT '',
            `metal_rate` DECIMAL(14,5) NOT NULL DEFAULT 0,
            `is_fixed` TINYINT(1) NOT NULL DEFAULT 0,
            `cr_days` INT NOT NULL DEFAULT 0,
            `cr_days_date` DATE DEFAULT NULL,
            `supp_inv_no` VARCHAR(30) NOT NULL DEFAULT '',
            `supp_inv_date` DATE DEFAULT NULL,
            `internal_unfix` TINYINT(1) NOT NULL DEFAULT 0,
            `posted_date` DATE DEFAULT NULL,
            `consignment_party` VARCHAR(30) NOT NULL DEFAULT '',
            `tourist_vat_refund` TINYINT(1) NOT NULL DEFAULT 0,
            `journal_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `narration` TEXT,
            `net_amount` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `rnd_off_amount` DECIMAL(10,2) NOT NULL DEFAULT 0,
            `rnd_net_amount` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `other_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `gross_total` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `vat_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `total_with_vat` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `sub_total` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `receipt_total` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `refund_due` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `adjust_sale_return` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `old_gold_exchange` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `gold_scheme_redeem` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `status` VARCHAR(20) NOT NULL DEFAULT 'draft',
            `authorized` TINYINT(1) NOT NULL DEFAULT 0,
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            KEY `idx_co_type_date` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_voucher_lines` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `voucher_id` INT UNSIGNED NOT NULL,
            `line_no` SMALLINT UNSIGNED NOT NULL DEFAULT 1,
            `stock_code` VARCHAR(20) NOT NULL DEFAULT '',
            `division` CHAR(2) NOT NULL DEFAULT 'G',
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `pcs` INT NOT NULL DEFAULT 0,
            `qty` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `gr_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `purity` DECIMAL(7,6) NOT NULL DEFAULT 0,
            `pure_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `mkg_rate` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `mkg_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `metal_rate` DECIMAL(14,5) NOT NULL DEFAULT 0,
            `metal_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `stone_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `wastage_pct` DECIMAL(8,4) NOT NULL DEFAULT 0,
            `wastage_qty` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `disc_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `total_amount` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `total_with_vat` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `net_amount` DECIMAL(16,2) NOT NULL DEFAULT 0,
            KEY `fk_voc` (`voucher_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_voucher_receipts` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `voucher_id` INT UNSIGNED NOT NULL,
            `receipt_mode` VARCHAR(20) NOT NULL DEFAULT 'CASH',
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `curr_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `amount_fc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `amount_lc` DECIMAL(14,2) NOT NULL DEFAULT 0,
            KEY `fk_voc_r` (`voucher_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_repair` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'REP',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `customer_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `customer_name` VARCHAR(120) NOT NULL DEFAULT '',
            `mobile` VARCHAR(20) NOT NULL DEFAULT '',
            `tel1` VARCHAR(20) NOT NULL DEFAULT '',
            `email` VARCHAR(100) NOT NULL DEFAULT '',
            `nationality` VARCHAR(30) NOT NULL DEFAULT '',
            `type_resi` VARCHAR(10) NOT NULL DEFAULT '',
            `state` VARCHAR(30) NOT NULL DEFAULT '',
            `religion` VARCHAR(30) NOT NULL DEFAULT '',
            `city` VARCHAR(30) NOT NULL DEFAULT '',
            `po_box` VARCHAR(20) NOT NULL DEFAULT '',
            `salesman` VARCHAR(20) NOT NULL DEFAULT '',
            `remarks` TEXT,
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `delivery_date` DATE DEFAULT NULL,
            `repair_narration` TEXT,
            `status` VARCHAR(20) NOT NULL DEFAULT 'received',
            `authorized` TINYINT(1) NOT NULL DEFAULT 0,
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            KEY `idx_co_rep` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_repair_items` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `repair_id` INT UNSIGNED NOT NULL,
            `line_no` SMALLINT UNSIGNED NOT NULL DEFAULT 1,
            `division` CHAR(2) NOT NULL DEFAULT 'G',
            `stock_code` VARCHAR(20) NOT NULL DEFAULT '',
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `item_remarks` VARCHAR(200) NOT NULL DEFAULT '',
            `bag_no` VARCHAR(20) NOT NULL DEFAULT '',
            `pcs` INT NOT NULL DEFAULT 0,
            `gr_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `repair_type` VARCHAR(10) NOT NULL DEFAULT 'DP',
            `item_type` VARCHAR(10) NOT NULL DEFAULT 'CN',
            `repair_status` VARCHAR(20) NOT NULL DEFAULT 'In Branch',
            `delivery_date` DATE DEFAULT NULL,
            `status_detail` VARCHAR(20) NOT NULL DEFAULT 'DELIVERED',
            KEY `fk_repair` (`repair_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_repair_transfer` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'RET',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `salesman` VARCHAR(20) NOT NULL DEFAULT '',
            `branch_to` VARCHAR(20) NOT NULL DEFAULT '',
            `party_code` VARCHAR(20) NOT NULL DEFAULT '',
            `party_name` VARCHAR(120) NOT NULL DEFAULT '',
            `division` VARCHAR(5) NOT NULL DEFAULT '',
            `transfer_remarks` TEXT,
            `status` VARCHAR(20) NOT NULL DEFAULT 'pending',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_rt` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_repair_transfer_items` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `transfer_id` INT UNSIGNED NOT NULL,
            `repair_voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `repair_branch` VARCHAR(10) NOT NULL DEFAULT '',
            `repair_bag_no` VARCHAR(20) NOT NULL DEFAULT '',
            `stock_code` VARCHAR(20) NOT NULL DEFAULT '',
            `voc_date` DATE DEFAULT NULL,
            `customer_name` VARCHAR(120) NOT NULL DEFAULT '',
            `mobile` VARCHAR(20) NOT NULL DEFAULT '',
            `repair_narration` VARCHAR(200) NOT NULL DEFAULT '',
            KEY `fk_rt` (`transfer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_repair_workshop_receive` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'RRC',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `salesman` VARCHAR(20) NOT NULL DEFAULT '',
            `party_code` VARCHAR(20) NOT NULL DEFAULT '',
            `party_name` VARCHAR(120) NOT NULL DEFAULT '',
            `party_curr` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `party_curr_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `customer_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `customer_name` VARCHAR(120) NOT NULL DEFAULT '',
            `supp_inv_no` VARCHAR(30) NOT NULL DEFAULT '',
            `supp_inv_date` DATE DEFAULT NULL,
            `receive_remarks` TEXT,
            `labour_charges` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `diamond_voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `diamond_voc_type` VARCHAR(10) NOT NULL DEFAULT '',
            `diamond_wgt` DECIMAL(10,4) NOT NULL DEFAULT 0,
            `diamond_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `diamond_total_vat` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `metal_voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `metal_voc_type` VARCHAR(10) NOT NULL DEFAULT '',
            `metal_wgt` DECIMAL(10,4) NOT NULL DEFAULT 0,
            `metal_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `metal_total_vat` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `job_done_properly` VARCHAR(5) NOT NULL DEFAULT 'Yes',
            `status` VARCHAR(20) NOT NULL DEFAULT 'received',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_rrc` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_repair_delivery` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'RTD',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `customer_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `customer_name` VARCHAR(120) NOT NULL DEFAULT '',
            `mobile` VARCHAR(20) NOT NULL DEFAULT '',
            `tel1` VARCHAR(20) NOT NULL DEFAULT '',
            `email` VARCHAR(100) NOT NULL DEFAULT '',
            `nationality` VARCHAR(30) NOT NULL DEFAULT '',
            `type_resi` VARCHAR(10) NOT NULL DEFAULT '',
            `state` VARCHAR(30) NOT NULL DEFAULT '',
            `religion` VARCHAR(30) NOT NULL DEFAULT '',
            `city` VARCHAR(30) NOT NULL DEFAULT '',
            `po_box` VARCHAR(20) NOT NULL DEFAULT '',
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `currency_rate` DECIMAL(12,6) NOT NULL DEFAULT 1,
            `salesman` VARCHAR(20) NOT NULL DEFAULT '',
            `delivery_remarks` TEXT,
            `diamond_checked` VARCHAR(5) NOT NULL DEFAULT 'Yes',
            `job_done_properly` VARCHAR(5) NOT NULL DEFAULT 'Yes',
            `receipt_received` VARCHAR(5) NOT NULL DEFAULT 'Yes',
            `pos_voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `pos_voc_type` VARCHAR(10) NOT NULL DEFAULT '',
            `repair_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `sub_total` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `round_off` DECIMAL(10,2) NOT NULL DEFAULT 0,
            `net_total` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `status` VARCHAR(20) NOT NULL DEFAULT 'pending',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_rtd` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_stock_verification` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'MSV',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `verified_by` VARCHAR(40) NOT NULL DEFAULT '',
            `location` VARCHAR(40) NOT NULL DEFAULT '',
            `metal_stone` CHAR(2) NOT NULL DEFAULT 'M',
            `division` VARCHAR(5) NOT NULL DEFAULT '',
            `batch` VARCHAR(20) NOT NULL DEFAULT 'Batch-01',
            `set_default_1pc` TINYINT(1) NOT NULL DEFAULT 0,
            `allow_multiple_entry` TINYINT(1) NOT NULL DEFAULT 0,
            `show_picture` TINYINT(1) NOT NULL DEFAULT 0,
            `block_exceeded_stock` TINYINT(1) NOT NULL DEFAULT 1,
            `import_with_qty` TINYINT(1) NOT NULL DEFAULT 0,
            `total_pcs` INT NOT NULL DEFAULT 0,
            `scanned_pcs` INT NOT NULL DEFAULT 0,
            `remaining_pcs` INT NOT NULL DEFAULT 0,
            `total_wgt` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `scanned_wgt` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `remaining_wgt` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `total_gold_computer` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `total_gold_physical` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `pure_wt_computer` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `pure_wt_physical` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `remarks` TEXT,
            `status` VARCHAR(20) NOT NULL DEFAULT 'in_progress',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_msv` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_stock_verification_lines` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `verification_id` INT UNSIGNED NOT NULL,
            `line_no` SMALLINT UNSIGNED NOT NULL DEFAULT 1,
            `stock_code` VARCHAR(20) NOT NULL DEFAULT '',
            `description` VARCHAR(120) NOT NULL DEFAULT '',
            `computer_pcs` INT NOT NULL DEFAULT 0,
            `computer_gr_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `physical_pcs` INT NOT NULL DEFAULT 0,
            `physical_gr_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `diff_pcs` INT NOT NULL DEFAULT 0,
            `diff_gr_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            KEY `fk_sv` (`verification_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_tourist_vat_refund` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'VRV',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `party_code` VARCHAR(20) NOT NULL DEFAULT '',
            `party_name` VARCHAR(120) NOT NULL DEFAULT '',
            `party_phone` VARCHAR(20) NOT NULL DEFAULT '',
            `party_email` VARCHAR(100) NOT NULL DEFAULT '',
            `party_ref_number` INT UNSIGNED NOT NULL DEFAULT 0,
            `party_ref_date` DATE DEFAULT NULL,
            `date_from` DATE DEFAULT NULL,
            `date_to` DATE DEFAULT NULL,
            `salesman` VARCHAR(20) NOT NULL DEFAULT '',
            `journal_ref` VARCHAR(30) NOT NULL DEFAULT '',
            `remarks` TEXT,
            `status` VARCHAR(20) NOT NULL DEFAULT 'draft',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_vrv` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_barcode` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `stock_code` VARCHAR(20) NOT NULL,
            `barcode` VARCHAR(40) NOT NULL,
            `item_type` VARCHAR(20) NOT NULL DEFAULT 'metal',
            `division` CHAR(2) NOT NULL DEFAULT 'G',
            `karat` VARCHAR(10) NOT NULL DEFAULT '',
            `gross_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `net_wt` DECIMAL(12,4) NOT NULL DEFAULT 0,
            `stone_wt` DECIMAL(10,4) NOT NULL DEFAULT 0,
            `purity` DECIMAL(7,6) NOT NULL DEFAULT 0,
            `tag_price` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `printed` TINYINT(1) NOT NULL DEFAULT 0,
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_bc` (`company_id`, `barcode`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_fixing` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `fix_type` VARCHAR(10) NOT NULL DEFAULT 'PF',
            `fix_date` DATE NOT NULL,
            `fix_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `party_code` VARCHAR(20) NOT NULL DEFAULT '',
            `party_name` VARCHAR(120) NOT NULL DEFAULT '',
            `metal` CHAR(2) NOT NULL DEFAULT 'G',
            `karat` VARCHAR(10) NOT NULL DEFAULT '',
            `rate_type` VARCHAR(10) NOT NULL DEFAULT 'GMS',
            `fix_rate` DECIMAL(14,5) NOT NULL DEFAULT 0,
            `fix_qty_gms` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `fix_amount` DECIMAL(16,2) NOT NULL DEFAULT 0,
            `unfixed_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
            `reference_voc` VARCHAR(30) NOT NULL DEFAULT '',
            `status` VARCHAR(20) NOT NULL DEFAULT 'open',
            `remarks` TEXT,
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_fix` (`company_id`, `fix_type`, `fix_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        "CREATE TABLE IF NOT EXISTS `epc_jewel_petty_cash` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
            `branch` VARCHAR(10) NOT NULL DEFAULT 'HO',
            `voc_type` VARCHAR(10) NOT NULL DEFAULT 'PCV',
            `voc_date` DATE NOT NULL,
            `voc_no` INT UNSIGNED NOT NULL DEFAULT 0,
            `description` VARCHAR(200) NOT NULL DEFAULT '',
            `account_code` VARCHAR(20) NOT NULL DEFAULT '',
            `account_name` VARCHAR(60) NOT NULL DEFAULT '',
            `debit_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `credit_amount` DECIMAL(14,2) NOT NULL DEFAULT 0,
            `currency` VARCHAR(5) NOT NULL DEFAULT 'AED',
            `approved_by` VARCHAR(40) NOT NULL DEFAULT '',
            `remarks` TEXT,
            `status` VARCHAR(20) NOT NULL DEFAULT 'draft',
            `created_by` VARCHAR(40) NOT NULL DEFAULT '',
            `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
            KEY `idx_co_pcv` (`company_id`, `voc_type`, `voc_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    );

    foreach ($stmts as $sql) {
        try { $db->exec($sql); } catch (\Throwable $e) { /* table may exist */ }
    }
}

/* ────────────────────────────────────────────
 * Voucher type registry
 * ──────────────────────────────────────────── */

function epc_jewel_voc_types(): array
{
    return array(
        'RIN' => array('label' => 'Retail Invoice', 'module' => 'sales'),
        'MSL' => array('label' => 'Metal Sale', 'module' => 'sales'),
        'SFX' => array('label' => 'Sales Fixing', 'module' => 'sales'),
        'SRT' => array('label' => 'Sales Return', 'module' => 'sales'),
        'ADV' => array('label' => 'POS Advance', 'module' => 'sales'),
        'RMP' => array('label' => 'Metal Purchase', 'module' => 'purchase'),
        'RDP' => array('label' => 'Diamond Purchase', 'module' => 'purchase'),
        'PFX' => array('label' => 'Purchase Fixing', 'module' => 'purchase'),
        'PWN' => array('label' => 'Purchase Window', 'module' => 'purchase'),
        'REP' => array('label' => 'Repair Receipt', 'module' => 'repair'),
        'RET' => array('label' => 'Repair Transfer', 'module' => 'repair'),
        'RTC' => array('label' => 'Repair to Workshop', 'module' => 'repair'),
        'RRC' => array('label' => 'Workshop Receive', 'module' => 'repair'),
        'RTD' => array('label' => 'Repair Delivery', 'module' => 'repair'),
        'RSL' => array('label' => 'Repair Sale', 'module' => 'repair'),
        'MSV' => array('label' => 'Stock Verification', 'module' => 'stock'),
        'VRV' => array('label' => 'Tourist VAT Refund', 'module' => 'finance'),
        'PCV' => array('label' => 'Petty Cash', 'module' => 'finance'),
        'JVL' => array('label' => 'Journal Voucher', 'module' => 'finance'),
    );
}

/* ────────────────────────────────────────────
 * Division registry
 * ──────────────────────────────────────────── */

function epc_jewel_divisions(): array
{
    return array(
        'G' => 'Gold',
        'S' => 'Silver',
        'T' => 'Platinum',
        'D' => 'Diamond',
        'P' => 'Pearl',
        'W' => 'Watch',
    );
}

/* ────────────────────────────────────────────
 * Karat helpers
 * ──────────────────────────────────────────── */

function epc_jewel_karat_list(PDO $db, int $companyId): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_karat_master` WHERE `company_id` = ? ORDER BY `karat_code`');
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_karat_save(PDO $db, int $companyId, array $data): int
{
    epc_jewel_ensure_schema($db);
    $sql = 'INSERT INTO `epc_jewel_karat_master`
            (`company_id`, `karat_code`, `description`, `std_purity`, `range_from`, `range_to`, `sp_gravity`, `pos_rate_min_max`, `division`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON DUPLICATE KEY UPDATE
            `description` = VALUES(`description`), `std_purity` = VALUES(`std_purity`),
            `range_from` = VALUES(`range_from`), `range_to` = VALUES(`range_to`),
            `sp_gravity` = VALUES(`sp_gravity`), `pos_rate_min_max` = VALUES(`pos_rate_min_max`),
            `division` = VALUES(`division`)';
    $stmt = $db->prepare($sql);
    $stmt->execute([
        $companyId,
        $data['karat_code'] ?? '',
        $data['description'] ?? '',
        $data['std_purity'] ?? 0,
        $data['range_from'] ?? 0,
        $data['range_to'] ?? 0,
        $data['sp_gravity'] ?? 0,
        $data['pos_rate_min_max'] ?? 0,
        $data['division'] ?? 'G',
    ]);
    return (int) $db->lastInsertId();
}

function epc_jewel_karat_seed(PDO $db, int $companyId): void
{
    $defaults = array(
        array('14', '14', 0.583000, 0.582000, 0.584000, 0, 0, 'G'),
        array('18', '18 Karat', 0.750000, 0.749000, 0.751000, 0, 0, 'G'),
        array('20', '20 Karat', 0.833000, 0.832000, 0.834000, 0, 0, 'G'),
        array('21', '21 Karat', 0.875000, 0.874000, 0.876000, 0, 0, 'G'),
        array('22', '22 Karat', 0.917000, 0.916000, 0.918000, 0, 0, 'G'),
        array('24', '24 Karat', 1.000000, 0.999000, 1.000000, 0, 0, 'G'),
        array('9', '9 Karat', 0.375000, 0.374000, 0.376000, 0, 0, 'G'),
        array('PLT', 'Plt 950', 0.950000, 0.949000, 0.951000, 0, 0, 'T'),
        array('SC', 'SC', 0.800000, 0.799000, 0.801000, 0, 0, 'G'),
    );
    foreach ($defaults as $row) {
        epc_jewel_karat_save($db, $companyId, array(
            'karat_code' => $row[0], 'description' => $row[1],
            'std_purity' => $row[2], 'range_from' => $row[3],
            'range_to' => $row[4], 'sp_gravity' => $row[5],
            'pos_rate_min_max' => $row[6], 'division' => $row[7],
        ));
    }
}

/* ────────────────────────────────────────────
 * Rate type helpers
 * ──────────────────────────────────────────── */

function epc_jewel_rate_type_list(PDO $db, int $companyId): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_rate_type` WHERE `company_id` = ? ORDER BY `metal`, `rate_type`');
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_rate_type_save(PDO $db, int $companyId, array $data): int
{
    epc_jewel_ensure_schema($db);
    $sql = 'INSERT INTO `epc_jewel_rate_type`
            (`company_id`, `metal`, `rate_type`, `conv_factor`, `conv_factor_oz`, `currency`, `curr_rate`, `rate_variance_pct`, `pos_margin_min`, `pos_margin_max`, `status`, `is_default`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON DUPLICATE KEY UPDATE
            `conv_factor` = VALUES(`conv_factor`), `conv_factor_oz` = VALUES(`conv_factor_oz`),
            `currency` = VALUES(`currency`), `curr_rate` = VALUES(`curr_rate`),
            `rate_variance_pct` = VALUES(`rate_variance_pct`), `pos_margin_min` = VALUES(`pos_margin_min`),
            `pos_margin_max` = VALUES(`pos_margin_max`), `status` = VALUES(`status`),
            `is_default` = VALUES(`is_default`)';
    $stmt = $db->prepare($sql);
    $stmt->execute([
        $companyId,
        $data['metal'] ?? 'G',
        $data['rate_type'] ?? 'GMS',
        $data['conv_factor'] ?? 1,
        $data['conv_factor_oz'] ?? 31.1035,
        $data['currency'] ?? 'AED',
        $data['curr_rate'] ?? 1,
        $data['rate_variance_pct'] ?? 50,
        $data['pos_margin_min'] ?? 1,
        $data['pos_margin_max'] ?? 50,
        $data['status'] ?? 'MULTIPLY',
        $data['is_default'] ?? 0,
    ]);
    return (int) $db->lastInsertId();
}

function epc_jewel_rate_type_seed(PDO $db, int $companyId): void
{
    $defaults = array(
        array('G', 'GMS', 1, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 1),
        array('G', 'GOZ', 31.1035, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
        array('G', 'KB', 1000, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
        array('T', 'PGM', 1, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
        array('T', 'POZ', 31.1035, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
        array('S', 'SGM', 1, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
        array('S', 'SOZ', 31.1035, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
        array('G', 'TTB', 116.64, 31.1035, 'AED', 1, 50, 1, 50, 'MULTIPLY', 0),
    );
    foreach ($defaults as $r) {
        epc_jewel_rate_type_save($db, $companyId, array(
            'metal' => $r[0], 'rate_type' => $r[1], 'conv_factor' => $r[2],
            'conv_factor_oz' => $r[3], 'currency' => $r[4], 'curr_rate' => $r[5],
            'rate_variance_pct' => $r[6], 'pos_margin_min' => $r[7], 'pos_margin_max' => $r[8],
            'status' => $r[9], 'is_default' => $r[10],
        ));
    }
}

/* ────────────────────────────────────────────
 * Currency helpers
 * ──────────────────────────────────────────── */

function epc_jewel_currency_list(PDO $db, int $companyId): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_currency` WHERE `company_id` = ? ORDER BY `curr_code`');
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_currency_seed(PDO $db, int $companyId): void
{
    $defaults = array(
        array('AED', 'U.A.E. Dirhams', 'FILLS', '', 1.000000, 1.000000, 1.000000, 'MULTIPLY'),
        array('BHD', 'Bahraini Dinar', '', '', 11.000000, 11.000000, 11.000000, 'MULTIPLY'),
        array('CHF', 'Swiss Franc', '', '', 3.250000, 3.250000, 3.250000, 'MULTIPLY'),
        array('EUR', 'Euro', '', '', 5.200000, 5.200000, 5.200000, 'MULTIPLY'),
        array('GBP', 'Great Britain Pound', '', '', 2.100000, 2.100000, 2.100000, 'MULTIPLY'),
        array('HKD', 'Hongkong Dollar', '', '', 2.500000, 2.500000, 2.500000, 'MULTIPLY'),
        array('INR', 'Indian Rupee', '', '', 10.500000, 10.500000, 10.500000, 'MULTIPLY'),
        array('SAR', 'Saudi Riyal', '', '', 1.020000, 1.020000, 1.020000, 'MULTIPLY'),
        array('USD', 'US Dollar', 'CENTS', '$', 3.670000, 3.670000, 3.670000, 'MULTIPLY'),
        array('KWD', 'Kuwaiti Dinar', '', '', 12.500000, 12.500000, 12.500000, 'MULTIPLY'),
    );
    foreach ($defaults as $r) {
        $sql = 'INSERT INTO `epc_jewel_currency`
                (`company_id`, `curr_code`, `description`, `fraction`, `symbol`, `conv_rate`, `min_conv_rate`, `max_conv_rate`, `status`)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `conv_rate` = VALUES(`conv_rate`)';
        $db->prepare($sql)->execute([$companyId, $r[0], $r[1], $r[2], $r[3], $r[4], $r[5], $r[6], $r[7]]);
    }
}

/* ────────────────────────────────────────────
 * Metal stock master helpers
 * ──────────────────────────────────────────── */

function epc_jewel_metal_stock_list(PDO $db, int $companyId, string $metal = '', int $limit = 100, int $offset = 0): array
{
    epc_jewel_ensure_schema($db);
    $where = '`company_id` = ?';
    $params = [$companyId];
    if ($metal !== '') { $where .= ' AND `metal` = ?'; $params[] = $metal; }
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_metal_stock` WHERE {$where} ORDER BY `item_code` LIMIT {$limit} OFFSET {$offset}");
    $stmt->execute($params);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_metal_stock_get(PDO $db, int $companyId, string $itemCode): ?array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_metal_stock` WHERE `company_id` = ? AND `item_code` = ? LIMIT 1');
    $stmt->execute([$companyId, $itemCode]);
    $row = $stmt->fetch(PDO::FETCH_ASSOC);
    return $row ?: null;
}

function epc_jewel_metal_stock_save(PDO $db, int $companyId, array $data): int
{
    epc_jewel_ensure_schema($db);
    $cols = array('company_id', 'metal', 'item_code', 'description', 'cc_making', 'cc_metal',
        'karat', 'purity', 'type', 'brand', 'category', 'sub_category',
        'vendor', 'vendor_ref', 'country', 'hs_code', 'mc_unit',
        'include_stone_weight', 'pass_purity_diff', 'in_pieces',
        'pc_weight_gms', 'pc_weight_oz', 'create_barcodes', 'barcode_prefix',
        'block_gross_wt_sales', 'ask_supplier', 'ask_wastage',
        'exclude_gst_trn', 'gst_trn_on_making_stone', 'allow_negative_stock',
        'allow_less_than_cost', 'conv_factor_oz', 'abc_code', 'loyalty_item',
        'std_cost', 'discount_pct', 'min_qty', 'max_qty',
        'reorder_level', 'reorder_qty', 'pur_cost_gms', 'sale_price_gms',
        'price1_code', 'price1_label', 'created_by');
    $vals = array();
    foreach ($cols as $c) {
        $vals[] = $data[$c] ?? ($c === 'company_id' ? $companyId : '');
    }
    $vals[0] = $companyId;
    $ph = implode(',', array_fill(0, count($cols), '?'));
    $colStr = '`' . implode('`, `', $cols) . '`';
    $updParts = array();
    foreach ($cols as $c) {
        if ($c === 'company_id' || $c === 'item_code') continue;
        $updParts[] = "`{$c}` = VALUES(`{$c}`)";
    }
    $sql = "INSERT INTO `epc_jewel_metal_stock` ({$colStr}) VALUES ({$ph})
            ON DUPLICATE KEY UPDATE " . implode(', ', $updParts);
    $db->prepare($sql)->execute($vals);
    return (int) $db->lastInsertId();
}

/* ────────────────────────────────────────────
 * Design master helpers
 * ──────────────────────────────────────────── */

function epc_jewel_design_list(PDO $db, int $companyId, int $limit = 100, int $offset = 0): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_design` WHERE `company_id` = ? ORDER BY `design_code` LIMIT {$limit} OFFSET {$offset}");
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_design_get(PDO $db, int $companyId, string $designCode): ?array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_design` WHERE `company_id` = ? AND `design_code` = ? LIMIT 1');
    $stmt->execute([$companyId, $designCode]);
    $row = $stmt->fetch(PDO::FETCH_ASSOC);
    if (!$row) return null;
    $stmt2 = $db->prepare('SELECT * FROM `epc_jewel_design_metals` WHERE `design_id` = ? ORDER BY `line_no`');
    $stmt2->execute([$row['id']]);
    $row['metals'] = $stmt2->fetchAll(PDO::FETCH_ASSOC) ?: array();
    $stmt3 = $db->prepare('SELECT * FROM `epc_jewel_design_stones` WHERE `design_id` = ? ORDER BY `line_no`');
    $stmt3->execute([$row['id']]);
    $row['stones'] = $stmt3->fetchAll(PDO::FETCH_ASSOC) ?: array();
    return $row;
}

/* ────────────────────────────────────────────
 * Diamond master helpers
 * ──────────────────────────────────────────── */

function epc_jewel_diamond_list(PDO $db, int $companyId, int $limit = 100, int $offset = 0): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_diamond_master` WHERE `company_id` = ? ORDER BY `item_code` LIMIT {$limit} OFFSET {$offset}");
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Voucher (sales / purchase) helpers
 * ──────────────────────────────────────────── */

function epc_jewel_voc_next_no(PDO $db, int $companyId, string $vocType, string $branch = 'HO'): int
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT MAX(`voc_no`) FROM `epc_jewel_voucher` WHERE `company_id` = ? AND `voc_type` = ? AND `branch` = ?');
    $stmt->execute([$companyId, $vocType, $branch]);
    return ((int) $stmt->fetchColumn()) + 1;
}

function epc_jewel_voc_list(PDO $db, int $companyId, string $vocType, string $from, string $to, int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_voucher` WHERE `company_id` = ? AND `voc_type` = ? AND `voc_date` BETWEEN ? AND ? ORDER BY `voc_no` DESC LIMIT {$limit}");
    $stmt->execute([$companyId, $vocType, $from, $to]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_voc_get(PDO $db, int $id): ?array
{
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_voucher` WHERE `id` = ? LIMIT 1');
    $stmt->execute([$id]);
    $row = $stmt->fetch(PDO::FETCH_ASSOC);
    if (!$row) return null;
    $stmt2 = $db->prepare('SELECT * FROM `epc_jewel_voucher_lines` WHERE `voucher_id` = ? ORDER BY `line_no`');
    $stmt2->execute([$row['id']]);
    $row['lines'] = $stmt2->fetchAll(PDO::FETCH_ASSOC) ?: array();
    $stmt3 = $db->prepare('SELECT * FROM `epc_jewel_voucher_receipts` WHERE `voucher_id` = ? ORDER BY `id`');
    $stmt3->execute([$row['id']]);
    $row['receipts'] = $stmt3->fetchAll(PDO::FETCH_ASSOC) ?: array();
    return $row;
}

/* ────────────────────────────────────────────
 * Repair helpers
 * ──────────────────────────────────────────── */

function epc_jewel_repair_next_no(PDO $db, int $companyId, string $vocType = 'REP', string $branch = 'HO'): int
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT MAX(`voc_no`) FROM `epc_jewel_repair` WHERE `company_id` = ? AND `voc_type` = ? AND `branch` = ?');
    $stmt->execute([$companyId, $vocType, $branch]);
    return ((int) $stmt->fetchColumn()) + 1;
}

function epc_jewel_repair_list(PDO $db, int $companyId, string $from, string $to, string $status = '', int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $where = '`company_id` = ? AND `voc_date` BETWEEN ? AND ?';
    $params = [$companyId, $from, $to];
    if ($status !== '') { $where .= ' AND `status` = ?'; $params[] = $status; }
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_repair` WHERE {$where} ORDER BY `voc_no` DESC LIMIT {$limit}");
    $stmt->execute($params);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jewel_repair_get(PDO $db, int $id): ?array
{
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_repair` WHERE `id` = ? LIMIT 1');
    $stmt->execute([$id]);
    $row = $stmt->fetch(PDO::FETCH_ASSOC);
    if (!$row) return null;
    $stmt2 = $db->prepare('SELECT * FROM `epc_jewel_repair_items` WHERE `repair_id` = ? ORDER BY `line_no`');
    $stmt2->execute([$row['id']]);
    $row['items'] = $stmt2->fetchAll(PDO::FETCH_ASSOC) ?: array();
    return $row;
}

function epc_jewel_repair_pending_jobs(PDO $db, int $companyId, string $division = '', string $branch = ''): array
{
    epc_jewel_ensure_schema($db);
    $where = 'r.`company_id` = ? AND r.`status` IN (?, ?)';
    $params = [$companyId, 'received', 'in_progress'];
    if ($division !== '') { $where .= ' AND ri.`division` = ?'; $params[] = $division; }
    if ($branch !== '') { $where .= ' AND r.`branch` = ?'; $params[] = $branch; }
    $sql = "SELECT r.*, ri.stock_code, ri.description AS item_desc, ri.bag_no, ri.pcs, ri.gr_wt,
            ri.repair_type, ri.repair_status
            FROM `epc_jewel_repair` r
            JOIN `epc_jewel_repair_items` ri ON ri.repair_id = r.id
            WHERE {$where}
            ORDER BY r.voc_no DESC";
    $stmt = $db->prepare($sql);
    $stmt->execute($params);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Stock verification helpers
 * ──────────────────────────────────────────── */

function epc_jewel_sv_list(PDO $db, int $companyId, int $limit = 50): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_stock_verification` WHERE `company_id` = ? ORDER BY `id` DESC LIMIT {$limit}");
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Metal stock balance report
 * ──────────────────────────────────────────── */

function epc_jewel_metal_stock_balance(PDO $db, int $companyId, string $metal = ''): array
{
    epc_jewel_ensure_schema($db);
    $where = '`company_id` = ? AND `stock_qty` > 0';
    $params = [$companyId];
    if ($metal !== '') { $where .= ' AND `metal` = ?'; $params[] = $metal; }
    $stmt = $db->prepare("SELECT `metal`, `karat`, SUM(`stock_pcs`) AS total_pcs,
            SUM(`stock_gms`) AS total_gms, SUM(`stock_value`) AS total_value
            FROM `epc_jewel_metal_stock` WHERE {$where}
            GROUP BY `metal`, `karat` ORDER BY `metal`, `karat`");
    $stmt->execute($params);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Metal sales analysis report
 * ──────────────────────────────────────────── */

function epc_jewel_sales_analysis(PDO $db, int $companyId, string $from, string $to, string $groupBy = 'date'): array
{
    epc_jewel_ensure_schema($db);
    $vocTypes = "('RIN','MSL','RSL')";
    switch ($groupBy) {
        case 'salesman':
            $grp = 'v.`salesman`'; $sel = 'v.`salesman` AS group_key'; break;
        case 'division':
            $grp = 'vl.`division`'; $sel = 'vl.`division` AS group_key'; break;
        default:
            $grp = 'v.`voc_date`'; $sel = 'v.`voc_date` AS group_key';
    }
    $sql = "SELECT {$sel}, COUNT(DISTINCT v.id) AS voc_count,
            SUM(vl.pcs) AS total_pcs, SUM(vl.gr_wt) AS total_wt,
            SUM(vl.metal_amount) AS metal_total,
            SUM(vl.mkg_amount) AS making_total,
            SUM(vl.total_amount) AS grand_total
            FROM `epc_jewel_voucher` v
            JOIN `epc_jewel_voucher_lines` vl ON vl.voucher_id = v.id
            WHERE v.`company_id` = ? AND v.`voc_type` IN {$vocTypes}
            AND v.`voc_date` BETWEEN ? AND ?
            GROUP BY {$grp} ORDER BY {$grp}";
    $stmt = $db->prepare($sql);
    $stmt->execute([$companyId, $from, $to]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Fixing helpers (purchase/sales fixing)
 * ──────────────────────────────────────────── */

function epc_jewel_fixing_list(PDO $db, int $companyId, string $fixType, int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_fixing` WHERE `company_id` = ? AND `fix_type` = ? ORDER BY `id` DESC LIMIT {$limit}");
    $stmt->execute([$companyId, $fixType]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Barcode helpers
 * ──────────────────────────────────────────── */

function epc_jewel_barcode_generate(PDO $db, int $companyId, string $stockCode, string $division, string $karat, float $grossWt, float $purity, float $tagPrice): string
{
    epc_jewel_ensure_schema($db);
    $prefix = strtoupper(substr($division, 0, 1)) . str_replace(array(' ', '.'), '', $karat);
    $stmt = $db->prepare('SELECT COUNT(*) FROM `epc_jewel_barcode` WHERE `company_id` = ?');
    $stmt->execute([$companyId]);
    $seq = ((int) $stmt->fetchColumn()) + 1;
    $barcode = $prefix . str_pad((string) $seq, 8, '0', STR_PAD_LEFT);

    $sql = 'INSERT INTO `epc_jewel_barcode`
            (`company_id`, `stock_code`, `barcode`, `item_type`, `division`, `karat`, `gross_wt`, `net_wt`, `purity`, `tag_price`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)';
    $netWt = $grossWt * $purity;
    $db->prepare($sql)->execute([$companyId, $stockCode, $barcode, 'metal', $division, $karat, $grossWt, $netWt, $purity, $tagPrice]);
    return $barcode;
}

function epc_jewel_barcode_list(PDO $db, int $companyId, int $limit = 200): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare("SELECT * FROM `epc_jewel_barcode` WHERE `company_id` = ? ORDER BY `id` DESC LIMIT {$limit}");
    $stmt->execute([$companyId]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Petty cash helpers
 * ──────────────────────────────────────────── */

function epc_jewel_petty_cash_list(PDO $db, int $companyId, string $from, string $to): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_petty_cash` WHERE `company_id` = ? AND `voc_date` BETWEEN ? AND ? ORDER BY `voc_no` DESC');
    $stmt->execute([$companyId, $from, $to]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Tourist VAT refund helpers
 * ──────────────────────────────────────────── */

function epc_jewel_tourist_vat_list(PDO $db, int $companyId, string $from, string $to): array
{
    epc_jewel_ensure_schema($db);
    $stmt = $db->prepare('SELECT * FROM `epc_jewel_tourist_vat_refund` WHERE `company_id` = ? AND `voc_date` BETWEEN ? AND ? ORDER BY `voc_no` DESC');
    $stmt->execute([$companyId, $from, $to]);
    return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ────────────────────────────────────────────
 * Gold valuation helper
 * ──────────────────────────────────────────── */

function epc_jewel_gold_valuation(float $grossWt, float $purity, float $ratePerGram, float $makingRate = 0, float $stoneAmount = 0): array
{
    $netWt = $grossWt * $purity;
    $metalValue = $netWt * $ratePerGram;
    $makingValue = $grossWt * $makingRate;
    $total = $metalValue + $makingValue + $stoneAmount;
    return array(
        'gross_wt' => $grossWt,
        'purity' => $purity,
        'net_wt' => round($netWt, 4),
        'rate_per_gram' => $ratePerGram,
        'metal_value' => round($metalValue, 2),
        'making_rate' => $makingRate,
        'making_value' => round($makingValue, 2),
        'stone_amount' => round($stoneAmount, 2),
        'total' => round($total, 2),
    );
}

/**
 * Central AJAX handler for all jw_* actions.
 */
function epc_jewel_handle_ajax(PDO $db, $action, array $post, $companyId)
{
    $cid = (int) $companyId;
    switch ($action) {
        /* master data — existing functions use (PDO, companyId, data) signature */
        case 'jw_karat_save':
            $id = epc_jewel_karat_save($db, $cid, $post);
            return array('ok' => true, 'message' => 'Karat saved', 'id' => $id);
        case 'jw_karat_seed':
            epc_jewel_karat_seed($db, $cid);
            return array('ok' => true, 'message' => 'Karat defaults seeded');
        case 'jw_rate_type_save':
            $id = epc_jewel_rate_type_save($db, $cid, $post);
            return array('ok' => true, 'message' => 'Rate type saved', 'id' => $id);
        case 'jw_rate_type_seed':
            epc_jewel_rate_type_seed($db, $cid);
            return array('ok' => true, 'message' => 'Rate types seeded');
        case 'jw_currency_save':
            return epc_jewel_currency_save_ajax($db, $post, $cid);
        case 'jw_currency_seed':
            epc_jewel_currency_seed($db, $cid);
            return array('ok' => true, 'message' => 'Currencies seeded');
        case 'jw_metal_stock_save':
            $id = epc_jewel_metal_stock_save($db, $cid, $post);
            return array('ok' => true, 'message' => 'Metal stock saved', 'id' => $id);
        case 'jw_diamond_save':
            return epc_jewel_diamond_save_ajax($db, $post, $cid);
        case 'jw_design_save':
            return epc_jewel_design_save_ajax($db, $post, $cid);
        case 'jw_pearl_save':
            return epc_jewel_pearl_save_ajax($db, $post, $cid);
        case 'jw_color_stone_save':
            return epc_jewel_color_stone_save($db, $post, $cid);
        /* vouchers / transactions */
        case 'jw_voucher_save':
            return epc_jewel_voucher_save($db, $post, $cid);
        case 'jw_fixing_save':
            return epc_jewel_fixing_save($db, $post, $cid);
        case 'jw_repair_save':
            return epc_jewel_repair_save($db, $post, $cid);
        case 'jw_repair_transfer_save':
            return epc_jewel_repair_transfer_save($db, $post, $cid);
        case 'jw_workshop_receive_save':
            return epc_jewel_workshop_receive_save($db, $post, $cid);
        case 'jw_repair_delivery_save':
            return epc_jewel_repair_delivery_save($db, $post, $cid);
        case 'jw_stock_verification_save':
            return epc_jewel_stock_verify_save($db, $post, $cid);
        case 'jw_petty_cash_save':
            return epc_jewel_petty_cash_save($db, $post, $cid);
        case 'jw_tourist_vat_save':
            return epc_jewel_tourist_vat_save($db, $post, $cid);
        /* purchase/sale voucher aliases — all route to generic voucher save */
        case 'jw_metal_purchase_save':
        case 'jw_diamond_purchase_save':
        case 'jw_retail_sale_save':
        case 'jw_metal_sale_save':
        case 'jw_sales_return_save':
        case 'jw_pos_advance_save':
            return epc_jewel_voucher_save($db, $post, $cid);
        case 'jw_purchase_fixing_save':
        case 'jw_sales_fixing_save':
            return epc_jewel_fixing_save($db, $post, $cid);
        case 'jw_repair_receipt_save':
            return epc_jewel_repair_save($db, $post, $cid);
        case 'jw_journal_voucher_save':
            return epc_jewel_voucher_save($db, $post, $cid);
        default:
            return array('ok' => false, 'message' => 'Unknown jewellery action: ' . $action);
    }
}

/* --- save helpers (colour stone, voucher, fixing, repair, etc.) --- */

function epc_jewel_color_stone_save(PDO $db, array $p, $companyId)
{
    $code = trim($p['item_code'] ?? '');
    if ($code === '') return array('ok' => false, 'message' => 'Item code required');
    $db->prepare("INSERT INTO epc_jewel_color_stone_master
        (company_id, item_code, description, stone_type, shape, clarity, size_mm, weight_ct, pcs, color_grade, treatment, origin, certificate_no, vendor, cost_per_ct, sell_per_ct, cost_centre)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        ON DUPLICATE KEY UPDATE description=VALUES(description), stone_type=VALUES(stone_type), shape=VALUES(shape),
        clarity=VALUES(clarity), size_mm=VALUES(size_mm), weight_ct=VALUES(weight_ct), pcs=VALUES(pcs)"
    )->execute(array(
        $companyId, $code, $p['description'] ?? '', $p['stone_type'] ?? 'Ruby', $p['shape'] ?? 'Round',
        $p['clarity'] ?? 'VS', (float)($p['size_mm'] ?? 0), (float)($p['weight_ct'] ?? 0), (int)($p['pcs'] ?? 1),
        $p['color_grade'] ?? '', $p['treatment'] ?? 'None', $p['origin'] ?? '', $p['certificate_no'] ?? '',
        $p['vendor'] ?? '', (float)($p['cost_per_ct'] ?? 0), (float)($p['sell_per_ct'] ?? 0), $p['cost_centre'] ?? 'CSTN',
    ));
    return array('ok' => true, 'message' => 'Colour stone saved');
}

function epc_jewel_voucher_save(PDO $db, array $p, $companyId)
{
    $vocType = trim($p['voc_type'] ?? '');
    if ($vocType === '') return array('ok' => false, 'message' => 'Voucher type required');
    $db->prepare("INSERT INTO epc_jewel_voucher
        (company_id, voc_type, branch, voc_date, party_code, party_name, currency, currency_rate,
         salesman, ref_invoice_no, credit_days, narration, net_amount, vat_amount, round_off, gross_total)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $vocType, $p['branch'] ?? 'HO', $p['voc_date'] ?? date('Y-m-d'),
        $p['party_code'] ?? '', $p['party_name'] ?? '', $p['currency'] ?? 'AED',
        (float)($p['currency_rate'] ?? 1), $p['salesman'] ?? '', $p['ref_invoice_no'] ?? '',
        (int)($p['credit_days'] ?? 0), $p['narration'] ?? '',
        (float)($p['net_amount'] ?? 0), (float)($p['vat_amount'] ?? 0),
        (float)($p['round_off'] ?? 0), (float)($p['gross_total'] ?? 0),
    ));
    $vocId = (int)$db->lastInsertId();
    return array('ok' => true, 'message' => $vocType . ' voucher saved', 'id' => $vocId);
}

function epc_jewel_fixing_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_fixing
        (company_id, fix_direction, branch, fix_date, party_code, party_name, metal, karat, rate_type,
         fixing_wt, fixing_rate, fixing_amount, ref_voucher, currency, narration)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['fix_direction'] ?? 'purchase', $p['branch'] ?? 'HO', $p['fix_date'] ?? date('Y-m-d'),
        $p['party_code'] ?? '', $p['party_name'] ?? '', $p['metal'] ?? 'G', $p['karat'] ?? '24',
        $p['rate_type'] ?? 'GMS', (float)($p['fixing_wt'] ?? 0), (float)($p['fixing_rate'] ?? 0),
        (float)($p['fixing_amount'] ?? 0), $p['ref_voucher'] ?? '', $p['currency'] ?? 'AED', $p['narration'] ?? '',
    ));
    return array('ok' => true, 'message' => 'Fixing saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_repair_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_repair
        (company_id, branch, receipt_date, customer_code, customer_name, mobile, salesman,
         promise_date, priority, total_est_cost, advance_amt, narration, status)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['branch'] ?? 'HO', $p['receipt_date'] ?? date('Y-m-d'),
        $p['customer_code'] ?? '', $p['customer_name'] ?? '', $p['mobile'] ?? '',
        $p['salesman'] ?? '', $p['promise_date'] ?? null, $p['priority'] ?? 'Normal',
        (float)($p['total_est_cost'] ?? 0), (float)($p['advance_amt'] ?? 0),
        $p['narration'] ?? '', 'Received',
    ));
    return array('ok' => true, 'message' => 'Repair receipt saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_repair_transfer_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_repair_transfer
        (company_id, from_branch, to_branch, transfer_date, workshop_contact, expected_return, narration)
        VALUES (?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['from_branch'] ?? 'HO', $p['to_branch'] ?? 'WS1',
        $p['transfer_date'] ?? date('Y-m-d'), $p['workshop_contact'] ?? '',
        $p['expected_return'] ?? null, $p['narration'] ?? '',
    ));
    return array('ok' => true, 'message' => 'Repair transfer saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_workshop_receive_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_repair_workshop_receive
        (company_id, branch, receive_date, transfer_ref, from_workshop, narration)
        VALUES (?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['branch'] ?? 'HO', $p['receive_date'] ?? date('Y-m-d'),
        $p['transfer_ref'] ?? '', $p['from_workshop'] ?? '', $p['narration'] ?? '',
    ));
    return array('ok' => true, 'message' => 'Workshop receive saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_repair_delivery_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_repair_delivery
        (company_id, branch, delivery_date, repair_no, customer_code, customer_name,
         total_charge, advance_paid, balance_due, pay_mode, amount_paid)
        VALUES (?,?,?,?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['branch'] ?? 'HO', $p['delivery_date'] ?? date('Y-m-d'),
        $p['repair_no'] ?? '', $p['customer_code'] ?? '', $p['customer_name'] ?? '',
        (float)($p['total_charge'] ?? 0), (float)($p['advance_paid'] ?? 0),
        (float)($p['balance_due'] ?? 0), $p['pay_mode'] ?? 'Cash', (float)($p['amount_paid'] ?? 0),
    ));
    return array('ok' => true, 'message' => 'Repair delivery saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_stock_verify_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_stock_verification
        (company_id, branch, count_date, division, counter, supervisor, narration, status)
        VALUES (?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['branch'] ?? 'HO', $p['count_date'] ?? date('Y-m-d'),
        $p['division'] ?? '', $p['counter'] ?? '', $p['supervisor'] ?? '',
        $p['narration'] ?? '', 'Draft',
    ));
    return array('ok' => true, 'message' => 'Stock verification saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_petty_cash_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_voucher
        (company_id, voc_type, branch, voc_date, party_code, party_name, narration, gross_total)
        VALUES (?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, 'PCV', $p['branch'] ?? 'HO', $p['voc_date'] ?? date('Y-m-d'),
        $p['cash_account'] ?? 'PETTY-CASH', $p['pay_to'] ?? '', $p['narration'] ?? '',
        (float)($p['total'] ?? 0),
    ));
    return array('ok' => true, 'message' => 'Petty cash voucher saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_tourist_vat_save(PDO $db, array $p, $companyId)
{
    $db->prepare("INSERT INTO epc_jewel_tourist_vat_refund
        (company_id, refund_date, tourist_name, passport_no, nationality, flight_no, departure_date,
         total_vat, total_refund, narration, status)
        VALUES (?,?,?,?,?,?,?,?,?,?,?)"
    )->execute(array(
        $companyId, $p['refund_date'] ?? date('Y-m-d'), $p['tourist_name'] ?? '',
        $p['passport_no'] ?? '', $p['nationality'] ?? '', $p['flight_no'] ?? '',
        $p['departure_date'] ?? null, (float)($p['total_vat'] ?? 0), (float)($p['total_refund'] ?? 0),
        $p['narration'] ?? '', 'Pending',
    ));
    return array('ok' => true, 'message' => 'Tourist VAT refund saved', 'id' => (int)$db->lastInsertId());
}

function epc_jewel_currency_save_ajax(PDO $db, array $p, int $companyId)
{
    $code = strtoupper(trim($p['curr_code'] ?? ''));
    if ($code === '') return array('ok' => false, 'message' => 'Currency code required');
    epc_jewel_ensure_schema($db);
    $db->prepare("INSERT INTO epc_jewel_currency
        (company_id, curr_code, description, fraction, symbol, conv_rate, min_conv_rate, max_conv_rate, status)
        VALUES (?,?,?,?,?,?,?,?,?)
        ON DUPLICATE KEY UPDATE description=VALUES(description), fraction=VALUES(fraction),
        symbol=VALUES(symbol), conv_rate=VALUES(conv_rate), status=VALUES(status)"
    )->execute(array(
        $companyId, $code, $p['description'] ?? '', $p['fraction'] ?? '', $p['symbol'] ?? '',
        (float)($p['conv_rate'] ?? 1), (float)($p['min_conv_rate'] ?? 1), (float)($p['max_conv_rate'] ?? 1),
        $p['status'] ?? 'MULTIPLY',
    ));
    return array('ok' => true, 'message' => 'Currency saved');
}

function epc_jewel_diamond_save_ajax(PDO $db, array $p, int $companyId)
{
    $code = trim($p['item_code'] ?? '');
    if ($code === '') return array('ok' => false, 'message' => 'Item code required');
    epc_jewel_ensure_schema($db);
    $db->prepare("INSERT INTO epc_jewel_diamond_master
        (company_id, item_code, description, design, rfid, category, sub_category, type, brand,
         color, clarity, fluorescence, style, set_ref, country, vendor, vendor_ref,
         currency, currency_rate, cost_centre, cost_amount, item_gr_wt,
         price1_code, price1_pct, price1_fc, price1_lc, promotional)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        ON DUPLICATE KEY UPDATE description=VALUES(description), design=VALUES(design),
        category=VALUES(category), color=VALUES(color), clarity=VALUES(clarity),
        cost_amount=VALUES(cost_amount), item_gr_wt=VALUES(item_gr_wt)"
    )->execute(array(
        $companyId, $code, $p['description'] ?? '', $p['design'] ?? '', $p['rfid'] ?? '',
        $p['category'] ?? '', $p['sub_category'] ?? '', $p['type'] ?? '', $p['brand'] ?? '',
        $p['color'] ?? '', $p['clarity'] ?? '', $p['fluorescence'] ?? '', $p['style'] ?? '',
        $p['set_ref'] ?? '', $p['country'] ?? '', $p['vendor'] ?? '', $p['vendor_ref'] ?? '',
        $p['currency'] ?? 'AED', (float)($p['currency_rate'] ?? 1), $p['cost_centre'] ?? '',
        (float)($p['cost_amount'] ?? 0), (float)($p['item_gr_wt'] ?? 0),
        $p['price1_code'] ?? 'TAG', (float)($p['price1_pct'] ?? 0),
        (float)($p['price1_fc'] ?? 0), (float)($p['price1_lc'] ?? 0),
        (int)($p['promotional'] ?? 0),
    ));
    return array('ok' => true, 'message' => 'Diamond item saved');
}

function epc_jewel_design_save_ajax(PDO $db, array $p, int $companyId)
{
    $code = trim($p['design_code'] ?? '');
    if ($code === '') return array('ok' => false, 'message' => 'Design code required');
    epc_jewel_ensure_schema($db);
    $db->prepare("INSERT INTO epc_jewel_design
        (company_id, design_code, description, currency, currency_rate, cost_centre,
         category, sub_category, type, brand, color, country, vendor, vendor_ref,
         cost_amount, price1_code, price1_pct, price1_fc, price1_lc)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        ON DUPLICATE KEY UPDATE description=VALUES(description), category=VALUES(category),
        type=VALUES(type), cost_amount=VALUES(cost_amount)"
    )->execute(array(
        $companyId, $code, $p['description'] ?? '', $p['currency'] ?? 'AED',
        (float)($p['currency_rate'] ?? 1), $p['cost_centre'] ?? '',
        $p['category'] ?? '', $p['sub_category'] ?? '', $p['type'] ?? '',
        $p['brand'] ?? '', $p['color'] ?? '', $p['country'] ?? '',
        $p['vendor'] ?? '', $p['vendor_ref'] ?? '',
        (float)($p['cost_amount'] ?? 0), $p['price1_code'] ?? 'GEN',
        (float)($p['price1_pct'] ?? 0), (float)($p['price1_fc'] ?? 0), (float)($p['price1_lc'] ?? 0),
    ));
    return array('ok' => true, 'message' => 'Design saved');
}

function epc_jewel_pearl_save_ajax(PDO $db, array $p, int $companyId)
{
    $code = trim($p['code'] ?? '');
    if ($code === '') return array('ok' => false, 'message' => 'Pearl code required');
    epc_jewel_ensure_schema($db);
    $db->prepare("INSERT INTO epc_jewel_pearl_master
        (company_id, code, nature, description, design, type, cost_centre,
         category, color, vendor, vendor_ref, luster, shape, size,
         brand, country, grade, sub_category, currency, currency_rate,
         cost_amount, price1_code, price1_pct, price1_fc, price1_lc)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        ON DUPLICATE KEY UPDATE description=VALUES(description), nature=VALUES(nature),
        type=VALUES(type), cost_amount=VALUES(cost_amount)"
    )->execute(array(
        $companyId, $code, $p['nature'] ?? 'Cultured', $p['description'] ?? '',
        $p['design'] ?? '', $p['type'] ?? '', $p['cost_centre'] ?? '',
        $p['category'] ?? '', $p['color'] ?? '', $p['vendor'] ?? '',
        $p['vendor_ref'] ?? '', $p['luster'] ?? '', $p['shape'] ?? '',
        $p['size'] ?? '', $p['brand'] ?? '', $p['country'] ?? '',
        $p['grade'] ?? '', $p['sub_category'] ?? '', $p['currency'] ?? 'AED',
        (float)($p['currency_rate'] ?? 1), (float)($p['cost_amount'] ?? 0),
        $p['price1_code'] ?? 'GEN', (float)($p['price1_pct'] ?? 0),
        (float)($p['price1_fc'] ?? 0), (float)($p['price1_lc'] ?? 0),
    ));
    return array('ok' => true, 'message' => 'Pearl saved');
}

function epc_jewel_purchase_list(PDO $db, int $companyId, string $type = 'METAL', int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $sql = 'SELECT * FROM `jw_vouchers` WHERE `company_id` = ? AND `voc_type` IN (?,?) ORDER BY `voc_date` DESC LIMIT ' . (int)$limit;
    $codes = ($type === 'DIAMOND') ? ['DMP','DLP'] : ['MMP','MLP'];
    $st = $db->prepare($sql);
    $st->execute([$companyId, $codes[0], $codes[1]]);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
}

function epc_jewel_sale_list(PDO $db, int $companyId, string $type = 'RETAIL', int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $map = [
        'RETAIL' => ['RSI','RSC'],
        'METAL'  => ['MSI','MSC'],
        'RETURN' => ['SRN','SRC'],
    ];
    $codes = $map[$type] ?? ['RSI','RSC'];
    $sql = 'SELECT * FROM `jw_vouchers` WHERE `company_id` = ? AND `voc_type` IN (?,?) ORDER BY `voc_date` DESC LIMIT ' . (int)$limit;
    $st = $db->prepare($sql);
    $st->execute([$companyId, $codes[0], $codes[1]]);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
}

function epc_jewel_advance_list(PDO $db, int $companyId, int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $sql = 'SELECT * FROM `jw_vouchers` WHERE `company_id` = ? AND `voc_type` IN (?,?) ORDER BY `voc_date` DESC LIMIT ' . (int)$limit;
    $st = $db->prepare($sql);
    $st->execute([$companyId, 'PAD', 'PAR']);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
}

function epc_jewel_color_stone_list(PDO $db, int $companyId, int $limit = 200): array
{
    epc_jewel_ensure_schema($db);
    $sql = 'SELECT * FROM `jw_color_stones` WHERE `company_id` = ? ORDER BY `stone_code` ASC LIMIT ' . (int)$limit;
    $st = $db->prepare($sql);
    $st->execute([$companyId]);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
}

function epc_jewel_pearl_list(PDO $db, int $companyId, int $limit = 200): array
{
    epc_jewel_ensure_schema($db);
    $sql = 'SELECT * FROM `jw_pearls` WHERE `company_id` = ? ORDER BY `pearl_code` ASC LIMIT ' . (int)$limit;
    $st = $db->prepare($sql);
    $st->execute([$companyId]);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
}

function epc_jewel_journal_list(PDO $db, int $companyId, int $limit = 100): array
{
    epc_jewel_ensure_schema($db);
    $sql = 'SELECT * FROM `jw_vouchers` WHERE `company_id` = ? AND `voc_type` IN (?,?) ORDER BY `voc_date` DESC LIMIT ' . (int)$limit;
    $st = $db->prepare($sql);
    $st->execute([$companyId, 'JVG', 'JVA']);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
}

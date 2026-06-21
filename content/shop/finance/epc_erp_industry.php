<?php
/**
 * Advanced ERP — industry-agnostic product / inventory foundation.
 *
 * Lets a tenant in ANY industry get a fit-for-purpose product & inventory
 * structure with one click. Each industry blueprint maps to:
 *   - a default inventory item type (standard / perishable / serialized)
 *   - sensible default unit of measure + an allowed unit list
 *   - a set of recommended custom field definitions (seeded into
 *     `epc_erp_inv_field_defs`)
 *   - a tax category hint that pairs with the worldwide tax toolkit
 *
 * Industry KEYS are aligned with the existing pricing/AI taxonomy
 * (epc_apai_industry_profiles) where they overlap, and extended to cover more
 * sectors so the ERP truly works "for all industries in the world".
 *
 * Safe for live tenants: applying an industry only ADDS custom field defs
 * (ON DUPLICATE KEY UPDATE) and writes settings; it never deletes existing
 * fields, items, or stock.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_advanced.php';
require_once __DIR__ . '/epc_erp_inventory.php';

if (!function_exists('epc_erp_industry_catalog')) {
    /**
     * Master catalog of supported industries and their ERP inventory blueprint.
     *
     * field_type is constrained to what epc_erp_inv_field_defs supports:
     * text | number | date | select. `options` (for select) is stored as
     * options_json.
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_erp_industry_catalog(): array
    {
        return array(
            'general' => array(
                'label' => 'General retail / trading',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'box', 'set', 'kg', 'm'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'barcode', 'label' => 'Barcode / UPC', 'field_type' => 'text'),
                    array('field_key' => 'brand', 'label' => 'Brand', 'field_type' => 'text'),
                ),
                'notes' => 'Balanced default that suits most resellers and trading businesses.',
            ),
            'auto_parts' => array(
                'label' => 'Auto parts & accessories',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'set', 'pair', 'litre', 'box'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'oem_number', 'label' => 'OEM / article number', 'field_type' => 'text'),
                    array('field_key' => 'brand', 'label' => 'Brand / manufacturer', 'field_type' => 'text'),
                    array('field_key' => 'fits_make', 'label' => 'Fits make', 'field_type' => 'text'),
                    array('field_key' => 'fits_model', 'label' => 'Fits model', 'field_type' => 'text'),
                    array('field_key' => 'fits_year', 'label' => 'Fits year range', 'field_type' => 'text'),
                    array('field_key' => 'side_position', 'label' => 'Position', 'field_type' => 'select', 'options' => array('Front', 'Rear', 'Left', 'Right', 'Upper', 'Lower', 'N/A')),
                ),
                'notes' => 'Brand + article (cross-reference) driven, matching the spare-parts workflow.',
            ),
            'electronics' => array(
                'label' => 'Consumer electronics',
                'item_type' => 'serialized',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'box', 'set'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'brand', 'label' => 'Brand', 'field_type' => 'text'),
                    array('field_key' => 'model_no', 'label' => 'Model number', 'field_type' => 'text'),
                    array('field_key' => 'warranty_months', 'label' => 'Warranty (months)', 'field_type' => 'number'),
                    array('field_key' => 'imei_serial', 'label' => 'IMEI / serial', 'field_type' => 'text'),
                    array('field_key' => 'voltage', 'label' => 'Voltage / power', 'field_type' => 'text'),
                ),
                'notes' => 'Serialized tracking with warranty for phones, laptops, appliances.',
            ),
            'fashion' => array(
                'label' => 'Fashion & apparel',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'pair', 'set'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'brand', 'label' => 'Brand', 'field_type' => 'text'),
                    array('field_key' => 'size', 'label' => 'Size', 'field_type' => 'select', 'options' => array('XS', 'S', 'M', 'L', 'XL', 'XXL', '3XL')),
                    array('field_key' => 'colour', 'label' => 'Colour', 'field_type' => 'text'),
                    array('field_key' => 'material', 'label' => 'Material / fabric', 'field_type' => 'text'),
                    array('field_key' => 'gender', 'label' => 'Gender', 'field_type' => 'select', 'options' => array('Men', 'Women', 'Unisex', 'Kids')),
                    array('field_key' => 'season', 'label' => 'Season', 'field_type' => 'text'),
                ),
                'notes' => 'Size / colour / material variants for clothing and footwear.',
            ),
            'jewellery' => array(
                'label' => 'Jewellery & watches',
                'item_type' => 'serialized',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'gram', 'carat', 'pair'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'metal', 'label' => 'Metal', 'field_type' => 'select', 'options' => array('Gold', 'Silver', 'Platinum', 'White Gold', 'Rose Gold', 'Steel')),
                    array('field_key' => 'purity', 'label' => 'Purity / karat', 'field_type' => 'text'),
                    array('field_key' => 'gross_weight', 'label' => 'Gross weight (g)', 'field_type' => 'number'),
                    array('field_key' => 'stone_type', 'label' => 'Stone type', 'field_type' => 'text'),
                    array('field_key' => 'stone_carat', 'label' => 'Stone carat', 'field_type' => 'number'),
                    array('field_key' => 'hallmark', 'label' => 'Hallmark / cert no.', 'field_type' => 'text'),
                ),
                'notes' => 'Weight, purity and certificate tracking with per-piece serialization.',
            ),
            'food_perishable' => array(
                'label' => 'Food, grocery & perishables',
                'item_type' => 'perishable',
                'track_expiry' => 1,
                'default_unit' => 'kg',
                'units' => array('kg', 'gram', 'litre', 'ml', 'pcs', 'pack', 'box'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'brand', 'label' => 'Brand', 'field_type' => 'text'),
                    array('field_key' => 'batch_no', 'label' => 'Batch / lot number', 'field_type' => 'text'),
                    array('field_key' => 'expiry_date', 'label' => 'Expiry / best-before', 'field_type' => 'date'),
                    array('field_key' => 'storage_temp', 'label' => 'Storage temperature', 'field_type' => 'text'),
                    array('field_key' => 'is_halal', 'label' => 'Halal certified', 'field_type' => 'select', 'options' => array('Yes', 'No')),
                ),
                'notes' => 'Batch + expiry (FEFO) tracking for grocery, bakery and FMCG.',
            ),
            'pharma' => array(
                'label' => 'Pharmacy & medical supplies',
                'item_type' => 'perishable',
                'track_expiry' => 1,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'box', 'strip', 'bottle', 'ml', 'gram'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'generic_name', 'label' => 'Generic name', 'field_type' => 'text'),
                    array('field_key' => 'batch_no', 'label' => 'Batch / lot number', 'field_type' => 'text'),
                    array('field_key' => 'expiry_date', 'label' => 'Expiry date', 'field_type' => 'date'),
                    array('field_key' => 'dosage', 'label' => 'Strength / dosage', 'field_type' => 'text'),
                    array('field_key' => 'reg_no', 'label' => 'Drug registration no.', 'field_type' => 'text'),
                    array('field_key' => 'rx_required', 'label' => 'Prescription required', 'field_type' => 'select', 'options' => array('Yes', 'No')),
                ),
                'notes' => 'Regulatory + batch/expiry control for pharmacies and clinics.',
            ),
            'cosmetics_beauty' => array(
                'label' => 'Cosmetics & beauty',
                'item_type' => 'perishable',
                'track_expiry' => 1,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'ml', 'gram', 'box', 'set'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'brand', 'label' => 'Brand', 'field_type' => 'text'),
                    array('field_key' => 'shade', 'label' => 'Shade / variant', 'field_type' => 'text'),
                    array('field_key' => 'volume', 'label' => 'Volume / size', 'field_type' => 'text'),
                    array('field_key' => 'expiry_date', 'label' => 'Expiry / PAO', 'field_type' => 'date'),
                    array('field_key' => 'skin_type', 'label' => 'Skin / hair type', 'field_type' => 'text'),
                ),
                'notes' => 'Shade / volume variants with shelf-life tracking.',
            ),
            'furniture_home' => array(
                'label' => 'Furniture & home',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'set', 'm', 'sqm'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'brand', 'label' => 'Brand', 'field_type' => 'text'),
                    array('field_key' => 'material', 'label' => 'Material', 'field_type' => 'text'),
                    array('field_key' => 'dimensions', 'label' => 'Dimensions (WxDxH)', 'field_type' => 'text'),
                    array('field_key' => 'colour', 'label' => 'Colour / finish', 'field_type' => 'text'),
                    array('field_key' => 'assembly', 'label' => 'Assembly required', 'field_type' => 'select', 'options' => array('Yes', 'No')),
                ),
                'notes' => 'Dimension and material driven, bulky-goods friendly.',
            ),
            'building_construction' => array(
                'label' => 'Building materials & hardware',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'kg', 'ton', 'bag', 'm', 'sqm', 'cum', 'litre'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'grade', 'label' => 'Grade / spec', 'field_type' => 'text'),
                    array('field_key' => 'dimensions', 'label' => 'Size / dimensions', 'field_type' => 'text'),
                    array('field_key' => 'material', 'label' => 'Material', 'field_type' => 'text'),
                    array('field_key' => 'standard', 'label' => 'Standard (ASTM/EN/BS)', 'field_type' => 'text'),
                ),
                'notes' => 'Bulk units (ton, bag, cubic metre) for hardware and construction.',
            ),
            'industrial_manufacturing' => array(
                'label' => 'Manufacturing & industrial',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'kg', 'm', 'litre', 'roll', 'set', 'box'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'part_no', 'label' => 'Part number', 'field_type' => 'text'),
                    array('field_key' => 'material_grade', 'label' => 'Material grade', 'field_type' => 'text'),
                    array('field_key' => 'tolerance', 'label' => 'Tolerance / spec', 'field_type' => 'text'),
                    array('field_key' => 'item_role', 'label' => 'Item role', 'field_type' => 'select', 'options' => array('Raw material', 'Component', 'WIP', 'Finished good', 'Consumable')),
                ),
                'notes' => 'Raw-material / component / finished-good classification for BOM-style ops.',
            ),
            'agriculture' => array(
                'label' => 'Agriculture & farming',
                'item_type' => 'perishable',
                'track_expiry' => 1,
                'default_unit' => 'kg',
                'units' => array('kg', 'ton', 'bag', 'litre', 'pcs', 'crate'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'variety', 'label' => 'Variety / breed', 'field_type' => 'text'),
                    array('field_key' => 'grade', 'label' => 'Grade / quality', 'field_type' => 'text'),
                    array('field_key' => 'harvest_date', 'label' => 'Harvest / pack date', 'field_type' => 'date'),
                    array('field_key' => 'origin', 'label' => 'Origin / farm', 'field_type' => 'text'),
                ),
                'notes' => 'Grade and harvest-date tracking for produce and inputs.',
            ),
            'books_media' => array(
                'label' => 'Books, stationery & media',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'set', 'pack', 'box'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'isbn', 'label' => 'ISBN / barcode', 'field_type' => 'text'),
                    array('field_key' => 'author', 'label' => 'Author / publisher', 'field_type' => 'text'),
                    array('field_key' => 'language', 'label' => 'Language', 'field_type' => 'text'),
                    array('field_key' => 'format', 'label' => 'Format', 'field_type' => 'select', 'options' => array('Paperback', 'Hardcover', 'Digital', 'Audio')),
                ),
                'notes' => 'ISBN / title driven catalogue for books and stationery.',
            ),
            'hospitality_fnb' => array(
                'label' => 'Restaurant & hospitality',
                'item_type' => 'perishable',
                'track_expiry' => 1,
                'default_unit' => 'pcs',
                'units' => array('pcs', 'plate', 'kg', 'gram', 'litre', 'ml', 'portion'),
                'tax_category' => 'goods',
                'fields' => array(
                    array('field_key' => 'category', 'label' => 'Menu category', 'field_type' => 'text'),
                    array('field_key' => 'is_veg', 'label' => 'Vegetarian', 'field_type' => 'select', 'options' => array('Veg', 'Non-veg', 'Vegan')),
                    array('field_key' => 'allergens', 'label' => 'Allergens', 'field_type' => 'text'),
                    array('field_key' => 'expiry_date', 'label' => 'Prep / expiry', 'field_type' => 'date'),
                ),
                'notes' => 'Menu items + raw ingredients with allergen and prep tracking.',
            ),
            'services_professional' => array(
                'label' => 'Professional / advisory services',
                'item_type' => 'standard',
                'track_expiry' => 0,
                'default_unit' => 'hour',
                'units' => array('hour', 'day', 'project', 'retainer', 'unit'),
                'tax_category' => 'services',
                'fields' => array(
                    array('field_key' => 'service_code', 'label' => 'Service code', 'field_type' => 'text'),
                    array('field_key' => 'billing_basis', 'label' => 'Billing basis', 'field_type' => 'select', 'options' => array('Hourly', 'Fixed', 'Retainer', 'Milestone')),
                    array('field_key' => 'sla', 'label' => 'SLA / turnaround', 'field_type' => 'text'),
                ),
                'notes' => 'Time / project billing for consulting, tax advisory, agencies.',
            ),
        );
    }
}

if (!function_exists('epc_erp_industry_keys')) {
    /** @return array<int,string> */
    function epc_erp_industry_keys(): array
    {
        return array_keys(epc_erp_industry_catalog());
    }
}

if (!function_exists('epc_erp_industry_blueprint')) {
    /** @return array<string,mixed>|null */
    function epc_erp_industry_blueprint(string $key): ?array
    {
        $cat = epc_erp_industry_catalog();
        return $cat[$key] ?? null;
    }
}

if (!function_exists('epc_erp_industry_ensure_schema')) {
    function epc_erp_industry_ensure_schema(PDO $db): void
    {
        epc_erp_adv_settings_ensure($db);
        $db->exec(
            "CREATE TABLE IF NOT EXISTS `epc_erp_industry_state` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `industry_key` varchar(64) NOT NULL,
                `item_type` varchar(32) NOT NULL DEFAULT 'standard',
                `default_unit` varchar(32) NOT NULL DEFAULT 'pcs',
                `fields_seeded` int(11) NOT NULL DEFAULT 0,
                `applied_by` int(11) NOT NULL DEFAULT 0,
                `time_applied` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `x_industry` (`industry_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8"
        );
    }
}

if (!function_exists('epc_erp_industry_current')) {
    /** @return array<string,mixed> */
    function epc_erp_industry_current(PDO $db): array
    {
        $key = epc_erp_adv_get_setting($db, 'erp_industry_profile', '');
        $bp = $key !== '' ? epc_erp_industry_blueprint($key) : null;
        return array(
            'key' => $key,
            'label' => $bp['label'] ?? '',
            'item_type' => epc_erp_adv_get_setting($db, 'erp_default_item_type', $bp['item_type'] ?? 'standard'),
            'default_unit' => epc_erp_adv_get_setting($db, 'erp_default_unit', $bp['default_unit'] ?? 'pcs'),
            'track_expiry' => epc_erp_adv_get_setting($db, 'erp_track_expiry', (string) ($bp['track_expiry'] ?? '0')),
        );
    }
}

if (!function_exists('epc_erp_industry_apply')) {
    /**
     * Apply an industry blueprint: seed its custom field definitions and set
     * the ERP defaults. Additive and idempotent.
     *
     * @return array<string,mixed>
     */
    function epc_erp_industry_apply(PDO $db, string $key, int $adminId = 0): array
    {
        $bp = epc_erp_industry_blueprint($key);
        if ($bp === null) {
            return array('status' => false, 'message' => 'Unknown industry: ' . $key, 'fields_seeded' => 0);
        }

        epc_erp_inventory_ensure_schema($db);
        epc_erp_industry_ensure_schema($db);

        $ins = $db->prepare(
            'INSERT INTO `epc_erp_inv_field_defs` (`field_key`, `label`, `field_type`, `options_json`, `sort_order`, `active`)
             VALUES (?, ?, ?, ?, ?, 1)
             ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `field_type` = VALUES(`field_type`),
                `options_json` = VALUES(`options_json`), `active` = 1'
        );

        $seeded = 0;
        $sort = 0;
        foreach ($bp['fields'] as $f) {
            $sort += 10;
            $type = in_array($f['field_type'], array('text', 'number', 'date', 'select'), true) ? $f['field_type'] : 'text';
            $optionsJson = null;
            if ($type === 'select' && !empty($f['options']) && is_array($f['options'])) {
                $optionsJson = json_encode(array_values($f['options']));
            }
            $ins->execute(array(
                substr((string) $f['field_key'], 0, 32),
                substr((string) $f['label'], 0, 120),
                $type,
                $optionsJson,
                $sort,
            ));
            $seeded++;
        }

        epc_erp_adv_set_setting($db, 'erp_industry_profile', $key);
        epc_erp_adv_set_setting($db, 'erp_default_item_type', (string) $bp['item_type']);
        epc_erp_adv_set_setting($db, 'erp_default_unit', (string) $bp['default_unit']);
        epc_erp_adv_set_setting($db, 'erp_track_expiry', (string) $bp['track_expiry']);
        epc_erp_adv_set_setting($db, 'erp_tax_category', (string) $bp['tax_category']);

        $db->prepare(
            'INSERT INTO `epc_erp_industry_state`
             (`industry_key`, `item_type`, `default_unit`, `fields_seeded`, `applied_by`, `time_applied`)
             VALUES (?, ?, ?, ?, ?, ?)'
        )->execute(array($key, $bp['item_type'], $bp['default_unit'], $seeded, $adminId, time()));

        return array(
            'status' => true,
            'message' => 'Industry "' . $bp['label'] . '" applied',
            'industry_key' => $key,
            'fields_seeded' => $seeded,
            'item_type' => $bp['item_type'],
            'default_unit' => $bp['default_unit'],
            'track_expiry' => (int) $bp['track_expiry'],
            'tax_category' => $bp['tax_category'],
        );
    }
}

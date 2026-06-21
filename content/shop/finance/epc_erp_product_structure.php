<?php
/**
 * Advanced ERP — Product structure (enterprise product dimensions + variants).
 *
 * enterprise models an item's structure with three dimension categories:
 *   - Product dimensions  : Configuration, Size, Colour, Style, Version
 *   - Storage dimensions  : Site, Warehouse, Location
 *   - Tracking dimensions : Batch, Serial
 * A "dimension group" selects which dimensions are active for an item; the
 * active *product* dimensions, crossed with their registered values, generate
 * the item's **variants** (one stock-keeping combination each).
 *
 * This layer is pure config + a small variant master. It does NOT change any
 * posting/valuation logic — variants reuse the existing `variant_label` already
 * present on `epc_erp_inv_stock`. Safe for live tenants (additive tables only).
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_erp_prod_dimension_catalog')) {
    /**
     * The fixed catalog of dimension categories and the dimensions inside each,
     * mirroring enterprise.
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_erp_prod_dimension_catalog(): array
    {
        return array(
            'product' => array(
                'label' => 'Product dimensions',
                'note' => 'Generate variants (one SKU per combination).',
                'dims' => array(
                    'configuration' => 'Configuration',
                    'size' => 'Size',
                    'color' => 'Colour',
                    'style' => 'Style',
                    'version' => 'Version',
                ),
            ),
            'storage' => array(
                'label' => 'Storage dimensions',
                'note' => 'Where stock is held (does not create variants).',
                'dims' => array(
                    'site' => 'Site',
                    'warehouse' => 'Warehouse',
                    'location' => 'Location',
                ),
            ),
            'tracking' => array(
                'label' => 'Tracking dimensions',
                'note' => 'How units are traced (does not create variants).',
                'dims' => array(
                    'batch' => 'Batch number',
                    'serial' => 'Serial number',
                ),
            ),
        );
    }
}

if (!function_exists('epc_erp_prod_dimension_label')) {
    function epc_erp_prod_dimension_label(string $category, string $dim): string
    {
        $cat = epc_erp_prod_dimension_catalog();
        return (string) ($cat[$category]['dims'][$dim] ?? $dim);
    }
}

if (!function_exists('epc_erp_prod_product_dim_keys')) {
    /**
     * Ordered list of the product-dimension keys (the ones that drive variants).
     *
     * @return array<int,string>
     */
    function epc_erp_prod_product_dim_keys(): array
    {
        return array_keys(epc_erp_prod_dimension_catalog()['product']['dims']);
    }
}

if (!function_exists('epc_erp_variant_code_token')) {
    /**
     * Normalise a dimension value into a short, SKU-safe token (upper, alnum).
     */
    function epc_erp_variant_code_token(string $value): string
    {
        $t = strtoupper((string) preg_replace('/[^A-Za-z0-9]+/', '', $value));
        return $t === '' ? 'X' : substr($t, 0, 6);
    }
}

if (!function_exists('epc_erp_variant_matrix')) {
    /**
     * Cartesian product of the supplied dimension -> values map.
     *
     * Input:  array('size' => array('S','M'), 'color' => array('Red','Blue'))
     * Output: array(
     *           array('size'=>'S','color'=>'Red'), array('size'=>'S','color'=>'Blue'),
     *           array('size'=>'M','color'=>'Red'), array('size'=>'M','color'=>'Blue'),
     *         )
     *
     * Dimensions with no values are ignored. Pure function (no DB, no globals).
     *
     * @param array<string,array<int,string>> $dimValues
     * @return array<int,array<string,string>>
     */
    function epc_erp_variant_matrix(array $dimValues): array
    {
        // Keep only product dimensions that actually have at least one value,
        // and preserve the canonical product-dimension order for stable SKUs.
        $ordered = array();
        foreach (epc_erp_prod_product_dim_keys() as $dim) {
            if (isset($dimValues[$dim]) && is_array($dimValues[$dim])) {
                $vals = array();
                foreach ($dimValues[$dim] as $v) {
                    $v = trim((string) $v);
                    if ($v !== '' && !in_array($v, $vals, true)) {
                        $vals[] = $v;
                    }
                }
                if ($vals) {
                    $ordered[$dim] = $vals;
                }
            }
        }
        if (!$ordered) {
            return array();
        }
        $result = array(array());
        foreach ($ordered as $dim => $vals) {
            $next = array();
            foreach ($result as $combo) {
                foreach ($vals as $val) {
                    $c = $combo;
                    $c[$dim] = $val;
                    $next[] = $c;
                }
            }
            $result = $next;
        }
        return $result;
    }
}

if (!function_exists('epc_erp_variant_label')) {
    /**
     * Human label for a variant combination, e.g. "M / Red".
     *
     * @param array<string,string> $combo
     */
    function epc_erp_variant_label(array $combo): string
    {
        $parts = array();
        foreach (epc_erp_prod_product_dim_keys() as $dim) {
            if (isset($combo[$dim]) && trim((string) $combo[$dim]) !== '') {
                $parts[] = trim((string) $combo[$dim]);
            }
        }
        return implode(' / ', $parts);
    }
}

if (!function_exists('epc_erp_variant_sku')) {
    /**
     * Build a deterministic variant SKU from a base SKU + a combination.
     *
     * @param array<string,string> $combo
     */
    function epc_erp_variant_sku(string $baseSku, array $combo): string
    {
        $tokens = array();
        foreach (epc_erp_prod_product_dim_keys() as $dim) {
            if (isset($combo[$dim]) && trim((string) $combo[$dim]) !== '') {
                $tokens[] = epc_erp_variant_code_token((string) $combo[$dim]);
            }
        }
        $base = trim($baseSku);
        if ($base === '') {
            $base = 'ITEM';
        }
        return $tokens ? $base . '-' . implode('-', $tokens) : $base;
    }
}

if (!function_exists('epc_erp_prod_structure_ensure_schema')) {
    function epc_erp_prod_structure_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_prod_dim_groups` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(32) NOT NULL,
            `name` varchar(120) NOT NULL,
            `product_dims_json` text,
            `storage_dims_json` text,
            `tracking_dims_json` text,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_prod_dim_values` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `dim_type` varchar(24) NOT NULL,
            `value` varchar(120) NOT NULL,
            `code` varchar(24) NOT NULL DEFAULT '',
            `sort_order` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_dim_value` (`dim_type`,`value`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_prod_variants` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `base_sku` varchar(64) NOT NULL DEFAULT '',
            `variant_sku` varchar(120) NOT NULL,
            `variant_label` varchar(160) NOT NULL DEFAULT '',
            `combo_json` text,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_variant_sku` (`variant_sku`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8");
    }
}

if (!function_exists('epc_erp_prod_dim_group_default')) {
    /**
     * The default (fallback) dimension group when none is saved yet.
     *
     * @return array<string,mixed>
     */
    function epc_erp_prod_dim_group_default(): array
    {
        return array(
            'code' => 'STD',
            'name' => 'Standard',
            'product' => array(),
            'storage' => array('warehouse'),
            'tracking' => array(),
        );
    }
}

if (!function_exists('epc_erp_prod_dim_group_get')) {
    /**
     * Read the tenant's (single) active dimension group, normalised.
     *
     * @return array<string,mixed>
     */
    function epc_erp_prod_dim_group_get(PDO $db): array
    {
        epc_erp_prod_structure_ensure_schema($db);
        $row = $db->query('SELECT * FROM `epc_erp_prod_dim_groups` WHERE `active`=1 ORDER BY `id` LIMIT 1')->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return epc_erp_prod_dim_group_default();
        }
        $decode = static function ($json): array {
            $a = json_decode((string) $json, true);
            return is_array($a) ? array_values(array_filter(array_map('strval', $a))) : array();
        };
        $cat = epc_erp_prod_dimension_catalog();
        $clean = static function (array $vals, string $category) use ($cat): array {
            $allowed = array_keys($cat[$category]['dims']);
            return array_values(array_intersect($vals, $allowed));
        };
        return array(
            'code' => (string) $row['code'],
            'name' => (string) $row['name'],
            'product' => $clean($decode($row['product_dims_json']), 'product'),
            'storage' => $clean($decode($row['storage_dims_json']), 'storage'),
            'tracking' => $clean($decode($row['tracking_dims_json']), 'tracking'),
        );
    }
}

if (!function_exists('epc_erp_prod_dim_group_save')) {
    /**
     * Upsert the tenant's active dimension group.
     *
     * @param array<string,mixed> $group
     */
    function epc_erp_prod_dim_group_save(PDO $db, array $group): void
    {
        epc_erp_prod_structure_ensure_schema($db);
        $cat = epc_erp_prod_dimension_catalog();
        $pick = static function ($vals, string $category) use ($cat): array {
            $vals = is_array($vals) ? array_map('strval', $vals) : array();
            $allowed = array_keys($cat[$category]['dims']);
            return array_values(array_intersect($vals, $allowed));
        };
        $code = trim((string) ($group['code'] ?? 'STD'));
        if ($code === '') {
            $code = 'STD';
        }
        $name = trim((string) ($group['name'] ?? 'Standard'));
        if ($name === '') {
            $name = 'Standard';
        }
        $existing = $db->query('SELECT `id` FROM `epc_erp_prod_dim_groups` WHERE `active`=1 ORDER BY `id` LIMIT 1')->fetchColumn();
        $prod = json_encode($pick($group['product'] ?? array(), 'product'));
        $stor = json_encode($pick($group['storage'] ?? array(), 'storage'));
        $trak = json_encode($pick($group['tracking'] ?? array(), 'tracking'));
        if ($existing) {
            $db->prepare('UPDATE `epc_erp_prod_dim_groups` SET `code`=?,`name`=?,`product_dims_json`=?,`storage_dims_json`=?,`tracking_dims_json`=? WHERE `id`=?')
                ->execute(array($code, $name, $prod, $stor, $trak, (int) $existing));
        } else {
            $db->prepare('INSERT INTO `epc_erp_prod_dim_groups` (`code`,`name`,`product_dims_json`,`storage_dims_json`,`tracking_dims_json`,`active`,`time_created`) VALUES (?,?,?,?,?,1,?)')
                ->execute(array($code, $name, $prod, $stor, $trak, time()));
        }
    }
}

if (!function_exists('epc_erp_prod_dim_values_list')) {
    /**
     * List registered values for a dimension type (or all when empty).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_erp_prod_dim_values_list(PDO $db, string $dimType = ''): array
    {
        epc_erp_prod_structure_ensure_schema($db);
        if ($dimType !== '') {
            $st = $db->prepare('SELECT * FROM `epc_erp_prod_dim_values` WHERE `dim_type`=? AND `active`=1 ORDER BY `sort_order`,`id`');
            $st->execute(array($dimType));
            return $st->fetchAll(PDO::FETCH_ASSOC);
        }
        return $db->query('SELECT * FROM `epc_erp_prod_dim_values` WHERE `active`=1 ORDER BY `dim_type`,`sort_order`,`id`')->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_erp_prod_dim_value_add')) {
    /**
     * Add a single dimension value (idempotent on dim_type+value).
     */
    function epc_erp_prod_dim_value_add(PDO $db, string $dimType, string $value, string $code = ''): bool
    {
        epc_erp_prod_structure_ensure_schema($db);
        $catDims = epc_erp_prod_product_dim_keys();
        if (!in_array($dimType, $catDims, true)) {
            return false;
        }
        $value = trim($value);
        if ($value === '') {
            return false;
        }
        if ($code === '') {
            $code = epc_erp_variant_code_token($value);
        }
        $db->prepare('INSERT INTO `epc_erp_prod_dim_values` (`dim_type`,`value`,`code`,`sort_order`,`active`) VALUES (?,?,?,?,1)
                      ON DUPLICATE KEY UPDATE `code`=VALUES(`code`),`active`=1')
            ->execute(array($dimType, $value, $code, 0));
        return true;
    }
}

if (!function_exists('epc_erp_prod_dim_value_delete')) {
    function epc_erp_prod_dim_value_delete(PDO $db, int $id): void
    {
        epc_erp_prod_structure_ensure_schema($db);
        $db->prepare('UPDATE `epc_erp_prod_dim_values` SET `active`=0 WHERE `id`=?')->execute(array($id));
    }
}

if (!function_exists('epc_erp_prod_variants_generate')) {
    /**
     * Generate (and persist) the variant matrix for an item from the active
     * product dimensions crossed with their registered values.
     *
     * Returns the list of variants that exist for the item afterwards. New
     * variants are inserted; existing variant SKUs are left untouched (idempotent).
     *
     * @return array{generated:int,existing:int,variants:array<int,array<string,mixed>>}
     */
    function epc_erp_prod_variants_generate(PDO $db, int $itemId, string $baseSku): array
    {
        epc_erp_prod_structure_ensure_schema($db);
        $group = epc_erp_prod_dim_group_get($db);
        $dimValues = array();
        foreach ((array) $group['product'] as $dim) {
            $vals = array();
            foreach (epc_erp_prod_dim_values_list($db, (string) $dim) as $v) {
                $vals[] = (string) $v['value'];
            }
            if ($vals) {
                $dimValues[(string) $dim] = $vals;
            }
        }
        $matrix = epc_erp_variant_matrix($dimValues);
        $generated = 0;
        $existing = 0;
        foreach ($matrix as $combo) {
            $sku = epc_erp_variant_sku($baseSku, $combo);
            $label = epc_erp_variant_label($combo);
            $chk = $db->prepare('SELECT `id` FROM `epc_erp_prod_variants` WHERE `variant_sku`=? LIMIT 1');
            $chk->execute(array($sku));
            if ($chk->fetchColumn()) {
                $existing++;
                continue;
            }
            $db->prepare('INSERT INTO `epc_erp_prod_variants` (`item_id`,`base_sku`,`variant_sku`,`variant_label`,`combo_json`,`active`,`time_created`) VALUES (?,?,?,?,?,1,?)')
                ->execute(array($itemId, $baseSku, $sku, $label, json_encode($combo), time()));
            $generated++;
        }
        return array(
            'generated' => $generated,
            'existing' => $existing,
            'variants' => epc_erp_prod_variants_for_item($db, $itemId),
        );
    }
}

if (!function_exists('epc_erp_prod_variants_for_item')) {
    /**
     * @return array<int,array<string,mixed>>
     */
    function epc_erp_prod_variants_for_item(PDO $db, int $itemId): array
    {
        epc_erp_prod_structure_ensure_schema($db);
        $st = $db->prepare('SELECT * FROM `epc_erp_prod_variants` WHERE `item_id`=? AND `active`=1 ORDER BY `variant_sku`');
        $st->execute(array($itemId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

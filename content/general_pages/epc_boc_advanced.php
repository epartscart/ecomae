<?php
/**
 * BOC advanced control — cross-tenant operating control over the platform's
 * existing multi-vendor (marketplace), multi-warehouse (inventory) and
 * multichannel (OMS) capabilities.
 *
 * Three control rooms, each a fleet-wide rollup + per-tenant drill-down:
 *   - Vendor & sourcing control   (epc_erp_suppliers / epc_scm_rfq / epc_erp_purchases)
 *   - Warehouse & inventory control (epc_erp_inv_warehouses / epc_erp_inv_stock + reorder planning)
 *   - Channel & order control (OMS)  (storefront / POS / API / marketplace channels)
 *
 * Design: the pure rollup/format functions take plain arrays and are unit
 * tested; the collectors take a tenant PDO and read existing ERP schema
 * defensively (missing table => zeros, never fatal); collection across the
 * fleet is wrapped per-tenant so one bad tenant never breaks the view.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_boc_kernel.php';

if (!function_exists('epc_boc_adv_money')) {
    /** Compact money formatting for control tiles (e.g. 1.2M, 38.4K). */
    function epc_boc_adv_money(float $v, string $cur = 'AED'): string
    {
        $abs = abs($v);
        $sfx = '';
        $n = $v;
        if ($abs >= 1000000) { $n = $v / 1000000; $sfx = 'M'; }
        elseif ($abs >= 1000) { $n = $v / 1000; $sfx = 'K'; }
        $num = $sfx === '' ? number_format($n, 0) : rtrim(rtrim(number_format($n, 1), '0'), '.') . $sfx;
        return $cur . ' ' . $num;
    }
}

if (!function_exists('epc_boc_adv_table_exists')) {
    function epc_boc_adv_table_exists(PDO $db, string $table): bool
    {
        try {
            $st = $db->prepare('SHOW TABLES LIKE ?');
            $st->execute(array($table));
            return (bool) $st->fetchColumn();
        } catch (Throwable $e) {
            return false;
        }
    }
}

if (!function_exists('epc_boc_adv_scalar')) {
    /** Run a scalar query defensively; return $default on any error. */
    function epc_boc_adv_scalar(PDO $db, string $sql, array $args = array(), float $default = 0.0): float
    {
        try {
            $st = $db->prepare($sql);
            $st->execute($args);
            $v = $st->fetchColumn();
            return $v === false || $v === null ? $default : (float) $v;
        } catch (Throwable $e) {
            return $default;
        }
    }
}

/* ----------------------------------------------------------------------- */
/* Pure rollups (unit-tested)                                              */
/* ----------------------------------------------------------------------- */

if (!function_exists('epc_boc_vendor_rollup')) {
    /**
     * Aggregate per-tenant vendor metrics into a fleet rollup. Pure.
     *
     * @param array<int,array{site_key:string,label:string,type:string,ok:bool,vendors:int,active_vendors:int,rfq_open:int,spend:float,currency:string}> $per
     * @return array{totals:array<string,mixed>,rows:array<int,array<string,mixed>>}
     */
    function epc_boc_vendor_rollup(array $per): array
    {
        $tVendors = 0; $tActive = 0; $tRfq = 0; $tSpend = 0.0; $reachable = 0;
        $rows = array();
        foreach ($per as $p) {
            $ok = !empty($p['ok']);
            if ($ok) { $reachable++; }
            $tVendors += (int) ($p['vendors'] ?? 0);
            $tActive += (int) ($p['active_vendors'] ?? 0);
            $tRfq += (int) ($p['rfq_open'] ?? 0);
            $tSpend += (float) ($p['spend'] ?? 0);
            $rows[] = array(
                'site_key' => (string) ($p['site_key'] ?? ''),
                'label'    => (string) ($p['label'] ?? ''),
                'type'     => (string) ($p['type'] ?? 'commerce'),
                'ok'       => $ok,
                'vendors'  => (int) ($p['vendors'] ?? 0),
                'active_vendors' => (int) ($p['active_vendors'] ?? 0),
                'rfq_open' => (int) ($p['rfq_open'] ?? 0),
                'spend'    => (float) ($p['spend'] ?? 0),
                'currency' => (string) ($p['currency'] ?? 'AED'),
                'note'     => (string) ($p['note'] ?? ''),
            );
        }
        usort($rows, static function ($a, $b) { return $b['vendors'] <=> $a['vendors']; });
        return array(
            'totals' => array(
                'tenants' => count($per), 'reachable' => $reachable,
                'vendors' => $tVendors, 'active_vendors' => $tActive,
                'rfq_open' => $tRfq, 'spend' => $tSpend,
            ),
            'rows' => $rows,
        );
    }
}

if (!function_exists('epc_boc_warehouse_rollup')) {
    /**
     * Aggregate per-tenant warehouse/inventory metrics. Pure. Each row gets a
     * RAG: red if unreachable or any out-of-stock, amber if low-stock, else green.
     *
     * @param array<int,array<string,mixed>> $per
     * @return array{totals:array<string,mixed>,rows:array<int,array<string,mixed>>}
     */
    function epc_boc_warehouse_rollup(array $per): array
    {
        $tWh = 0; $tValue = 0.0; $tLow = 0; $tOut = 0; $tSkus = 0; $reachable = 0;
        $rows = array();
        foreach ($per as $p) {
            $ok = !empty($p['ok']);
            if ($ok) { $reachable++; }
            $wh = (int) ($p['warehouses'] ?? 0);
            $low = (int) ($p['low_stock'] ?? 0);
            $out = (int) ($p['out_of_stock'] ?? 0);
            $tWh += $wh; $tValue += (float) ($p['stock_value'] ?? 0);
            $tLow += $low; $tOut += $out; $tSkus += (int) ($p['skus'] ?? 0);
            $rag = 'green';
            if (!$ok || $out > 0) { $rag = 'red'; }
            elseif ($low > 0) { $rag = 'amber'; }
            $rows[] = array(
                'site_key' => (string) ($p['site_key'] ?? ''),
                'label'    => (string) ($p['label'] ?? ''),
                'type'     => (string) ($p['type'] ?? 'commerce'),
                'ok'       => $ok,
                'warehouses' => $wh,
                'skus'     => (int) ($p['skus'] ?? 0),
                'stock_value' => (float) ($p['stock_value'] ?? 0),
                'low_stock' => $low,
                'out_of_stock' => $out,
                'currency' => (string) ($p['currency'] ?? 'AED'),
                'rag'      => $rag,
                'note'     => (string) ($p['note'] ?? ''),
            );
        }
        usort($rows, static function ($a, $b) { return $b['stock_value'] <=> $a['stock_value']; });
        return array(
            'totals' => array(
                'tenants' => count($per), 'reachable' => $reachable,
                'warehouses' => $tWh, 'stock_value' => $tValue,
                'low_stock' => $tLow, 'out_of_stock' => $tOut, 'skus' => $tSkus,
            ),
            'rows' => $rows,
        );
    }
}

if (!function_exists('epc_boc_channel_rollup')) {
    /**
     * Aggregate per-tenant channel/OMS metrics. Pure. A "channel" is any active
     * selling surface (web storefront, POS, API, marketplace listing target).
     *
     * @param array<int,array<string,mixed>> $per
     * @return array{totals:array<string,mixed>,rows:array<int,array<string,mixed>>}
     */
    function epc_boc_channel_rollup(array $per): array
    {
        $tChannels = 0; $tWeb = 0; $tPos = 0; $tApi = 0; $tMkt = 0; $tArb = 0; $reachable = 0;
        $rows = array();
        foreach ($per as $p) {
            $ok = !empty($p['ok']);
            if ($ok) { $reachable++; }
            $web = !empty($p['web']) ? 1 : 0;
            $pos = !empty($p['pos']) ? 1 : 0;
            $api = !empty($p['api']) ? 1 : 0;
            $mkt = (int) ($p['marketplaces'] ?? 0);
            $arb = !empty($p['arbitrage']) ? 1 : 0;
            $count = $web + $pos + $api + $mkt;
            $tChannels += $count; $tWeb += $web; $tPos += $pos; $tApi += $api; $tMkt += $mkt; $tArb += $arb;
            $rows[] = array(
                'site_key' => (string) ($p['site_key'] ?? ''),
                'label'    => (string) ($p['label'] ?? ''),
                'type'     => (string) ($p['type'] ?? 'commerce'),
                'ok'       => $ok,
                'web'      => (bool) $web, 'pos' => (bool) $pos, 'api' => (bool) $api,
                'marketplaces' => $mkt, 'arbitrage' => (bool) $arb,
                'channels' => $count,
                'note'     => (string) ($p['note'] ?? ''),
            );
        }
        usort($rows, static function ($a, $b) { return $b['channels'] <=> $a['channels']; });
        return array(
            'totals' => array(
                'tenants' => count($per), 'reachable' => $reachable,
                'channels' => $tChannels, 'web' => $tWeb, 'pos' => $tPos,
                'api' => $tApi, 'marketplaces' => $tMkt, 'arbitrage' => $tArb,
            ),
            'rows' => $rows,
        );
    }
}

/* ----------------------------------------------------------------------- */
/* Per-tenant collectors (read existing ERP schema defensively)            */
/* ----------------------------------------------------------------------- */

if (!function_exists('epc_boc_collect_vendor')) {
    function epc_boc_collect_vendor(PDO $db): array
    {
        $out = array('vendors' => 0, 'active_vendors' => 0, 'rfq_open' => 0, 'spend' => 0.0, 'has_erp' => false);
        if (epc_boc_adv_table_exists($db, 'epc_erp_suppliers')) {
            $out['has_erp'] = true;
            $out['vendors'] = (int) epc_boc_adv_scalar($db, 'SELECT COUNT(*) FROM `epc_erp_suppliers`');
            $out['active_vendors'] = (int) epc_boc_adv_scalar($db, 'SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1');
        }
        if (epc_boc_adv_table_exists($db, 'epc_scm_rfq')) {
            $out['rfq_open'] = (int) epc_boc_adv_scalar($db, "SELECT COUNT(*) FROM `epc_scm_rfq` WHERE `status` IN ('draft','sent','open','responded')");
        }
        if (epc_boc_adv_table_exists($db, 'epc_erp_purchases')) {
            $out['spend'] = epc_boc_adv_scalar($db, 'SELECT COALESCE(SUM(`total_amount`),0) FROM `epc_erp_purchases`');
        }
        return $out;
    }
}

if (!function_exists('epc_boc_collect_warehouse')) {
    function epc_boc_collect_warehouse(PDO $db): array
    {
        $out = array('warehouses' => 0, 'skus' => 0, 'stock_value' => 0.0, 'low_stock' => 0, 'out_of_stock' => 0, 'has_erp' => false);
        if (epc_boc_adv_table_exists($db, 'epc_erp_inv_warehouses')) {
            $out['has_erp'] = true;
            $out['warehouses'] = (int) epc_boc_adv_scalar($db, 'SELECT COUNT(*) FROM `epc_erp_inv_warehouses` WHERE `active` = 1');
        }
        if (epc_boc_adv_table_exists($db, 'epc_erp_inv_items')) {
            $out['skus'] = (int) epc_boc_adv_scalar($db, 'SELECT COUNT(*) FROM `epc_erp_inv_items` WHERE `active` = 1');
        }
        if (epc_boc_adv_table_exists($db, 'epc_erp_inv_stock')) {
            $out['stock_value'] = epc_boc_adv_scalar($db, 'SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`),0) FROM `epc_erp_inv_stock`');
            $out['out_of_stock'] = (int) epc_boc_adv_scalar($db, 'SELECT COUNT(*) FROM (SELECT `item_id` FROM `epc_erp_inv_stock` GROUP BY `item_id` HAVING SUM(`qty_on_hand`) <= 0) t');
            // Low stock = items below their planning reorder point (if planning set).
            if (epc_boc_adv_table_exists($db, 'epc_scm_item_planning')) {
                $out['low_stock'] = (int) epc_boc_adv_scalar(
                    $db,
                    'SELECT COUNT(*) FROM `epc_scm_item_planning` p ' .
                    'JOIN (SELECT `item_id`, SUM(`qty_on_hand`) qoh FROM `epc_erp_inv_stock` GROUP BY `item_id`) s ON s.`item_id` = p.`item_id` ' .
                    'WHERE p.`reorder_point` > 0 AND s.qoh > 0 AND s.qoh <= p.`reorder_point`'
                );
            }
        }
        return $out;
    }
}

if (!function_exists('epc_boc_collect_channel')) {
    /**
     * Channel presence for a tenant. Web storefront is assumed for commerce
     * tenants; POS/API detected from schema; marketplace channels from the
     * Auto-Price marketplace registry (country-driven).
     */
    function epc_boc_collect_channel(?PDO $platformDb, PDO $tenantDb, string $siteKey, string $type): array
    {
        $out = array('web' => false, 'pos' => false, 'api' => false, 'marketplaces' => 0, 'arbitrage' => false);
        $out['web'] = ($type === 'commerce' || $type === 'demo');
        if (epc_boc_adv_table_exists($tenantDb, 'epc_pos_registers') || epc_boc_adv_table_exists($tenantDb, 'epc_pos_sales')) {
            $out['pos'] = (bool) epc_boc_adv_scalar($tenantDb, 'SELECT COUNT(*) FROM `epc_pos_registers`', array(), 0.0);
        }
        if ($platformDb instanceof PDO && epc_boc_adv_table_exists($platformDb, 'epc_api_clients')) {
            $out['api'] = (bool) epc_boc_adv_scalar($platformDb, 'SELECT COUNT(*) FROM `epc_api_clients` WHERE `site_key` = ?', array($siteKey), 0.0);
        }
        if (function_exists('epc_apai_marketplace_channels_for_tenant') && $platformDb instanceof PDO) {
            try {
                $ch = epc_apai_marketplace_channels_for_tenant($platformDb, $siteKey);
                $sell = isset($ch['sell']) && is_array($ch['sell']) ? $ch['sell'] : array();
                $out['marketplaces'] = count($sell);
            } catch (Throwable $e) {
            }
        }
        if (function_exists('epc_apai_marketplace_arbitrage_enabled') && $platformDb instanceof PDO) {
            try {
                $out['arbitrage'] = (bool) epc_apai_marketplace_arbitrage_enabled($platformDb, $siteKey);
            } catch (Throwable $e) {
            }
        }
        return $out;
    }
}

if (!function_exists('epc_boc_adv_fleet_metrics')) {
    /**
     * Walk the registry, connect to each tenant DB, and collect a metric bundle
     * via $collector(PDO $tenantDb, array $tenantRow): array. Per-tenant errors
     * are captured as ok=false with a note; never fatal.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_boc_adv_fleet_metrics(PDO $platformDb, callable $collector): array
    {
        $rows = array();
        if (!function_exists('epc_portal_tenant_control_list_all')) {
            return $rows;
        }
        try {
            $tenants = epc_portal_tenant_control_list_all($platformDb);
        } catch (Throwable $e) {
            return $rows;
        }
        foreach ($tenants as $t) {
            $siteKey = (string) ($t['site_key'] ?? '');
            $type = epc_boc_classify_tenant($t);
            $label = (string) ($t['trade_name'] ?? $t['system_name'] ?? $siteKey);
            $base = array('site_key' => $siteKey, 'label' => $label, 'type' => $type, 'ok' => false, 'note' => '');
            try {
                $conn = epc_portal_tenant_control_tenant_pdo_connect($t);
                $pdo = $conn['pdo'];
                if (!$pdo instanceof PDO) {
                    $base['note'] = 'DB unreachable';
                    $rows[] = $base;
                    continue;
                }
                $metrics = $collector($pdo, $t);
                $base['ok'] = true;
                $rows[] = array_merge($base, is_array($metrics) ? $metrics : array());
            } catch (Throwable $e) {
                $base['note'] = 'Collect error';
                $rows[] = $base;
            }
        }
        return $rows;
    }
}

/* ----------------------------------------------------------------------- */
/* Renderers                                                               */
/* ----------------------------------------------------------------------- */

if (!function_exists('epc_boc_adv_tile')) {
    function epc_boc_adv_tile(string $label, string $value, string $tone = '', string $hint = ''): string
    {
        $cls = 'epc-boc__tile' . ($tone !== '' ? ' epc-boc__tile--' . $tone : '');
        $h = $hint !== '' ? '<div class="epc-boc__tile-hint">' . epc_boc_h($hint) . '</div>' : '';
        return '<div class="' . $cls . '"><div class="epc-boc__tile-label">' . epc_boc_h($label) . '</div><div class="epc-boc__tile-val">' . epc_boc_h($value) . '</div>' . $h . '</div>';
    }
}

if (!function_exists('epc_boc_adv_rag_chip')) {
    function epc_boc_adv_rag_chip(string $rag): string
    {
        $map = array('green' => 'OK', 'amber' => 'ATTENTION', 'red' => 'CRITICAL');
        return '<span class="epc-boc__chip epc-boc__chip--' . epc_boc_h($rag) . '">' . epc_boc_h($map[$rag] ?? strtoupper($rag)) . '</span>';
    }
}

if (!function_exists('epc_boc_adv_yn')) {
    function epc_boc_adv_yn(bool $v): string
    {
        return $v
            ? '<span class="epc-boc__chip epc-boc__chip--green">on</span>'
            : '<span class="epc-boc__chip epc-boc__chip--type" style="opacity:.45">—</span>';
    }
}

if (!function_exists('epc_boc_adv_hero')) {
    function epc_boc_adv_hero(string $badge, string $icon, string $title, string $sub): void
    {
        echo '<div class="epc-boc__hero"><div>';
        echo '<span class="epc-boc__env" style="background:rgba(255,255,255,.15);color:#fff;border-color:rgba(255,255,255,.25)">' . epc_boc_h($badge) . '</span>';
        echo '<h2><i class="fa ' . epc_boc_h($icon) . '"></i> ' . epc_boc_h($title) . '</h2>';
        echo '<p>' . epc_boc_h($sub) . '</p>';
        echo '</div></div>';
    }
}

if (!function_exists('epc_boc_render_vendor_control')) {
    function epc_boc_render_vendor_control(?PDO $db, string $base, ?array $rollup = null): void
    {
        if ($rollup === null) {
            $per = epc_boc_adv_fleet_metrics($db, static function (PDO $pdo) {
                return epc_boc_collect_vendor($pdo);
            });
            $rollup = epc_boc_vendor_rollup($per);
        }
        $r = $rollup;
        $t = $r['totals'];
        epc_boc_adv_hero('MULTI-VENDOR', 'fa-truck', 'Vendor & Sourcing Control', 'Every supplier, RFQ and purchase commitment across the fleet — one sourcing spine.');
        echo '<div class="epc-boc__tiles">';
        echo epc_boc_adv_tile('Vendors', number_format($t['vendors']));
        echo epc_boc_adv_tile('Active', number_format($t['active_vendors']), 'green');
        echo epc_boc_adv_tile('Open RFQs', number_format($t['rfq_open']), 'amber');
        echo epc_boc_adv_tile('Purchase spend', epc_boc_adv_money((float) $t['spend']));
        echo epc_boc_adv_tile('Reachable units', $t['reachable'] . ' / ' . $t['tenants']);
        echo '</div>';
        echo '<div class="epc-boc__panel"><div class="epc-boc__panel-h"><i class="fa fa-list"></i> By tenant</div>';
        echo '<table><thead><tr><th>Tenant</th><th>Type</th><th>Vendors</th><th>Active</th><th>Open RFQs</th><th>Spend</th><th>Status</th></tr></thead><tbody>';
        foreach ($r['rows'] as $row) {
            echo '<tr><td><strong>' . epc_boc_h($row['label']) . '</strong><br><code>' . epc_boc_h($row['site_key']) . '</code></td>';
            echo '<td>' . epc_boc_h(epc_boc_type_label($row['type'])) . '</td>';
            echo '<td>' . number_format($row['vendors']) . '</td>';
            echo '<td>' . number_format($row['active_vendors']) . '</td>';
            echo '<td>' . number_format($row['rfq_open']) . '</td>';
            echo '<td>' . epc_boc_h(epc_boc_adv_money((float) $row['spend'], $row['currency'])) . '</td>';
            echo '<td>' . ($row['ok'] ? epc_boc_adv_rag_chip('green') : epc_boc_adv_rag_chip('red') . ' <span style="color:#94a3b8">' . epc_boc_h($row['note']) . '</span>') . '</td></tr>';
        }
        if (empty($r['rows'])) { echo '<tr><td colspan="7" style="color:#94a3b8">No tenants in registry.</td></tr>'; }
        echo '</tbody></table></div>';
    }
}

if (!function_exists('epc_boc_render_warehouse_control')) {
    function epc_boc_render_warehouse_control(?PDO $db, string $base, ?array $rollup = null): void
    {
        if ($rollup === null) {
            $per = epc_boc_adv_fleet_metrics($db, static function (PDO $pdo) {
                return epc_boc_collect_warehouse($pdo);
            });
            $rollup = epc_boc_warehouse_rollup($per);
        }
        $r = $rollup;
        $t = $r['totals'];
        epc_boc_adv_hero('MULTI-WAREHOUSE', 'fa-cubes', 'Warehouse & Inventory Control', 'Stock value, locations and replenishment risk across every tenant warehouse.');
        echo '<div class="epc-boc__tiles">';
        echo epc_boc_adv_tile('Warehouses', number_format($t['warehouses']));
        echo epc_boc_adv_tile('Stock value', epc_boc_adv_money((float) $t['stock_value']));
        echo epc_boc_adv_tile('SKUs', number_format($t['skus']));
        echo epc_boc_adv_tile('Low stock', number_format($t['low_stock']), $t['low_stock'] > 0 ? 'amber' : 'green');
        echo epc_boc_adv_tile('Out of stock', number_format($t['out_of_stock']), $t['out_of_stock'] > 0 ? 'red' : 'green');
        echo '</div>';
        echo '<div class="epc-boc__panel"><div class="epc-boc__panel-h"><i class="fa fa-list"></i> By tenant</div>';
        echo '<table><thead><tr><th>Tenant</th><th>Type</th><th>Warehouses</th><th>SKUs</th><th>Stock value</th><th>Low</th><th>Out</th><th>Health</th></tr></thead><tbody>';
        foreach ($r['rows'] as $row) {
            echo '<tr><td><strong>' . epc_boc_h($row['label']) . '</strong><br><code>' . epc_boc_h($row['site_key']) . '</code></td>';
            echo '<td>' . epc_boc_h(epc_boc_type_label($row['type'])) . '</td>';
            echo '<td>' . number_format($row['warehouses']) . '</td>';
            echo '<td>' . number_format($row['skus']) . '</td>';
            echo '<td>' . epc_boc_h(epc_boc_adv_money((float) $row['stock_value'], $row['currency'])) . '</td>';
            echo '<td>' . number_format($row['low_stock']) . '</td>';
            echo '<td>' . number_format($row['out_of_stock']) . '</td>';
            echo '<td>' . epc_boc_adv_rag_chip($row['rag']) . ($row['note'] !== '' ? ' <span style="color:#94a3b8">' . epc_boc_h($row['note']) . '</span>' : '') . '</td></tr>';
        }
        if (empty($r['rows'])) { echo '<tr><td colspan="8" style="color:#94a3b8">No tenants in registry.</td></tr>'; }
        echo '</tbody></table></div>';
    }
}

if (!function_exists('epc_boc_render_channel_control')) {
    function epc_boc_render_channel_control(?PDO $db, string $base, ?array $rollup = null): void
    {
        if ($rollup === null) {
            $per = epc_boc_adv_fleet_metrics($db, static function (PDO $pdo, array $t) use ($db) {
                $siteKey = (string) ($t['site_key'] ?? '');
                $type = epc_boc_classify_tenant($t);
                return epc_boc_collect_channel($db, $pdo, $siteKey, $type);
            });
            $rollup = epc_boc_channel_rollup($per);
        }
        $r = $rollup;
        $t = $r['totals'];
        epc_boc_adv_hero('MULTICHANNEL · OMS', 'fa-sitemap', 'Channel & Order Control', 'Every selling surface — web, POS, API and marketplaces — across the fleet.');
        echo '<div class="epc-boc__tiles">';
        echo epc_boc_adv_tile('Channels', number_format($t['channels']));
        echo epc_boc_adv_tile('Web', number_format($t['web']), 'green');
        echo epc_boc_adv_tile('POS', number_format($t['pos']));
        echo epc_boc_adv_tile('API', number_format($t['api']));
        echo epc_boc_adv_tile('Marketplaces', number_format($t['marketplaces']), 'amber');
        echo epc_boc_adv_tile('Arbitrage on', number_format($t['arbitrage']));
        echo '</div>';
        echo '<div class="epc-boc__panel"><div class="epc-boc__panel-h"><i class="fa fa-list"></i> By tenant</div>';
        echo '<table><thead><tr><th>Tenant</th><th>Type</th><th>Web</th><th>POS</th><th>API</th><th>Marketplaces</th><th>Arbitrage</th><th>Channels</th></tr></thead><tbody>';
        foreach ($r['rows'] as $row) {
            echo '<tr><td><strong>' . epc_boc_h($row['label']) . '</strong><br><code>' . epc_boc_h($row['site_key']) . '</code></td>';
            echo '<td>' . epc_boc_h(epc_boc_type_label($row['type'])) . '</td>';
            echo '<td>' . epc_boc_adv_yn($row['web']) . '</td>';
            echo '<td>' . epc_boc_adv_yn($row['pos']) . '</td>';
            echo '<td>' . epc_boc_adv_yn($row['api']) . '</td>';
            echo '<td>' . number_format($row['marketplaces']) . '</td>';
            echo '<td>' . epc_boc_adv_yn($row['arbitrage']) . '</td>';
            echo '<td><strong>' . number_format($row['channels']) . '</strong></td></tr>';
        }
        if (empty($r['rows'])) { echo '<tr><td colspan="8" style="color:#94a3b8">No tenants in registry.</td></tr>'; }
        echo '</tbody></table></div>';
    }
}

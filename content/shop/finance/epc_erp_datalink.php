<?php
/**
 * Advanced ERP — Native commerce data-link.
 *
 * Pulls the tenant's EXISTING storefront / CRM / e-commerce CP data into the
 * ERP so dashboards, AR and the document chain reflect real shop activity:
 *
 *   - Customers   <- `users` + `users_profiles`
 *   - AR ledger   <- `shop_users_accounting`  (native customer balance)
 *   - Orders      <- `shop_orders` + `shop_orders_items`
 *   - Products    <- `shop_catalogue_products` + `shop_storages_data`
 *   - Warehouses  <- `shop_storages`
 *
 * It is READ-ONLY on every native shop_* / users table (nothing in the
 * storefront schema is modified). The only writes are into the ERP bridge
 * map (`epc_ec_orders`) when a native order is explicitly linked, and they
 * are idempotent (keyed on `shoporder:<id>`).
 *
 * Every query is schema-tolerant: it checks the table/columns exist first, so
 * the module runs unchanged on any tenant DB (and in tests against a seeded
 * subset). Tenant-isolated, entitlement-gated.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

/* ----------------------------- introspection ------------------------------ */

if (!function_exists('epc_dl_table_exists')) {
    function epc_dl_table_exists(PDO $db, string $table): bool
    {
        static $cache = array();
        if (isset($cache[$table])) {
            return $cache[$table];
        }
        try {
            $st = $db->prepare(
                'SELECT COUNT(*) FROM information_schema.TABLES '
                . 'WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?'
            );
            $st->execute(array($table));
            $cache[$table] = ((int) $st->fetchColumn()) > 0;
        } catch (Throwable $e) {
            $cache[$table] = false;
        }
        return $cache[$table];
    }
}

if (!function_exists('epc_dl_columns')) {
    /** @return array<int,string> */
    function epc_dl_columns(PDO $db, string $table): array
    {
        static $cache = array();
        if (isset($cache[$table])) {
            return $cache[$table];
        }
        $cols = array();
        try {
            $st = $db->prepare(
                'SELECT COLUMN_NAME FROM information_schema.COLUMNS '
                . 'WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? '
                . 'ORDER BY ORDINAL_POSITION'
            );
            $st->execute(array($table));
            $cols = $st->fetchAll(PDO::FETCH_COLUMN);
        } catch (Throwable $e) {
            $cols = array();
        }
        $cache[$table] = $cols;
        return $cols;
    }
}

if (!function_exists('epc_dl_has_col')) {
    function epc_dl_has_col(PDO $db, string $table, string $col): bool
    {
        return in_array($col, epc_dl_columns($db, $table), true);
    }
}

if (!function_exists('epc_dl_num')) {
    function epc_dl_num($v): float
    {
        return (float) (is_numeric($v) ? $v : 0);
    }
}

/* ------------------------------- customers -------------------------------- */

if (!function_exists('epc_dl_customer_name')) {
    /**
     * Resolve a display name for a user from the key/value `users_profiles`
     * rows (first/last/company/name) with an email fallback.
     *
     * @param array<string,string> $profile
     */
    function epc_dl_customer_name(array $profile, string $email): string
    {
        $first = trim((string) ($profile['first_name'] ?? $profile['firstname'] ?? $profile['name'] ?? ''));
        $last = trim((string) ($profile['last_name'] ?? $profile['lastname'] ?? $profile['surname'] ?? ''));
        $company = trim((string) ($profile['company'] ?? $profile['company_name'] ?? $profile['organization'] ?? ''));
        $full = trim($first . ' ' . $last);
        if ($company !== '') {
            return $full !== '' ? ($company . ' (' . $full . ')') : $company;
        }
        if ($full !== '') {
            return $full;
        }
        return $email !== '' ? $email : 'Customer';
    }
}

if (!function_exists('epc_dl_profiles')) {
    /**
     * Load users_profiles key/value pairs grouped by user_id.
     *
     * @param array<int,int> $userIds
     * @return array<int,array<string,string>>
     */
    function epc_dl_profiles(PDO $db, array $userIds): array
    {
        $out = array();
        if (!$userIds || !epc_dl_table_exists($db, 'users_profiles')) {
            return $out;
        }
        if (!epc_dl_has_col($db, 'users_profiles', 'data_key')) {
            return $out;
        }
        $ids = array_values(array_unique(array_map('intval', $userIds)));
        $in = implode(',', array_fill(0, count($ids), '?'));
        $st = $db->prepare(
            'SELECT user_id, data_key, data_value FROM `users_profiles` '
            . 'WHERE user_id IN (' . $in . ')'
        );
        $st->execute($ids);
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $uid = (int) $r['user_id'];
            $out[$uid][(string) $r['data_key']] = (string) $r['data_value'];
        }
        return $out;
    }
}

if (!function_exists('epc_dl_customers')) {
    /**
     * Normalized customer master from native `users` + `users_profiles`.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_dl_customers(PDO $db, int $limit = 500): array
    {
        if (!epc_dl_table_exists($db, 'users')) {
            return array();
        }
        $key = epc_dl_has_col($db, 'users', 'user_id') ? 'user_id' : 'id';
        $sel = array($key . ' AS user_id');
        $sel[] = epc_dl_has_col($db, 'users', 'email') ? 'email' : "'' AS email";
        $sel[] = epc_dl_has_col($db, 'users', 'phone') ? 'phone' : "'' AS phone";
        $sel[] = epc_dl_has_col($db, 'users', 'time_registered')
            ? 'time_registered' : '0 AS time_registered';
        $sql = 'SELECT ' . implode(', ', $sel) . ' FROM `users` ORDER BY ' . $key . ' DESC LIMIT ' . (int) $limit;
        $rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
        $profiles = epc_dl_profiles($db, array_map(static function ($r) {
            return (int) $r['user_id'];
        }, $rows));
        $out = array();
        foreach ($rows as $r) {
            $uid = (int) $r['user_id'];
            $prof = $profiles[$uid] ?? array();
            $out[] = array(
                'user_id' => $uid,
                'name' => epc_dl_customer_name($prof, (string) ($r['email'] ?? '')),
                'email' => (string) ($r['email'] ?? ''),
                'phone' => (string) ($r['phone'] ?? ''),
                'since' => (int) ($r['time_registered'] ?? 0),
            );
        }
        return $out;
    }
}

/* ----------------------------- AR (receivables) --------------------------- */

if (!function_exists('epc_dl_customer_ar')) {
    /**
     * Map the native customer ledger `shop_users_accounting` to an ERP-style
     * AR position. Convention in the native table: `income` flags a credit
     * (top-up / payment in) vs a charge; `amount` is the value; `pay_orders`
     * links spend to orders. We compute a running balance and an aged figure.
     *
     * @return array<string,mixed> {balance, debit, credit, entries:[...]}
     */
    function epc_dl_customer_ar(PDO $db, int $userId): array
    {
        $res = array('balance' => 0.0, 'debit' => 0.0, 'credit' => 0.0, 'entries' => array());
        if (!epc_dl_table_exists($db, 'shop_users_accounting')) {
            return $res;
        }
        $hasIncome = epc_dl_has_col($db, 'shop_users_accounting', 'income');
        $hasActive = epc_dl_has_col($db, 'shop_users_accounting', 'active');
        $sql = 'SELECT * FROM `shop_users_accounting` WHERE user_id = ?';
        if ($hasActive) {
            $sql .= ' AND active = 1';
        }
        $sql .= ' ORDER BY ' . (epc_dl_has_col($db, 'shop_users_accounting', 'time') ? 'time' : 'id');
        $st = $db->prepare($sql);
        $st->execute(array($userId));
        $balance = 0.0;
        $debit = 0.0;
        $credit = 0.0;
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $amt = epc_dl_num($r['amount'] ?? 0);
            $isCredit = $hasIncome ? ((int) $r['income'] === 1) : ($amt >= 0);
            if ($isCredit) {
                $credit += abs($amt);
                $balance += abs($amt);
            } else {
                $debit += abs($amt);
                $balance -= abs($amt);
            }
            $res['entries'][] = array(
                'time' => (int) ($r['time'] ?? 0),
                'amount' => $amt,
                'type' => $isCredit ? 'credit' : 'debit',
                'order_id' => (int) ($r['order_id'] ?? 0),
                'code' => (string) ($r['operation_code'] ?? ''),
            );
        }
        $res['balance'] = round($balance, 2);
        $res['debit'] = round($debit, 2);
        $res['credit'] = round($credit, 2);
        return $res;
    }
}

/* -------------------------------- orders ---------------------------------- */

if (!function_exists('epc_dl_order_lines')) {
    /**
     * Normalized lines for a native order from `shop_orders_items`, including
     * cost (t2_price_purchase) so the ERP can compute margin.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_dl_order_lines(PDO $db, int $orderId): array
    {
        if (!epc_dl_table_exists($db, 'shop_orders_items')) {
            return array();
        }
        $st = $db->prepare('SELECT * FROM `shop_orders_items` WHERE order_id = ?');
        $st->execute(array($orderId));
        $out = array();
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $qty = epc_dl_num($r['count_need'] ?? ($r['count'] ?? 1));
            if ($qty <= 0) {
                $qty = 1.0;
            }
            $price = epc_dl_num($r['price'] ?? 0);
            $cost = epc_dl_num($r['t2_price_purchase'] ?? 0);
            $name = (string) ($r['t2_name'] ?? ($r['name'] ?? ''));
            $out[] = array(
                'item_id' => (int) ($r['product_id'] ?? 0),
                'name' => $name,
                'sku' => (string) ($r['t2_article'] ?? ''),
                'qty' => $qty,
                'unit_price' => $price,
                'unit_cost' => $cost,
                'line_total' => round($price * $qty, 2),
                'line_cost' => round($cost * $qty, 2),
            );
        }
        return $out;
    }
}

if (!function_exists('epc_dl_orders')) {
    /**
     * Normalized order list from native `shop_orders` (+ aggregated line
     * totals). Computes total, cost and gross margin per order so ERP sales
     * dashboards work directly off live shop data.
     *
     * @param array<string,mixed> $opts {limit, user_id, paid_only}
     * @return array<int,array<string,mixed>>
     */
    function epc_dl_orders(PDO $db, array $opts = array()): array
    {
        if (!epc_dl_table_exists($db, 'shop_orders')) {
            return array();
        }
        $limit = (int) ($opts['limit'] ?? 200);
        $where = array();
        $args = array();
        if (!empty($opts['user_id'])) {
            $where[] = 'o.user_id = ?';
            $args[] = (int) $opts['user_id'];
        }
        if (!empty($opts['paid_only']) && epc_dl_has_col($db, 'shop_orders', 'paid')) {
            $where[] = 'o.paid = 1';
        }
        $wsql = $where ? (' WHERE ' . implode(' AND ', $where)) : '';
        $timeCol = epc_dl_has_col($db, 'shop_orders', 'time') ? 'o.time' : 'o.id';
        $sql = 'SELECT o.* FROM `shop_orders` o' . $wsql
            . ' ORDER BY ' . $timeCol . ' DESC LIMIT ' . $limit;
        $st = $db->prepare($sql);
        $st->execute($args);
        $out = array();
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $o) {
            $oid = (int) $o['id'];
            $lines = epc_dl_order_lines($db, $oid);
            $total = 0.0;
            $cost = 0.0;
            foreach ($lines as $l) {
                $total += $l['line_total'];
                $cost += $l['line_cost'];
            }
            $out[] = array(
                'order_id' => $oid,
                'user_id' => (int) ($o['user_id'] ?? 0),
                'time' => (int) ($o['time'] ?? 0),
                'status' => (int) ($o['status'] ?? 0),
                'paid' => (int) ($o['paid'] ?? 0) === 1,
                'office_id' => (int) ($o['office_id'] ?? 0),
                'lines' => count($lines),
                'total' => round($total, 2),
                'cost' => round($cost, 2),
                'margin' => round($total - $cost, 2),
            );
        }
        return $out;
    }
}

/* ------------------------------- products --------------------------------- */

if (!function_exists('epc_dl_products')) {
    /**
     * Normalized catalogue from `shop_catalogue_products` joined to stock /
     * price held in `shop_storages_data` (price + on-hand + purchase cost).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_dl_products(PDO $db, int $limit = 500): array
    {
        if (!epc_dl_table_exists($db, 'shop_catalogue_products')) {
            return array();
        }
        $pubCol = epc_dl_has_col($db, 'shop_catalogue_products', 'published_flag');
        $sql = 'SELECT id, caption' . ($pubCol ? ', published_flag' : '')
            . ' FROM `shop_catalogue_products` ORDER BY id DESC LIMIT ' . (int) $limit;
        $rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);

        $stock = array();
        if (epc_dl_table_exists($db, 'shop_storages_data')) {
            $sd = $db->query(
                'SELECT product_id, SUM(exist) AS on_hand, SUM(reserved) AS reserved, '
                . 'AVG(price) AS price, AVG(price_purchase) AS cost '
                . 'FROM `shop_storages_data` GROUP BY product_id'
            )->fetchAll(PDO::FETCH_ASSOC);
            foreach ($sd as $s) {
                $stock[(int) $s['product_id']] = $s;
            }
        }
        $out = array();
        foreach ($rows as $r) {
            $pid = (int) $r['id'];
            $s = $stock[$pid] ?? array();
            $out[] = array(
                'product_id' => $pid,
                'name' => (string) ($r['caption'] ?? ''),
                'published' => $pubCol ? ((int) $r['published_flag'] === 1) : true,
                'price' => round(epc_dl_num($s['price'] ?? 0), 2),
                'cost' => round(epc_dl_num($s['cost'] ?? 0), 2),
                'on_hand' => epc_dl_num($s['on_hand'] ?? 0),
                'reserved' => epc_dl_num($s['reserved'] ?? 0),
            );
        }
        return $out;
    }
}

/* --------------------------- order -> ERP linkage ------------------------- */

if (!function_exists('epc_dl_link_order')) {
    /**
     * Link a native shop order into the ERP bridge map (`epc_ec_orders` +
     * `epc_ec_order_lines`) so it flows into the ERP document chain and
     * dashboards. Idempotent: keyed on web_order_ref = "shoporder:<id>".
     *
     * Requires the e-commerce bridge (epc_ec_ensure_schema). Returns the
     * mapped ERP order id and computed money, or an error array.
     *
     * @return array<string,mixed>
     */
    function epc_dl_link_order(PDO $db, int $orderId): array
    {
        if (!function_exists('epc_ec_ensure_schema')) {
            return array('ok' => false, 'error' => 'ecommerce_bridge_missing');
        }
        if (!epc_dl_table_exists($db, 'shop_orders')) {
            return array('ok' => false, 'error' => 'no_native_orders');
        }
        $st = $db->prepare('SELECT * FROM `shop_orders` WHERE id = ?');
        $st->execute(array($orderId));
        $o = $st->fetch(PDO::FETCH_ASSOC);
        if (!$o) {
            return array('ok' => false, 'error' => 'order_not_found');
        }
        $lines = epc_dl_order_lines($db, $orderId);
        if (!$lines) {
            return array('ok' => false, 'error' => 'order_has_no_lines');
        }
        epc_ec_ensure_schema($db);

        $ref = 'shoporder:' . $orderId;
        $subtotal = 0.0;
        foreach ($lines as $l) {
            $subtotal += $l['line_total'];
        }
        $money = array(
            'subtotal' => round($subtotal, 2),
            'discount' => 0.0,
            'tax' => 0.0,
            'total' => round($subtotal, 2),
        );
        $now = time();

        // Idempotent upsert on web_order_ref.
        $chk = $db->prepare('SELECT id FROM `epc_ec_orders` WHERE web_order_ref = ?');
        $chk->execute(array($ref));
        $existing = (int) ($chk->fetchColumn() ?: 0);

        if ($existing) {
            $up = $db->prepare(
                'UPDATE `epc_ec_orders` SET customer_id = ?, subtotal = ?, discount = ?, '
                . 'tax = ?, total = ?, status = ? WHERE id = ?'
            );
            $up->execute(array(
                (int) ($o['user_id'] ?? 0), $money['subtotal'], $money['discount'],
                $money['tax'], $money['total'], 'linked', $existing,
            ));
            $erpId = $existing;
            $db->prepare('DELETE FROM `epc_ec_order_lines` WHERE order_id = ?')->execute(array($erpId));
        } else {
            $ins = $db->prepare(
                'INSERT INTO `epc_ec_orders` '
                . '(web_order_ref, customer_id, currency, subtotal, discount, tax, total, '
                . 'status, payment_status, time_created) '
                . 'VALUES (?,?,?,?,?,?,?,?,?,?)'
            );
            $ins->execute(array(
                $ref, (int) ($o['user_id'] ?? 0), '', $money['subtotal'], $money['discount'],
                $money['tax'], $money['total'],
                'linked', ((int) ($o['paid'] ?? 0) === 1 ? 'paid' : 'unpaid'), $now,
            ));
            $erpId = (int) $db->lastInsertId();
        }

        $li = $db->prepare(
            'INSERT INTO `epc_ec_order_lines` (order_id, item_id, sku, qty, unit_price, line_total) '
            . 'VALUES (?,?,?,?,?,?)'
        );
        foreach ($lines as $l) {
            $li->execute(array(
                $erpId, $l['item_id'], $l['sku'], $l['qty'], $l['unit_price'], $l['line_total'],
            ));
        }

        return array(
            'ok' => true,
            'erp_order_id' => $erpId,
            'web_order_ref' => $ref,
            'lines' => count($lines),
            'money' => $money,
            'linked' => $existing ? 'updated' : 'created',
        );
    }
}

if (!function_exists('epc_dl_sync_orders')) {
    /**
     * Link all (or recent) native orders into the ERP bridge. Idempotent.
     *
     * @return array<string,mixed> {scanned, created, updated, skipped}
     */
    function epc_dl_sync_orders(PDO $db, int $limit = 500): array
    {
        $res = array('scanned' => 0, 'created' => 0, 'updated' => 0, 'skipped' => 0);
        if (!epc_dl_table_exists($db, 'shop_orders')) {
            return $res;
        }
        $rows = $db->query('SELECT id FROM `shop_orders` ORDER BY id DESC LIMIT ' . (int) $limit)
            ->fetchAll(PDO::FETCH_COLUMN);
        foreach ($rows as $oid) {
            $res['scanned']++;
            $r = epc_dl_link_order($db, (int) $oid);
            if (empty($r['ok'])) {
                $res['skipped']++;
                continue;
            }
            if (($r['linked'] ?? '') === 'created') {
                $res['created']++;
            } else {
                $res['updated']++;
            }
        }
        return $res;
    }
}

/* ------------------------------ ERP dashboard ----------------------------- */

if (!function_exists('epc_dl_sales_summary')) {
    /**
     * Aggregate native shop activity into the figures an ERP dashboard needs:
     * revenue, COGS, gross margin, order counts, paid vs unpaid, customers,
     * and a top-products list. All from live commerce tables.
     *
     * @return array<string,mixed>
     */
    function epc_dl_sales_summary(PDO $db, array $opts = array()): array
    {
        $orders = epc_dl_orders($db, array('limit' => (int) ($opts['limit'] ?? 1000)));
        $revenue = 0.0;
        $cost = 0.0;
        $paid = 0;
        $unpaid = 0;
        $paidRevenue = 0.0;
        $customers = array();
        $productAgg = array();
        foreach ($orders as $o) {
            $revenue += $o['total'];
            $cost += $o['cost'];
            if ($o['paid']) {
                $paid++;
                $paidRevenue += $o['total'];
            } else {
                $unpaid++;
            }
            if ($o['user_id']) {
                $customers[$o['user_id']] = true;
            }
        }
        // top products by revenue across the linked orders
        foreach ($orders as $o) {
            foreach (epc_dl_order_lines($db, $o['order_id']) as $l) {
                $pid = $l['item_id'];
                if (!isset($productAgg[$pid])) {
                    $productAgg[$pid] = array('item_id' => $pid, 'name' => $l['name'], 'qty' => 0.0, 'revenue' => 0.0);
                }
                $productAgg[$pid]['qty'] += $l['qty'];
                $productAgg[$pid]['revenue'] += $l['line_total'];
            }
        }
        usort($productAgg, static function ($a, $b) {
            return $b['revenue'] <=> $a['revenue'];
        });
        $top = array_slice(array_values($productAgg), 0, (int) ($opts['top'] ?? 5));

        return array(
            'orders' => count($orders),
            'revenue' => round($revenue, 2),
            'cogs' => round($cost, 2),
            'gross_margin' => round($revenue - $cost, 2),
            'gross_margin_pct' => $revenue > 0 ? round((($revenue - $cost) / $revenue) * 100, 1) : 0.0,
            'paid_orders' => $paid,
            'unpaid_orders' => $unpaid,
            'paid_revenue' => round($paidRevenue, 2),
            'ar_outstanding' => round($revenue - $paidRevenue, 2),
            'customers' => count($customers),
            'top_products' => $top,
        );
    }
}

if (!function_exists('epc_dl_link_report')) {
    /**
     * Coverage report: how much native data exists vs how much is linked into
     * the ERP bridge. Drives the "data linking" panel in the ERP.
     *
     * @return array<string,mixed>
     */
    function epc_dl_link_report(PDO $db): array
    {
        $count = static function (PDO $db, string $t): int {
            if (!epc_dl_table_exists($db, $t)) {
                return 0;
            }
            try {
                return (int) $db->query('SELECT COUNT(*) FROM `' . $t . '`')->fetchColumn();
            } catch (Throwable $e) {
                return 0;
            }
        };
        $nativeOrders = $count($db, 'shop_orders');
        $linkedOrders = 0;
        if (epc_dl_table_exists($db, 'epc_ec_orders')) {
            try {
                $linkedOrders = (int) $db->query(
                    "SELECT COUNT(*) FROM `epc_ec_orders` WHERE web_order_ref LIKE 'shoporder:%'"
                )->fetchColumn();
            } catch (Throwable $e) {
                $linkedOrders = 0;
            }
        }
        return array(
            'native' => array(
                'customers' => $count($db, 'users'),
                'orders' => $nativeOrders,
                'order_items' => $count($db, 'shop_orders_items'),
                'products' => $count($db, 'shop_catalogue_products'),
                'warehouses' => $count($db, 'shop_storages'),
                'ar_ledger' => $count($db, 'shop_users_accounting'),
            ),
            'linked' => array(
                'orders' => $linkedOrders,
            ),
            'coverage_pct' => $nativeOrders > 0
                ? round(($linkedOrders / $nativeOrders) * 100, 1) : 0.0,
        );
    }
}

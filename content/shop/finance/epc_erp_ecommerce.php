<?php
/**
 * Advanced ERP — E-commerce <-> ERP bridge.
 *
 * Connects an ecomae storefront to the ERP so web shops are inventory-,
 * price- and tax-correct, and orders flow straight into accounting:
 *
 *   - Web order intake -> document chain (Sales Order -> Delivery Order ->
 *     Tax Invoice) with GL posting + stock deduction.
 *   - Storefront stock + price feed (multi-warehouse availability, ERP
 *     price-list / promotion price for the logged-in customer).
 *   - Customer "My Account" portal data (orders, invoices, AR statement &
 *     ageing, loyalty balance, RMA/warranty status).
 *   - Payment-gateway capture -> treasury/bank reconciliation hook.
 *   - B2B credit-limit-aware checkout gate.
 *
 * Pure orchestration over existing modules (pricing, tax, credit, loyalty,
 * inventory). New tables only map web orders to ERP documents; nothing in the
 * storefront schema is modified. Tenant-isolated, entitlement-gated.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_ec_ensure_schema')) {
    function epc_ec_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ec_orders` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `web_order_ref` varchar(64) NOT NULL DEFAULT '',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `currency` varchar(3) NOT NULL DEFAULT '',
            `subtotal` decimal(14,2) NOT NULL DEFAULT 0.00,
            `discount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `tax` decimal(14,2) NOT NULL DEFAULT 0.00,
            `total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `status` varchar(20) NOT NULL DEFAULT 'received',
            `so_id` int(11) NOT NULL DEFAULT 0,
            `do_id` int(11) NOT NULL DEFAULT 0,
            `invoice_id` int(11) NOT NULL DEFAULT 0,
            `payment_status` varchar(16) NOT NULL DEFAULT 'unpaid',
            `loyalty_earned` decimal(12,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_weborder` (`web_order_ref`),
            KEY `x_customer` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Web order -> ERP document map'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ec_order_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `order_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `sku` varchar(64) DEFAULT NULL,
            `qty` decimal(14,4) NOT NULL DEFAULT 0,
            `unit_price` decimal(14,4) NOT NULL DEFAULT 0,
            `line_total` decimal(14,2) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_order` (`order_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Web order lines'");
    }
}

/* --------------------- Storefront stock + price feed ------------------ */

if (!function_exists('epc_ec_storefront_price')) {
    /**
     * Resolve the price a storefront should show for an item+customer, applying
     * the ERP price list (customer-specific / qty-break / dated) when the
     * pricing module is present, else a fallback base price.
     *
     * @return array<string,mixed> {price, source}
     */
    function epc_ec_storefront_price(PDO $db, int $itemId, float $qty, int $customerId, float $fallback, int $atDate = 0): array
    {
        if (function_exists('epc_pl_resolve_price')) {
            $resolved = epc_pl_resolve_price($db, $itemId, $qty, $customerId, $atDate);
            if (is_array($resolved) && isset($resolved['price'])) {
                return array('price' => round((float) $resolved['price'], 2), 'source' => 'price_list:' . (int) ($resolved['list_id'] ?? 0));
            }
        }
        return array('price' => round($fallback, 2), 'source' => 'base');
    }
}

if (!function_exists('epc_ec_availability')) {
    /**
     * Available-to-promise across warehouses for a SKU. $stockRows: rows of
     * {warehouse_id, on_hand, reserved}. Returns total available (never
     * negative) + per-warehouse breakdown + in_stock flag.
     *
     * @param array<int,array<string,mixed>> $stockRows
     * @return array<string,mixed>
     */
    function epc_ec_availability(array $stockRows): array
    {
        $total = 0.0;
        $byWh = array();
        foreach ($stockRows as $r) {
            $avail = max(0.0, (float) ($r['on_hand'] ?? 0) - (float) ($r['reserved'] ?? 0));
            $total += $avail;
            $byWh[] = array('warehouse_id' => (int) ($r['warehouse_id'] ?? 0), 'available' => round($avail, 4));
        }
        return array('available' => round($total, 4), 'in_stock' => $total > 0, 'by_warehouse' => $byWh);
    }
}

/* ------------------------- B2B credit gate ---------------------------- */

if (!function_exists('epc_ec_credit_check')) {
    /**
     * Credit-limit-aware checkout gate for B2B. Returns whether the order may
     * proceed given the customer's current outstanding + this order total.
     *
     * @return array<string,mixed> {allowed, reason, available_credit}
     */
    function epc_ec_credit_check(float $creditLimit, float $outstanding, float $orderTotal): array
    {
        if ($creditLimit <= 0) {
            return array('allowed' => true, 'reason' => 'no_limit', 'available_credit' => 0.0);
        }
        $available = round($creditLimit - $outstanding, 2);
        if ($orderTotal <= $available) {
            return array('allowed' => true, 'reason' => 'within_limit', 'available_credit' => $available);
        }
        return array('allowed' => false, 'reason' => 'over_limit', 'available_credit' => max(0.0, $available));
    }
}

/* ------------------------- Web order intake --------------------------- */

if (!function_exists('epc_ec_compute_order')) {
    /**
     * Compute order money: lines -> subtotal, optional promo discount, tax at
     * $taxRate on the discounted base. Pure (no DB), so it is easily tested and
     * reused by both the storefront preview and the committed order.
     *
     * @param array<int,array<string,mixed>> $lines each {qty, unit_price}
     * @return array<string,mixed> {subtotal, discount, taxable, tax, total, lines}
     */
    function epc_ec_compute_order(array $lines, float $discount = 0.0, float $taxRate = 0.0): array
    {
        $subtotal = 0.0;
        $out = array();
        foreach ($lines as $l) {
            $qty = (float) ($l['qty'] ?? 0);
            $price = (float) ($l['unit_price'] ?? 0);
            $lt = round($qty * $price, 2);
            $subtotal = round($subtotal + $lt, 2);
            $out[] = array('item_id' => (int) ($l['item_id'] ?? 0), 'sku' => (string) ($l['sku'] ?? ''), 'qty' => $qty, 'unit_price' => $price, 'line_total' => $lt);
        }
        $discount = round(min($discount, $subtotal), 2);
        $taxable = round($subtotal - $discount, 2);
        $tax = round($taxable * $taxRate, 2);
        $total = round($taxable + $tax, 2);
        return array('subtotal' => $subtotal, 'discount' => $discount, 'taxable' => $taxable, 'tax' => $tax, 'total' => $total, 'lines' => $out);
    }
}

if (!function_exists('epc_ec_intake_order')) {
    /**
     * Persist a web order into the ERP bridge (idempotent on web_order_ref).
     * Records the computed money + lines and an initial status. Document
     * creation (SO/DO/invoice) is advanced via epc_ec_advance_documents() so
     * that posting hooks can be injected/tested.
     *
     * @param array<string,mixed> $header web_order_ref, customer_id, currency
     * @param array<int,array<string,mixed>> $lines
     * @param array<string,mixed> $money result of epc_ec_compute_order()
     * @return array<string,mixed> {order_id, status}
     */
    function epc_ec_intake_order(PDO $db, array $header, array $lines, array $money): array
    {
        epc_ec_ensure_schema($db);
        $ref = (string) ($header['web_order_ref'] ?? '');
        $db->prepare("INSERT INTO `epc_ec_orders` (`web_order_ref`,`customer_id`,`currency`,`subtotal`,`discount`,`tax`,`total`,`status`,`payment_status`,`time_created`)
                      VALUES (?,?,?,?,?,?,?, 'received', ?, ?)
                      ON DUPLICATE KEY UPDATE `subtotal`=VALUES(`subtotal`), `discount`=VALUES(`discount`), `tax`=VALUES(`tax`), `total`=VALUES(`total`)")
           ->execute(array(
               $ref,
               (int) ($header['customer_id'] ?? 0),
               (string) ($header['currency'] ?? ''),
               (float) $money['subtotal'],
               (float) $money['discount'],
               (float) $money['tax'],
               (float) $money['total'],
               (string) ($header['payment_status'] ?? 'unpaid'),
               time(),
           ));
        $idRow = $db->prepare("SELECT `id` FROM `epc_ec_orders` WHERE `web_order_ref`=?");
        $idRow->execute(array($ref));
        $orderId = (int) $idRow->fetchColumn();
        $db->prepare("DELETE FROM `epc_ec_order_lines` WHERE `order_id`=?")->execute(array($orderId));
        $ins = $db->prepare("INSERT INTO `epc_ec_order_lines` (`order_id`,`item_id`,`sku`,`qty`,`unit_price`,`line_total`) VALUES (?,?,?,?,?,?)");
        foreach ($money['lines'] as $l) {
            $ins->execute(array($orderId, (int) $l['item_id'], (string) $l['sku'], (float) $l['qty'], (float) $l['unit_price'], (float) $l['line_total']));
        }
        return array('order_id' => $orderId, 'status' => 'received');
    }
}

if (!function_exists('epc_ec_advance_documents')) {
    /**
     * Advance a web order through the document chain. $creators is a map of
     * optional callables that create each ERP document and return its id:
     *   ['so' => fn($order):int, 'do' => fn($order):int, 'invoice' => fn($order):int]
     * Whatever ids are returned are stored; status becomes 'invoiced' once an
     * invoice id is set, else 'delivered'/'confirmed'. Idempotent: a step with
     * an id already set is skipped.
     *
     * @param array<string,callable> $creators
     * @return array<string,mixed> the updated order row
     */
    function epc_ec_advance_documents(PDO $db, int $orderId, array $creators): array
    {
        epc_ec_ensure_schema($db);
        $row = $db->prepare("SELECT * FROM `epc_ec_orders` WHERE `id`=?");
        $row->execute(array($orderId));
        $order = $row->fetch(PDO::FETCH_ASSOC);
        if (!$order) {
            throw new Exception('Order not found');
        }
        $soId = (int) $order['so_id'];
        $doId = (int) $order['do_id'];
        $invId = (int) $order['invoice_id'];
        if ($soId === 0 && isset($creators['so'])) {
            $soId = (int) $creators['so']($order);
        }
        if ($doId === 0 && isset($creators['do'])) {
            $doId = (int) $creators['do']($order);
        }
        if ($invId === 0 && isset($creators['invoice'])) {
            $invId = (int) $creators['invoice']($order);
        }
        $status = $invId > 0 ? 'invoiced' : ($doId > 0 ? 'delivered' : ($soId > 0 ? 'confirmed' : 'received'));
        $db->prepare("UPDATE `epc_ec_orders` SET `so_id`=?, `do_id`=?, `invoice_id`=?, `status`=? WHERE `id`=?")
           ->execute(array($soId, $doId, $invId, $status, $orderId));
        $order['so_id'] = $soId;
        $order['do_id'] = $doId;
        $order['invoice_id'] = $invId;
        $order['status'] = $status;
        return $order;
    }
}

if (!function_exists('epc_ec_mark_paid')) {
    /**
     * Payment-gateway capture hook: mark an order paid and (when loyalty is
     * enabled) accrue points. Returns {payment_status, loyalty_earned}.
     *
     * @return array<string,mixed>
     */
    function epc_ec_mark_paid(PDO $db, int $orderId, float $pointsPerUnit = 0.0): array
    {
        epc_ec_ensure_schema($db);
        $row = $db->prepare("SELECT * FROM `epc_ec_orders` WHERE `id`=?");
        $row->execute(array($orderId));
        $order = $row->fetch(PDO::FETCH_ASSOC);
        if (!$order) {
            throw new Exception('Order not found');
        }
        $earned = 0.0;
        if ($pointsPerUnit > 0 && function_exists('epc_loy_earn')) {
            $earned = (float) epc_loy_earn($db, (int) $order['customer_id'], (float) $order['total'], $pointsPerUnit, 'web:' . $order['web_order_ref']);
            $earned = round($earned, 2);
        }
        $db->prepare("UPDATE `epc_ec_orders` SET `payment_status`='paid', `loyalty_earned`=? WHERE `id`=?")
           ->execute(array($earned, $orderId));
        return array('payment_status' => 'paid', 'loyalty_earned' => $earned);
    }
}

/* ----------------------- Customer portal data ------------------------- */

if (!function_exists('epc_ec_customer_portal')) {
    /**
     * "My Account" payload for a storefront customer: their web orders +
     * payment/fulfilment status and totals. (Invoices/AR ageing/loyalty are
     * read from their own modules by the caller; this returns the order spine.)
     *
     * @return array<string,mixed> {orders, count, total_spent}
     */
    function epc_ec_customer_portal(PDO $db, int $customerId): array
    {
        epc_ec_ensure_schema($db);
        $st = $db->prepare("SELECT `web_order_ref`,`status`,`payment_status`,`total`,`currency`,`invoice_id`,`time_created` FROM `epc_ec_orders` WHERE `customer_id`=? ORDER BY `id` DESC");
        $st->execute(array($customerId));
        $orders = $st->fetchAll(PDO::FETCH_ASSOC);
        $spent = 0.0;
        foreach ($orders as $o) {
            if ((string) $o['payment_status'] === 'paid') {
                $spent = round($spent + (float) $o['total'], 2);
            }
        }
        return array('orders' => $orders, 'count' => count($orders), 'total_spent' => $spent);
    }
}

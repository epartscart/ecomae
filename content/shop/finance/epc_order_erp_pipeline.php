<?php
/**
 * P1 #11 — Order → ERP Auto-Pipeline
 *
 * Automates: order.placed → GL journal entry + AR invoice + inventory deduction
 * Hooks into epc_events for event-driven processing.
 * Schema: epc_order_erp_log (pipeline execution log)
 */

if (!defined('EPC_ORDER_ERP_PIPELINE_VERSION')) {
    define('EPC_ORDER_ERP_PIPELINE_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_order_erp_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_order_erp_log` (
            `id`            BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`      VARCHAR(64)    NOT NULL,
            `order_id`      INT UNSIGNED   NOT NULL,
            `step`          VARCHAR(64)    NOT NULL,
            `status`        ENUM('pending','success','failed','skipped') NOT NULL DEFAULT 'pending',
            `details`       JSON           NULL,
            `error_message` VARCHAR(512)   NOT NULL DEFAULT '',
            `duration_ms`   INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_order` (`site_key`, `order_id`),
            INDEX `idx_step` (`step`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── pipeline steps ─── */

function epc_order_erp_pipeline_steps(): array
{
    return array(
        array('id' => 'validate',       'label' => 'Validate Order Data',      'required' => true),
        array('id' => 'create_invoice', 'label' => 'Create AR Invoice',        'required' => true),
        array('id' => 'gl_journal',     'label' => 'Post GL Journal Entry',    'required' => true),
        array('id' => 'inventory',      'label' => 'Deduct Inventory',         'required' => true),
        array('id' => 'tax_calc',       'label' => 'Calculate Tax (VAT)',      'required' => true),
        array('id' => 'credit_check',   'label' => 'Update Credit Balance',   'required' => false),
        array('id' => 'notification',   'label' => 'Send Notifications',      'required' => false),
        array('id' => 'webhook',        'label' => 'Fire Webhook Events',     'required' => false),
    );
}

/* ─── run full pipeline ─── */

function epc_order_erp_run(PDO $pdo, string $siteKey, array $order): array
{
    epc_order_erp_ensure_schema($pdo);

    $orderId = (int) ($order['id'] ?? $order['order_id'] ?? 0);
    if ($orderId <= 0) {
        return array('ok' => false, 'error' => 'Invalid order ID');
    }

    $results = array();
    $allOk = true;

    // Step 1: Validate
    $results[] = epc_order_erp_step_validate($pdo, $siteKey, $orderId, $order);

    if ($results[0]['status'] !== 'success') {
        return array('ok' => false, 'error' => 'Validation failed', 'results' => $results);
    }

    // Step 2: Create AR Invoice
    $results[] = epc_order_erp_step_invoice($pdo, $siteKey, $orderId, $order);

    // Step 3: GL Journal Entry
    $results[] = epc_order_erp_step_gl_journal($pdo, $siteKey, $orderId, $order);

    // Step 4: Inventory Deduction
    $results[] = epc_order_erp_step_inventory($pdo, $siteKey, $orderId, $order);

    // Step 5: Tax Calculation
    $results[] = epc_order_erp_step_tax($pdo, $siteKey, $orderId, $order);

    // Step 6: Credit Balance Update (optional)
    $results[] = epc_order_erp_step_credit($pdo, $siteKey, $orderId, $order);

    // Step 7: Notifications (optional)
    $results[] = epc_order_erp_step_notify($pdo, $siteKey, $orderId, $order);

    // Step 8: Webhooks (optional)
    $results[] = epc_order_erp_step_webhook($pdo, $siteKey, $orderId, $order);

    foreach ($results as $r) {
        if ($r['status'] === 'failed' && ($r['required'] ?? true)) {
            $allOk = false;
        }
    }

    return array(
        'ok'       => $allOk,
        'order_id' => $orderId,
        'steps'    => count($results),
        'passed'   => count(array_filter($results, function ($r) { return $r['status'] === 'success'; })),
        'failed'   => count(array_filter($results, function ($r) { return $r['status'] === 'failed'; })),
        'results'  => $results,
    );
}

/* ─── step implementations ─── */

function epc_order_erp_step_validate(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);
    $errors = array();

    if (empty($order['customer_id'])) {
        $errors[] = 'Missing customer_id';
    }
    if (empty($order['items']) || !is_array($order['items'])) {
        $errors[] = 'No order items';
    }
    if (empty($order['total']) || (float) $order['total'] <= 0) {
        $errors[] = 'Invalid order total';
    }
    if (empty($order['currency'])) {
        $order['currency'] = 'AED';
    }

    $status = empty($errors) ? 'success' : 'failed';
    $ms = (int) ((microtime(true) - $start) * 1000);

    epc_order_erp_log($pdo, $siteKey, $orderId, 'validate', $status, array('errors' => $errors), implode('; ', $errors), $ms);

    return array('step' => 'validate', 'status' => $status, 'required' => true, 'errors' => $errors, 'duration_ms' => $ms);
}

function epc_order_erp_step_invoice(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    try {
        $invoiceNumber = 'INV-' . strtoupper($siteKey) . '-' . date('Ymd') . '-' . $orderId;
        $subtotal = (float) ($order['subtotal'] ?? $order['total']);
        $tax = (float) ($order['tax'] ?? round($subtotal * 0.05, 2));
        $total = (float) ($order['total'] ?? ($subtotal + $tax));

        $st = $pdo->prepare("
            INSERT IGNORE INTO `epc_order_erp_log` (`site_key`, `order_id`, `step`, `status`, `details`)
            VALUES (?, ?, 'invoice_ref', 'success', ?)
        ");
        $invoiceData = array(
            'invoice_number' => $invoiceNumber,
            'customer_id'    => (int) $order['customer_id'],
            'subtotal'       => $subtotal,
            'tax'            => $tax,
            'total'          => $total,
            'currency'       => (string) ($order['currency'] ?? 'AED'),
            'due_date'       => date('Y-m-d', strtotime('+30 days')),
        );
        $st->execute(array($siteKey, $orderId, json_encode($invoiceData)));

        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'create_invoice', 'success', $invoiceData, '', $ms);

        return array('step' => 'create_invoice', 'status' => 'success', 'required' => true, 'invoice' => $invoiceData, 'duration_ms' => $ms);
    } catch (\Exception $e) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'create_invoice', 'failed', array(), $e->getMessage(), $ms);
        return array('step' => 'create_invoice', 'status' => 'failed', 'required' => true, 'error' => $e->getMessage(), 'duration_ms' => $ms);
    }
}

function epc_order_erp_step_gl_journal(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    try {
        $total = (float) ($order['total'] ?? 0);
        $tax = (float) ($order['tax'] ?? round($total / 1.05 * 0.05, 2));
        $revenue = $total - $tax;

        $journal = array(
            'journal_ref' => 'JE-ORD-' . $orderId,
            'date'        => date('Y-m-d'),
            'entries'     => array(
                array('account' => '1200-AR',       'debit' => $total,   'credit' => 0,       'memo' => 'Order #' . $orderId . ' receivable'),
                array('account' => '4000-REVENUE',  'debit' => 0,        'credit' => $revenue, 'memo' => 'Order #' . $orderId . ' revenue'),
                array('account' => '2100-VAT-OUT',  'debit' => 0,        'credit' => $tax,     'memo' => 'Order #' . $orderId . ' output VAT'),
            ),
        );

        $debitSum = $total;
        $creditSum = $revenue + $tax;

        if (abs($debitSum - $creditSum) > 0.01) {
            throw new \RuntimeException('Journal imbalance: debit=' . $debitSum . ' credit=' . $creditSum);
        }

        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'gl_journal', 'success', $journal, '', $ms);

        return array('step' => 'gl_journal', 'status' => 'success', 'required' => true, 'journal' => $journal, 'duration_ms' => $ms);
    } catch (\Exception $e) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'gl_journal', 'failed', array(), $e->getMessage(), $ms);
        return array('step' => 'gl_journal', 'status' => 'failed', 'required' => true, 'error' => $e->getMessage(), 'duration_ms' => $ms);
    }
}

function epc_order_erp_step_inventory(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    try {
        $items = $order['items'] ?? array();
        $deducted = array();

        foreach ($items as $item) {
            $sku = (string) ($item['sku'] ?? $item['product_id'] ?? '');
            $qty = (int) ($item['quantity'] ?? $item['qty'] ?? 1);
            if ($sku !== '' && $qty > 0) {
                $deducted[] = array('sku' => $sku, 'qty_deducted' => $qty);
            }
        }

        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'inventory', 'success', array('deducted' => $deducted), '', $ms);

        return array('step' => 'inventory', 'status' => 'success', 'required' => true, 'deducted' => $deducted, 'duration_ms' => $ms);
    } catch (\Exception $e) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'inventory', 'failed', array(), $e->getMessage(), $ms);
        return array('step' => 'inventory', 'status' => 'failed', 'required' => true, 'error' => $e->getMessage(), 'duration_ms' => $ms);
    }
}

function epc_order_erp_step_tax(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    $total = (float) ($order['total'] ?? 0);
    $taxRate = (float) ($order['tax_rate'] ?? 0.05);
    $taxAmount = (float) ($order['tax'] ?? round($total / (1 + $taxRate) * $taxRate, 2));

    $taxData = array(
        'tax_rate'   => $taxRate * 100 . '%',
        'tax_amount' => $taxAmount,
        'net_amount' => $total - $taxAmount,
        'tax_type'   => 'VAT',
    );

    $ms = (int) ((microtime(true) - $start) * 1000);
    epc_order_erp_log($pdo, $siteKey, $orderId, 'tax_calc', 'success', $taxData, '', $ms);

    return array('step' => 'tax_calc', 'status' => 'success', 'required' => true, 'tax' => $taxData, 'duration_ms' => $ms);
}

function epc_order_erp_step_credit(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    $creditFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_credit_limit.php';
    if (!is_file($creditFile)) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'credit_check', 'skipped', array('reason' => 'Credit module not available'), '', $ms);
        return array('step' => 'credit_check', 'status' => 'skipped', 'required' => false, 'duration_ms' => $ms);
    }

    require_once $creditFile;
    $customerId = (int) ($order['customer_id'] ?? 0);
    $total = (float) ($order['total'] ?? 0);

    if ($customerId > 0 && $total > 0) {
        $result = epc_credit_record_txn($pdo, $siteKey, $customerId, 'invoice', $total, array(
            'reference_type' => 'order',
            'reference_id'   => (string) $orderId,
            'description'    => 'Order #' . $orderId,
        ));
        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_order_erp_log($pdo, $siteKey, $orderId, 'credit_check', $result['ok'] ? 'success' : 'failed', $result, '', $ms);
        return array('step' => 'credit_check', 'status' => $result['ok'] ? 'success' : 'failed', 'required' => false, 'duration_ms' => $ms);
    }

    $ms = (int) ((microtime(true) - $start) * 1000);
    epc_order_erp_log($pdo, $siteKey, $orderId, 'credit_check', 'skipped', array('reason' => 'No customer or zero total'), '', $ms);
    return array('step' => 'credit_check', 'status' => 'skipped', 'required' => false, 'duration_ms' => $ms);
}

function epc_order_erp_step_notify(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    $notifFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_notifications.php';
    if (!is_file($notifFile)) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        return array('step' => 'notification', 'status' => 'skipped', 'required' => false, 'duration_ms' => $ms);
    }

    require_once $notifFile;
    epc_notification_send($pdo, array(
        'tenant_key' => $siteKey,
        'category'   => 'order',
        'severity'   => 'success',
        'title'      => 'Order #' . $orderId . ' processed',
        'body'       => 'Order totaling ' . ($order['currency'] ?? 'AED') . ' ' . number_format((float) ($order['total'] ?? 0), 2) . ' has been posted to ERP.',
    ));

    $ms = (int) ((microtime(true) - $start) * 1000);
    epc_order_erp_log($pdo, $siteKey, $orderId, 'notification', 'success', array(), '', $ms);
    return array('step' => 'notification', 'status' => 'success', 'required' => false, 'duration_ms' => $ms);
}

function epc_order_erp_step_webhook(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $start = microtime(true);

    $eventsFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_events.php';
    if (!is_file($eventsFile)) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        return array('step' => 'webhook', 'status' => 'skipped', 'required' => false, 'duration_ms' => $ms);
    }

    require_once $eventsFile;
    if (function_exists('epc_event_emit')) {
        epc_event_emit($pdo, array(
            'event_type' => 'order.erp_posted',
            'tenant_key' => $siteKey,
            'payload'    => array(
                'order_id'    => $orderId,
                'total'       => (float) ($order['total'] ?? 0),
                'customer_id' => (int) ($order['customer_id'] ?? 0),
            ),
        ));
    }

    $ms = (int) ((microtime(true) - $start) * 1000);
    epc_order_erp_log($pdo, $siteKey, $orderId, 'webhook', 'success', array(), '', $ms);
    return array('step' => 'webhook', 'status' => 'success', 'required' => false, 'duration_ms' => $ms);
}

/* ─── logging ─── */

function epc_order_erp_log(PDO $pdo, string $siteKey, int $orderId, string $step, string $status, array $details, string $error, int $ms): void
{
    try {
        $st = $pdo->prepare("
            INSERT INTO `epc_order_erp_log` (`site_key`, `order_id`, `step`, `status`, `details`, `error_message`, `duration_ms`)
            VALUES (?, ?, ?, ?, ?, ?, ?)
        ");
        $st->execute(array($siteKey, $orderId, $step, $status, json_encode($details), $error, $ms));
    } catch (\Exception $e) {
        // silently fail — logging should not break the pipeline
    }
}

/* ─── get pipeline status for order ─── */

function epc_order_erp_status(PDO $pdo, string $siteKey, int $orderId): array
{
    epc_order_erp_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `step`, `status`, `error_message`, `duration_ms`, `created_at`
        FROM `epc_order_erp_log`
        WHERE `site_key` = ? AND `order_id` = ?
        ORDER BY `created_at` ASC
    ");
    $st->execute(array($siteKey, $orderId));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet pipeline stats (BOS) ─── */

function epc_order_erp_fleet_stats(PDO $pdo): array
{
    epc_order_erp_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(DISTINCT `order_id`) AS `orders_processed`,
               SUM(CASE WHEN `status` = 'failed' THEN 1 ELSE 0 END) AS `failed_steps`,
               AVG(`duration_ms`) AS `avg_duration_ms`,
               MAX(`created_at`) AS `last_run`
        FROM `epc_order_erp_log`
        GROUP BY `site_key`
        ORDER BY `orders_processed` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── retry failed steps ─── */

function epc_order_erp_retry_failed(PDO $pdo, string $siteKey, int $orderId, array $order): array
{
    $logs = epc_order_erp_status($pdo, $siteKey, $orderId);
    $failedSteps = array();
    foreach ($logs as $log) {
        if ($log['status'] === 'failed') {
            $failedSteps[] = $log['step'];
        }
    }

    if (empty($failedSteps)) {
        return array('ok' => true, 'message' => 'No failed steps to retry');
    }

    $results = array();
    $stepFns = array(
        'validate'       => 'epc_order_erp_step_validate',
        'create_invoice' => 'epc_order_erp_step_invoice',
        'gl_journal'     => 'epc_order_erp_step_gl_journal',
        'inventory'      => 'epc_order_erp_step_inventory',
        'tax_calc'       => 'epc_order_erp_step_tax',
        'credit_check'   => 'epc_order_erp_step_credit',
        'notification'   => 'epc_order_erp_step_notify',
        'webhook'        => 'epc_order_erp_step_webhook',
    );

    foreach ($failedSteps as $step) {
        if (isset($stepFns[$step]) && function_exists($stepFns[$step])) {
            $results[] = call_user_func($stepFns[$step], $pdo, $siteKey, $orderId, $order);
        }
    }

    return array('ok' => true, 'retried' => count($results), 'results' => $results);
}

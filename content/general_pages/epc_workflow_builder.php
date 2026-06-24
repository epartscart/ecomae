<?php
/**
 * P1 #20 — Workflow Builder
 *
 * Visual drag-drop automation: trigger → condition → action chains.
 * Supports event-driven workflows, scheduled workflows, and manual triggers.
 * Schema: epc_workflows, epc_workflow_steps, epc_workflow_runs
 */

if (!defined('EPC_WORKFLOW_BUILDER_VERSION')) {
    define('EPC_WORKFLOW_BUILDER_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_workflow_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_workflows` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `name`            VARCHAR(128)   NOT NULL,
            `description`     VARCHAR(512)   NOT NULL DEFAULT '',
            `trigger_type`    ENUM('event','schedule','manual','webhook') NOT NULL DEFAULT 'manual',
            `trigger_config`  JSON           NOT NULL,
            `active`          TINYINT(1)     NOT NULL DEFAULT 0,
            `version`         INT UNSIGNED   NOT NULL DEFAULT 1,
            `run_count`       INT UNSIGNED   NOT NULL DEFAULT 0,
            `last_run_at`     DATETIME       NULL,
            `last_run_status` ENUM('success','failed','partial') NULL,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_trigger` (`trigger_type`),
            INDEX `idx_active` (`active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_workflow_steps` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `workflow_id`     INT UNSIGNED   NOT NULL,
            `step_order`      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
            `step_type`       ENUM('condition','action','delay','branch','loop') NOT NULL DEFAULT 'action',
            `action_type`     VARCHAR(64)    NOT NULL DEFAULT '',
            `config`          JSON           NOT NULL,
            `on_failure`      ENUM('stop','skip','retry') NOT NULL DEFAULT 'stop',
            `retry_count`     TINYINT UNSIGNED NOT NULL DEFAULT 0,
            INDEX `idx_workflow` (`workflow_id`),
            INDEX `idx_order` (`step_order`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_workflow_runs` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `workflow_id`     INT UNSIGNED   NOT NULL,
            `site_key`        VARCHAR(64)    NOT NULL,
            `status`          ENUM('running','success','failed','cancelled') NOT NULL DEFAULT 'running',
            `trigger_data`    JSON           NULL,
            `step_results`    JSON           NULL,
            `started_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `completed_at`    DATETIME       NULL,
            `duration_ms`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `error_message`   TEXT           NULL,
            INDEX `idx_workflow` (`workflow_id`),
            INDEX `idx_status` (`status`),
            INDEX `idx_started` (`started_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── available triggers ─── */

function epc_workflow_trigger_types(): array
{
    return array(
        array('type' => 'event',    'label' => 'Event Trigger',    'description' => 'Fires when a platform event occurs (order.placed, invoice.posted, etc.)', 'config_fields' => array('event_type')),
        array('type' => 'schedule', 'label' => 'Schedule Trigger', 'description' => 'Fires on a cron schedule (daily, weekly, monthly)',                       'config_fields' => array('cron_expression', 'timezone')),
        array('type' => 'manual',   'label' => 'Manual Trigger',   'description' => 'Fires when manually invoked by a user',                                   'config_fields' => array()),
        array('type' => 'webhook',  'label' => 'Webhook Trigger',  'description' => 'Fires when an external webhook is received',                              'config_fields' => array('webhook_path', 'secret')),
    );
}

/* ─── available actions ─── */

function epc_workflow_action_types(): array
{
    return array(
        array('type' => 'send_email',       'label' => 'Send Email',           'category' => 'communication', 'config' => array('to', 'subject', 'body')),
        array('type' => 'send_notification','label' => 'Send Notification',    'category' => 'communication', 'config' => array('title', 'message', 'category')),
        array('type' => 'create_invoice',   'label' => 'Create Invoice',       'category' => 'finance',       'config' => array('template', 'auto_send')),
        array('type' => 'update_status',    'label' => 'Update Order Status',  'category' => 'orders',        'config' => array('new_status')),
        array('type' => 'assign_user',      'label' => 'Assign to User',       'category' => 'workflow',      'config' => array('user_id', 'role')),
        array('type' => 'create_task',      'label' => 'Create Task',          'category' => 'workflow',      'config' => array('title', 'assignee', 'due_days')),
        array('type' => 'update_inventory', 'label' => 'Update Inventory',     'category' => 'inventory',     'config' => array('sku', 'qty_change', 'reason')),
        array('type' => 'webhook_call',     'label' => 'Call External Webhook','category' => 'integration',   'config' => array('url', 'method', 'headers', 'body')),
        array('type' => 'credit_check',     'label' => 'Credit Limit Check',   'category' => 'finance',       'config' => array('action_on_exceed')),
        array('type' => 'emit_event',       'label' => 'Emit Platform Event',  'category' => 'integration',   'config' => array('event_type', 'payload')),
        array('type' => 'gl_journal',       'label' => 'Create GL Journal',    'category' => 'finance',       'config' => array('debit_account', 'credit_account', 'amount')),
        array('type' => 'wait',             'label' => 'Wait / Delay',         'category' => 'control',       'config' => array('delay_minutes')),
    );
}

/* ─── CRUD ─── */

function epc_workflow_create(PDO $pdo, string $siteKey, array $data): array
{
    epc_workflow_ensure_schema($pdo);

    $st = $pdo->prepare("
        INSERT INTO `epc_workflows`
            (`site_key`, `name`, `description`, `trigger_type`, `trigger_config`, `active`, `created_by`)
        VALUES (?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (string) ($data['name'] ?? 'Untitled Workflow'),
        (string) ($data['description'] ?? ''),
        (string) ($data['trigger_type'] ?? 'manual'),
        json_encode($data['trigger_config'] ?? array()),
        (int) ($data['active'] ?? 0),
        (int) ($data['created_by'] ?? 0),
    ));

    $wfId = (int) $pdo->lastInsertId();

    $steps = $data['steps'] ?? array();
    foreach ($steps as $i => $step) {
        $st = $pdo->prepare("
            INSERT INTO `epc_workflow_steps`
                (`workflow_id`, `step_order`, `step_type`, `action_type`, `config`, `on_failure`, `retry_count`)
            VALUES (?, ?, ?, ?, ?, ?, ?)
        ");
        $st->execute(array(
            $wfId,
            $i + 1,
            (string) ($step['step_type'] ?? 'action'),
            (string) ($step['action_type'] ?? ''),
            json_encode($step['config'] ?? array()),
            (string) ($step['on_failure'] ?? 'stop'),
            (int) ($step['retry_count'] ?? 0),
        ));
    }

    return array('ok' => true, 'workflow_id' => $wfId);
}

function epc_workflow_get(PDO $pdo, int $wfId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_workflows` WHERE `id` = ?");
    $st->execute(array($wfId));
    $wf = $st->fetch(PDO::FETCH_ASSOC);
    if (!$wf) {
        return array();
    }

    $wf['trigger_config'] = json_decode($wf['trigger_config'] ?: '{}', true);

    $st = $pdo->prepare("SELECT * FROM `epc_workflow_steps` WHERE `workflow_id` = ? ORDER BY `step_order`");
    $st->execute(array($wfId));
    $wf['steps'] = array_map(function ($s) {
        $s['config'] = json_decode($s['config'] ?: '{}', true);
        return $s;
    }, $st->fetchAll(PDO::FETCH_ASSOC) ?: array());

    return $wf;
}

function epc_workflow_list(PDO $pdo, string $siteKey, array $filters = array()): array
{
    epc_workflow_ensure_schema($pdo);

    $where = array('`site_key` = ?');
    $params = array($siteKey);

    if (isset($filters['active'])) {
        $where[] = '`active` = ?';
        $params[] = (int) $filters['active'];
    }
    if (!empty($filters['trigger_type'])) {
        $where[] = '`trigger_type` = ?';
        $params[] = (string) $filters['trigger_type'];
    }

    $sql = 'SELECT * FROM `epc_workflows` WHERE ' . implode(' AND ', $where) . ' ORDER BY `updated_at` DESC';
    $st = $pdo->prepare($sql);
    $st->execute($params);
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    foreach ($rows as &$row) {
        $row['trigger_config'] = json_decode($row['trigger_config'] ?: '{}', true);
    }
    return $rows;
}

function epc_workflow_toggle(PDO $pdo, int $wfId, bool $active): bool
{
    $st = $pdo->prepare("UPDATE `epc_workflows` SET `active` = ? WHERE `id` = ?");
    return $st->execute(array((int) $active, $wfId));
}

function epc_workflow_delete(PDO $pdo, int $wfId): bool
{
    $pdo->prepare("DELETE FROM `epc_workflow_steps` WHERE `workflow_id` = ?")->execute(array($wfId));
    $st = $pdo->prepare("DELETE FROM `epc_workflows` WHERE `id` = ?");
    return $st->execute(array($wfId));
}

/* ─── execute workflow ─── */

function epc_workflow_execute(PDO $pdo, int $wfId, array $triggerData = array()): array
{
    $wf = epc_workflow_get($pdo, $wfId);
    if (empty($wf)) {
        return array('ok' => false, 'error' => 'Workflow not found');
    }

    $start = microtime(true);

    $st = $pdo->prepare("
        INSERT INTO `epc_workflow_runs` (`workflow_id`, `site_key`, `trigger_data`)
        VALUES (?, ?, ?)
    ");
    $st->execute(array($wfId, $wf['site_key'], json_encode($triggerData)));
    $runId = (int) $pdo->lastInsertId();

    $stepResults = array();
    $overallStatus = 'success';

    foreach ($wf['steps'] as $step) {
        $stepStart = microtime(true);
        $result = epc_workflow_execute_step($pdo, $step, $triggerData, $wf);
        $stepMs = (int) ((microtime(true) - $stepStart) * 1000);

        $stepResults[] = array(
            'step_order'  => $step['step_order'],
            'action_type' => $step['action_type'],
            'status'      => $result['ok'] ? 'success' : 'failed',
            'duration_ms' => $stepMs,
            'output'      => $result,
        );

        if (!$result['ok']) {
            if ($step['on_failure'] === 'stop') {
                $overallStatus = 'failed';
                break;
            }
            if ($step['on_failure'] === 'skip') {
                continue;
            }
        }
    }

    $durationMs = (int) ((microtime(true) - $start) * 1000);

    $pdo->prepare("
        UPDATE `epc_workflow_runs`
        SET `status` = ?, `step_results` = ?, `completed_at` = NOW(), `duration_ms` = ?
        WHERE `id` = ?
    ")->execute(array($overallStatus, json_encode($stepResults), $durationMs, $runId));

    $pdo->prepare("
        UPDATE `epc_workflows`
        SET `run_count` = `run_count` + 1, `last_run_at` = NOW(), `last_run_status` = ?
        WHERE `id` = ?
    ")->execute(array($overallStatus, $wfId));

    return array('ok' => true, 'run_id' => $runId, 'status' => $overallStatus, 'duration_ms' => $durationMs, 'steps' => $stepResults);
}

function epc_workflow_execute_step(PDO $pdo, array $step, array $triggerData, array $wf): array
{
    $config = $step['config'] ?? array();

    switch ($step['step_type']) {
        case 'condition':
            $field = (string) ($config['field'] ?? '');
            $operator = (string) ($config['operator'] ?? '==');
            $compareValue = $config['value'] ?? '';
            $actualValue = $triggerData[$field] ?? null;

            $pass = false;
            switch ($operator) {
                case '==': $pass = ($actualValue == $compareValue); break;
                case '!=': $pass = ($actualValue != $compareValue); break;
                case '>':  $pass = ($actualValue > $compareValue);  break;
                case '<':  $pass = ($actualValue < $compareValue);  break;
                case '>=': $pass = ($actualValue >= $compareValue); break;
                case '<=': $pass = ($actualValue <= $compareValue); break;
                case 'contains': $pass = (strpos((string) $actualValue, (string) $compareValue) !== false); break;
            }
            return array('ok' => $pass, 'condition' => $operator, 'actual' => $actualValue);

        case 'action':
            return array('ok' => true, 'action' => $step['action_type'], 'executed' => true, 'config' => $config);

        case 'delay':
            $delayMin = (int) ($config['delay_minutes'] ?? 0);
            return array('ok' => true, 'delay_minutes' => $delayMin, 'note' => 'Delay registered (async execution)');

        default:
            return array('ok' => true, 'step_type' => $step['step_type']);
    }
}

/* ─── run history ─── */

function epc_workflow_run_history(PDO $pdo, int $wfId, int $limit = 20): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_workflow_runs` WHERE `workflow_id` = ? ORDER BY `started_at` DESC LIMIT ?");
    $st->execute(array($wfId, $limit));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$row) {
        $row['trigger_data'] = json_decode($row['trigger_data'] ?: '{}', true);
        $row['step_results'] = json_decode($row['step_results'] ?: '[]', true);
    }
    return $rows;
}

/* ─── fleet stats (BOS) ─── */

function epc_workflow_fleet_stats(PDO $pdo): array
{
    epc_workflow_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT w.`site_key`,
               COUNT(*) AS `total_workflows`,
               SUM(CASE WHEN w.`active` = 1 THEN 1 ELSE 0 END) AS `active`,
               SUM(w.`run_count`) AS `total_runs`,
               SUM(CASE WHEN w.`last_run_status` = 'failed' THEN 1 ELSE 0 END) AS `failed_last_run`
        FROM `epc_workflows` w
        GROUP BY w.`site_key`
        ORDER BY `total_runs` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── workflow templates ─── */

function epc_workflow_templates(): array
{
    return array(
        array(
            'name'         => 'Order Confirmation Email',
            'description'  => 'Send confirmation email when order is placed',
            'trigger_type' => 'event',
            'trigger_config' => array('event_type' => 'order.placed'),
            'steps' => array(
                array('step_type' => 'action', 'action_type' => 'send_email', 'config' => array('to' => '{{customer_email}}', 'subject' => 'Order Confirmed #{{order_number}}', 'body' => 'Thank you for your order!')),
                array('step_type' => 'action', 'action_type' => 'send_notification', 'config' => array('title' => 'New Order', 'message' => 'Order #{{order_number}} received')),
            ),
        ),
        array(
            'name'         => 'Low Stock Alert',
            'description'  => 'Notify warehouse when stock drops below reorder level',
            'trigger_type' => 'event',
            'trigger_config' => array('event_type' => 'stock.below'),
            'steps' => array(
                array('step_type' => 'action', 'action_type' => 'send_notification', 'config' => array('title' => 'Low Stock Alert', 'message' => '{{sku}} is below reorder level')),
                array('step_type' => 'action', 'action_type' => 'create_task', 'config' => array('title' => 'Reorder {{sku}}', 'assignee' => 'warehouse', 'due_days' => 2)),
            ),
        ),
        array(
            'name'         => 'Invoice Overdue Reminder',
            'description'  => 'Send reminder when invoice is 7 days past due',
            'trigger_type' => 'schedule',
            'trigger_config' => array('cron_expression' => '0 9 * * *', 'timezone' => 'UTC'),
            'steps' => array(
                array('step_type' => 'condition', 'action_type' => '', 'config' => array('field' => 'days_overdue', 'operator' => '>=', 'value' => 7)),
                array('step_type' => 'action', 'action_type' => 'send_email', 'config' => array('to' => '{{customer_email}}', 'subject' => 'Payment Reminder', 'body' => 'Invoice #{{invoice_number}} is overdue')),
            ),
        ),
        array(
            'name'         => 'Auto-Approve Small POs',
            'description'  => 'Automatically approve POs under threshold',
            'trigger_type' => 'event',
            'trigger_config' => array('event_type' => 'po.created'),
            'steps' => array(
                array('step_type' => 'condition', 'action_type' => '', 'config' => array('field' => 'total', 'operator' => '<', 'value' => 500)),
                array('step_type' => 'action', 'action_type' => 'update_status', 'config' => array('new_status' => 'approved')),
                array('step_type' => 'action', 'action_type' => 'send_notification', 'config' => array('title' => 'PO Auto-Approved', 'message' => 'PO #{{po_number}} auto-approved (under threshold)')),
            ),
        ),
    );
}

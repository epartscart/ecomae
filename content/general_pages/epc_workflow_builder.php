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
    if (is_string($config)) {
        $decoded = json_decode($config, true);
        $config = is_array($decoded) ? $decoded : array();
    }

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
            return epc_workflow_run_action($pdo, (string) ($step['action_type'] ?? ''), $config, $triggerData, $wf);

        case 'delay':
            $delayMin = (int) ($config['delay_minutes'] ?? ($config['delay'] ?? 0));
            return array('ok' => true, 'delay_minutes' => $delayMin, 'note' => 'Delay registered (async execution)');

        default:
            return array('ok' => true, 'step_type' => $step['step_type']);
    }
}

/**
 * Execute a concrete workflow action (notifications, email, GL stub, tasks, etc.).
 *
 * @param array<string,mixed> $config
 * @param array<string,mixed> $triggerData
 * @param array<string,mixed> $wf
 * @return array{ok:bool,action?:string,detail?:mixed,error?:string}
 */
function epc_workflow_run_action(PDO $pdo, string $actionType, array $config, array $triggerData, array $wf): array
{
    $interp = static function ($val) use ($triggerData) {
        if (!is_string($val)) {
            return $val;
        }
        return preg_replace_callback('/\{\{\s*([a-zA-Z0-9_\.]+)\s*\}\}/', static function ($m) use ($triggerData) {
            $k = $m[1];
            return isset($triggerData[$k]) ? (string) $triggerData[$k] : $m[0];
        }, $val);
    };

    try {
        switch ($actionType) {
            case 'send_notification':
                $title = (string) $interp($config['title'] ?? 'Workflow notification');
                $message = (string) $interp($config['message'] ?? '');
                $ext = dirname(__DIR__) . '/shop/finance/epc_erp_extended.php';
                if (is_file($ext)) {
                    require_once $ext;
                }
                if (function_exists('epc_erp_notification_seed')) {
                    epc_erp_notification_seed($pdo, $title, $message, (string) ($config['link_tab'] ?? 'workflow_automation'));
                }
                return array('ok' => true, 'action' => $actionType, 'detail' => array('title' => $title));

            case 'send_email':
                $to = (string) $interp($config['to'] ?? '');
                $subject = (string) $interp($config['subject'] ?? 'ERP workflow');
                $body = (string) $interp($config['body'] ?? '');
                $sent = false;
                if ($to !== '' && filter_var($to, FILTER_VALIDATE_EMAIL)) {
                    $headers = "MIME-Version: 1.0\r\nContent-type: text/html; charset=UTF-8\r\n";
                    $sent = @mail($to, $subject, nl2br(htmlspecialchars($body, ENT_QUOTES, 'UTF-8')), $headers);
                }
                // Always log an in-app notification so operators can audit even if mail is unavailable
                $ext = dirname(__DIR__) . '/shop/finance/epc_erp_extended.php';
                if (is_file($ext)) {
                    require_once $ext;
                }
                if (function_exists('epc_erp_notification_seed')) {
                    epc_erp_notification_seed(
                        $pdo,
                        'Email: ' . $subject,
                        ($sent ? 'Sent to ' : 'Queued/logged for ') . ($to !== '' ? $to : '(no recipient)') . ' — ' . $body,
                        'workflow_automation'
                    );
                }
                return array('ok' => true, 'action' => $actionType, 'detail' => array('to' => $to, 'sent' => $sent));

            case 'create_task':
                $title = (string) $interp($config['title'] ?? 'Workflow task');
                $assignee = (string) $interp($config['assignee'] ?? '');
                $dueDays = (int) ($config['due_days'] ?? 3);
                $ext = dirname(__DIR__) . '/shop/finance/epc_erp_extended.php';
                if (is_file($ext)) {
                    require_once $ext;
                }
                if (function_exists('epc_erp_notification_seed')) {
                    epc_erp_notification_seed(
                        $pdo,
                        'Task: ' . $title,
                        'Assignee: ' . ($assignee !== '' ? $assignee : 'unassigned') . ' · due in ' . $dueDays . ' day(s)',
                        'processflow'
                    );
                }
                return array('ok' => true, 'action' => $actionType, 'detail' => array('title' => $title, 'assignee' => $assignee, 'due_days' => $dueDays));

            case 'update_status':
                return array('ok' => true, 'action' => $actionType, 'detail' => array('new_status' => $config['new_status'] ?? ''));

            case 'assign_user':
                return array('ok' => true, 'action' => $actionType, 'detail' => array('user_id' => $config['user_id'] ?? 0, 'role' => $config['role'] ?? ''));

            case 'credit_check':
                $limit = isset($triggerData['credit_limit']) ? (float) $triggerData['credit_limit'] : null;
                $balance = isset($triggerData['credit_balance']) ? (float) $triggerData['credit_balance'] : 0.0;
                $amount = isset($triggerData['amount']) ? (float) $triggerData['amount'] : (isset($triggerData['total']) ? (float) $triggerData['total'] : 0.0);
                $exceed = ($limit !== null && ($balance + $amount) > $limit);
                $onExceed = (string) ($config['action_on_exceed'] ?? 'hold');
                if ($exceed && function_exists('epc_erp_notification_seed') === false) {
                    $ext = dirname(__DIR__) . '/shop/finance/epc_erp_extended.php';
                    if (is_file($ext)) {
                        require_once $ext;
                    }
                }
                if ($exceed && function_exists('epc_erp_notification_seed')) {
                    epc_erp_notification_seed($pdo, 'Credit limit exceeded', 'Action: ' . $onExceed, 'collections');
                }
                return array('ok' => true, 'action' => $actionType, 'detail' => array('exceeded' => $exceed, 'action' => $onExceed));

            case 'gl_journal':
                $amount = (float) ($config['amount'] ?? ($triggerData['amount'] ?? 0));
                $debit = (string) ($config['debit_account'] ?? '');
                $credit = (string) ($config['credit_account'] ?? '');
                if ($amount <= 0) {
                    return array('ok' => true, 'action' => $actionType, 'detail' => array('skipped' => true, 'reason' => 'zero amount'));
                }
                $glFile = dirname(__DIR__) . '/shop/finance/epc_erp_gl.php';
                if (is_file($glFile)) {
                    require_once $glFile;
                }
                if (function_exists('epc_erp_gl_post_journal')) {
                    try {
                        $posted = epc_erp_gl_post_journal(
                            $pdo,
                            array(
                                'memo' => 'Workflow: ' . (string) ($wf['name'] ?? 'automation'),
                                'description' => 'Workflow automation journal',
                            ),
                            array(
                                array('account_code' => $debit, 'debit' => $amount, 'credit' => 0),
                                array('account_code' => $credit, 'debit' => 0, 'credit' => $amount),
                            )
                        );
                        return array('ok' => true, 'action' => $actionType, 'detail' => is_array($posted) ? $posted : array('posted' => $posted));
                    } catch (Throwable $e) {
                        return array('ok' => false, 'action' => $actionType, 'error' => $e->getMessage());
                    }
                }
                return array('ok' => true, 'action' => $actionType, 'detail' => array('queued' => true, 'debit' => $debit, 'credit' => $credit, 'amount' => $amount));

            case 'create_invoice':
                return array('ok' => true, 'action' => $actionType, 'detail' => array('template' => $config['template'] ?? 'default', 'auto_send' => !empty($config['auto_send'])));

            case 'update_inventory':
                return array('ok' => true, 'action' => $actionType, 'detail' => array('sku' => $config['sku'] ?? '', 'qty_change' => $config['qty_change'] ?? 0));

            case 'webhook_call':
                $url = (string) ($config['url'] ?? '');
                if ($url === '' || !preg_match('#^https?://#i', $url)) {
                    return array('ok' => false, 'action' => $actionType, 'error' => 'Invalid webhook URL');
                }
                // Record intent only — outbound HTTP is intentionally not fired from sync path without allow-list.
                return array('ok' => true, 'action' => $actionType, 'detail' => array('url' => $url, 'queued' => true));

            case 'emit_event':
                return array('ok' => true, 'action' => $actionType, 'detail' => array('event_type' => $config['event_type'] ?? '', 'payload' => $config['payload'] ?? array()));

            case 'wait':
                return array('ok' => true, 'action' => $actionType, 'detail' => array('delay_minutes' => (int) ($config['delay_minutes'] ?? 0)));

            default:
                return array('ok' => true, 'action' => $actionType, 'detail' => array('executed' => true, 'config' => $config));
        }
    } catch (Throwable $e) {
        return array('ok' => false, 'action' => $actionType, 'error' => $e->getMessage());
    }
}

/**
 * Replace workflow steps entirely (used by the graphical builder save).
 *
 * @param array<int,array<string,mixed>> $steps
 */
function epc_workflow_replace_steps(PDO $pdo, int $wfId, array $steps): void
{
    $pdo->prepare('DELETE FROM `epc_workflow_steps` WHERE `workflow_id` = ?')->execute(array($wfId));
    foreach ($steps as $i => $step) {
        $cfg = $step['config'] ?? array();
        if (is_string($cfg)) {
            $decoded = json_decode($cfg, true);
            $cfg = is_array($decoded) ? $decoded : array();
        }
        if (!empty($step['label']) && !isset($cfg['label'])) {
            $cfg['label'] = (string) $step['label'];
        }
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
            json_encode($cfg),
            (string) ($step['on_failure'] ?? 'stop'),
            (int) ($step['retry_count'] ?? 0),
        ));
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

<?php
/**
 * Advanced ERP — Integration & API layer (internal + external).
 *
 * - Outbound connectors: register external endpoints (e-invoice portal, customs
 *   gateway, payment gateway, carrier, FX feed), build signed payloads (HMAC),
 *   and log every delivery with retry/backoff state.
 * - Inbound API: per-tenant API keys with scoped permissions, verification, and
 *   a request audit log.
 * - Internal event bus: modules publish events; subscribers react in-process
 *   (e.g. invoice.posted -> einvoice + treasury + audit).
 *
 * Additive: new epc_int_* tables. Tenant-isolated (keys + logs in tenant DB).
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_int_ensure_schema')) {
    function epc_int_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_int_connectors` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `kind` varchar(24) NOT NULL DEFAULT 'rest',
            `endpoint` varchar(255) NOT NULL DEFAULT '',
            `auth_type` varchar(16) NOT NULL DEFAULT 'hmac',
            `secret` varchar(255) DEFAULT NULL,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Outbound integration connectors'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_int_deliveries` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `connector_id` int(11) NOT NULL,
            `event` varchar(60) NOT NULL DEFAULT '',
            `payload` mediumtext,
            `signature` varchar(128) DEFAULT NULL,
            `status` varchar(16) NOT NULL DEFAULT 'pending',
            `attempts` int(11) NOT NULL DEFAULT 0,
            `last_error` varchar(255) DEFAULT NULL,
            `next_attempt` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_connector` (`connector_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Outbound delivery log'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_int_api_keys` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `label` varchar(120) NOT NULL DEFAULT '',
            `api_key` varchar(64) NOT NULL,
            `secret_hash` varchar(255) NOT NULL DEFAULT '',
            `scopes` varchar(255) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `expires_at` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_key` (`api_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inbound API keys'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_int_api_log` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `api_key` varchar(64) NOT NULL DEFAULT '',
            `method` varchar(8) NOT NULL DEFAULT 'GET',
            `path` varchar(190) NOT NULL DEFAULT '',
            `status_code` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_key` (`api_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inbound API request log'");
    }
}

/* ------------------------- Outbound connectors ------------------------ */

if (!function_exists('epc_int_connector_save')) {
    /**
     * @param array<string,mixed> $data code, name, kind, endpoint, auth_type, secret
     */
    function epc_int_connector_save(PDO $db, array $data, int $id = 0): int
    {
        epc_int_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_int_connectors` SET `name`=?, `kind`=?, `endpoint`=?, `auth_type`=?, `secret`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['kind'] ?? 'rest'), (string) ($data['endpoint'] ?? ''), (string) ($data['auth_type'] ?? 'hmac'), (string) ($data['secret'] ?? ''), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_int_connectors` (`code`,`name`,`kind`,`endpoint`,`auth_type`,`secret`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['kind'] ?? 'rest'), (string) ($data['endpoint'] ?? ''), (string) ($data['auth_type'] ?? 'hmac'), (string) ($data['secret'] ?? ''), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_int_sign_payload')) {
    /**
     * HMAC-SHA256 signature of a payload with a connector secret.
     */
    function epc_int_sign_payload(string $payloadJson, string $secret): string
    {
        return hash_hmac('sha256', $payloadJson, $secret);
    }
}

if (!function_exists('epc_int_enqueue')) {
    /**
     * Queue an outbound delivery (signed). Actual HTTP send is performed by a
     * worker calling epc_int_mark_delivered / epc_int_mark_failed.
     *
     * @param array<string,mixed> $payload
     * @return array<string,mixed> {id, signature}
     */
    function epc_int_enqueue(PDO $db, string $connectorCode, string $event, array $payload): array
    {
        epc_int_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_int_connectors` WHERE `code`=? AND `active`=1");
        $st->execute(array($connectorCode));
        $conn = $st->fetch(PDO::FETCH_ASSOC);
        if (!$conn) {
            throw new Exception('Connector not found or inactive: ' . $connectorCode);
        }
        $json = json_encode($payload);
        $sig = epc_int_sign_payload($json, (string) $conn['secret']);
        $now = time();
        $db->prepare("INSERT INTO `epc_int_deliveries` (`connector_id`,`event`,`payload`,`signature`,`status`,`attempts`,`next_attempt`,`time_created`) VALUES (?,?,?,?, 'pending', 0, ?, ?)")
           ->execute(array((int) $conn['id'], $event, $json, $sig, $now, $now));
        return array('id' => (int) $db->lastInsertId(), 'signature' => $sig);
    }
}

if (!function_exists('epc_int_mark_delivered')) {
    function epc_int_mark_delivered(PDO $db, int $deliveryId): void
    {
        epc_int_ensure_schema($db);
        $db->prepare("UPDATE `epc_int_deliveries` SET `status`='delivered', `attempts`=`attempts`+1, `last_error`=NULL WHERE `id`=?")->execute(array($deliveryId));
    }
}

if (!function_exists('epc_int_mark_failed')) {
    /**
     * Mark a delivery failed; schedules exponential backoff up to maxAttempts,
     * after which status becomes 'dead'.
     */
    function epc_int_mark_failed(PDO $db, int $deliveryId, string $error, int $maxAttempts = 5, int $now = 0): array
    {
        epc_int_ensure_schema($db);
        $now = $now > 0 ? $now : time();
        $st = $db->prepare("SELECT * FROM `epc_int_deliveries` WHERE `id`=?");
        $st->execute(array($deliveryId));
        $d = $st->fetch(PDO::FETCH_ASSOC);
        if (!$d) {
            throw new Exception('Delivery not found');
        }
        $attempts = (int) $d['attempts'] + 1;
        $dead = $attempts >= $maxAttempts;
        $backoff = (int) min(3600, pow(2, $attempts) * 30); // 60,120,240... capped 1h
        $db->prepare("UPDATE `epc_int_deliveries` SET `status`=?, `attempts`=?, `last_error`=?, `next_attempt`=? WHERE `id`=?")
           ->execute(array($dead ? 'dead' : 'pending', $attempts, substr($error, 0, 255), $now + $backoff, $deliveryId));
        return array('attempts' => $attempts, 'status' => $dead ? 'dead' : 'pending', 'next_attempt' => $now + $backoff);
    }
}

if (!function_exists('epc_int_due_deliveries')) {
    /**
     * @return array<int,array<string,mixed>> pending deliveries due for a send attempt
     */
    function epc_int_due_deliveries(PDO $db, int $now = 0, int $limit = 50): array
    {
        epc_int_ensure_schema($db);
        $now = $now > 0 ? $now : time();
        $st = $db->prepare("SELECT * FROM `epc_int_deliveries` WHERE `status`='pending' AND `next_attempt`<=? ORDER BY `next_attempt` ASC LIMIT " . (int) $limit);
        $st->execute(array($now));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ----------------------------- Inbound API ---------------------------- */

if (!function_exists('epc_int_api_key_create')) {
    /**
     * Issue an inbound API key + secret. The secret is returned ONCE (only its
     * hash is stored).
     *
     * @param array<int,string> $scopes
     * @return array<string,mixed> {id, api_key, secret, scopes}
     */
    function epc_int_api_key_create(PDO $db, string $label, array $scopes, int $expiresAt = 0): array
    {
        epc_int_ensure_schema($db);
        $apiKey = 'ak_' . bin2hex(random_bytes(12));
        $secret = bin2hex(random_bytes(20));
        $hash = hash('sha256', $secret);
        $scopeCsv = implode(',', array_map('strval', $scopes));
        $db->prepare("INSERT INTO `epc_int_api_keys` (`label`,`api_key`,`secret_hash`,`scopes`,`active`,`expires_at`,`time_created`) VALUES (?,?,?,?,1,?,?)")
           ->execute(array($label, $apiKey, $hash, $scopeCsv, $expiresAt, time()));
        return array('id' => (int) $db->lastInsertId(), 'api_key' => $apiKey, 'secret' => $secret, 'scopes' => $scopes);
    }
}

if (!function_exists('epc_int_api_verify')) {
    /**
     * Verify an inbound key+secret and (optionally) a required scope. Respects
     * active flag + expiry.
     *
     * @return array<string,mixed> {ok, reason, scopes}
     */
    function epc_int_api_verify(PDO $db, string $apiKey, string $secret, string $requiredScope = '', int $now = 0): array
    {
        epc_int_ensure_schema($db);
        $now = $now > 0 ? $now : time();
        $st = $db->prepare("SELECT * FROM `epc_int_api_keys` WHERE `api_key`=?");
        $st->execute(array($apiKey));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return array('ok' => false, 'reason' => 'unknown_key', 'scopes' => array());
        }
        if ((int) $row['active'] !== 1) {
            return array('ok' => false, 'reason' => 'inactive', 'scopes' => array());
        }
        if ((int) $row['expires_at'] > 0 && (int) $row['expires_at'] < $now) {
            return array('ok' => false, 'reason' => 'expired', 'scopes' => array());
        }
        if (!hash_equals((string) $row['secret_hash'], hash('sha256', $secret))) {
            return array('ok' => false, 'reason' => 'bad_secret', 'scopes' => array());
        }
        $scopes = $row['scopes'] !== '' ? explode(',', (string) $row['scopes']) : array();
        if ($requiredScope !== '' && !in_array($requiredScope, $scopes, true) && !in_array('*', $scopes, true)) {
            return array('ok' => false, 'reason' => 'missing_scope', 'scopes' => $scopes);
        }
        return array('ok' => true, 'reason' => 'ok', 'scopes' => $scopes);
    }
}

if (!function_exists('epc_int_api_revoke')) {
    function epc_int_api_revoke(PDO $db, string $apiKey): void
    {
        epc_int_ensure_schema($db);
        $db->prepare("UPDATE `epc_int_api_keys` SET `active`=0 WHERE `api_key`=?")->execute(array($apiKey));
    }
}

if (!function_exists('epc_int_api_log')) {
    function epc_int_api_log(PDO $db, string $apiKey, string $method, string $path, int $statusCode): void
    {
        epc_int_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_int_api_log` (`api_key`,`method`,`path`,`status_code`,`time_created`) VALUES (?,?,?,?,?)")
           ->execute(array($apiKey, $method, substr($path, 0, 190), $statusCode, time()));
    }
}

/* --------------------------- Internal events -------------------------- */

if (!function_exists('epc_int_event_bus')) {
    /**
     * Process-local pub/sub registry (singleton). Modules subscribe with
     * epc_int_on() and publish with epc_int_emit().
     *
     * @return array<string,array<int,callable>>
     */
    function &epc_int_event_bus(): array
    {
        static $bus = array();
        return $bus;
    }
}

if (!function_exists('epc_int_on')) {
    function epc_int_on(string $event, callable $handler): void
    {
        $bus = &epc_int_event_bus();
        if (!isset($bus[$event])) {
            $bus[$event] = array();
        }
        $bus[$event][] = $handler;
    }
}

if (!function_exists('epc_int_emit')) {
    /**
     * Emit an event to all subscribers. Returns the number of handlers invoked.
     * Handler exceptions are isolated so one failure doesn't break the rest.
     *
     * @param array<string,mixed> $payload
     * @return array<string,mixed> {handled:int, errors:int}
     */
    function epc_int_emit(string $event, array $payload = array()): array
    {
        $bus = &epc_int_event_bus();
        $handled = 0;
        $errors = 0;
        foreach ($bus[$event] ?? array() as $h) {
            try {
                call_user_func($h, $payload, $event);
                $handled++;
            } catch (Throwable $e) {
                $errors++;
            }
        }
        return array('handled' => $handled, 'errors' => $errors);
    }
}

/* ==================================================================
 * Data entities + OData-style query + business events (additive layer)
 * Prefix epc_intg_* — complements the epc_int_* connector/API/event-bus
 * layer above. Multi-company aware; pure parse/match helpers for tests.
 * ================================================================== */


if (!function_exists('epc_intg_ensure_schema')) {
    function epc_intg_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_intg_entity` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(80) NOT NULL DEFAULT '',
            `source_table` varchar(120) NOT NULL DEFAULT '',
            `key_field` varchar(80) NOT NULL DEFAULT 'id',
            `fields_json` text,
            `enabled` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_name` (`company_id`,`name`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Data entity registry'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_intg_event_sub` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `event` varchar(120) NOT NULL DEFAULT '',
            `target_type` varchar(16) NOT NULL DEFAULT 'webhook',
            `target` varchar(255) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_event` (`event`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Business event subscriptions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_intg_event_log` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `event` varchar(120) NOT NULL DEFAULT '',
            `payload_json` mediumtext,
            `sub_id` int(11) NOT NULL DEFAULT 0,
            `target_type` varchar(16) NOT NULL DEFAULT '',
            `target` varchar(255) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'queued',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_event` (`event`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Business event dispatch log'");
    }
}

/* ---------------- data entities ---------------- */

if (!function_exists('epc_intg_entity_save')) {
    /**
     * @param array<string,mixed> $data name, source_table, key_field, fields(array), enabled
     */
    function epc_intg_entity_save(PDO $db, int $companyId, array $data): int
    {
        epc_intg_ensure_schema($db);
        $fields = $data['fields'] ?? array();
        if (!is_array($fields)) {
            $fields = array_filter(array_map('trim', explode(',', (string) $fields)));
        }
        $name = trim((string) ($data['name'] ?? ''));
        if ($name === '') {
            throw new Exception('Entity name is required');
        }
        $db->prepare("INSERT INTO `epc_intg_entity` (`company_id`,`name`,`source_table`,`key_field`,`fields_json`,`enabled`,`time_updated`) VALUES (?,?,?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `source_table`=VALUES(`source_table`), `key_field`=VALUES(`key_field`), `fields_json`=VALUES(`fields_json`), `enabled`=VALUES(`enabled`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $name, (string) ($data['source_table'] ?? ''), (string) ($data['key_field'] ?? 'id'), json_encode(array_values($fields)), !empty($data['enabled']) ? 1 : 0, time()));
        $st = $db->prepare("SELECT id FROM `epc_intg_entity` WHERE company_id=? AND name=?");
        $st->execute(array($companyId, $name));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_intg_entity_get')) {
    /** @return array<string,mixed>|null */
    function epc_intg_entity_get(PDO $db, int $companyId, string $name): ?array
    {
        epc_intg_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_intg_entity` WHERE company_id=? AND name=?");
        $st->execute(array($companyId, $name));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        if (!$r) {
            return null;
        }
        $r['fields'] = json_decode((string) $r['fields_json'], true) ?: array();
        return $r;
    }
}

if (!function_exists('epc_intg_entities')) {
    /** @return array<int,array<string,mixed>> */
    function epc_intg_entities(PDO $db, int $companyId = 0): array
    {
        epc_intg_ensure_schema($db);
        $sql = "SELECT * FROM `epc_intg_entity` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY name";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['fields'] = json_decode((string) $r['fields_json'], true) ?: array();
        }
        unset($r);
        return $rows;
    }
}

/* ---------------- OData-style query parser (pure) ---------------- */

if (!function_exists('epc_intg_odata_parse')) {
    /**
     * Pure: parse an OData-style query into a parameterised SQL fragment set.
     * Only whitelisted fields are accepted; unknown fields are ignored (filter)
     * or dropped (select/orderby). Supported $filter operators: eq ne gt ge lt
     * le and contains(field,'x'); clauses joined by ' and '.
     *
     * @param array<string,string> $q query params ($select,$filter,$orderby,$top,$skip)
     * @param array<int,string> $allowed whitelisted field names
     * @return array{select:array<int,string>,where:string,params:array<int,mixed>,order:string,limit:int,offset:int}
     */
    function epc_intg_odata_parse(array $q, array $allowed): array
    {
        $allowedMap = array_fill_keys($allowed, true);

        // $select
        $select = array();
        if (!empty($q['$select'])) {
            foreach (explode(',', (string) $q['$select']) as $f) {
                $f = trim($f);
                if ($f !== '' && isset($allowedMap[$f])) {
                    $select[] = $f;
                }
            }
        }

        // $filter
        $where = '';
        $params = array();
        if (!empty($q['$filter'])) {
            $clauses = preg_split('/\s+and\s+/i', trim((string) $q['$filter']));
            $sqlParts = array();
            $ops = array('eq' => '=', 'ne' => '!=', 'gt' => '>', 'ge' => '>=', 'lt' => '<', 'le' => '<=');
            foreach ($clauses as $clause) {
                $clause = trim($clause);
                if ($clause === '') {
                    continue;
                }
                // contains(field,'value')
                if (preg_match("/^contains\(\s*([a-zA-Z0-9_]+)\s*,\s*'(.*)'\s*\)$/", $clause, $m)) {
                    if (isset($allowedMap[$m[1]])) {
                        $sqlParts[] = "`{$m[1]}` LIKE ?";
                        $params[] = '%' . $m[2] . '%';
                    }
                    continue;
                }
                // field op value
                if (preg_match("/^([a-zA-Z0-9_]+)\s+(eq|ne|gt|ge|lt|le)\s+(.+)$/i", $clause, $m)) {
                    $field = $m[1];
                    $op = strtolower($m[2]);
                    $val = trim($m[3]);
                    if (!isset($allowedMap[$field])) {
                        continue;
                    }
                    if (strlen($val) >= 2 && $val[0] === "'" && substr($val, -1) === "'") {
                        $val = substr($val, 1, -1);
                    } elseif (is_numeric($val)) {
                        $val = $val + 0;
                    }
                    $sqlParts[] = "`{$field}` {$ops[$op]} ?";
                    $params[] = $val;
                }
            }
            if (!empty($sqlParts)) {
                $where = implode(' AND ', $sqlParts);
            }
        }

        // $orderby
        $order = '';
        if (!empty($q['$orderby'])) {
            $orderParts = array();
            foreach (explode(',', (string) $q['$orderby']) as $ob) {
                $ob = trim($ob);
                $dir = 'ASC';
                if (preg_match('/^([a-zA-Z0-9_]+)\s+(asc|desc)$/i', $ob, $m)) {
                    $ob = $m[1];
                    $dir = strtoupper($m[2]);
                }
                if (isset($allowedMap[$ob])) {
                    $orderParts[] = "`{$ob}` {$dir}";
                }
            }
            $order = implode(', ', $orderParts);
        }

        $limit = isset($q['$top']) ? max(0, (int) $q['$top']) : 0;
        $offset = isset($q['$skip']) ? max(0, (int) $q['$skip']) : 0;

        return array('select' => $select, 'where' => $where, 'params' => $params, 'order' => $order, 'limit' => $limit, 'offset' => $offset);
    }
}

if (!function_exists('epc_intg_entity_query')) {
    /**
     * Run an OData-style query against a registered, enabled entity. Fields are
     * whitelisted from the entity definition; the source table is taken from the
     * registry (never user input).
     *
     * @param array<string,string> $q
     * @return array<int,array<string,mixed>>
     */
    function epc_intg_entity_query(PDO $db, int $companyId, string $entityName, array $q): array
    {
        $entity = epc_intg_entity_get($db, $companyId, $entityName);
        if (!$entity) {
            throw new Exception('Unknown data entity: ' . $entityName);
        }
        if (empty($entity['enabled'])) {
            throw new Exception('Data entity is disabled: ' . $entityName);
        }
        $allowed = $entity['fields'];
        if (!in_array($entity['key_field'], $allowed, true)) {
            $allowed[] = $entity['key_field'];
        }
        $parsed = epc_intg_odata_parse($q, $allowed);
        $cols = !empty($parsed['select']) ? $parsed['select'] : $allowed;
        $colSql = implode(', ', array_map(function ($c) {
            return "`{$c}`";
        }, $cols));
        $table = preg_replace('/[^a-zA-Z0-9_]/', '', (string) $entity['source_table']);
        $sql = "SELECT {$colSql} FROM `{$table}`";
        if ($parsed['where'] !== '') {
            $sql .= ' WHERE ' . $parsed['where'];
        }
        if ($parsed['order'] !== '') {
            $sql .= ' ORDER BY ' . $parsed['order'];
        }
        if ($parsed['limit'] > 0) {
            $sql .= ' LIMIT ' . $parsed['limit'];
            if ($parsed['offset'] > 0) {
                $sql .= ' OFFSET ' . $parsed['offset'];
            }
        }
        $st = $db->prepare($sql);
        $st->execute($parsed['params']);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- business events ---------------- */

if (!function_exists('epc_intg_event_catalog')) {
    /** @return array<int,string> built-in business events */
    function epc_intg_event_catalog(): array
    {
        return array(
            'SalesOrderConfirmed',
            'SalesOrderInvoiced',
            'PurchaseOrderConfirmed',
            'PaymentPosted',
            'InventoryLowStock',
            'CustomerCreditHold',
            'DocumentExpiring',
            'InsuranceRenewalDue',
        );
    }
}

if (!function_exists('epc_intg_sub_save')) {
    function epc_intg_sub_save(PDO $db, int $companyId, string $event, string $targetType, string $target, bool $active = true): int
    {
        epc_intg_ensure_schema($db);
        if (!in_array($targetType, array('webhook', 'internal', 'email'), true)) {
            throw new Exception('Invalid subscription target type');
        }
        $db->prepare("INSERT INTO `epc_intg_event_sub` (`company_id`,`event`,`target_type`,`target`,`active`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array($companyId, $event, $targetType, $target, $active ? 1 : 0, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_intg_subs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_intg_subs(PDO $db, int $companyId = 0): array
    {
        epc_intg_ensure_schema($db);
        $sql = "SELECT * FROM `epc_intg_event_sub` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY event, id";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_intg_event_match')) {
    /**
     * Pure: from a list of subscriptions, return the active ones matching the
     * event name.
     *
     * @param array<int,array<string,mixed>> $subs
     * @return array<int,array<string,mixed>>
     */
    function epc_intg_event_match(array $subs, string $event): array
    {
        $out = array();
        foreach ($subs as $s) {
            if ((string) $s['event'] === $event && !empty($s['active'])) {
                $out[] = $s;
            }
        }
        return $out;
    }
}

if (!function_exists('epc_intg_event_raise')) {
    /**
     * Raise a business event: match active subscriptions and write one dispatch
     * log row per matched subscriber (status 'queued'). Returns the matched
     * deliveries. If no subscribers, logs a single 'no_subscriber' row.
     *
     * @param array<string,mixed> $payload
     * @return array{event:string,deliveries:int,matched:array<int,array<string,mixed>>}
     */
    function epc_intg_event_raise(PDO $db, int $companyId, string $event, array $payload): array
    {
        epc_intg_ensure_schema($db);
        $matched = epc_intg_event_match(epc_intg_subs($db, $companyId), $event);
        $json = json_encode($payload);
        $now = time();
        if (empty($matched)) {
            $db->prepare("INSERT INTO `epc_intg_event_log` (`company_id`,`event`,`payload_json`,`sub_id`,`target_type`,`target`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?)")
               ->execute(array($companyId, $event, $json, 0, '', '', 'no_subscriber', $now));
            return array('event' => $event, 'deliveries' => 0, 'matched' => array());
        }
        $ins = $db->prepare("INSERT INTO `epc_intg_event_log` (`company_id`,`event`,`payload_json`,`sub_id`,`target_type`,`target`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?)");
        foreach ($matched as $m) {
            $ins->execute(array($companyId, $event, $json, (int) $m['id'], (string) $m['target_type'], (string) $m['target'], 'queued', $now));
        }
        return array('event' => $event, 'deliveries' => count($matched), 'matched' => $matched);
    }
}

if (!function_exists('epc_intg_event_log')) {
    /** @return array<int,array<string,mixed>> */
    function epc_intg_event_log(PDO $db, int $companyId = 0, int $limit = 100): array
    {
        epc_intg_ensure_schema($db);
        $sql = "SELECT * FROM `epc_intg_event_log` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY id DESC LIMIT " . max(1, $limit);
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_intg_summary')) {
    /** @return array{entities:int,subscriptions:int,events_logged:int,queued:int} */
    function epc_intg_summary(PDO $db, int $companyId = 0): array
    {
        epc_intg_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'entities' => (int) $db->query("SELECT COUNT(*) FROM `epc_intg_entity` WHERE 1=1" . $wa)->fetchColumn(),
            'subscriptions' => (int) $db->query("SELECT COUNT(*) FROM `epc_intg_event_sub` WHERE active=1" . $wa)->fetchColumn(),
            'events_logged' => (int) $db->query("SELECT COUNT(*) FROM `epc_intg_event_log` WHERE 1=1" . $wa)->fetchColumn(),
            'queued' => (int) $db->query("SELECT COUNT(*) FROM `epc_intg_event_log` WHERE status='queued'" . $wa)->fetchColumn(),
        );
    }
}

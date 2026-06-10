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

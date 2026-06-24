<?php
/**
 * P1 #15 — REST API v2
 *
 * Versioned, rate-limited REST API with API key auth, OpenAPI spec,
 * JSON request/response, and per-tenant scoping.
 * Schema: epc_api_keys, epc_api_rate_limits, epc_api_logs
 */

if (!defined('EPC_REST_API_V2_VERSION')) {
    define('EPC_REST_API_V2_VERSION', '2.0.0');
}

/* ─── schema ─── */

function epc_api_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_api_keys` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `key_hash`        VARCHAR(128)   NOT NULL,
            `key_prefix`      VARCHAR(12)    NOT NULL,
            `label`           VARCHAR(128)   NOT NULL DEFAULT '',
            `scopes`          JSON           NOT NULL,
            `rate_limit`      INT UNSIGNED   NOT NULL DEFAULT 1000,
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `last_used`       DATETIME       NULL,
            `expires_at`      DATETIME       NULL,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_prefix` (`key_prefix`),
            INDEX `idx_active` (`active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_api_rate_limits` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `key_id`          INT UNSIGNED   NOT NULL,
            `window_start`    DATETIME       NOT NULL,
            `request_count`   INT UNSIGNED   NOT NULL DEFAULT 0,
            UNIQUE KEY `uk_window` (`key_id`, `window_start`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_api_logs` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `key_id`          INT UNSIGNED   NOT NULL DEFAULT 0,
            `site_key`        VARCHAR(64)    NOT NULL DEFAULT '',
            `method`          VARCHAR(10)    NOT NULL,
            `endpoint`        VARCHAR(255)   NOT NULL,
            `status_code`     SMALLINT UNSIGNED NOT NULL DEFAULT 200,
            `response_ms`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `ip_address`      VARCHAR(45)    NOT NULL DEFAULT '',
            `user_agent`      VARCHAR(255)   NOT NULL DEFAULT '',
            `request_body`    TEXT           NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_key` (`key_id`),
            INDEX `idx_endpoint` (`endpoint`),
            INDEX `idx_created` (`created_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── API key management ─── */

function epc_api_key_generate(PDO $pdo, string $siteKey, array $opts = array()): array
{
    epc_api_ensure_schema($pdo);

    $rawKey = 'epc_' . bin2hex(random_bytes(24));
    $prefix = substr($rawKey, 0, 8);
    $hash = hash('sha256', $rawKey);
    $scopes = $opts['scopes'] ?? array('read');
    $rateLimit = (int) ($opts['rate_limit'] ?? 1000);
    $label = (string) ($opts['label'] ?? 'API Key');
    $expiresAt = !empty($opts['expires_at']) ? (string) $opts['expires_at'] : null;

    $st = $pdo->prepare("
        INSERT INTO `epc_api_keys`
            (`site_key`, `key_hash`, `key_prefix`, `label`, `scopes`, `rate_limit`, `expires_at`, `created_by`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array($siteKey, $hash, $prefix, $label, json_encode($scopes), $rateLimit, $expiresAt, (int) ($opts['created_by'] ?? 0)));

    return array(
        'ok'       => true,
        'key_id'   => (int) $pdo->lastInsertId(),
        'api_key'  => $rawKey,
        'prefix'   => $prefix,
        'scopes'   => $scopes,
        'warning'  => 'Store this key securely — it cannot be retrieved again.',
    );
}

function epc_api_key_validate(PDO $pdo, string $rawKey): array
{
    epc_api_ensure_schema($pdo);

    $hash = hash('sha256', $rawKey);
    $st = $pdo->prepare("SELECT * FROM `epc_api_keys` WHERE `key_hash` = ? AND `active` = 1");
    $st->execute(array($hash));
    $key = $st->fetch(PDO::FETCH_ASSOC);

    if (!$key) {
        return array('valid' => false, 'error' => 'Invalid API key');
    }

    if ($key['expires_at'] && strtotime($key['expires_at']) < time()) {
        return array('valid' => false, 'error' => 'API key expired');
    }

    $pdo->prepare("UPDATE `epc_api_keys` SET `last_used` = NOW() WHERE `id` = ?")->execute(array($key['id']));

    $key['scopes'] = json_decode($key['scopes'] ?: '[]', true);
    return array('valid' => true, 'key' => $key);
}

function epc_api_key_revoke(PDO $pdo, int $keyId): bool
{
    $st = $pdo->prepare("UPDATE `epc_api_keys` SET `active` = 0 WHERE `id` = ?");
    return $st->execute(array($keyId));
}

function epc_api_keys_list(PDO $pdo, string $siteKey): array
{
    epc_api_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT `id`, `key_prefix`, `label`, `scopes`, `rate_limit`, `active`, `last_used`, `expires_at`, `created_at` FROM `epc_api_keys` WHERE `site_key` = ? ORDER BY `created_at` DESC");
    $st->execute(array($siteKey));
    $keys = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($keys as &$k) {
        $k['scopes'] = json_decode($k['scopes'] ?: '[]', true);
    }
    return $keys;
}

/* ─── rate limiting ─── */

function epc_api_rate_check(PDO $pdo, int $keyId, int $limit = 1000): array
{
    $windowStart = date('Y-m-d H:00:00');

    $st = $pdo->prepare("
        INSERT INTO `epc_api_rate_limits` (`key_id`, `window_start`, `request_count`)
        VALUES (?, ?, 1)
        ON DUPLICATE KEY UPDATE `request_count` = `request_count` + 1
    ");
    $st->execute(array($keyId, $windowStart));

    $st = $pdo->prepare("SELECT `request_count` FROM `epc_api_rate_limits` WHERE `key_id` = ? AND `window_start` = ?");
    $st->execute(array($keyId, $windowStart));
    $count = (int) $st->fetchColumn();

    return array(
        'allowed'   => $count <= $limit,
        'remaining' => max(0, $limit - $count),
        'limit'     => $limit,
        'reset'     => strtotime($windowStart . ' +1 hour'),
    );
}

/* ─── API logging ─── */

function epc_api_log(PDO $pdo, array $data): void
{
    try {
        $st = $pdo->prepare("
            INSERT INTO `epc_api_logs`
                (`key_id`, `site_key`, `method`, `endpoint`, `status_code`, `response_ms`, `ip_address`, `user_agent`, `request_body`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        ");
        $st->execute(array(
            (int) ($data['key_id'] ?? 0),
            (string) ($data['site_key'] ?? ''),
            (string) ($data['method'] ?? 'GET'),
            (string) ($data['endpoint'] ?? ''),
            (int) ($data['status_code'] ?? 200),
            (int) ($data['response_ms'] ?? 0),
            (string) ($data['ip_address'] ?? ''),
            substr((string) ($data['user_agent'] ?? ''), 0, 255),
            (string) ($data['request_body'] ?? ''),
        ));
    } catch (\Exception $e) {
        // logging should not break API calls
    }
}

/* ─── API endpoints registry ─── */

function epc_api_v2_endpoints(): array
{
    return array(
        array('method' => 'GET',    'path' => '/api/v2/products',           'scope' => 'read',    'description' => 'List products with pagination'),
        array('method' => 'GET',    'path' => '/api/v2/products/{id}',      'scope' => 'read',    'description' => 'Get product by ID'),
        array('method' => 'POST',   'path' => '/api/v2/products',           'scope' => 'write',   'description' => 'Create a new product'),
        array('method' => 'PUT',    'path' => '/api/v2/products/{id}',      'scope' => 'write',   'description' => 'Update a product'),
        array('method' => 'DELETE', 'path' => '/api/v2/products/{id}',      'scope' => 'write',   'description' => 'Delete a product'),
        array('method' => 'GET',    'path' => '/api/v2/orders',             'scope' => 'read',    'description' => 'List orders with filters'),
        array('method' => 'GET',    'path' => '/api/v2/orders/{id}',        'scope' => 'read',    'description' => 'Get order by ID'),
        array('method' => 'POST',   'path' => '/api/v2/orders',             'scope' => 'write',   'description' => 'Create a new order'),
        array('method' => 'PUT',    'path' => '/api/v2/orders/{id}/status', 'scope' => 'write',   'description' => 'Update order status'),
        array('method' => 'GET',    'path' => '/api/v2/customers',          'scope' => 'read',    'description' => 'List customers'),
        array('method' => 'GET',    'path' => '/api/v2/customers/{id}',     'scope' => 'read',    'description' => 'Get customer by ID'),
        array('method' => 'POST',   'path' => '/api/v2/customers',          'scope' => 'write',   'description' => 'Create a new customer'),
        array('method' => 'GET',    'path' => '/api/v2/inventory',          'scope' => 'read',    'description' => 'Get inventory levels'),
        array('method' => 'PUT',    'path' => '/api/v2/inventory/{sku}',    'scope' => 'write',   'description' => 'Update stock for SKU'),
        array('method' => 'GET',    'path' => '/api/v2/invoices',           'scope' => 'finance', 'description' => 'List invoices'),
        array('method' => 'POST',   'path' => '/api/v2/invoices',           'scope' => 'finance', 'description' => 'Create invoice'),
        array('method' => 'GET',    'path' => '/api/v2/reports/sales',      'scope' => 'reports', 'description' => 'Sales summary report'),
        array('method' => 'GET',    'path' => '/api/v2/reports/inventory',  'scope' => 'reports', 'description' => 'Inventory report'),
        array('method' => 'GET',    'path' => '/api/v2/webhooks',           'scope' => 'admin',   'description' => 'List webhook endpoints'),
        array('method' => 'POST',   'path' => '/api/v2/webhooks',           'scope' => 'admin',   'description' => 'Register webhook endpoint'),
    );
}

/* ─── scope check ─── */

function epc_api_has_scope(array $keyScopes, string $requiredScope): bool
{
    if (in_array('admin', $keyScopes, true)) {
        return true;
    }
    if (in_array('*', $keyScopes, true)) {
        return true;
    }
    return in_array($requiredScope, $keyScopes, true);
}

/* ─── request handler ─── */

function epc_api_v2_handle(PDO $pdo, string $method, string $path, array $params = array()): array
{
    $start = microtime(true);

    $authHeader = (string) ($_SERVER['HTTP_AUTHORIZATION'] ?? '');
    $apiKey = '';
    if (strpos($authHeader, 'Bearer ') === 0) {
        $apiKey = substr($authHeader, 7);
    } elseif (!empty($params['api_key'])) {
        $apiKey = (string) $params['api_key'];
    }

    if ($apiKey === '') {
        return epc_api_v2_error(401, 'Missing API key. Use Authorization: Bearer <key>');
    }

    $auth = epc_api_key_validate($pdo, $apiKey);
    if (!$auth['valid']) {
        return epc_api_v2_error(401, $auth['error']);
    }

    $key = $auth['key'];
    $rateCheck = epc_api_rate_check($pdo, (int) $key['id'], (int) $key['rate_limit']);
    if (!$rateCheck['allowed']) {
        return epc_api_v2_error(429, 'Rate limit exceeded', array(
            'X-RateLimit-Limit'     => $rateCheck['limit'],
            'X-RateLimit-Remaining' => 0,
            'X-RateLimit-Reset'     => $rateCheck['reset'],
        ));
    }

    $endpoints = epc_api_v2_endpoints();
    $matched = null;
    $pathParams = array();

    foreach ($endpoints as $ep) {
        if ($ep['method'] !== strtoupper($method)) {
            continue;
        }
        $pattern = preg_replace('/\{([^}]+)\}/', '(?P<$1>[^/]+)', $ep['path']);
        if (preg_match('#^' . $pattern . '$#', $path, $matches)) {
            $matched = $ep;
            foreach ($matches as $mk => $mv) {
                if (is_string($mk)) {
                    $pathParams[$mk] = $mv;
                }
            }
            break;
        }
    }

    if (!$matched) {
        $ms = (int) ((microtime(true) - $start) * 1000);
        epc_api_log($pdo, array('key_id' => $key['id'], 'site_key' => $key['site_key'], 'method' => $method, 'endpoint' => $path, 'status_code' => 404, 'response_ms' => $ms));
        return epc_api_v2_error(404, 'Endpoint not found');
    }

    if (!epc_api_has_scope($key['scopes'], $matched['scope'])) {
        return epc_api_v2_error(403, 'Insufficient scope. Required: ' . $matched['scope']);
    }

    $response = array(
        'ok'       => true,
        'api'      => 'v2',
        'endpoint' => $matched['path'],
        'method'   => $matched['method'],
        'site_key' => $key['site_key'],
        'params'   => $pathParams,
        'data'     => array(),
        'meta'     => array(
            'page'     => (int) ($params['page'] ?? 1),
            'per_page' => (int) ($params['per_page'] ?? 25),
        ),
    );

    $ms = (int) ((microtime(true) - $start) * 1000);
    $response['meta']['response_ms'] = $ms;
    $response['meta']['rate_limit'] = array(
        'remaining' => $rateCheck['remaining'],
        'limit'     => $rateCheck['limit'],
        'reset'     => $rateCheck['reset'],
    );

    epc_api_log($pdo, array('key_id' => $key['id'], 'site_key' => $key['site_key'], 'method' => $method, 'endpoint' => $path, 'status_code' => 200, 'response_ms' => $ms));

    return $response;
}

function epc_api_v2_error(int $code, string $message, array $headers = array()): array
{
    return array(
        'ok'      => false,
        'error'   => array('code' => $code, 'message' => $message),
        'headers' => $headers,
    );
}

/* ─── OpenAPI spec generator ─── */

function epc_api_v2_openapi_spec(): array
{
    $endpoints = epc_api_v2_endpoints();
    $paths = array();

    foreach ($endpoints as $ep) {
        $pathKey = $ep['path'];
        if (!isset($paths[$pathKey])) {
            $paths[$pathKey] = array();
        }
        $method = strtolower($ep['method']);
        $paths[$pathKey][$method] = array(
            'summary'     => $ep['description'],
            'tags'        => array(explode('/', $ep['path'])[3] ?? 'general'),
            'security'    => array(array('BearerAuth' => array())),
            'parameters'  => array(),
            'responses'   => array(
                '200' => array('description' => 'Success'),
                '401' => array('description' => 'Unauthorized'),
                '403' => array('description' => 'Forbidden — insufficient scope'),
                '429' => array('description' => 'Rate limit exceeded'),
            ),
        );
    }

    return array(
        'openapi' => '3.0.3',
        'info'    => array(
            'title'       => 'ecomae Platform API',
            'version'     => EPC_REST_API_V2_VERSION,
            'description' => 'Multi-tenant ERP/Commerce API with per-tenant scoping, API key auth, and rate limiting.',
        ),
        'servers' => array(
            array('url' => 'https://www.ecomae.com', 'description' => 'Production'),
        ),
        'components' => array(
            'securitySchemes' => array(
                'BearerAuth' => array(
                    'type'   => 'http',
                    'scheme' => 'bearer',
                ),
            ),
        ),
        'paths' => $paths,
    );
}

/* ─── API usage stats (BOS) ─── */

function epc_api_fleet_stats(PDO $pdo): array
{
    epc_api_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT l.`site_key`,
               COUNT(*) AS `total_requests`,
               SUM(CASE WHEN l.`status_code` >= 400 THEN 1 ELSE 0 END) AS `errors`,
               AVG(l.`response_ms`) AS `avg_response_ms`,
               COUNT(DISTINCT l.`key_id`) AS `active_keys`,
               MAX(l.`created_at`) AS `last_request`
        FROM `epc_api_logs` l
        WHERE l.`created_at` >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
        GROUP BY l.`site_key`
        ORDER BY `total_requests` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_api_usage_by_endpoint(PDO $pdo, string $siteKey, int $hours = 24): array
{
    $st = $pdo->prepare("
        SELECT `endpoint`, `method`, COUNT(*) AS `count`, AVG(`response_ms`) AS `avg_ms`,
               SUM(CASE WHEN `status_code` >= 400 THEN 1 ELSE 0 END) AS `errors`
        FROM `epc_api_logs`
        WHERE `site_key` = ? AND `created_at` >= DATE_SUB(NOW(), INTERVAL ? HOUR)
        GROUP BY `endpoint`, `method`
        ORDER BY `count` DESC
    ");
    $st->execute(array($siteKey, $hours));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

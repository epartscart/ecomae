<?php
/**
 * P1 #14 — SSO / SAML 2.0 Federation
 *
 * Enterprise SSO with SAML 2.0 SP-initiated flow, IdP metadata import,
 * attribute mapping, JIT provisioning, and session management.
 * Schema: epc_sso_providers, epc_sso_sessions
 */

if (!defined('EPC_SSO_SAML_VERSION')) {
    define('EPC_SSO_SAML_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_sso_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_sso_providers` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `provider_name`   VARCHAR(128)   NOT NULL,
            `provider_type`   ENUM('saml2','oidc','oauth2') NOT NULL DEFAULT 'saml2',
            `entity_id`       VARCHAR(512)   NOT NULL DEFAULT '',
            `sso_url`         VARCHAR(512)   NOT NULL DEFAULT '',
            `slo_url`         VARCHAR(512)   NOT NULL DEFAULT '',
            `certificate`     TEXT           NOT NULL,
            `metadata_xml`    MEDIUMTEXT     NULL,
            `attribute_map`   JSON           NOT NULL,
            `jit_provision`   TINYINT(1)     NOT NULL DEFAULT 1,
            `default_role`    VARCHAR(32)    NOT NULL DEFAULT 'viewer',
            `active`          TINYINT(1)     NOT NULL DEFAULT 0,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_type` (`provider_type`),
            INDEX `idx_active` (`active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_sso_sessions` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `provider_id`     INT UNSIGNED   NOT NULL,
            `user_id`         INT UNSIGNED   NOT NULL DEFAULT 0,
            `name_id`         VARCHAR(256)   NOT NULL DEFAULT '',
            `email`           VARCHAR(256)   NOT NULL DEFAULT '',
            `attributes`      JSON           NULL,
            `session_index`   VARCHAR(256)   NOT NULL DEFAULT '',
            `status`          ENUM('active','expired','logged_out') NOT NULL DEFAULT 'active',
            `ip_address`      VARCHAR(45)    NOT NULL DEFAULT '',
            `user_agent`      VARCHAR(255)   NOT NULL DEFAULT '',
            `login_at`        DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `logout_at`       DATETIME       NULL,
            `expires_at`      DATETIME       NOT NULL,
            INDEX `idx_site_user` (`site_key`, `user_id`),
            INDEX `idx_provider` (`provider_id`),
            INDEX `idx_status` (`status`),
            INDEX `idx_expires` (`expires_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── SP metadata ─── */

function epc_sso_sp_metadata(string $siteKey): array
{
    $baseUrl = 'https://www.ecomae.com';
    return array(
        'entity_id'    => $baseUrl . '/saml/sp/' . $siteKey,
        'acs_url'      => $baseUrl . '/saml/acs/' . $siteKey,
        'slo_url'      => $baseUrl . '/saml/slo/' . $siteKey,
        'metadata_url' => $baseUrl . '/saml/metadata/' . $siteKey,
        'name_id_format' => 'urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress',
    );
}

function epc_sso_sp_metadata_xml(string $siteKey): string
{
    $sp = epc_sso_sp_metadata($siteKey);
    return '<?xml version="1.0" encoding="UTF-8"?>
<md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata"
    entityID="' . htmlspecialchars($sp['entity_id'], ENT_XML1) . '">
  <md:SPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol"
      AuthnRequestsSigned="true" WantAssertionsSigned="true">
    <md:NameIDFormat>' . $sp['name_id_format'] . '</md:NameIDFormat>
    <md:AssertionConsumerService
        Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
        Location="' . htmlspecialchars($sp['acs_url'], ENT_XML1) . '"
        index="0" isDefault="true"/>
    <md:SingleLogoutService
        Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"
        Location="' . htmlspecialchars($sp['slo_url'], ENT_XML1) . '"/>
  </md:SPSSODescriptor>
</md:EntityDescriptor>';
}

/* ─── IdP provider CRUD ─── */

function epc_sso_provider_create(PDO $pdo, string $siteKey, array $data): array
{
    epc_sso_ensure_schema($pdo);

    $attrMap = $data['attribute_map'] ?? array(
        'email'      => 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
        'first_name' => 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname',
        'last_name'  => 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname',
        'role'       => 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
    );

    $st = $pdo->prepare("
        INSERT INTO `epc_sso_providers`
            (`site_key`, `provider_name`, `provider_type`, `entity_id`, `sso_url`, `slo_url`,
             `certificate`, `metadata_xml`, `attribute_map`, `jit_provision`, `default_role`, `active`, `created_by`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (string) ($data['provider_name'] ?? 'SSO Provider'),
        (string) ($data['provider_type'] ?? 'saml2'),
        (string) ($data['entity_id'] ?? ''),
        (string) ($data['sso_url'] ?? ''),
        (string) ($data['slo_url'] ?? ''),
        (string) ($data['certificate'] ?? ''),
        (string) ($data['metadata_xml'] ?? ''),
        json_encode($attrMap),
        (int) ($data['jit_provision'] ?? 1),
        (string) ($data['default_role'] ?? 'viewer'),
        (int) ($data['active'] ?? 0),
        (int) ($data['created_by'] ?? 0),
    ));

    return array('ok' => true, 'provider_id' => (int) $pdo->lastInsertId());
}

function epc_sso_provider_get(PDO $pdo, int $providerId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_sso_providers` WHERE `id` = ?");
    $st->execute(array($providerId));
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if (!$row) {
        return array();
    }
    $row['attribute_map'] = json_decode($row['attribute_map'] ?: '{}', true);
    return $row;
}

function epc_sso_provider_list(PDO $pdo, string $siteKey): array
{
    epc_sso_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `id`, `provider_name`, `provider_type`, `entity_id`, `sso_url`, `active`, `jit_provision`, `default_role`, `updated_at`
        FROM `epc_sso_providers`
        WHERE `site_key` = ?
        ORDER BY `provider_name`
    ");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_sso_provider_toggle(PDO $pdo, int $providerId, bool $active): bool
{
    $st = $pdo->prepare("UPDATE `epc_sso_providers` SET `active` = ? WHERE `id` = ?");
    return $st->execute(array((int) $active, $providerId));
}

function epc_sso_provider_delete(PDO $pdo, int $providerId): bool
{
    $st = $pdo->prepare("DELETE FROM `epc_sso_providers` WHERE `id` = ?");
    return $st->execute(array($providerId));
}

/* ─── SAML AuthnRequest generation ─── */

function epc_sso_authn_request(string $siteKey, array $provider): array
{
    $sp = epc_sso_sp_metadata($siteKey);
    $id = '_' . bin2hex(random_bytes(16));
    $issueInstant = gmdate('Y-m-d\TH:i:s\Z');

    $request = '<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
        xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
        ID="' . $id . '"
        Version="2.0"
        IssueInstant="' . $issueInstant . '"
        Destination="' . htmlspecialchars($provider['sso_url'], ENT_XML1) . '"
        AssertionConsumerServiceURL="' . htmlspecialchars($sp['acs_url'], ENT_XML1) . '"
        ProtocolBinding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST">
    <saml:Issuer>' . htmlspecialchars($sp['entity_id'], ENT_XML1) . '</saml:Issuer>
    <samlp:NameIDPolicy Format="' . $sp['name_id_format'] . '" AllowCreate="true"/>
</samlp:AuthnRequest>';

    $encoded = base64_encode(gzdeflate($request));
    $redirectUrl = $provider['sso_url'] . '?' . http_build_query(array('SAMLRequest' => $encoded));

    return array(
        'ok'           => true,
        'request_id'   => $id,
        'redirect_url' => $redirectUrl,
        'raw_request'  => $request,
    );
}

/* ─── SAML Response processing ─── */

function epc_sso_process_response(PDO $pdo, string $siteKey, string $samlResponse, int $providerId): array
{
    $provider = epc_sso_provider_get($pdo, $providerId);
    if (empty($provider)) {
        return array('ok' => false, 'error' => 'Provider not found');
    }

    $xml = base64_decode($samlResponse);
    if ($xml === false) {
        return array('ok' => false, 'error' => 'Invalid SAML response encoding');
    }

    $doc = new \DOMDocument();
    $loadResult = @$doc->loadXML($xml);
    if (!$loadResult) {
        return array('ok' => false, 'error' => 'Invalid SAML response XML');
    }

    $attrMap = $provider['attribute_map'] ?? array();
    $attributes = array();

    $xpath = new \DOMXPath($doc);
    $xpath->registerNamespace('saml', 'urn:oasis:names:tc:SAML:2.0:assertion');
    $xpath->registerNamespace('samlp', 'urn:oasis:names:tc:SAML:2.0:protocol');

    $statusNodes = $xpath->query('//samlp:Status/samlp:StatusCode/@Value');
    if ($statusNodes->length > 0) {
        $statusValue = $statusNodes->item(0)->nodeValue;
        if (strpos($statusValue, 'Success') === false) {
            return array('ok' => false, 'error' => 'SAML authentication failed: ' . $statusValue);
        }
    }

    $nameIdNodes = $xpath->query('//saml:Assertion/saml:Subject/saml:NameID');
    $nameId = $nameIdNodes->length > 0 ? $nameIdNodes->item(0)->nodeValue : '';

    $attrStatements = $xpath->query('//saml:Assertion/saml:AttributeStatement/saml:Attribute');
    foreach ($attrStatements as $attr) {
        $attrName = $attr->getAttribute('Name');
        $valueNodes = $xpath->query('saml:AttributeValue', $attr);
        $value = $valueNodes->length > 0 ? $valueNodes->item(0)->nodeValue : '';
        $attributes[$attrName] = $value;
    }

    $email = '';
    $firstName = '';
    $lastName = '';
    $role = $provider['default_role'];

    foreach ($attrMap as $field => $claim) {
        if (isset($attributes[$claim])) {
            switch ($field) {
                case 'email': $email = $attributes[$claim]; break;
                case 'first_name': $firstName = $attributes[$claim]; break;
                case 'last_name': $lastName = $attributes[$claim]; break;
                case 'role': $role = $attributes[$claim]; break;
            }
        }
    }

    if ($email === '' && $nameId !== '' && filter_var($nameId, FILTER_VALIDATE_EMAIL)) {
        $email = $nameId;
    }

    $sessionIndex = '';
    $siNodes = $xpath->query('//saml:Assertion/saml:AuthnStatement/@SessionIndex');
    if ($siNodes->length > 0) {
        $sessionIndex = $siNodes->item(0)->nodeValue;
    }

    $st = $pdo->prepare("
        INSERT INTO `epc_sso_sessions`
            (`site_key`, `provider_id`, `name_id`, `email`, `attributes`, `session_index`, `ip_address`, `user_agent`, `expires_at`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, DATE_ADD(NOW(), INTERVAL 8 HOUR))
    ");
    $st->execute(array(
        $siteKey,
        $providerId,
        $nameId,
        $email,
        json_encode($attributes),
        $sessionIndex,
        (string) ($_SERVER['REMOTE_ADDR'] ?? ''),
        substr((string) ($_SERVER['HTTP_USER_AGENT'] ?? ''), 0, 255),
    ));

    return array(
        'ok'            => true,
        'session_id'    => (int) $pdo->lastInsertId(),
        'email'         => $email,
        'first_name'    => $firstName,
        'last_name'     => $lastName,
        'role'          => $role,
        'name_id'       => $nameId,
        'jit_provision' => (bool) $provider['jit_provision'],
    );
}

/* ─── session management ─── */

function epc_sso_active_sessions(PDO $pdo, string $siteKey): array
{
    epc_sso_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT s.*, p.`provider_name`
        FROM `epc_sso_sessions` s
        JOIN `epc_sso_providers` p ON s.`provider_id` = p.`id`
        WHERE s.`site_key` = ? AND s.`status` = 'active' AND s.`expires_at` > NOW()
        ORDER BY s.`login_at` DESC
    ");
    $st->execute(array($siteKey));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$row) {
        $row['attributes'] = json_decode($row['attributes'] ?: '{}', true);
    }
    return $rows;
}

function epc_sso_logout(PDO $pdo, int $sessionId): bool
{
    $st = $pdo->prepare("UPDATE `epc_sso_sessions` SET `status` = 'logged_out', `logout_at` = NOW() WHERE `id` = ?");
    return $st->execute(array($sessionId));
}

function epc_sso_expire_old(PDO $pdo): int
{
    $st = $pdo->query("UPDATE `epc_sso_sessions` SET `status` = 'expired' WHERE `status` = 'active' AND `expires_at` < NOW()");
    return $st->rowCount();
}

/* ─── fleet stats (BOS) ─── */

function epc_sso_fleet_stats(PDO $pdo): array
{
    epc_sso_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT p.`site_key`,
               COUNT(DISTINCT p.`id`) AS `providers`,
               SUM(CASE WHEN p.`active` = 1 THEN 1 ELSE 0 END) AS `active_providers`,
               (SELECT COUNT(*) FROM `epc_sso_sessions` s WHERE s.`site_key` = p.`site_key` AND s.`status` = 'active') AS `active_sessions`,
               (SELECT COUNT(*) FROM `epc_sso_sessions` s WHERE s.`site_key` = p.`site_key`) AS `total_logins`
        FROM `epc_sso_providers` p
        GROUP BY p.`site_key`
        ORDER BY `active_providers` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

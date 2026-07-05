<?php
/**
 * CRM Integration Module — connect external CRMs (Salesforce, HubSpot, Zoho, Dynamics).
 * Bidirectional sync of contacts, leads, opportunities, activities.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_crm_int_ensure_schema')) {
    function epc_crm_int_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_connections` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `platform` varchar(30) NOT NULL DEFAULT '' COMMENT 'salesforce,hubspot,zoho,dynamics365,pipedrive,freshsales',
            `instance_url` varchar(500) NOT NULL DEFAULT '',
            `client_id` varchar(200) NOT NULL DEFAULT '',
            `client_secret` varchar(300) NOT NULL DEFAULT '',
            `access_token` varchar(1000) NOT NULL DEFAULT '',
            `refresh_token` varchar(500) NOT NULL DEFAULT '',
            `token_expires_at` datetime DEFAULT NULL,
            `sync_contacts` tinyint(1) NOT NULL DEFAULT 1,
            `sync_leads` tinyint(1) NOT NULL DEFAULT 1,
            `sync_opportunities` tinyint(1) NOT NULL DEFAULT 1,
            `sync_activities` tinyint(1) NOT NULL DEFAULT 0,
            `sync_direction` enum('pull','push','bidirectional') NOT NULL DEFAULT 'bidirectional',
            `last_sync_at` datetime DEFAULT NULL,
            `status` enum('active','paused','error','disconnected') NOT NULL DEFAULT 'active',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='External CRM connections'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_contact_map` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `connection_id` int(11) NOT NULL DEFAULT 0,
            `external_id` varchar(100) NOT NULL DEFAULT '',
            `internal_customer_id` int(11) NOT NULL DEFAULT 0,
            `external_name` varchar(200) NOT NULL DEFAULT '',
            `last_synced_at` datetime DEFAULT NULL,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_conn_ext` (`connection_id`, `external_id`),
            KEY `x_internal` (`internal_customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM contact ID mapping'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_sync_log` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `connection_id` int(11) NOT NULL DEFAULT 0,
            `entity_type` varchar(30) NOT NULL DEFAULT '',
            `direction` enum('pull','push') NOT NULL DEFAULT 'pull',
            `records_processed` int(11) NOT NULL DEFAULT 0,
            `records_synced` int(11) NOT NULL DEFAULT 0,
            `records_failed` int(11) NOT NULL DEFAULT 0,
            `error_details` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_connection` (`connection_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM sync log'");
    }

    function epc_crm_int_connect_salesforce(PDO $db, int $companyId, array $data): array
    {
        $tokenUrl = rtrim($data['instance_url'] ?? '', '/') . '/services/oauth2/token';
        $postData = http_build_query([
            'grant_type' => 'password',
            'client_id' => $data['client_id'] ?? '',
            'client_secret' => $data['client_secret'] ?? '',
            'username' => $data['username'] ?? '',
            'password' => $data['password'] ?? '',
        ]);

        $ctx = stream_context_create(['http' => ['method' => 'POST', 'header' => "Content-Type: application/x-www-form-urlencoded\r\n", 'content' => $postData, 'timeout' => 15]]);
        $response = @file_get_contents($tokenUrl, false, $ctx);
        if ($response === false) return ['ok' => false, 'error' => 'Failed to connect to Salesforce'];

        $json = json_decode($response, true);
        if (!isset($json['access_token'])) return ['ok' => false, 'error' => $json['error_description'] ?? 'Authentication failed'];

        $stmt = $db->prepare("INSERT INTO `epc_crm_connections` (`company_id`,`platform`,`instance_url`,`client_id`,`client_secret`,`access_token`,`refresh_token`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$companyId, 'salesforce', $json['instance_url'] ?? $data['instance_url'], $data['client_id'], $data['client_secret'], $json['access_token'], $json['refresh_token'] ?? '', 'active', time()]);

        return ['ok' => true, 'connection_id' => (int) $db->lastInsertId()];
    }

    function epc_crm_int_list(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_crm_connections` WHERE `company_id` = ? ORDER BY `time_created` DESC");
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}

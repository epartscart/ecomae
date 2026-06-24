<?php
/**
 * P2 #37 — Document Vault
 *
 * Versioned file storage per tenant: upload, version control,
 * folder hierarchy, access control, retention policies.
 * Schema: epc_vault_folders, epc_vault_documents, epc_vault_versions
 */

if (!defined('EPC_DOCUMENT_VAULT_VERSION')) {
    define('EPC_DOCUMENT_VAULT_VERSION', '1.0.0');
}

function epc_vault_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_vault_folders` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `parent_id`       INT UNSIGNED   NULL,
            `name`            VARCHAR(128)   NOT NULL,
            `path`            VARCHAR(512)   NOT NULL DEFAULT '/',
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_parent` (`parent_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_vault_documents` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `folder_id`       INT UNSIGNED   NULL,
            `filename`        VARCHAR(256)   NOT NULL,
            `mime_type`       VARCHAR(128)   NOT NULL DEFAULT 'application/octet-stream',
            `file_size`       BIGINT UNSIGNED NOT NULL DEFAULT 0,
            `current_version` INT UNSIGNED   NOT NULL DEFAULT 1,
            `tags`            JSON           NULL,
            `access_level`    ENUM('public','tenant','role','private') NOT NULL DEFAULT 'tenant',
            `retention_days`  INT UNSIGNED   NOT NULL DEFAULT 0,
            `status`          ENUM('active','archived','deleted') NOT NULL DEFAULT 'active',
            `uploaded_by`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_folder` (`folder_id`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_vault_versions` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `document_id`     INT UNSIGNED   NOT NULL,
            `version_number`  INT UNSIGNED   NOT NULL DEFAULT 1,
            `file_path`       VARCHAR(512)   NOT NULL,
            `file_size`       BIGINT UNSIGNED NOT NULL DEFAULT 0,
            `checksum`        VARCHAR(64)    NOT NULL DEFAULT '',
            `change_note`     VARCHAR(256)   NOT NULL DEFAULT '',
            `uploaded_by`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_doc` (`document_id`),
            UNIQUE KEY `uk_version` (`document_id`, `version_number`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_vault_create_folder(PDO $pdo, string $siteKey, string $name, int $parentId = 0, int $userId = 0): array
{
    epc_vault_ensure_schema($pdo);
    $parentPath = '/';
    if ($parentId > 0) {
        $st = $pdo->prepare("SELECT `path`, `name` FROM `epc_vault_folders` WHERE `id`=? AND `site_key`=?");
        $st->execute(array($parentId, $siteKey));
        $p = $st->fetch(PDO::FETCH_ASSOC);
        if ($p) $parentPath = rtrim($p['path'], '/') . '/' . $p['name'] . '/';
    }
    $st = $pdo->prepare("INSERT INTO `epc_vault_folders` (`site_key`,`parent_id`,`name`,`path`,`created_by`) VALUES (?,?,?,?,?)");
    $st->execute(array($siteKey, $parentId ?: null, $name, $parentPath, $userId));
    return array('ok' => true, 'folder_id' => (int)$pdo->lastInsertId(), 'path' => $parentPath . $name . '/');
}

function epc_vault_list_folders(PDO $pdo, string $siteKey, int $parentId = 0): array
{
    epc_vault_ensure_schema($pdo);
    if ($parentId > 0) {
        $st = $pdo->prepare("SELECT * FROM `epc_vault_folders` WHERE `site_key`=? AND `parent_id`=? ORDER BY `name`");
        $st->execute(array($siteKey, $parentId));
    } else {
        $st = $pdo->prepare("SELECT * FROM `epc_vault_folders` WHERE `site_key`=? AND `parent_id` IS NULL ORDER BY `name`");
        $st->execute(array($siteKey));
    }
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_vault_upload(PDO $pdo, string $siteKey, array $data): array
{
    epc_vault_ensure_schema($pdo);
    $st = $pdo->prepare("INSERT INTO `epc_vault_documents` (`site_key`,`folder_id`,`filename`,`mime_type`,`file_size`,`tags`,`access_level`,`retention_days`,`uploaded_by`) VALUES (?,?,?,?,?,?,?,?,?)");
    $st->execute(array(
        $siteKey, (int)($data['folder_id']??0) ?: null, (string)($data['filename']??''),
        (string)($data['mime_type']??'application/octet-stream'), (int)($data['file_size']??0),
        json_encode($data['tags']??array()), (string)($data['access_level']??'tenant'),
        (int)($data['retention_days']??0), (int)($data['uploaded_by']??0),
    ));
    $docId = (int)$pdo->lastInsertId();

    $pdo->prepare("INSERT INTO `epc_vault_versions` (`document_id`,`version_number`,`file_path`,`file_size`,`checksum`,`change_note`,`uploaded_by`) VALUES (?,1,?,?,?,?,?)")
        ->execute(array($docId, (string)($data['file_path']??''), (int)($data['file_size']??0), (string)($data['checksum']??''), 'Initial upload', (int)($data['uploaded_by']??0)));

    return array('ok' => true, 'document_id' => $docId, 'version' => 1);
}

function epc_vault_new_version(PDO $pdo, int $docId, array $data): array
{
    $st = $pdo->prepare("SELECT `current_version` FROM `epc_vault_documents` WHERE `id`=?");
    $st->execute(array($docId));
    $cv = (int)$st->fetchColumn();
    if ($cv === 0) return array('ok' => false, 'error' => 'Document not found');

    $newVer = $cv + 1;
    $pdo->prepare("INSERT INTO `epc_vault_versions` (`document_id`,`version_number`,`file_path`,`file_size`,`checksum`,`change_note`,`uploaded_by`) VALUES (?,?,?,?,?,?,?)")
        ->execute(array($docId, $newVer, (string)($data['file_path']??''), (int)($data['file_size']??0), (string)($data['checksum']??''), (string)($data['change_note']??''), (int)($data['uploaded_by']??0)));
    $pdo->prepare("UPDATE `epc_vault_documents` SET `current_version`=?, `file_size`=? WHERE `id`=?")->execute(array($newVer, (int)($data['file_size']??0), $docId));

    return array('ok' => true, 'version' => $newVer);
}

function epc_vault_list_documents(PDO $pdo, string $siteKey, int $folderId = 0): array
{
    epc_vault_ensure_schema($pdo);
    if ($folderId > 0) {
        $st = $pdo->prepare("SELECT * FROM `epc_vault_documents` WHERE `site_key`=? AND `folder_id`=? AND `status`='active' ORDER BY `filename`");
        $st->execute(array($siteKey, $folderId));
    } else {
        $st = $pdo->prepare("SELECT * FROM `epc_vault_documents` WHERE `site_key`=? AND `status`='active' ORDER BY `filename`");
        $st->execute(array($siteKey));
    }
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) { $r['tags'] = json_decode($r['tags']?:'[]', true); }
    return $rows;
}

function epc_vault_versions(PDO $pdo, int $docId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_vault_versions` WHERE `document_id`=? ORDER BY `version_number` DESC");
    $st->execute(array($docId));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_vault_fleet_stats(PDO $pdo): array
{
    epc_vault_ensure_schema($pdo);
    $st = $pdo->query("SELECT `site_key`, COUNT(*) AS `documents`, SUM(`file_size`) AS `total_size`, MAX(`current_version`) AS `max_versions` FROM `epc_vault_documents` WHERE `status`='active' GROUP BY `site_key`");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── GDPR / Data Subject Rights ─── */

function epc_vault_gdpr_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_vault_gdpr_requests` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `request_type`    ENUM('access','erasure','portability','rectification') NOT NULL,
            `subject_email`   VARCHAR(256)   NOT NULL,
            `subject_name`    VARCHAR(256)   NOT NULL DEFAULT '',
            `status`          ENUM('pending','processing','completed','rejected') NOT NULL DEFAULT 'pending',
            `documents_found` INT            NOT NULL DEFAULT 0,
            `response_json`   MEDIUMTEXT     NULL,
            `processed_by`    INT UNSIGNED   NULL,
            `completed_at`    DATETIME       NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_status` (`status`),
            INDEX `idx_email` (`subject_email`(64))
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    ");
}

function epc_vault_gdpr_request(PDO $pdo, string $siteKey, string $type, string $email, string $name = ''): array
{
    epc_vault_gdpr_ensure_schema($pdo);
    $pdo->prepare("INSERT INTO `epc_vault_gdpr_requests` (`site_key`,`request_type`,`subject_email`,`subject_name`) VALUES (?,?,?,?)")
        ->execute(array($siteKey, $type, $email, $name));
    return array('ok' => true, 'request_id' => (int) $pdo->lastInsertId(), 'message' => ucfirst($type) . ' request created');
}

function epc_vault_gdpr_process(PDO $pdo, int $requestId, int $userId): array
{
    epc_vault_gdpr_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_vault_gdpr_requests` WHERE `id`=?");
    $st->execute(array($requestId));
    $req = $st->fetch(PDO::FETCH_ASSOC);
    if (!$req) return array('ok' => false, 'error' => 'Request not found');

    epc_vault_ensure_schema($pdo);
    $stDocs = $pdo->prepare("SELECT `id`,`filename`,`mime_type`,`file_size`,`tags` FROM `epc_vault_documents` WHERE `site_key`=? AND `status`='active' AND (`tags` LIKE ? OR `filename` LIKE ?)");
    $emailLike = '%' . $req['subject_email'] . '%';
    $stDocs->execute(array($req['site_key'], $emailLike, $emailLike));
    $docs = $stDocs->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $response = array('documents_found' => count($docs), 'documents' => $docs);

    if ($req['request_type'] === 'erasure') {
        foreach ($docs as $doc) {
            $pdo->prepare("UPDATE `epc_vault_documents` SET `status`='deleted' WHERE `id`=?")->execute(array($doc['id']));
        }
        $response['erased'] = count($docs);
    }

    $pdo->prepare("UPDATE `epc_vault_gdpr_requests` SET `status`='completed', `documents_found`=?, `response_json`=?, `processed_by`=?, `completed_at`=NOW() WHERE `id`=?")
        ->execute(array(count($docs), json_encode($response), $userId, $requestId));

    return array('ok' => true, 'response' => $response);
}

function epc_vault_gdpr_list(PDO $pdo, string $siteKey = '', string $status = ''): array
{
    epc_vault_gdpr_ensure_schema($pdo);
    $where = array('1=1');
    $params = array();
    if ($siteKey !== '') { $where[] = '`site_key`=?'; $params[] = $siteKey; }
    if ($status !== '') { $where[] = '`status`=?'; $params[] = $status; }
    $st = $pdo->prepare("SELECT * FROM `epc_vault_gdpr_requests` WHERE " . implode(' AND ', $where) . " ORDER BY `created_at` DESC LIMIT 200");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Search & Lifecycle ─── */

function epc_vault_search(PDO $pdo, string $siteKey, string $query): array
{
    epc_vault_ensure_schema($pdo);
    $like = '%' . $query . '%';
    $st = $pdo->prepare("SELECT * FROM `epc_vault_documents` WHERE `site_key`=? AND `status`='active' AND (`filename` LIKE ? OR `tags` LIKE ?) ORDER BY `created_at` DESC LIMIT 100");
    $st->execute(array($siteKey, $like, $like));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_vault_delete(PDO $pdo, int $docId): array
{
    $pdo->prepare("UPDATE `epc_vault_documents` SET `status`='deleted' WHERE `id`=?")->execute(array($docId));
    return array('ok' => true);
}

function epc_vault_restore(PDO $pdo, int $docId): array
{
    $pdo->prepare("UPDATE `epc_vault_documents` SET `status`='active' WHERE `id`=?")->execute(array($docId));
    return array('ok' => true);
}

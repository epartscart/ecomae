<?php
/**
 * Document Attachment Module — attach files to any ERP transaction.
 * Supports PDF, images, Excel. Links to invoices, POs, journals, etc.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_doc_attach_ensure_schema')) {
    function epc_doc_attach_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_doc_attachments` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `entity_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'invoice,po,journal,payment,receipt,customer,supplier,inventory',
            `entity_id` int(11) NOT NULL DEFAULT 0,
            `file_name` varchar(300) NOT NULL DEFAULT '',
            `file_path` varchar(500) NOT NULL DEFAULT '',
            `file_size` int(11) NOT NULL DEFAULT 0,
            `mime_type` varchar(100) NOT NULL DEFAULT '',
            `description` varchar(500) NOT NULL DEFAULT '',
            `uploaded_by` int(11) NOT NULL DEFAULT 0,
            `uploaded_by_name` varchar(120) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_entity` (`entity_type`, `entity_id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Transaction document attachments'");
    }

    function epc_doc_attach_upload(PDO $db, array $data, array $file): array
    {
        $allowed = ['application/pdf','image/jpeg','image/png','image/gif',
            'application/vnd.ms-excel','application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'application/msword','application/vnd.openxmlformats-officedocument.wordprocessingml.document'];
        $maxSize = 10 * 1024 * 1024; // 10MB

        if (!isset($file['tmp_name']) || !is_uploaded_file($file['tmp_name'])) {
            return ['ok' => false, 'error' => 'No file uploaded'];
        }
        if ($file['size'] > $maxSize) {
            return ['ok' => false, 'error' => 'File too large (max 10MB)'];
        }
        $mime = mime_content_type($file['tmp_name']) ?: '';
        if (!in_array($mime, $allowed, true)) {
            return ['ok' => false, 'error' => 'File type not allowed'];
        }

        $uploadDir = $_SERVER['DOCUMENT_ROOT'] . '/uploads/doc_attachments/' . ($data['company_id'] ?? 0) . '/';
        if (!is_dir($uploadDir)) {
            @mkdir($uploadDir, 0755, true);
        }
        $safeName = preg_replace('/[^a-zA-Z0-9._-]/', '_', basename($file['name']));
        $uniqueName = time() . '_' . $safeName;
        $destPath = $uploadDir . $uniqueName;

        if (!move_uploaded_file($file['tmp_name'], $destPath)) {
            return ['ok' => false, 'error' => 'Failed to save file'];
        }

        $stmt = $db->prepare("INSERT INTO `epc_doc_attachments`
            (`company_id`,`entity_type`,`entity_id`,`file_name`,`file_path`,`file_size`,`mime_type`,`description`,`uploaded_by`,`uploaded_by_name`,`time_created`)
            VALUES (?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([
            $data['company_id'] ?? 0, $data['entity_type'] ?? '', $data['entity_id'] ?? 0,
            $safeName, $destPath, $file['size'], $mime, $data['description'] ?? '',
            $data['uploaded_by'] ?? 0, $data['uploaded_by_name'] ?? '', time()
        ]);

        return ['ok' => true, 'id' => (int) $db->lastInsertId(), 'file_name' => $safeName];
    }

    function epc_doc_attach_list(PDO $db, string $entityType, int $entityId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_doc_attachments` WHERE `entity_type` = ? AND `entity_id` = ? ORDER BY `time_created` DESC");
        $stmt->execute([$entityType, $entityId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_doc_attach_delete(PDO $db, int $id): bool
    {
        $stmt = $db->prepare("SELECT `file_path` FROM `epc_doc_attachments` WHERE `id` = ?");
        $stmt->execute([$id]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        if ($row && is_file($row['file_path'])) {
            @unlink($row['file_path']);
        }
        $db->prepare("DELETE FROM `epc_doc_attachments` WHERE `id` = ?")->execute([$id]);
        return true;
    }

    function epc_doc_attach_render_button(string $entityType, int $entityId): string
    {
        return '<button type="button" class="btn btn-default btn-xs epc-doc-attach-btn" data-entity-type="' . htmlspecialchars($entityType, ENT_QUOTES, 'UTF-8') . '" data-entity-id="' . $entityId . '" title="Attach document"><i class="fa fa-paperclip"></i> Attach</button>';
    }
}

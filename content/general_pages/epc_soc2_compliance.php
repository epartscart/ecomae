<?php
/**
 * P2 #39 — SOC 2 Compliance Toolkit
 *
 * SOC 2 Type II readiness: control catalog (Trust Service Criteria),
 * evidence collection, policy management, continuous monitoring,
 * gap analysis, audit trail.
 * Schema: epc_soc2_controls, epc_soc2_evidence, epc_soc2_policies
 */

if (!defined('EPC_SOC2_VERSION')) {
    define('EPC_SOC2_VERSION', '1.0.0');
}

function epc_soc2_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_soc2_controls` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `control_id`      VARCHAR(16)    NOT NULL UNIQUE,
            `category`        ENUM('security','availability','processing_integrity','confidentiality','privacy') NOT NULL,
            `title`           VARCHAR(256)   NOT NULL,
            `description`     TEXT           NOT NULL DEFAULT '',
            `implementation`  TEXT           NOT NULL DEFAULT '',
            `status`          ENUM('not_started','in_progress','implemented','tested','effective') NOT NULL DEFAULT 'not_started',
            `owner`           VARCHAR(128)   NOT NULL DEFAULT '',
            `frequency`       ENUM('continuous','daily','weekly','monthly','quarterly','annual') NOT NULL DEFAULT 'annual',
            `last_tested`     DATE           NULL,
            `next_review`     DATE           NULL,
            `risk_level`      ENUM('low','medium','high','critical') NOT NULL DEFAULT 'medium',
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_category` (`category`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_soc2_evidence` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `control_id`      VARCHAR(16)    NOT NULL,
            `evidence_type`   ENUM('screenshot','log','config','report','attestation','document') NOT NULL DEFAULT 'document',
            `title`           VARCHAR(256)   NOT NULL,
            `file_path`       VARCHAR(512)   NOT NULL DEFAULT '',
            `collected_at`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `collected_by`    VARCHAR(128)   NOT NULL DEFAULT '',
            `valid_from`      DATE           NULL,
            `valid_to`        DATE           NULL,
            `notes`           TEXT           NOT NULL DEFAULT '',
            INDEX `idx_control` (`control_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_soc2_policies` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `policy_code`     VARCHAR(32)    NOT NULL UNIQUE,
            `title`           VARCHAR(256)   NOT NULL,
            `content`         LONGTEXT       NOT NULL DEFAULT '',
            `version`         VARCHAR(16)    NOT NULL DEFAULT '1.0',
            `status`          ENUM('draft','review','approved','archived') NOT NULL DEFAULT 'draft',
            `owner`           VARCHAR(128)   NOT NULL DEFAULT '',
            `approved_by`     VARCHAR(128)   NOT NULL DEFAULT '',
            `approved_at`     DATETIME       NULL,
            `review_date`     DATE           NULL,
            `related_controls` JSON          NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_soc2_builtin_controls(): array
{
    return array(
        array('control_id' => 'CC1.1', 'category' => 'security', 'title' => 'Security governance & oversight', 'risk_level' => 'high'),
        array('control_id' => 'CC1.2', 'category' => 'security', 'title' => 'Board and management accountability', 'risk_level' => 'high'),
        array('control_id' => 'CC2.1', 'category' => 'security', 'title' => 'Information and communication policies', 'risk_level' => 'medium'),
        array('control_id' => 'CC3.1', 'category' => 'security', 'title' => 'Risk assessment process', 'risk_level' => 'high'),
        array('control_id' => 'CC4.1', 'category' => 'security', 'title' => 'Monitoring of controls', 'risk_level' => 'high'),
        array('control_id' => 'CC5.1', 'category' => 'security', 'title' => 'Logical access controls', 'risk_level' => 'critical'),
        array('control_id' => 'CC5.2', 'category' => 'security', 'title' => 'Authentication mechanisms (MFA)', 'risk_level' => 'critical'),
        array('control_id' => 'CC5.3', 'category' => 'security', 'title' => 'Access provisioning and deprovisioning', 'risk_level' => 'high'),
        array('control_id' => 'CC6.1', 'category' => 'security', 'title' => 'Encryption at rest and in transit', 'risk_level' => 'critical'),
        array('control_id' => 'CC6.2', 'category' => 'security', 'title' => 'Network security and firewall', 'risk_level' => 'critical'),
        array('control_id' => 'CC7.1', 'category' => 'security', 'title' => 'Incident detection and response', 'risk_level' => 'high'),
        array('control_id' => 'CC7.2', 'category' => 'security', 'title' => 'Incident communication', 'risk_level' => 'medium'),
        array('control_id' => 'CC8.1', 'category' => 'security', 'title' => 'Change management process', 'risk_level' => 'high'),
        array('control_id' => 'CC9.1', 'category' => 'security', 'title' => 'Vendor and subservice management', 'risk_level' => 'medium'),
        array('control_id' => 'A1.1',  'category' => 'availability', 'title' => 'System availability monitoring', 'risk_level' => 'high'),
        array('control_id' => 'A1.2',  'category' => 'availability', 'title' => 'Disaster recovery and BCP', 'risk_level' => 'critical'),
        array('control_id' => 'PI1.1', 'category' => 'processing_integrity', 'title' => 'Data validation and completeness', 'risk_level' => 'high'),
        array('control_id' => 'C1.1',  'category' => 'confidentiality', 'title' => 'Data classification and handling', 'risk_level' => 'high'),
        array('control_id' => 'C1.2',  'category' => 'confidentiality', 'title' => 'Tenant data isolation', 'risk_level' => 'critical'),
        array('control_id' => 'P1.1',  'category' => 'privacy', 'title' => 'Privacy notice and consent', 'risk_level' => 'medium'),
        array('control_id' => 'P1.2',  'category' => 'privacy', 'title' => 'Data retention and deletion', 'risk_level' => 'medium'),
    );
}

function epc_soc2_seed_controls(PDO $pdo): int
{
    epc_soc2_ensure_schema($pdo);
    $controls = epc_soc2_builtin_controls();
    $inserted = 0;
    foreach ($controls as $c) {
        $st = $pdo->prepare("SELECT COUNT(*) FROM `epc_soc2_controls` WHERE `control_id`=?");
        $st->execute(array($c['control_id']));
        if ((int)$st->fetchColumn() === 0) {
            $pdo->prepare("INSERT INTO `epc_soc2_controls` (`control_id`,`category`,`title`,`risk_level`) VALUES (?,?,?,?)")
                ->execute(array($c['control_id'], $c['category'], $c['title'], $c['risk_level']));
            $inserted++;
        }
    }
    return $inserted;
}

function epc_soc2_list_controls(PDO $pdo, string $category = ''): array
{
    epc_soc2_ensure_schema($pdo);
    if ($category !== '') {
        $st = $pdo->prepare("SELECT * FROM `epc_soc2_controls` WHERE `category`=? ORDER BY `control_id`");
        $st->execute(array($category));
    } else {
        $st = $pdo->query("SELECT * FROM `epc_soc2_controls` ORDER BY `control_id`");
    }
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_soc2_update_control(PDO $pdo, string $controlId, array $data): array
{
    $fields = array();
    $params = array();
    $allowed = array('status', 'implementation', 'owner', 'frequency', 'last_tested', 'next_review');
    foreach ($allowed as $f) {
        if (isset($data[$f])) { $fields[] = "`{$f}` = ?"; $params[] = $data[$f]; }
    }
    if (empty($fields)) return array('ok' => false, 'error' => 'No fields to update');
    $params[] = $controlId;
    $pdo->prepare("UPDATE `epc_soc2_controls` SET " . implode(', ', $fields) . " WHERE `control_id`=?")->execute($params);
    return array('ok' => true);
}

function epc_soc2_add_evidence(PDO $pdo, string $controlId, array $data): array
{
    $pdo->prepare("INSERT INTO `epc_soc2_evidence` (`control_id`,`evidence_type`,`title`,`file_path`,`collected_by`,`valid_from`,`valid_to`,`notes`) VALUES (?,?,?,?,?,?,?,?)")
        ->execute(array($controlId, (string)($data['evidence_type']??'document'), (string)($data['title']??''), (string)($data['file_path']??''), (string)($data['collected_by']??''), $data['valid_from']??null, $data['valid_to']??null, (string)($data['notes']??'')));
    return array('ok' => true, 'evidence_id' => (int)$pdo->lastInsertId());
}

function epc_soc2_control_evidence(PDO $pdo, string $controlId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_soc2_evidence` WHERE `control_id`=? ORDER BY `collected_at` DESC");
    $st->execute(array($controlId));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_soc2_gap_analysis(PDO $pdo): array
{
    epc_soc2_ensure_schema($pdo);
    $controls = epc_soc2_list_controls($pdo);
    $total = count($controls);
    $byStatus = array('not_started' => 0, 'in_progress' => 0, 'implemented' => 0, 'tested' => 0, 'effective' => 0);
    $gaps = array();
    foreach ($controls as $c) {
        $byStatus[$c['status']] = ($byStatus[$c['status']] ?? 0) + 1;
        if ($c['status'] === 'not_started' || $c['status'] === 'in_progress') {
            $gaps[] = array('control_id' => $c['control_id'], 'title' => $c['title'], 'risk_level' => $c['risk_level'], 'status' => $c['status']);
        }
    }
    $readiness = $total > 0 ? round(($byStatus['effective'] + $byStatus['tested']) / $total * 100, 1) : 0;
    return array('total_controls' => $total, 'by_status' => $byStatus, 'gaps' => $gaps, 'readiness_pct' => $readiness);
}

function epc_soc2_create_policy(PDO $pdo, array $data): array
{
    epc_soc2_ensure_schema($pdo);
    $pdo->prepare("INSERT INTO `epc_soc2_policies` (`policy_code`,`title`,`content`,`owner`,`related_controls`) VALUES (?,?,?,?,?)")
        ->execute(array(strtoupper((string)($data['policy_code']??'')), (string)($data['title']??''), (string)($data['content']??''), (string)($data['owner']??''), json_encode($data['related_controls']??array())));
    return array('ok' => true, 'policy_id' => (int)$pdo->lastInsertId());
}

function epc_soc2_list_policies(PDO $pdo): array
{
    epc_soc2_ensure_schema($pdo);
    $st = $pdo->query("SELECT * FROM `epc_soc2_policies` ORDER BY `policy_code`");
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) { $r['related_controls'] = json_decode($r['related_controls']?:'[]', true); }
    return $rows;
}

function epc_soc2_fleet_stats(PDO $pdo): array
{
    epc_soc2_ensure_schema($pdo);
    $gap = epc_soc2_gap_analysis($pdo);
    $st = $pdo->query("SELECT COUNT(*) FROM `epc_soc2_policies`");
    $policies = (int)$st->fetchColumn();
    $st2 = $pdo->query("SELECT COUNT(*) FROM `epc_soc2_evidence`");
    $evidence = (int)$st2->fetchColumn();
    return array('controls' => $gap['total_controls'], 'readiness_pct' => $gap['readiness_pct'], 'gaps' => count($gap['gaps']), 'policies' => $policies, 'evidence_items' => $evidence);
}

/* ─── Audit Schedule & Remediation ─── */

function epc_soc2_remediation_plan(PDO $pdo): array
{
    epc_soc2_ensure_schema($pdo);
    $gap = epc_soc2_gap_analysis($pdo);
    $plan = array();
    $priorityMap = array('critical' => 1, 'high' => 2, 'medium' => 3, 'low' => 4);
    foreach ($gap['gaps'] as $g) {
        $plan[] = array(
            'control_id' => $g['control_id'],
            'title' => $g['title'],
            'risk_level' => $g['risk_level'],
            'priority' => $priorityMap[$g['risk_level']] ?? 5,
            'status' => $g['status'],
            'action' => $g['status'] === 'not_started' ? 'Implement control' : 'Complete implementation',
        );
    }
    usort($plan, function($a, $b) { return $a['priority'] - $b['priority']; });
    return $plan;
}

function epc_soc2_evidence_summary(PDO $pdo): array
{
    $st = $pdo->query("SELECT `control_id`, COUNT(*) AS `evidence_count`, MAX(`collected_at`) AS `last_collected` FROM `epc_soc2_evidence` GROUP BY `control_id`");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_soc2_update_policy(PDO $pdo, int $policyId, array $data): array
{
    $fields = array();
    $params = array();
    $allowed = array('title', 'content', 'owner', 'status');
    foreach ($allowed as $f) {
        if (array_key_exists($f, $data)) {
            $fields[] = "`{$f}` = ?";
            $params[] = $data[$f];
        }
    }
    if (empty($fields)) return array('ok' => false);
    $fields[] = '`updated_at` = NOW()';
    $params[] = $policyId;
    $pdo->prepare("UPDATE `epc_soc2_policies` SET " . implode(', ', $fields) . " WHERE `id`=?")->execute($params);
    return array('ok' => true);
}

function epc_soc2_compliance_report(PDO $pdo): array
{
    $gap = epc_soc2_gap_analysis($pdo);
    $evidence = epc_soc2_evidence_summary($pdo);
    $policies = epc_soc2_list_policies($pdo);
    return array(
        'generated_at' => date('c'),
        'readiness' => $gap['readiness_pct'],
        'controls_total' => $gap['total_controls'],
        'controls_by_status' => $gap['by_status'],
        'outstanding_gaps' => count($gap['gaps']),
        'evidence_items' => count($evidence),
        'policies' => count($policies),
        'report_type' => 'SOC 2 Type II Readiness Assessment',
    );
}

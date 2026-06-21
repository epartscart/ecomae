<?php
/**
 * Platform depth — electronic reporting (configurable export formats).
 *
 * Define an electronic reporting format (output type + field mappings) once,
 * then generate a structured file (CSV / XML / JSON) from any dataset of rows.
 * Each generation is logged as a run. Additive epc_er_* tables, multi-company.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_er_ensure_schema')) {
    function epc_er_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_er_format` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `output_type` varchar(8) NOT NULL DEFAULT 'csv',
            `root_element` varchar(60) NOT NULL DEFAULT 'rows',
            `row_element` varchar(60) NOT NULL DEFAULT 'row',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Electronic reporting formats'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_er_field` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `format_id` int(11) NOT NULL DEFAULT 0,
            `label` varchar(80) NOT NULL DEFAULT '',
            `source_key` varchar(80) NOT NULL DEFAULT '',
            `ordinal` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_format` (`format_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Electronic reporting field map'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_er_run` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `format_id` int(11) NOT NULL DEFAULT 0,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `row_count` int(11) NOT NULL DEFAULT 0,
            `output_type` varchar(8) NOT NULL DEFAULT 'csv',
            `preview` mediumtext,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_format` (`format_id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Electronic reporting runs'");
    }
}

if (!function_exists('epc_er_format_save')) {
    /** @param array<string,mixed> $data company_id, code, name, output_type, root_element, row_element, active */
    function epc_er_format_save(PDO $db, array $data, int $id = 0): int
    {
        epc_er_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        $name = trim((string) ($data['name'] ?? ''));
        if ($code === '' || $name === '') {
            throw new Exception('Code and name are required');
        }
        $type = (string) ($data['output_type'] ?? 'csv');
        if (!in_array($type, array('csv', 'xml', 'json'), true)) {
            throw new Exception('Output type must be csv, xml or json');
        }
        $active = isset($data['active']) ? (int) ((int) $data['active'] === 1) : 1;
        if ($id > 0) {
            $db->prepare("UPDATE `epc_er_format` SET `code`=?, `name`=?, `output_type`=?, `root_element`=?, `row_element`=?, `active`=? WHERE `id`=?")
               ->execute(array($code, $name, $type, (string) ($data['root_element'] ?? 'rows'), (string) ($data['row_element'] ?? 'row'), $active, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_er_format` (`company_id`,`code`,`name`,`output_type`,`root_element`,`row_element`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $code, $name, $type, (string) ($data['root_element'] ?? 'rows'), (string) ($data['row_element'] ?? 'row'), $active, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_er_format_get')) {
    /** @return array<string,mixed>|null */
    function epc_er_format_get(PDO $db, int $id): ?array
    {
        epc_er_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_er_format` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_er_formats')) {
    /** @return array<int,array<string,mixed>> */
    function epc_er_formats(PDO $db, int $companyId): array
    {
        epc_er_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_er_format` WHERE `company_id`=? ORDER BY `code` ASC");
        $st->execute(array($companyId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_er_field_add')) {
    /** @param array<string,mixed> $data label, source_key, ordinal */
    function epc_er_field_add(PDO $db, int $formatId, array $data): int
    {
        epc_er_ensure_schema($db);
        if (!epc_er_format_get($db, $formatId)) {
            throw new Exception('Format not found');
        }
        $label = trim((string) ($data['label'] ?? ''));
        $key = trim((string) ($data['source_key'] ?? ''));
        if ($label === '' || $key === '') {
            throw new Exception('Field label and source key are required');
        }
        $ord = isset($data['ordinal']) ? (int) $data['ordinal'] : 0;
        if ($ord <= 0) {
            $st = $db->prepare("SELECT COALESCE(MAX(`ordinal`),0)+1 FROM `epc_er_field` WHERE `format_id`=?");
            $st->execute(array($formatId));
            $ord = (int) $st->fetchColumn();
        }
        $db->prepare("INSERT INTO `epc_er_field` (`format_id`,`label`,`source_key`,`ordinal`) VALUES (?,?,?,?)")
           ->execute(array($formatId, $label, $key, $ord));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_er_fields')) {
    /** @return array<int,array<string,mixed>> */
    function epc_er_fields(PDO $db, int $formatId): array
    {
        epc_er_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_er_field` WHERE `format_id`=? ORDER BY `ordinal` ASC, `id` ASC");
        $st->execute(array($formatId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_er_render')) {
    /**
     * Render rows into the format's output type using its field map.
     * @param array<int,array<string,mixed>> $rows
     */
    function epc_er_render(PDO $db, int $formatId, array $rows): string
    {
        $format = epc_er_format_get($db, $formatId);
        if (!$format) {
            throw new Exception('Format not found');
        }
        $fields = epc_er_fields($db, $formatId);
        if (count($fields) === 0) {
            throw new Exception('Add at least one field before generating');
        }
        $type = (string) $format['output_type'];
        if ($type === 'csv') {
            $out = implode(',', array_map('epc_er_csv_cell', array_column($fields, 'label')));
            foreach ($rows as $r) {
                $cells = array();
                foreach ($fields as $f) {
                    $cells[] = epc_er_csv_cell((string) ($r[$f['source_key']] ?? ''));
                }
                $out .= "\n" . implode(',', $cells);
            }
            return $out;
        }
        if ($type === 'json') {
            $mapped = array();
            foreach ($rows as $r) {
                $obj = array();
                foreach ($fields as $f) {
                    $obj[$f['label']] = $r[$f['source_key']] ?? null;
                }
                $mapped[] = $obj;
            }
            return (string) json_encode(array($format['root_element'] => $mapped), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
        }
        // xml
        $root = (string) $format['root_element'];
        $rowEl = (string) $format['row_element'];
        $out = '<?xml version="1.0" encoding="UTF-8"?>' . "\n<" . $root . ">";
        foreach ($rows as $r) {
            $out .= "\n  <" . $rowEl . ">";
            foreach ($fields as $f) {
                $tag = preg_replace('/[^A-Za-z0-9_]/', '_', (string) $f['label']);
                $out .= "\n    <" . $tag . '>' . htmlspecialchars((string) ($r[$f['source_key']] ?? ''), ENT_XML1 | ENT_QUOTES, 'UTF-8') . '</' . $tag . '>';
            }
            $out .= "\n  </" . $rowEl . ">";
        }
        $out .= "\n</" . $root . ">";
        return $out;
    }
}

if (!function_exists('epc_er_csv_cell')) {
    function epc_er_csv_cell(string $v): string
    {
        if (strpbrk($v, ",\"\n") !== false) {
            return '"' . str_replace('"', '""', $v) . '"';
        }
        return $v;
    }
}

if (!function_exists('epc_er_generate')) {
    /**
     * Render rows and log a run; returns the run id.
     * @param array<int,array<string,mixed>> $rows
     */
    function epc_er_generate(PDO $db, int $formatId, int $companyId, array $rows): int
    {
        $format = epc_er_format_get($db, $formatId);
        if (!$format) {
            throw new Exception('Format not found');
        }
        $output = epc_er_render($db, $formatId, $rows);
        $preview = mb_substr($output, 0, 4000);
        $db->prepare("INSERT INTO `epc_er_run` (`format_id`,`company_id`,`row_count`,`output_type`,`preview`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array($formatId, $companyId, count($rows), (string) $format['output_type'], $preview, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_er_runs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_er_runs(PDO $db, int $companyId, int $formatId = 0): array
    {
        epc_er_ensure_schema($db);
        $sql = "SELECT * FROM `epc_er_run` WHERE `company_id`=?";
        $args = array($companyId);
        if ($formatId > 0) {
            $sql .= " AND `format_id`=?";
            $args[] = $formatId;
        }
        $sql .= " ORDER BY `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_er_run_get')) {
    /** @return array<string,mixed>|null */
    function epc_er_run_get(PDO $db, int $id): ?array
    {
        epc_er_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_er_run` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

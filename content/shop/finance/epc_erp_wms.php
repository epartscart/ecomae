<?php
/**
 * Advanced Warehouse Management (WMS) — enterprise warehousing layer:
 * warehouse locations / bins (zones), license plates (LP / pallets), work
 * pool (put-away, pick, count, move), wave processing for outbound orders,
 * and mobile "RF" step actions (scan-driven complete).
 *
 * Multi-company aware (company_id stamping + filtering). Additive — does not
 * change posting/GL; inventory quantity-on-hand is reflected by LP balances
 * per location, mirroring the platform's on-hand-by-location model.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_wms_ensure_schema')) {
    function epc_wms_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_wms_locations` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `warehouse` varchar(40) NOT NULL DEFAULT 'MAIN',
            `zone` varchar(40) NOT NULL DEFAULT '',
            `code` varchar(60) NOT NULL DEFAULT '',
            `type` varchar(16) NOT NULL DEFAULT 'pick',
            `capacity` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_wh` (`warehouse`),
            UNIQUE KEY `x_loc` (`company_id`,`warehouse`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='WMS warehouse locations / bins'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_wms_lp` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `lp_code` varchar(60) NOT NULL DEFAULT '',
            `location_id` int(11) NOT NULL DEFAULT 0,
            `item` varchar(120) NOT NULL DEFAULT '',
            `qty` decimal(18,3) NOT NULL DEFAULT 0.000,
            `status` varchar(16) NOT NULL DEFAULT 'active',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_loc` (`location_id`),
            KEY `x_item` (`item`),
            UNIQUE KEY `x_lp` (`company_id`,`lp_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='WMS license plates (pallets)'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_wms_waves` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `wave_no` varchar(40) NOT NULL DEFAULT '',
            `reference` varchar(120) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='WMS waves'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_wms_work` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `work_type` varchar(16) NOT NULL DEFAULT 'pick',
            `reference` varchar(120) NOT NULL DEFAULT '',
            `wave_id` int(11) NOT NULL DEFAULT 0,
            `item` varchar(120) NOT NULL DEFAULT '',
            `qty` decimal(18,3) NOT NULL DEFAULT 0.000,
            `from_location_id` int(11) NOT NULL DEFAULT 0,
            `to_location_id` int(11) NOT NULL DEFAULT 0,
            `lp_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `assigned_to` varchar(120) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`),
            KEY `x_wave` (`wave_id`),
            KEY `x_type` (`work_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='WMS work pool (pick/putaway/count/move)'");
    }
}

if (!function_exists('epc_wms_work_types')) {
    /** @return array<string,string> */
    function epc_wms_work_types(): array
    {
        return array(
            'putaway' => 'Put away',
            'pick' => 'Pick',
            'move' => 'Move',
            'count' => 'Cycle count',
        );
    }
}

if (!function_exists('epc_wms_location_types')) {
    /** @return array<string,string> */
    function epc_wms_location_types(): array
    {
        return array(
            'receive' => 'Receiving dock',
            'pick' => 'Pick face',
            'bulk' => 'Bulk storage',
            'ship' => 'Shipping dock',
            'count' => 'Count / quarantine',
        );
    }
}

/* ---------------- locations ---------------- */

if (!function_exists('epc_wms_location_save')) {
    /** @param array<string,mixed> $data */
    function epc_wms_location_save(PDO $db, array $data, int $id = 0): int
    {
        epc_wms_ensure_schema($db);
        $type = (string) ($data['type'] ?? 'pick');
        if (!isset(epc_wms_location_types()[$type])) {
            $type = 'pick';
        }
        $code = strtoupper(trim((string) ($data['code'] ?? '')));
        if ($code === '') {
            throw new Exception('Location code is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_erp_wms_locations` SET `warehouse`=?, `zone`=?, `code`=?, `type`=?, `capacity`=?, `active`=? WHERE `id`=?")
               ->execute(array(
                   strtoupper((string) ($data['warehouse'] ?? 'MAIN')), (string) ($data['zone'] ?? ''), $code, $type,
                   (int) ($data['capacity'] ?? 0), (int) ($data['active'] ?? 1), $id,
               ));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_erp_wms_locations` (`company_id`,`warehouse`,`zone`,`code`,`type`,`capacity`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array(
               (int) ($data['company_id'] ?? 0), strtoupper((string) ($data['warehouse'] ?? 'MAIN')), (string) ($data['zone'] ?? ''),
               $code, $type, (int) ($data['capacity'] ?? 0), (int) ($data['active'] ?? 1), time(),
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_wms_location_get')) {
    /** @return array<string,mixed>|null */
    function epc_wms_location_get(PDO $db, int $id): ?array
    {
        epc_wms_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_wms_locations` WHERE `id`=?");
        $st->execute(array($id));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_wms_locations')) {
    /** @return array<int,array<string,mixed>> */
    function epc_wms_locations(PDO $db, int $companyId = 0, string $warehouse = ''): array
    {
        epc_wms_ensure_schema($db);
        $sql = "SELECT l.*, (SELECT COALESCE(SUM(p.qty),0) FROM `epc_erp_wms_lp` p WHERE p.location_id=l.id AND p.status='active') AS on_hand
                FROM `epc_erp_wms_locations` l WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND l.company_id=?";
            $args[] = $companyId;
        }
        if ($warehouse !== '') {
            $sql .= " AND l.warehouse=?";
            $args[] = strtoupper($warehouse);
        }
        $sql .= " ORDER BY l.warehouse, l.zone, l.code";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_wms_location_delete')) {
    function epc_wms_location_delete(PDO $db, int $id): void
    {
        epc_wms_ensure_schema($db);
        $n = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE location_id=" . $id . " AND status='active' AND qty>0")->fetchColumn();
        if ($n > 0) {
            throw new Exception('Location holds stock — move or close license plates first');
        }
        $db->prepare("DELETE FROM `epc_erp_wms_locations` WHERE `id`=?")->execute(array($id));
    }
}

/* ---------------- license plates ---------------- */

if (!function_exists('epc_wms_lp_get')) {
    /** @return array<string,mixed>|null */
    function epc_wms_lp_get(PDO $db, int $id): ?array
    {
        epc_wms_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_wms_lp` WHERE `id`=?");
        $st->execute(array($id));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_wms_lp_upsert')) {
    /**
     * Add qty of an item onto a license plate at a location (creates the LP if
     * new). Returns the LP id. Merges onto an existing active LP with the same
     * code, else creates one.
     */
    function epc_wms_lp_upsert(PDO $db, int $companyId, string $lpCode, int $locationId, string $item, float $qty): int
    {
        epc_wms_ensure_schema($db);
        $lpCode = strtoupper(trim($lpCode));
        if ($lpCode === '') {
            $lpCode = 'LP' . str_pad((string) (epc_wms_next_seq($db, $companyId, 'lp')), 6, '0', STR_PAD_LEFT);
        }
        $st = $db->prepare("SELECT * FROM `epc_erp_wms_lp` WHERE `company_id`=? AND `lp_code`=? LIMIT 1");
        $st->execute(array($companyId, $lpCode));
        $ex = $st->fetch(PDO::FETCH_ASSOC);
        if ($ex) {
            $newQty = (float) $ex['qty'] + $qty;
            $db->prepare("UPDATE `epc_erp_wms_lp` SET `location_id`=?, `item`=?, `qty`=?, `status`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array($locationId, $item !== '' ? $item : $ex['item'], $newQty, $newQty > 0 ? 'active' : 'closed', time(), (int) $ex['id']));
            return (int) $ex['id'];
        }
        $db->prepare("INSERT INTO `epc_erp_wms_lp` (`company_id`,`lp_code`,`location_id`,`item`,`qty`,`status`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array($companyId, $lpCode, $locationId, $item, $qty, $qty > 0 ? 'active' : 'closed', time(), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_wms_lp_move')) {
    /** Move an LP to a new location (whole-LP move). */
    function epc_wms_lp_move(PDO $db, int $lpId, int $toLocationId): void
    {
        epc_wms_ensure_schema($db);
        $db->prepare("UPDATE `epc_erp_wms_lp` SET `location_id`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($toLocationId, time(), $lpId));
    }
}

if (!function_exists('epc_wms_lp_adjust')) {
    /** Adjust qty on an LP by a delta (negative to deduct); closes LP at zero. */
    function epc_wms_lp_adjust(PDO $db, int $lpId, float $delta): float
    {
        epc_wms_ensure_schema($db);
        $lp = epc_wms_lp_get($db, $lpId);
        if (!$lp) {
            throw new Exception('License plate not found');
        }
        $newQty = round((float) $lp['qty'] + $delta, 3);
        if ($newQty < 0) {
            throw new Exception('Insufficient quantity on license plate');
        }
        $db->prepare("UPDATE `epc_erp_wms_lp` SET `qty`=?, `status`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($newQty, $newQty > 0 ? 'active' : 'closed', time(), $lpId));
        return $newQty;
    }
}

if (!function_exists('epc_wms_lps')) {
    /** @return array<int,array<string,mixed>> */
    function epc_wms_lps(PDO $db, int $companyId = 0, string $item = '', bool $activeOnly = true): array
    {
        epc_wms_ensure_schema($db);
        $sql = "SELECT p.*, l.code AS location_code, l.warehouse FROM `epc_erp_wms_lp` p LEFT JOIN `epc_erp_wms_locations` l ON l.id=p.location_id WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND p.company_id=?";
            $args[] = $companyId;
        }
        if ($item !== '') {
            $sql .= " AND p.item=?";
            $args[] = $item;
        }
        if ($activeOnly) {
            $sql .= " AND p.status='active'";
        }
        $sql .= " ORDER BY p.id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_wms_on_hand')) {
    /** Total active on-hand qty for an item (optionally a location). */
    function epc_wms_on_hand(PDO $db, int $companyId, string $item, int $locationId = 0): float
    {
        epc_wms_ensure_schema($db);
        $sql = "SELECT COALESCE(SUM(qty),0) FROM `epc_erp_wms_lp` WHERE company_id=? AND item=? AND status='active'";
        $args = array($companyId, $item);
        if ($locationId > 0) {
            $sql .= " AND location_id=?";
            $args[] = $locationId;
        }
        $st = $db->prepare($sql);
        $st->execute($args);
        return (float) $st->fetchColumn();
    }
}

/* ---------------- sequences ---------------- */

if (!function_exists('epc_wms_next_seq')) {
    function epc_wms_next_seq(PDO $db, int $companyId, string $kind): int
    {
        epc_wms_ensure_schema($db);
        if ($kind === 'wave') {
            return (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_waves` WHERE company_id=" . $companyId)->fetchColumn() + 1;
        }
        if ($kind === 'work') {
            return (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE company_id=" . $companyId)->fetchColumn() + 1;
        }
        return (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE company_id=" . $companyId)->fetchColumn() + 1;
    }
}

/* ---------------- work ---------------- */

if (!function_exists('epc_wms_work_create')) {
    /** @param array<string,mixed> $data */
    function epc_wms_work_create(PDO $db, array $data): int
    {
        epc_wms_ensure_schema($db);
        $type = (string) ($data['work_type'] ?? 'pick');
        if (!isset(epc_wms_work_types()[$type])) {
            $type = 'pick';
        }
        $db->prepare("INSERT INTO `epc_erp_wms_work` (`company_id`,`work_type`,`reference`,`wave_id`,`item`,`qty`,`from_location_id`,`to_location_id`,`lp_id`,`status`,`assigned_to`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)")
           ->execute(array(
               (int) ($data['company_id'] ?? 0), $type, (string) ($data['reference'] ?? ''), (int) ($data['wave_id'] ?? 0),
               (string) ($data['item'] ?? ''), (float) ($data['qty'] ?? 0), (int) ($data['from_location_id'] ?? 0),
               (int) ($data['to_location_id'] ?? 0), (int) ($data['lp_id'] ?? 0), (string) ($data['status'] ?? 'open'),
               (string) ($data['assigned_to'] ?? ''), time(), time(),
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_wms_work_get')) {
    /** @return array<string,mixed>|null */
    function epc_wms_work_get(PDO $db, int $id): ?array
    {
        epc_wms_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_wms_work` WHERE `id`=?");
        $st->execute(array($id));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_wms_work_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_wms_work_list(PDO $db, int $companyId = 0, string $status = '', string $type = ''): array
    {
        epc_wms_ensure_schema($db);
        $sql = "SELECT w.*, lf.code AS from_code, lt.code AS to_code FROM `epc_erp_wms_work` w
                LEFT JOIN `epc_erp_wms_locations` lf ON lf.id=w.from_location_id
                LEFT JOIN `epc_erp_wms_locations` lt ON lt.id=w.to_location_id WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND w.company_id=?";
            $args[] = $companyId;
        }
        if ($status !== '') {
            $sql .= " AND w.status=?";
            $args[] = $status;
        }
        if ($type !== '') {
            $sql .= " AND w.work_type=?";
            $args[] = $type;
        }
        $sql .= " ORDER BY (w.status='closed'), w.id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_wms_work_assign')) {
    function epc_wms_work_assign(PDO $db, int $id, string $user): void
    {
        epc_wms_ensure_schema($db);
        $db->prepare("UPDATE `epc_erp_wms_work` SET `assigned_to`=?, `status`=IF(`status`='open','in_progress',`status`), `time_updated`=? WHERE `id`=?")
           ->execute(array($user, time(), $id));
    }
}

if (!function_exists('epc_wms_work_complete')) {
    /**
     * Complete a work line (the RF "scan + confirm" step). Applies the stock
     * effect by work type:
     *  - putaway: move/created LP from receiving to the destination location.
     *  - pick:    deduct qty from a source LP at from_location.
     *  - move:    relocate the LP (or qty) to to_location.
     *  - count:   set on-hand of the counted LP to qty (adjustment).
     */
    function epc_wms_work_complete(PDO $db, int $id): array
    {
        epc_wms_ensure_schema($db);
        $w = epc_wms_work_get($db, $id);
        if (!$w) {
            throw new Exception('Work not found');
        }
        if ((string) $w['status'] === 'closed') {
            throw new Exception('Work already closed');
        }
        $companyId = (int) $w['company_id'];
        $type = (string) $w['work_type'];
        $qty = (float) $w['qty'];
        $lpId = (int) $w['lp_id'];

        if ($type === 'putaway') {
            if ($lpId > 0) {
                epc_wms_lp_move($db, $lpId, (int) $w['to_location_id']);
            } else {
                $lpId = epc_wms_lp_upsert($db, $companyId, '', (int) $w['to_location_id'], (string) $w['item'], $qty);
            }
        } elseif ($type === 'pick') {
            if ($lpId <= 0) {
                $st = $db->prepare("SELECT id FROM `epc_erp_wms_lp` WHERE company_id=? AND item=? AND status='active' AND qty>=? " . ((int) $w['from_location_id'] > 0 ? "AND location_id=" . (int) $w['from_location_id'] . " " : "") . "ORDER BY qty ASC LIMIT 1");
                $st->execute(array($companyId, (string) $w['item'], $qty));
                $lpId = (int) $st->fetchColumn();
            }
            if ($lpId <= 0) {
                throw new Exception('No license plate with enough stock to pick');
            }
            epc_wms_lp_adjust($db, $lpId, -$qty);
        } elseif ($type === 'move') {
            if ($lpId > 0) {
                epc_wms_lp_move($db, $lpId, (int) $w['to_location_id']);
            }
        } elseif ($type === 'count') {
            if ($lpId > 0) {
                $lp = epc_wms_lp_get($db, $lpId);
                if ($lp) {
                    epc_wms_lp_adjust($db, $lpId, $qty - (float) $lp['qty']);
                }
            }
        }

        $db->prepare("UPDATE `epc_erp_wms_work` SET `status`='closed', `lp_id`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($lpId, time(), $id));
        $work = epc_wms_work_get($db, $id);
        if ($work !== null && (int) $work['wave_id'] > 0) {
            epc_wms_wave_maybe_close($db, (int) $work['wave_id']);
        }
        return $work ?? array();
    }
}

/* ---------------- inbound / outbound helpers ---------------- */

if (!function_exists('epc_wms_receive')) {
    /**
     * Inbound receipt: place qty of item onto a (new) LP at the receiving dock
     * and raise put-away work to the destination location.
     * @return array{lp_id:int,work_id:int}
     */
    function epc_wms_receive(PDO $db, int $companyId, string $item, float $qty, int $receiveLocationId, int $putawayLocationId, string $reference = '', string $lpCode = ''): array
    {
        epc_wms_ensure_schema($db);
        $lpId = epc_wms_lp_upsert($db, $companyId, $lpCode, $receiveLocationId, $item, $qty);
        $workId = epc_wms_work_create($db, array(
            'company_id' => $companyId, 'work_type' => 'putaway', 'reference' => $reference,
            'item' => $item, 'qty' => $qty, 'from_location_id' => $receiveLocationId,
            'to_location_id' => $putawayLocationId, 'lp_id' => $lpId, 'status' => 'open',
        ));
        return array('lp_id' => $lpId, 'work_id' => $workId);
    }
}

/* ---------------- waves ---------------- */

if (!function_exists('epc_wms_wave_create')) {
    function epc_wms_wave_create(PDO $db, int $companyId, string $reference = ''): int
    {
        epc_wms_ensure_schema($db);
        $no = 'WAVE' . str_pad((string) epc_wms_next_seq($db, $companyId, 'wave'), 5, '0', STR_PAD_LEFT);
        $db->prepare("INSERT INTO `epc_erp_wms_waves` (`company_id`,`wave_no`,`reference`,`status`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?)")
           ->execute(array($companyId, $no, $reference, 'open', time(), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_wms_wave_add_pick')) {
    /** Add a pick line (item+qty) to a wave from an optional source location. */
    function epc_wms_wave_add_pick(PDO $db, int $waveId, string $item, float $qty, int $fromLocationId = 0, int $toLocationId = 0): int
    {
        epc_wms_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_wms_waves` WHERE `id`=?");
        $st->execute(array($waveId));
        $wave = $st->fetch(PDO::FETCH_ASSOC);
        if (!$wave) {
            throw new Exception('Wave not found');
        }
        return epc_wms_work_create($db, array(
            'company_id' => (int) $wave['company_id'], 'work_type' => 'pick', 'reference' => (string) $wave['reference'],
            'wave_id' => $waveId, 'item' => $item, 'qty' => $qty, 'from_location_id' => $fromLocationId,
            'to_location_id' => $toLocationId, 'status' => 'open',
        ));
    }
}

if (!function_exists('epc_wms_wave_release')) {
    function epc_wms_wave_release(PDO $db, int $waveId): void
    {
        epc_wms_ensure_schema($db);
        $db->prepare("UPDATE `epc_erp_wms_waves` SET `status`='released', `time_updated`=? WHERE `id`=?")->execute(array(time(), $waveId));
    }
}

if (!function_exists('epc_wms_wave_maybe_close')) {
    /** Close a wave automatically once all its work is closed. */
    function epc_wms_wave_maybe_close(PDO $db, int $waveId): void
    {
        epc_wms_ensure_schema($db);
        $open = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE wave_id=" . $waveId . " AND status<>'closed'")->fetchColumn();
        $total = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE wave_id=" . $waveId)->fetchColumn();
        if ($total > 0 && $open === 0) {
            $db->prepare("UPDATE `epc_erp_wms_waves` SET `status`='closed', `time_updated`=? WHERE `id`=?")->execute(array(time(), $waveId));
        }
    }
}

if (!function_exists('epc_wms_waves')) {
    /** @return array<int,array<string,mixed>> */
    function epc_wms_waves(PDO $db, int $companyId = 0): array
    {
        epc_wms_ensure_schema($db);
        $sql = "SELECT wv.*, (SELECT COUNT(*) FROM `epc_erp_wms_work` w WHERE w.wave_id=wv.id) AS work_lines,
                (SELECT COUNT(*) FROM `epc_erp_wms_work` w WHERE w.wave_id=wv.id AND w.status='closed') AS work_done
                FROM `epc_erp_wms_waves` wv WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND wv.company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY wv.id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_wms_summary')) {
    /** @return array{locations:int,license_plates:int,open_work:int,open_waves:int,on_hand:float} */
    function epc_wms_summary(PDO $db, int $companyId = 0): array
    {
        epc_wms_ensure_schema($db);
        $w = $companyId > 0 ? " WHERE company_id=" . $companyId : "";
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'locations' => (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_locations`" . $w)->fetchColumn(),
            'license_plates' => (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE status='active'" . $wa)->fetchColumn(),
            'open_work' => (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE status<>'closed'" . $wa)->fetchColumn(),
            'open_waves' => (int) $db->query("SELECT COUNT(*) FROM `epc_erp_wms_waves` WHERE status<>'closed'" . $wa)->fetchColumn(),
            'on_hand' => (float) $db->query("SELECT COALESCE(SUM(qty),0) FROM `epc_erp_wms_lp` WHERE status='active'" . $wa)->fetchColumn(),
        );
    }
}

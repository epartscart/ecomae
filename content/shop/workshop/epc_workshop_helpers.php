<?php
/**
 * Auto Workshop / Garage — schema, CRUD, seed.
 * Professional job-card flow: check-in → estimate → repair → QC → ready → delivered.
 */
declare(strict_types=1);

if (!function_exists('epc_ws_statuses')) {
	/** @return array<string,string> */
	function epc_ws_statuses(): array
	{
		return array(
			'checkin' => 'Check-in',
			'estimate' => 'Estimate',
			'approved' => 'Approved',
			'in_progress' => 'In progress',
			'qc' => 'QC / test',
			'ready' => 'Ready',
			'delivered' => 'Delivered',
			'cancelled' => 'Cancelled',
		);
	}
}

if (!function_exists('epc_ws_ensure_schema')) {
	function epc_ws_ensure_schema(PDO $db): void
	{
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_ws_bays` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`code` varchar(20) NOT NULL DEFAULT '',
				`name` varchar(80) NOT NULL DEFAULT '',
				`active` tinyint(1) NOT NULL DEFAULT 1,
				`sort_order` int(11) NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				UNIQUE KEY `u_code` (`code`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Workshop bays / ramps'"
		);

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_ws_technicians` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`name` varchar(120) NOT NULL DEFAULT '',
				`phone` varchar(40) NOT NULL DEFAULT '',
				`skill` varchar(80) NOT NULL DEFAULT '',
				`active` tinyint(1) NOT NULL DEFAULT 1,
				PRIMARY KEY (`id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Workshop technicians'"
		);

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_ws_jobs` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`job_no` varchar(40) NOT NULL DEFAULT '',
				`status` varchar(20) NOT NULL DEFAULT 'checkin',
				`customer_name` varchar(160) NOT NULL DEFAULT '',
				`customer_phone` varchar(40) NOT NULL DEFAULT '',
				`customer_email` varchar(160) NOT NULL DEFAULT '',
				`customer_id` int(11) NOT NULL DEFAULT 0,
				`plate` varchar(40) NOT NULL DEFAULT '',
				`vin` varchar(40) NOT NULL DEFAULT '',
				`make` varchar(80) NOT NULL DEFAULT '',
				`model` varchar(80) NOT NULL DEFAULT '',
				`year` varchar(10) NOT NULL DEFAULT '',
				`odometer` int(11) NOT NULL DEFAULT 0,
				`complaint` text,
				`bay_id` int(11) NOT NULL DEFAULT 0,
				`tech_id` int(11) NOT NULL DEFAULT 0,
				`estimate_approved` tinyint(1) NOT NULL DEFAULT 0,
				`under_warranty` tinyint(1) NOT NULL DEFAULT 0,
				`parts_total` decimal(14,2) NOT NULL DEFAULT 0.00,
				`labour_total` decimal(14,2) NOT NULL DEFAULT 0.00,
				`tax_total` decimal(14,2) NOT NULL DEFAULT 0.00,
				`grand_total` decimal(14,2) NOT NULL DEFAULT 0.00,
				`notes` text,
				`time_promised` int(11) NOT NULL DEFAULT 0,
				`time_created` int(11) NOT NULL DEFAULT 0,
				`time_updated` int(11) NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				UNIQUE KEY `u_job_no` (`job_no`),
				KEY `x_status` (`status`),
				KEY `x_plate` (`plate`),
				KEY `x_phone` (`customer_phone`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Garage job cards'"
		);

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_ws_job_lines` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`job_id` int(11) NOT NULL,
				`line_type` varchar(10) NOT NULL DEFAULT 'part',
				`description` varchar(190) NOT NULL DEFAULT '',
				`item_id` int(11) NOT NULL DEFAULT 0,
				`qty` decimal(14,4) NOT NULL DEFAULT 0.0000,
				`unit_price` decimal(14,2) NOT NULL DEFAULT 0.00,
				`tax_percent` decimal(7,3) NOT NULL DEFAULT 5.000,
				`chargeable` tinyint(1) NOT NULL DEFAULT 1,
				PRIMARY KEY (`id`),
				KEY `x_job` (`job_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Job parts and labour lines'"
		);

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_ws_appointments` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`ref_no` varchar(40) NOT NULL DEFAULT '',
				`status` varchar(20) NOT NULL DEFAULT 'scheduled',
				`customer_name` varchar(160) NOT NULL DEFAULT '',
				`customer_phone` varchar(40) NOT NULL DEFAULT '',
				`customer_email` varchar(160) NOT NULL DEFAULT '',
				`customer_id` int(11) NOT NULL DEFAULT 0,
				`garage_id` int(11) NOT NULL DEFAULT 0,
				`plate` varchar(40) NOT NULL DEFAULT '',
				`make` varchar(80) NOT NULL DEFAULT '',
				`model` varchar(80) NOT NULL DEFAULT '',
				`year` varchar(10) NOT NULL DEFAULT '',
				`service_type` varchar(80) NOT NULL DEFAULT 'General service',
				`notes` text,
				`time_slot` int(11) NOT NULL DEFAULT 0,
				`job_id` int(11) NOT NULL DEFAULT 0,
				`time_created` int(11) NOT NULL DEFAULT 0,
				`time_updated` int(11) NOT NULL DEFAULT 0,
				PRIMARY KEY (`id`),
				UNIQUE KEY `u_ref` (`ref_no`),
				KEY `x_slot` (`time_slot`),
				KEY `x_status` (`status`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Garage appointments'"
		);

		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_ws_labour_ops` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`code` varchar(40) NOT NULL DEFAULT '',
				`name` varchar(160) NOT NULL DEFAULT '',
				`hours` decimal(8,2) NOT NULL DEFAULT 1.00,
				`rate` decimal(14,2) NOT NULL DEFAULT 150.00,
				`active` tinyint(1) NOT NULL DEFAULT 1,
				PRIMARY KEY (`id`),
				UNIQUE KEY `u_code` (`code`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Standard labour operations'"
		);

		$alters = array(
			"ALTER TABLE `epc_ws_jobs` ADD COLUMN `garage_id` int(11) NOT NULL DEFAULT 0",
			"ALTER TABLE `epc_ws_jobs` ADD COLUMN `appointment_id` int(11) NOT NULL DEFAULT 0",
			"ALTER TABLE `epc_ws_jobs` ADD COLUMN `invoice_ref` varchar(64) NOT NULL DEFAULT ''",
			"ALTER TABLE `epc_ws_jobs` ADD KEY `x_garage` (`garage_id`)",
		);
		foreach ($alters as $sql) {
			try { $db->exec($sql); } catch (Throwable $e) { /* already applied */ }
		}
	}
}

if (!function_exists('epc_ws_next_job_no')) {
	function epc_ws_next_job_no(PDO $db): string
	{
		$day = date('ymd');
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `job_no` LIKE ?");
		$st->execute(array('WS-' . $day . '-%'));
		$n = ((int) $st->fetchColumn()) + 1;
		return sprintf('WS-%s-%03d', $day, $n);
	}
}

if (!function_exists('epc_ws_recalc')) {
	/** @return array{parts_total:float,labour_total:float,tax_total:float,grand_total:float} */
	function epc_ws_recalc(PDO $db, int $jobId): array
	{
		$st = $db->prepare('SELECT * FROM `epc_ws_job_lines` WHERE `job_id` = ?');
		$st->execute(array($jobId));
		$parts = 0.0;
		$labour = 0.0;
		$tax = 0.0;
		foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $ln) {
			if ((int) $ln['chargeable'] !== 1) {
				continue;
			}
			$net = (float) $ln['qty'] * (float) $ln['unit_price'];
			$tax += $net * ((float) $ln['tax_percent'] / 100.0);
			if (($ln['line_type'] ?? '') === 'labour') {
				$labour += $net;
			} else {
				$parts += $net;
			}
		}
		$parts = round($parts, 2);
		$labour = round($labour, 2);
		$tax = round($tax, 2);
		$grand = round($parts + $labour + $tax, 2);
		$db->prepare(
			'UPDATE `epc_ws_jobs` SET `parts_total`=?, `labour_total`=?, `tax_total`=?, `grand_total`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array($parts, $labour, $tax, $grand, time(), $jobId));
		return array(
			'parts_total' => $parts,
			'labour_total' => $labour,
			'tax_total' => $tax,
			'grand_total' => $grand,
		);
	}
}

if (!function_exists('epc_ws_job_create')) {
	/**
	 * @param array<string,mixed> $data
	 */
	function epc_ws_job_create(PDO $db, array $data): int
	{
		epc_ws_ensure_schema($db);
		$now = time();
		$jobNo = trim((string) ($data['job_no'] ?? ''));
		if ($jobNo === '') {
			$jobNo = epc_ws_next_job_no($db);
		}
		$status = (string) ($data['status'] ?? 'checkin');
		if (!isset(epc_ws_statuses()[$status])) {
			$status = 'checkin';
		}
		$db->prepare(
			'INSERT INTO `epc_ws_jobs`
			(`job_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,
			 `plate`,`vin`,`make`,`model`,`year`,`odometer`,`complaint`,`bay_id`,`tech_id`,
			 `estimate_approved`,`under_warranty`,`notes`,`time_promised`,`time_created`,`time_updated`)
			 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array(
			$jobNo,
			$status,
			trim((string) ($data['customer_name'] ?? '')),
			trim((string) ($data['customer_phone'] ?? '')),
			trim((string) ($data['customer_email'] ?? '')),
			(int) ($data['customer_id'] ?? 0),
			strtoupper(trim((string) ($data['plate'] ?? ''))),
			strtoupper(trim((string) ($data['vin'] ?? ''))),
			trim((string) ($data['make'] ?? '')),
			trim((string) ($data['model'] ?? '')),
			trim((string) ($data['year'] ?? '')),
			(int) ($data['odometer'] ?? 0),
			trim((string) ($data['complaint'] ?? '')),
			(int) ($data['bay_id'] ?? 0),
			(int) ($data['tech_id'] ?? 0),
			!empty($data['estimate_approved']) ? 1 : 0,
			!empty($data['under_warranty']) ? 1 : 0,
			trim((string) ($data['notes'] ?? '')),
			(int) ($data['time_promised'] ?? 0),
			$now,
			$now,
		));
		return (int) $db->lastInsertId();
	}
}

if (!function_exists('epc_ws_job_add_line')) {
	/**
	 * @param array<string,mixed> $line
	 */
	function epc_ws_job_add_line(PDO $db, int $jobId, array $line): int
	{
		$db->prepare(
			'INSERT INTO `epc_ws_job_lines`
			(`job_id`,`line_type`,`description`,`item_id`,`qty`,`unit_price`,`tax_percent`,`chargeable`)
			 VALUES (?,?,?,?,?,?,?,?)'
		)->execute(array(
			$jobId,
			(($line['line_type'] ?? '') === 'labour') ? 'labour' : 'part',
			trim((string) ($line['description'] ?? '')),
			(int) ($line['item_id'] ?? 0),
			(float) ($line['qty'] ?? 1),
			(float) ($line['unit_price'] ?? 0),
			(float) ($line['tax_percent'] ?? 5),
			isset($line['chargeable']) ? (int) (bool) $line['chargeable'] : 1,
		));
		$id = (int) $db->lastInsertId();
		epc_ws_recalc($db, $jobId);
		return $id;
	}
}

if (!function_exists('epc_ws_job_set_status')) {
	function epc_ws_job_set_status(PDO $db, int $jobId, string $status): bool
	{
		if (!isset(epc_ws_statuses()[$status])) {
			return false;
		}
		$est = ($status === 'approved' || $status === 'in_progress' || $status === 'qc' || $status === 'ready' || $status === 'delivered') ? 1 : null;
		if ($est === 1) {
			$db->prepare('UPDATE `epc_ws_jobs` SET `status`=?, `estimate_approved`=1, `time_updated`=? WHERE `id`=?')
				->execute(array($status, time(), $jobId));
		} else {
			$db->prepare('UPDATE `epc_ws_jobs` SET `status`=?, `time_updated`=? WHERE `id`=?')
				->execute(array($status, time(), $jobId));
		}
		return true;
	}
}

if (!function_exists('epc_ws_job_get')) {
	/**
	 * @return array{header:array<string,mixed>,lines:list<array<string,mixed>>}|null
	 */
	function epc_ws_job_get(PDO $db, int $jobId): ?array
	{
		$st = $db->prepare(
			'SELECT j.*,
				b.name AS bay_name, b.code AS bay_code,
				t.name AS tech_name
			 FROM `epc_ws_jobs` j
			 LEFT JOIN `epc_ws_bays` b ON b.id = j.bay_id
			 LEFT JOIN `epc_ws_technicians` t ON t.id = j.tech_id
			 WHERE j.id = ? LIMIT 1'
		);
		$st->execute(array($jobId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return null;
		}
		$ls = $db->prepare('SELECT * FROM `epc_ws_job_lines` WHERE `job_id` = ? ORDER BY `id` ASC');
		$ls->execute(array($jobId));
		return array(
			'header' => $row,
			'lines' => $ls->fetchAll(PDO::FETCH_ASSOC) ?: array(),
		);
	}
}

if (!function_exists('epc_ws_job_find_public')) {
	/**
	 * Public status lookup by job_no or plate (+ optional phone).
	 *
	 * @return array{header:array<string,mixed>,lines:list<array<string,mixed>>}|null
	 */
	function epc_ws_job_find_public(PDO $db, string $ref, string $phone = ''): ?array
	{
		$ref = trim($ref);
		if ($ref === '') {
			return null;
		}
		$st = $db->prepare(
			'SELECT id FROM `epc_ws_jobs`
			 WHERE `job_no` = ? OR `plate` = ?
			 ORDER BY id DESC LIMIT 1'
		);
		$st->execute(array($ref, strtoupper($ref)));
		$id = (int) $st->fetchColumn();
		if ($id <= 0) {
			return null;
		}
		$job = epc_ws_job_get($db, $id);
		if (!$job) {
			return null;
		}
		if ($phone !== '') {
			$jp = preg_replace('/\D+/', '', (string) $job['header']['customer_phone']);
			$pp = preg_replace('/\D+/', '', $phone);
			if ($jp !== '' && $pp !== '' && substr($jp, -7) !== substr($pp, -7)) {
				return null;
			}
		}
		return $job;
	}
}

if (!function_exists('epc_ws_list_jobs')) {
	/**
	 * @return list<array<string,mixed>>
	 */
	function epc_ws_list_jobs(PDO $db, string $status = '', int $limit = 200): array
	{
		$limit = max(1, min(500, $limit));
		$sql = 'SELECT j.*, b.name AS bay_name, t.name AS tech_name
			FROM `epc_ws_jobs` j
			LEFT JOIN `epc_ws_bays` b ON b.id = j.bay_id
			LEFT JOIN `epc_ws_technicians` t ON t.id = j.tech_id';
		$args = array();
		if ($status !== '' && isset(epc_ws_statuses()[$status])) {
			$sql .= ' WHERE j.status = ?';
			$args[] = $status;
		}
		$sql .= ' ORDER BY FIELD(j.status,\'checkin\',\'estimate\',\'approved\',\'in_progress\',\'qc\',\'ready\',\'delivered\',\'cancelled\'), j.id DESC LIMIT ' . $limit;
		$st = $db->prepare($sql);
		$st->execute($args);
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}
}

if (!function_exists('epc_ws_dashboard')) {
	/** @return array<string,int|float> */
	function epc_ws_dashboard(PDO $db): array
	{
		$out = array(
			'open' => 0,
			'in_progress' => 0,
			'ready' => 0,
			'delivered_today' => 0,
			'revenue_open' => 0.0,
		);
		$st = $db->query(
			"SELECT `status`, COUNT(*) c, SUM(`grand_total`) t FROM `epc_ws_jobs` GROUP BY `status`"
		);
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($rows as $r) {
			$c = (int) $r['c'];
			$t = (float) $r['t'];
			$s = (string) $r['status'];
			if (!in_array($s, array('delivered', 'cancelled'), true)) {
				$out['open'] += $c;
				$out['revenue_open'] += $t;
			}
			if ($s === 'in_progress' || $s === 'qc') {
				$out['in_progress'] += $c;
			}
			if ($s === 'ready') {
				$out['ready'] += $c;
			}
		}
		$dayStart = strtotime('today');
		$st2 = $db->prepare("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `status`='delivered' AND `time_updated` >= ?");
		$st2->execute(array($dayStart));
		$out['delivered_today'] = (int) $st2->fetchColumn();
		$out['revenue_open'] = round((float) $out['revenue_open'], 2);
		return $out;
	}
}

if (!function_exists('epc_ws_list_bays')) {
	/** @return list<array<string,mixed>> */
	function epc_ws_list_bays(PDO $db, bool $activeOnly = false): array
	{
		$sql = 'SELECT * FROM `epc_ws_bays`';
		if ($activeOnly) {
			$sql .= ' WHERE `active` = 1';
		}
		$sql .= ' ORDER BY `sort_order` ASC, `id` ASC';
		$st = $db->query($sql);
		return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
	}
}

if (!function_exists('epc_ws_list_techs')) {
	/** @return list<array<string,mixed>> */
	function epc_ws_list_techs(PDO $db, bool $activeOnly = false): array
	{
		$sql = 'SELECT * FROM `epc_ws_technicians`';
		if ($activeOnly) {
			$sql .= ' WHERE `active` = 1';
		}
		$sql .= ' ORDER BY `name` ASC';
		$st = $db->query($sql);
		return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
	}
}

if (!function_exists('epc_ws_seed_demo')) {
	/**
	 * Idempotent demo garage data for UAE workshop.
	 *
	 * @return array{bays:int,techs:int,jobs:int}
	 */
	function epc_ws_seed_demo(PDO $db): array
	{
		epc_ws_ensure_schema($db);
		$bays = array(
			array('B1', 'Bay 1 — Quick service', 1),
			array('B2', 'Bay 2 — Mechanical', 2),
			array('B3', 'Bay 3 — Diagnostic', 3),
		);
		$bayIds = array();
		foreach ($bays as $b) {
			$st = $db->prepare('SELECT id FROM `epc_ws_bays` WHERE `code` = ? LIMIT 1');
			$st->execute(array($b[0]));
			$id = (int) $st->fetchColumn();
			if ($id <= 0) {
				$db->prepare('INSERT INTO `epc_ws_bays` (`code`,`name`,`active`,`sort_order`) VALUES (?,?,1,?)')
					->execute(array($b[0], $b[1], $b[2]));
				$id = (int) $db->lastInsertId();
			}
			$bayIds[] = $id;
		}

		$techs = array(
			array('Ahmed Hassan', '+971501112233', 'General / brakes'),
			array('Rajesh Kumar', '+971502223344', 'Electrical / AC'),
			array('Omar Al Mansoori', '+971503334455', 'Diagnostics'),
		);
		$techIds = array();
		foreach ($techs as $t) {
			$st = $db->prepare('SELECT id FROM `epc_ws_technicians` WHERE `name` = ? LIMIT 1');
			$st->execute(array($t[0]));
			$id = (int) $st->fetchColumn();
			if ($id <= 0) {
				$db->prepare('INSERT INTO `epc_ws_technicians` (`name`,`phone`,`skill`,`active`) VALUES (?,?,?,1)')
					->execute($t);
				$id = (int) $db->lastInsertId();
			}
			$techIds[] = $id;
		}

		$demoJobs = array(
			array(
				'job_no' => 'WS-DEMO-001',
				'status' => 'in_progress',
				'customer_name' => 'Fatima Al Zaabi',
				'customer_phone' => '+971567607011',
				'customer_email' => 'fatima.demo@example.com',
				'plate' => 'D-12345',
				'vin' => 'WVWZZZ3CZWE123456',
				'make' => 'Toyota',
				'model' => 'Land Cruiser',
				'year' => '2021',
				'odometer' => 68420,
				'complaint' => 'Front brake noise + oil service due',
				'bay_id' => $bayIds[1] ?? 0,
				'tech_id' => $techIds[0] ?? 0,
				'estimate_approved' => 1,
				'lines' => array(
					array('line_type' => 'labour', 'description' => 'Brake pads replace (front)', 'qty' => 1.5, 'unit_price' => 180),
					array('line_type' => 'part', 'description' => 'Brake pad set — OEM', 'qty' => 1, 'unit_price' => 420),
					array('line_type' => 'labour', 'description' => 'Engine oil & filter service', 'qty' => 0.8, 'unit_price' => 150),
					array('line_type' => 'part', 'description' => '0W-20 oil 6L + filter', 'qty' => 1, 'unit_price' => 210),
				),
			),
			array(
				'job_no' => 'WS-DEMO-002',
				'status' => 'estimate',
				'customer_name' => 'Gulf Fleet Services LLC',
				'customer_phone' => '+97144556677',
				'customer_email' => 'fleet@example.ae',
				'plate' => 'DXB-88901',
				'vin' => 'JN1TANR35U0123456',
				'make' => 'Nissan',
				'model' => 'Patrol',
				'year' => '2019',
				'odometer' => 112300,
				'complaint' => 'AC not cooling; intermittent compressor cut-out',
				'bay_id' => $bayIds[2] ?? 0,
				'tech_id' => $techIds[1] ?? 0,
				'lines' => array(
					array('line_type' => 'labour', 'description' => 'AC diagnose + pressure test', 'qty' => 1.0, 'unit_price' => 200),
					array('line_type' => 'part', 'description' => 'AC compressor (estimate)', 'qty' => 1, 'unit_price' => 1850),
				),
			),
			array(
				'job_no' => 'WS-DEMO-003',
				'status' => 'ready',
				'customer_name' => 'John Peters',
				'customer_phone' => '+971552223344',
				'customer_email' => 'john.demo@example.com',
				'plate' => 'A-7788',
				'vin' => '',
				'make' => 'BMW',
				'model' => 'X5',
				'year' => '2020',
				'odometer' => 45110,
				'complaint' => 'Battery warning light; weak start',
				'bay_id' => $bayIds[0] ?? 0,
				'tech_id' => $techIds[2] ?? 0,
				'estimate_approved' => 1,
				'lines' => array(
					array('line_type' => 'labour', 'description' => 'Battery test & replace', 'qty' => 0.5, 'unit_price' => 120),
					array('line_type' => 'part', 'description' => 'AGM battery 95Ah', 'qty' => 1, 'unit_price' => 680),
				),
			),
			array(
				'job_no' => 'WS-DEMO-004',
				'status' => 'checkin',
				'customer_name' => 'Sara Khan',
				'customer_phone' => '+971501234567',
				'customer_email' => '',
				'plate' => 'SHJ-4421',
				'vin' => '',
				'make' => 'Honda',
				'model' => 'CR-V',
				'year' => '2018',
				'odometer' => 98000,
				'complaint' => 'Annual service + tyre rotation',
				'bay_id' => 0,
				'tech_id' => 0,
				'lines' => array(),
			),
		);

		$jobsCreated = 0;
		foreach ($demoJobs as $dj) {
			$st = $db->prepare('SELECT id FROM `epc_ws_jobs` WHERE `job_no` = ? LIMIT 1');
			$st->execute(array($dj['job_no']));
			$existing = (int) $st->fetchColumn();
			if ($existing > 0) {
				continue;
			}
			$lines = $dj['lines'];
			unset($dj['lines']);
			$jobId = epc_ws_job_create($db, $dj);
			foreach ($lines as $ln) {
				epc_ws_job_add_line($db, $jobId, $ln);
			}
			$jobsCreated++;
		}

		if (function_exists('epc_ws_seed_labour_ops')) { epc_ws_seed_labour_ops($db); }
		return array(
			'bays' => count($bayIds),
			'techs' => count($techIds),
			'jobs' => $jobsCreated,
		);
	}
}


if (!function_exists('epc_ws_appointment_statuses')) {
	/** @return array<string,string> */
	function epc_ws_appointment_statuses(): array
	{
		return array(
			'scheduled' => 'Scheduled',
			'confirmed' => 'Confirmed',
			'arrived' => 'Arrived',
			'converted' => 'Checked in',
			'no_show' => 'No-show',
			'cancelled' => 'Cancelled',
		);
	}
}

if (!function_exists('epc_ws_next_appointment_ref')) {
	function epc_ws_next_appointment_ref(PDO $db): string
	{
		$day = date('ymd');
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_ws_appointments` WHERE `ref_no` LIKE ?");
		$st->execute(array('AP-' . $day . '-%'));
		$n = ((int) $st->fetchColumn()) + 1;
		return sprintf('AP-%s-%03d', $day, $n);
	}
}

if (!function_exists('epc_ws_appointment_create')) {
	/**
	 * @param array<string,mixed> $data
	 */
	function epc_ws_appointment_create(PDO $db, array $data): int
	{
		epc_ws_ensure_schema($db);
		$now = time();
		$ref = trim((string) ($data['ref_no'] ?? ''));
		if ($ref === '') {
			$ref = epc_ws_next_appointment_ref($db);
		}
		$status = (string) ($data['status'] ?? 'scheduled');
		if (!isset(epc_ws_appointment_statuses()[$status])) {
			$status = 'scheduled';
		}
		$slot = (int) ($data['time_slot'] ?? 0);
		if ($slot <= 0) {
			$slot = $now + 86400;
		}
		$db->prepare(
			'INSERT INTO `epc_ws_appointments`
			(`ref_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,`garage_id`,
			 `plate`,`make`,`model`,`year`,`service_type`,`notes`,`time_slot`,`job_id`,`time_created`,`time_updated`)
			 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?,?)'
		)->execute(array(
			$ref,
			$status,
			trim((string) ($data['customer_name'] ?? '')),
			trim((string) ($data['customer_phone'] ?? '')),
			trim((string) ($data['customer_email'] ?? '')),
			(int) ($data['customer_id'] ?? 0),
			(int) ($data['garage_id'] ?? 0),
			strtoupper(trim((string) ($data['plate'] ?? ''))),
			trim((string) ($data['make'] ?? '')),
			trim((string) ($data['model'] ?? '')),
			trim((string) ($data['year'] ?? '')),
			trim((string) ($data['service_type'] ?? 'General service')),
			trim((string) ($data['notes'] ?? '')),
			$slot,
			$now,
			$now,
		));
		return (int) $db->lastInsertId();
	}
}

if (!function_exists('epc_ws_list_appointments')) {
	/**
	 * @return list<array<string,mixed>>
	 */
	function epc_ws_list_appointments(PDO $db, int $fromTs = 0, int $toTs = 0, int $limit = 100): array
	{
		epc_ws_ensure_schema($db);
		$limit = max(1, min(300, $limit));
		$sql = 'SELECT * FROM `epc_ws_appointments` WHERE 1=1';
		$args = array();
		if ($fromTs > 0) {
			$sql .= ' AND `time_slot` >= ?';
			$args[] = $fromTs;
		}
		if ($toTs > 0) {
			$sql .= ' AND `time_slot` <= ?';
			$args[] = $toTs;
		}
		$sql .= ' ORDER BY `time_slot` ASC LIMIT ' . $limit;
		$st = $db->prepare($sql);
		$st->execute($args);
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}
}

if (!function_exists('epc_ws_appointment_to_job')) {
	function epc_ws_appointment_to_job(PDO $db, int $appointmentId): int
	{
		$st = $db->prepare('SELECT * FROM `epc_ws_appointments` WHERE `id` = ? LIMIT 1');
		$st->execute(array($appointmentId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			throw new RuntimeException('Appointment not found');
		}
		if ((int) $row['job_id'] > 0) {
			return (int) $row['job_id'];
		}
		$jobId = epc_ws_job_create($db, array(
			'status' => 'checkin',
			'customer_name' => $row['customer_name'],
			'customer_phone' => $row['customer_phone'],
			'customer_email' => $row['customer_email'],
			'customer_id' => (int) $row['customer_id'],
			'plate' => $row['plate'],
			'make' => $row['make'],
			'model' => $row['model'],
			'year' => $row['year'],
			'complaint' => trim($row['service_type'] . ' — ' . (string) $row['notes']),
			'notes' => 'From appointment ' . $row['ref_no'],
		));
		$db->prepare('UPDATE `epc_ws_jobs` SET `garage_id`=?, `appointment_id`=?, `time_updated`=? WHERE `id`=?')
			->execute(array((int) $row['garage_id'], $appointmentId, time(), $jobId));
		$db->prepare('UPDATE `epc_ws_appointments` SET `status`=\'converted\', `job_id`=?, `time_updated`=? WHERE `id`=?')
			->execute(array($jobId, time(), $appointmentId));
		return $jobId;
	}
}

if (!function_exists('epc_ws_list_labour_ops')) {
	/** @return list<array<string,mixed>> */
	function epc_ws_list_labour_ops(PDO $db, bool $activeOnly = true): array
	{
		epc_ws_ensure_schema($db);
		$sql = 'SELECT * FROM `epc_ws_labour_ops`';
		if ($activeOnly) {
			$sql .= ' WHERE `active` = 1';
		}
		$sql .= ' ORDER BY `name` ASC';
		$st = $db->query($sql);
		return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
	}
}

if (!function_exists('epc_ws_seed_labour_ops')) {
	function epc_ws_seed_labour_ops(PDO $db): int
	{
		epc_ws_ensure_schema($db);
		$ops = array(
			array('OIL-SVC', 'Engine oil & filter service', 0.8, 150),
			array('BRAKE-F', 'Front brake pads replace', 1.5, 180),
			array('BRAKE-R', 'Rear brake pads replace', 1.2, 180),
			array('DIAG', 'Computer diagnosis', 1.0, 200),
			array('AC-SVC', 'AC diagnose & recharge', 1.5, 200),
			array('BATTERY', 'Battery test & replace', 0.5, 120),
			array('TYRE-ROT', 'Tyre rotation & balance', 0.8, 100),
			array('ANNUAL', 'Annual multi-point service', 2.5, 160),
		);
		$n = 0;
		foreach ($ops as $o) {
			$st = $db->prepare('SELECT id FROM `epc_ws_labour_ops` WHERE `code` = ? LIMIT 1');
			$st->execute(array($o[0]));
			if ((int) $st->fetchColumn() > 0) {
				continue;
			}
			$db->prepare('INSERT INTO `epc_ws_labour_ops` (`code`,`name`,`hours`,`rate`,`active`) VALUES (?,?,?,?,1)')
				->execute($o);
			$n++;
		}
		return $n;
	}
}

if (!function_exists('epc_ws_staff_ok')) {
	function epc_ws_staff_ok(): bool
	{
		if (!class_exists('DP_User')) {
			return false;
		}
		return DP_User::isAdmin() || DP_User::isBackendGroup();
	}
}

if (!function_exists('epc_ws_customer_garage_cars')) {
	/**
	 * @return list<array<string,mixed>>
	 */
	function epc_ws_customer_garage_cars(PDO $db, int $userId): array
	{
		if ($userId <= 0) {
			return array();
		}
		try {
			$st = $db->prepare('SELECT * FROM `shop_docpart_garage` WHERE `user_id` = ? ORDER BY `id` DESC LIMIT 50');
			$st->execute(array($userId));
			return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Throwable $e) {
			return array();
		}
	}
}
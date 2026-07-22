<?php
/**
 * CP Workshop AJAX — admin only.
 */
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => 'Database connection failed'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_workshop_helpers.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	echo json_encode(array('status' => false, 'message' => 'No action'));
	exit;
}

// CSRF
$session = DP_User::getAdminSession();
$csrf = (string) ($session['csrf_guard_key'] ?? '');
if ($csrf === '' || (string) ($_POST['csrf_guard_key'] ?? '') !== $csrf) {
	echo json_encode(array('status' => false, 'message' => 'CSRF failed'));
	exit;
}

epc_ws_ensure_schema($db_link);
$action = (string) $_POST['action'];

try {
	switch ($action) {
		case 'seed_demo':
			$r = epc_ws_seed_demo($db_link);
			echo json_encode(array('status' => true, 'message' => 'Demo garage data ready', 'result' => $r));
			break;

		case 'create_job':
			$id = epc_ws_job_create($db_link, $_POST);
			if (!empty($_POST['labour_desc'])) {
				epc_ws_job_add_line($db_link, $id, array(
					'line_type' => 'labour',
					'description' => (string) $_POST['labour_desc'],
					'qty' => (float) ($_POST['labour_hours'] ?? 1),
					'unit_price' => (float) ($_POST['labour_rate'] ?? 150),
				));
			}
			if (!empty($_POST['part_desc'])) {
				epc_ws_job_add_line($db_link, $id, array(
					'line_type' => 'part',
					'description' => (string) $_POST['part_desc'],
					'qty' => (float) ($_POST['part_qty'] ?? 1),
					'unit_price' => (float) ($_POST['part_price'] ?? 0),
				));
			}
			$job = epc_ws_job_get($db_link, $id);
			echo json_encode(array('status' => true, 'message' => 'Job created', 'job' => $job));
			break;

		case 'set_status':
			$jobId = (int) ($_POST['job_id'] ?? 0);
			$status = (string) ($_POST['status'] ?? '');
			if ($jobId <= 0 || !epc_ws_job_set_status($db_link, $jobId, $status)) {
				throw new RuntimeException('Invalid job or status');
			}
			echo json_encode(array('status' => true, 'message' => 'Status updated', 'job' => epc_ws_job_get($db_link, $jobId)));
			break;

		case 'assign':
			$jobId = (int) ($_POST['job_id'] ?? 0);
			if ($jobId <= 0) {
				throw new RuntimeException('Invalid job');
			}
			$db_link->prepare('UPDATE `epc_ws_jobs` SET `bay_id`=?, `tech_id`=?, `time_updated`=? WHERE `id`=?')
				->execute(array((int) ($_POST['bay_id'] ?? 0), (int) ($_POST['tech_id'] ?? 0), time(), $jobId));
			echo json_encode(array('status' => true, 'message' => 'Assignment saved', 'job' => epc_ws_job_get($db_link, $jobId)));
			break;

		case 'add_line':
			$jobId = (int) ($_POST['job_id'] ?? 0);
			if ($jobId <= 0) {
				throw new RuntimeException('Invalid job');
			}
			$lineId = epc_ws_job_add_line($db_link, $jobId, $_POST);
			echo json_encode(array('status' => true, 'message' => 'Line added', 'line_id' => $lineId, 'job' => epc_ws_job_get($db_link, $jobId)));
			break;

		case 'get_job':
			$jobId = (int) ($_POST['job_id'] ?? 0);
			$job = epc_ws_job_get($db_link, $jobId);
			if (!$job) {
				throw new RuntimeException('Job not found');
			}
			echo json_encode(array('status' => true, 'job' => $job));
			break;

		case 'list_jobs':
			$status = (string) ($_POST['status'] ?? '');
			echo json_encode(array(
				'status' => true,
				'jobs' => epc_ws_list_jobs($db_link, $status),
				'dashboard' => epc_ws_dashboard($db_link),
			));
			break;

		case 'save_bay':
			$code = strtoupper(trim((string) ($_POST['code'] ?? '')));
			$name = trim((string) ($_POST['name'] ?? ''));
			if ($code === '' || $name === '') {
				throw new RuntimeException('Bay code and name required');
			}
			$id = (int) ($_POST['id'] ?? 0);
			if ($id > 0) {
				$db_link->prepare('UPDATE `epc_ws_bays` SET `code`=?, `name`=?, `active`=?, `sort_order`=? WHERE `id`=?')
					->execute(array($code, $name, !empty($_POST['active']) ? 1 : 0, (int) ($_POST['sort_order'] ?? 0), $id));
			} else {
				$db_link->prepare('INSERT INTO `epc_ws_bays` (`code`,`name`,`active`,`sort_order`) VALUES (?,?,?,?)')
					->execute(array($code, $name, 1, (int) ($_POST['sort_order'] ?? 0)));
				$id = (int) $db_link->lastInsertId();
			}
			echo json_encode(array('status' => true, 'id' => $id, 'bays' => epc_ws_list_bays($db_link)));
			break;

		case 'save_tech':
			$name = trim((string) ($_POST['name'] ?? ''));
			if ($name === '') {
				throw new RuntimeException('Technician name required');
			}
			$id = (int) ($_POST['id'] ?? 0);
			if ($id > 0) {
				$db_link->prepare('UPDATE `epc_ws_technicians` SET `name`=?, `phone`=?, `skill`=?, `active`=? WHERE `id`=?')
					->execute(array($name, trim((string) ($_POST['phone'] ?? '')), trim((string) ($_POST['skill'] ?? '')), !empty($_POST['active']) ? 1 : 0, $id));
			} else {
				$db_link->prepare('INSERT INTO `epc_ws_technicians` (`name`,`phone`,`skill`,`active`) VALUES (?,?,?,1)')
					->execute(array($name, trim((string) ($_POST['phone'] ?? '')), trim((string) ($_POST['skill'] ?? ''))));
				$id = (int) $db_link->lastInsertId();
			}
			echo json_encode(array('status' => true, 'id' => $id, 'techs' => epc_ws_list_techs($db_link)));
			break;

		case 'create_appointment':
			$id = epc_ws_appointment_create($db_link, $_POST);
			echo json_encode(array('status' => true, 'message' => 'Appointment scheduled', 'id' => $id));
			break;

		case 'convert_appointment':
			$jobId = epc_ws_appointment_to_job($db_link, (int) ($_POST['appointment_id'] ?? 0));
			echo json_encode(array('status' => true, 'message' => 'Checked in', 'job' => epc_ws_job_get($db_link, $jobId)));
			break;

		case 'list_appointments':
			echo json_encode(array(
				'status' => true,
				'appointments' => epc_ws_list_appointments($db_link, time() - 86400, time() + 21 * 86400, 80),
			));
			break;

		default:
			throw new RuntimeException('Unknown action');
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}

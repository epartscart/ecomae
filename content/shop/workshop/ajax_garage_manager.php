<?php
/**
 * Garage Manager AJAX — staff (admin / backend group).
 * Direct: /content/shop/workshop/ajax_garage_manager.php
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
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_workshop_helpers.php';

if (!epc_ws_staff_ok()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied — garage staff login required'));
	exit;
}

$session = DP_User::getAdminSession();
$csrf = (string) ($session['csrf_guard_key'] ?? '');
if ($csrf === '' || (string) ($_POST['csrf_guard_key'] ?? '') !== $csrf) {
	echo json_encode(array('status' => false, 'message' => 'CSRF failed — refresh and retry'));
	exit;
}

epc_ws_ensure_schema($db_link);
$action = (string) ($_POST['action'] ?? '');

try {
	switch ($action) {
		case 'set_status':
			$jobId = (int) ($_POST['job_id'] ?? 0);
			$status = (string) ($_POST['status'] ?? '');
			if ($jobId <= 0 || !epc_ws_job_set_status($db_link, $jobId, $status)) {
				throw new RuntimeException('Invalid job or status');
			}
			echo json_encode(array('status' => true, 'message' => 'Status → ' . $status, 'job' => epc_ws_job_get($db_link, $jobId)));
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
			echo json_encode(array(
				'status' => true,
				'message' => 'Job card created',
				'job' => epc_ws_job_get($db_link, $id),
			));
			break;

		case 'create_appointment':
			$id = epc_ws_appointment_create($db_link, $_POST);
			$st = $db_link->prepare('SELECT `ref_no` FROM `epc_ws_appointments` WHERE `id` = ?');
			$st->execute(array($id));
			$ref = (string) $st->fetchColumn();
			echo json_encode(array('status' => true, 'message' => 'Appointment ' . $ref . ' scheduled', 'id' => $id, 'ref_no' => $ref));
			break;

		case 'convert_appointment':
			$aid = (int) ($_POST['appointment_id'] ?? 0);
			$jobId = epc_ws_appointment_to_job($db_link, $aid);
			$job = epc_ws_job_get($db_link, $jobId);
			echo json_encode(array(
				'status' => true,
				'message' => 'Checked in as ' . ($job['header']['job_no'] ?? ('#' . $jobId)),
				'job_id' => $jobId,
				'job' => $job,
			));
			break;

		case 'dashboard':
			echo json_encode(array(
				'status' => true,
				'dashboard' => epc_ws_dashboard($db_link),
				'jobs' => epc_ws_list_jobs($db_link),
				'appointments' => epc_ws_list_appointments($db_link, time() - 86400, time() + 14 * 86400, 50),
			));
			break;

		default:
			throw new RuntimeException('Unknown action');
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}

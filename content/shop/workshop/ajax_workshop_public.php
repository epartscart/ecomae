<?php
/**
 * Public Auto Workshop — book service + track job status.
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => 'Service temporarily unavailable'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_workshop_helpers.php';
epc_ws_ensure_schema($db);

$action = (string) ($_POST['action'] ?? $_GET['action'] ?? '');

try {
	if ($action === 'track') {
		$ref = (string) ($_POST['ref'] ?? $_GET['ref'] ?? '');
		$phone = (string) ($_POST['phone'] ?? $_GET['phone'] ?? '');
		$job = epc_ws_job_find_public($db, $ref, $phone);
		if (!$job) {
			echo json_encode(array('status' => false, 'message' => 'No job found for that reference.'));
			exit;
		}
		$h = $job['header'];
		$statuses = epc_ws_statuses();
		echo json_encode(array(
			'status' => true,
			'job' => array(
				'job_no' => $h['job_no'],
				'status' => $h['status'],
				'status_label' => $statuses[$h['status']] ?? $h['status'],
				'plate' => $h['plate'],
				'make' => $h['make'],
				'model' => $h['model'],
				'customer_name' => $h['customer_name'],
				'grand_total' => (float) $h['grand_total'],
				'time_updated' => (int) $h['time_updated'],
				'estimate_approved' => (int) $h['estimate_approved'],
			),
		));
		exit;
	}

	if ($action === 'book') {
		$name = trim((string) ($_POST['customer_name'] ?? ''));
		$phone = trim((string) ($_POST['customer_phone'] ?? ''));
		$plate = trim((string) ($_POST['plate'] ?? ''));
		$complaint = trim((string) ($_POST['complaint'] ?? ''));
		if ($name === '' || $phone === '' || $plate === '' || $complaint === '') {
			throw new RuntimeException('Name, phone, plate, and complaint are required.');
		}
		$wantAppt = !empty($_POST['as_appointment']);
		$slot = (int) ($_POST['time_slot'] ?? 0);
		if ($wantAppt || $slot > 0) {
			$apptId = epc_ws_appointment_create($db, array(
				'customer_name' => $name,
				'customer_phone' => $phone,
				'customer_email' => trim((string) ($_POST['customer_email'] ?? '')),
				'plate' => $plate,
				'make' => trim((string) ($_POST['make'] ?? '')),
				'model' => trim((string) ($_POST['model'] ?? '')),
				'year' => trim((string) ($_POST['year'] ?? '')),
				'service_type' => mb_substr($complaint, 0, 80),
				'notes' => $complaint,
				'time_slot' => $slot > 0 ? $slot : (time() + 86400),
				'status' => 'scheduled',
			));
			$st = $db->prepare('SELECT `ref_no` FROM `epc_ws_appointments` WHERE `id` = ?');
			$st->execute(array($apptId));
			$ref = (string) $st->fetchColumn();
			echo json_encode(array(
				'status' => true,
				'message' => 'Appointment booked. Keep reference ' . $ref . ' for tracking.',
				'appointment_id' => $apptId,
				'ref_no' => $ref,
				'job_no' => $ref,
			));
			exit;
		}
		$id = epc_ws_job_create($db, array(
			'status' => 'checkin',
			'customer_name' => $name,
			'customer_phone' => $phone,
			'customer_email' => trim((string) ($_POST['customer_email'] ?? '')),
			'plate' => $plate,
			'vin' => trim((string) ($_POST['vin'] ?? '')),
			'make' => trim((string) ($_POST['make'] ?? '')),
			'model' => trim((string) ($_POST['model'] ?? '')),
			'year' => trim((string) ($_POST['year'] ?? '')),
			'odometer' => (int) ($_POST['odometer'] ?? 0),
			'complaint' => $complaint,
			'notes' => 'Booked from storefront /auto-workshop',
		));
		$job = epc_ws_job_get($db, $id);
		echo json_encode(array(
			'status' => true,
			'message' => 'Service request received. Keep your job number for tracking.',
			'job_no' => $job['header']['job_no'] ?? '',
			'job_id' => $id,
		));
		exit;
	}

	throw new RuntimeException('Unknown action');
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}

<?php
/**
 * CP AJAX — marketing & growth progress, KPIs, reviews.
 */
defined('_ASTEXE_') or die('No access');

header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_helpers.php';

if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $GLOBALS['DP_Config']->host . ';dbname=' . $GLOBALS['DP_Config']->db,
			$GLOBALS['DP_Config']->user,
			$GLOBALS['DP_Config']->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		echo json_encode(array('status' => false, 'message' => 'No database'));
		exit;
	}
}

$userId = isset($user_session['user_id']) ? (int)$user_session['user_id'] : 0;
$action = isset($_POST['action']) ? (string)$_POST['action'] : '';

try {
	epc_marketing_ensure_schema($db_link);
	switch ($action) {
		case 'toggle_task':
			$strategy = isset($_POST['strategy_key']) ? (string)$_POST['strategy_key'] : '';
			$task = isset($_POST['task_key']) ? (string)$_POST['task_key'] : '';
			$done = !empty($_POST['is_done']);
			$strategies = epc_marketing_strategies();
			if (!isset($strategies[$strategy]) || !isset($strategies[$strategy]['follow_tasks'][$task])) {
				throw new Exception('Invalid task');
			}
			epc_marketing_toggle_task($db_link, $strategy, $task, $done, $userId);
			$progress = epc_marketing_load_progress($db_link);
			$stats = epc_marketing_completion_stats($strategies, $progress);
			echo json_encode(array(
				'status' => true,
				'message' => $done ? 'Task marked done' : 'Task reopened',
				'completion' => $stats,
			));
			break;

		case 'save_kpi':
			$strategy = isset($_POST['strategy_key']) ? (string)$_POST['strategy_key'] : '';
			$kpi = isset($_POST['kpi_key']) ? (string)$_POST['kpi_key'] : '';
			$value = isset($_POST['value']) ? $_POST['value'] : '';
			$note = isset($_POST['note']) ? trim((string)$_POST['note']) : '';
			$strategies = epc_marketing_strategies();
			if (!isset($strategies[$strategy]['kpis'][$kpi])) {
				throw new Exception('Invalid KPI');
			}
			epc_marketing_save_kpi($db_link, $strategy, $kpi, $value, $note, $userId);
			echo json_encode(array('status' => true, 'message' => 'KPI recorded'));
			break;

		case 'save_review':
			$strategy = isset($_POST['strategy_key']) ? (string)$_POST['strategy_key'] : '';
			$type = isset($_POST['review_type']) ? (string)$_POST['review_type'] : 'weekly';
			$score = isset($_POST['score']) ? (int)$_POST['score'] : 0;
			$notes = isset($_POST['notes']) ? trim((string)$_POST['notes']) : '';
			$strategies = epc_marketing_strategies();
			if (!isset($strategies[$strategy])) {
				throw new Exception('Invalid strategy');
			}
			epc_marketing_save_review($db_link, $strategy, $type, $score, $notes, $userId);
			echo json_encode(array('status' => true, 'message' => 'Review saved'));
			break;

		case 'snapshot':
			echo json_encode(array(
				'status' => true,
				'data' => epc_marketing_demo_report($db_link),
			));
			break;

		default:
			echo json_encode(array('status' => false, 'message' => 'Unknown action'));
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}

<?php
/**
 * In-process notification dispatch (no HTTP curl loopback).
 */
defined('_ASTEXE_') or die('No access');

function docpart_dispatch_notification($db_link, $DP_Config, $name, $vars, $persons, $files, $multilang_params)
{
	if (!is_array($vars) || !is_array($persons)) {
		return array('status' => false, 'message' => 'Invalid vars or persons');
	}
	if (!is_array($files)) {
		$files = array();
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/lib/DocpartMailer/docpart_mailer.php';

	$notify_query = $db_link->prepare('SELECT * FROM `notifications_settings` WHERE `name` = ?;');
	$notify_query->execute(array($name));
	$notify = $notify_query->fetch(PDO::FETCH_ASSOC);
	if (!$notify) {
		return array('status' => false, 'message' => 'Notification not found');
	}

	$notify['vars'] = json_decode($notify['vars'], true);
	if (!is_array($notify['vars'])) {
		$notify['vars'] = array();
	}

	$email_subject = translate_str_by_id($notify['email_subject']);
	$email_body = translate_str_by_id($notify['email_body']);
	$sms_body = translate_str_by_id($notify['sms_body']);
	if ($email_subject === null || $email_subject === false || $email_subject === '') {
		$email_subject = (string)$notify['email_subject'];
	}
	if ($email_body === null || $email_body === false || $email_body === '') {
		$email_body = (string)$notify['email_body'];
	}
	if ($sms_body === null || $sms_body === false || $sms_body === '') {
		$sms_body = (string)$notify['sms_body'];
	}

	for ($i = 0; $i < count($notify['vars']); $i++) {
		if ($notify['vars'][$i]['type'] === 'text') {
			$key = $notify['vars'][$i]['name'];
			$val = isset($vars[$key]) ? $vars[$key] : '';
			$email_subject = str_replace('%' . $key . '%', $val, $email_subject);
			$email_body = str_replace('%' . $key . '%', $val, $email_body);
			$sms_body = str_replace('%' . $key . '%', $val, $sms_body);
		}
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/template.php';

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/epc_whatsapp_notify.php';

	$sms_operator_query = $db_link->prepare('SELECT * FROM `sms_api` WHERE `active` = ?;');
	$sms_operator_query->execute(array(1));
	$sms_api = $sms_operator_query->fetch(PDO::FETCH_ASSOC);

	for ($i = 0; $i < count($persons); $i++) {
		if ($persons[$i]['type'] === 'user_id') {
			$persons[$i]['contacts'] = array(
				'phone' => array(),
				'email' => array(),
			);
		}

		$persons[$i]['contacts']['email']['tried_to_send'] = false;
		$persons[$i]['contacts']['email']['email_confirmed'] = false;
		$persons[$i]['contacts']['email']['status'] = false;
		$persons[$i]['contacts']['phone']['tried_to_send'] = false;
		$persons[$i]['contacts']['phone']['phone_confirmed'] = false;
		$persons[$i]['contacts']['phone']['status'] = false;
		if (!isset($persons[$i]['contacts']['whatsapp'])) {
			$persons[$i]['contacts']['whatsapp'] = array(
				'tried_to_send' => false,
				'status' => false,
				'error' => '',
			);
		}

		$email = '';
		$email_confirmed = false;
		$email_to_send = false;
		$phone = '';
		$phone_confirmed = false;
		$phone_to_send = false;

		if ($persons[$i]['type'] === 'user_id') {
			$user_query = $db_link->prepare('SELECT * FROM `users` WHERE `user_id` = ?;');
			$user_query->execute(array($persons[$i]['user_id']));
			$user = $user_query->fetch(PDO::FETCH_ASSOC);
			if ($user) {
				if (!empty($user['email']) && ($user['email_confirmed'] || $notify['send_for_not_confirmed'])) {
					$email = $user['email'];
					$email_confirmed = (bool)$user['email_confirmed'];
					$email_to_send = true;
				}
				if (!empty($user['phone']) && ($user['phone_confirmed'] || $notify['send_for_not_confirmed'])) {
					$phone = $user['phone'];
					$phone_confirmed = (bool)$user['phone_confirmed'];
					$phone_to_send = true;
				}
			}
		} elseif ($persons[$i]['type'] === 'direct_contact') {
			if (!empty($persons[$i]['contacts']['email']['value']) && $notify['send_for_not_confirmed']) {
				$email = $persons[$i]['contacts']['email']['value'];
				$email_to_send = true;
			}
			if (!empty($persons[$i]['contacts']['phone']['value']) && $notify['send_for_not_confirmed']) {
				$phone = $persons[$i]['contacts']['phone']['value'];
				$phone_to_send = true;
			}
		}

		if ((int)$notify['email_on'] === 0) {
			$email_to_send = false;
		}
		if ((int)$notify['sms_on'] === 0) {
			$phone_to_send = false;
		}

		if (isset($DP_Config->orders_statuses_notifications_settings) && (string)$DP_Config->orders_statuses_notifications_settings === '1') {
			$ref = isset($vars['status_ref']) && is_array($vars['status_ref']) ? $vars['status_ref'] : null;
			if ($ref) {
				if ($notify['name'] === 'order_status_to_manager' && (int)($ref['to_manager_sms'] ?? 1) === 0) {
					$phone_to_send = false;
				}
				if ($notify['name'] === 'order_status_to_customer' && (int)($ref['to_customer_sms'] ?? 1) === 0) {
					$phone_to_send = false;
				}
				if ($notify['name'] === 'order_item_status_to_manager' && (int)($ref['to_manager_sms'] ?? 1) === 0) {
					$phone_to_send = false;
				}
				if ($notify['name'] === 'order_item_status_to_customer' && (int)($ref['to_customer_sms'] ?? 1) === 0) {
					$phone_to_send = false;
				}
			}
		}

		if ($email_to_send) {
			$persons[$i]['contacts']['email']['tried_to_send'] = true;
			$persons[$i]['contacts']['email']['email_confirmed'] = $email_confirmed;
			$docpartMailer = new DocpartMailer();
			$docpartMailer->Subject = $email_subject;
			$docpartMailer->Body = $email_body;
			$docpartMailer->CharSet = 'UTF-8';
			$docpartMailer->addAddress($email, $email);
			$docpartMailer->IsSMTP();
			$docpartMailer->IsHTML(true);
			$docpartMailer->SMTPDebug = 0;
			if (!empty($files)) {
				for ($f = 0; $f < count($files); $f++) {
					$docpartMailer->addAttachment($files[$f]['url'], $files[$f]['name']);
				}
			}
			$persons[$i]['contacts']['email']['status'] = (bool)$docpartMailer->Send();
		}

		if ($phone_to_send && $sms_api) {
			$persons[$i]['contacts']['phone']['tried_to_send'] = true;
			$persons[$i]['contacts']['phone']['phone_confirmed'] = $phone_confirmed;
			$phone = str_replace(array(' ', '+7', '(', ')', '-', '_', '+'), '', $phone);
			$postdata = http_build_query(array(
				'check' => $DP_Config->secret_succession,
				'body' => $sms_body,
				'main_field' => $phone,
				'parameters_values' => $sms_api['parameters_values'],
			));
			$curl = curl_init();
			curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path . 'content/sms/handlers/' . $sms_api['handler'] . '/send_sms.php');
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			curl_setopt($curl, CURLOPT_TIMEOUT, 30);
			$curl_result = json_decode((string)curl_exec($curl), true);
			curl_close($curl);
			$persons[$i]['contacts']['phone']['status'] = is_array($curl_result) && !empty($curl_result['status']);
		}

		if ($phone !== '' && function_exists('epc_wa_dispatch_for_person')) {
			$wa = epc_wa_dispatch_for_person(
				$db_link,
				$DP_Config,
				$notify,
				$vars,
				$sms_body,
				$email_body,
				$phone,
				true
			);
			$persons[$i]['contacts']['whatsapp'] = array(
				'tried_to_send' => $wa['tried_to_send'],
				'status' => $wa['status'],
				'error' => $wa['error'],
			);
		}
	}

	return array(
		'status' => true,
		'message' => '',
		'persons' => $persons,
		'dispatch' => 'inline',
	);
}

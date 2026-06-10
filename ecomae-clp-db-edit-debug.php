<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$editPath = '/site/www.ecomae.com/database/user/edit/ecomae';
$html = epc_clp_web_request($panel . $editPath, array(), $cookie);
echo "len=" . strlen($html) . "\n";
if (preg_match_all('/<input[^>]+>/i', $html, $inputs)) {
	foreach ($inputs[0] as $inp) {
		echo $inp . "\n";
	}
}
if (preg_match_all('/<button[^>]+>/i', $html, $btns)) {
	foreach ($btns[0] as $b) {
		echo $b . "\n";
	}
}
if (!empty($_GET['try_post'])) {
	$newPass = trim((string) ($_GET['db_password'] ?? '2674f7feac3e3ac95ba8a965'));
	if (preg_match('/name="site_database_user_edit\[_token\]" value="([^"]+)"/', $html, $tm)) {
		foreach (array('', 'Save', 'Update') as $submit) {
			$data = array(
				'site_database_user_edit' => array(
					'password' => $newPass,
					'_token' => $tm[1],
					'submit' => $submit,
				),
			);
			$resp = epc_clp_web_request($panel . $editPath, array(
				'method' => 'POST',
				'body' => http_build_query($data),
			), $cookie);
			echo "\nPOST submit=" . var_export($submit, true) . " len=" . strlen($resp) . "\n";
			echo substr(strip_tags($resp), 0, 600) . "\n";
		}
	}
}

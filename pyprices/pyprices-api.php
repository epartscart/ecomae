<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

$pyprices = __DIR__;
$api = $pyprices . '/api.py';

$candidates = [
	$pyprices . '/bin/python',
	$pyprices . '/venv/bin/python',
	$pyprices . '/pyprices/bin/python',
	'/usr/bin/python3',
	'/usr/local/bin/python3',
	'python3',
	'python',
];

$python = null;
foreach ($candidates as $candidate) {
	$cmd = escapeshellcmd($candidate) . ' -V 2>&1';
	$output = [];
	$code = 1;
	@exec($cmd, $output, $code);
	if ($code === 0 && isset($output[0]) && preg_match('/Python 3\.(\d+)/', $output[0])) {
		$python = $candidate;
		break;
	}
}

if ($python === null) {
	echo json_encode([
		'status' => false,
		'message' => 'Python 3 is not available on the server',
		'imap_status' => null,
		'list_to_handle' => [],
		'list_to_handle_incorrect' => [],
		'list_to_handle_email' => new stdClass(),
		'errors_general_list' => [],
	]);
	exit;
}

$postBody = http_build_query($_POST);
$descriptorSpec = [
	0 => ['pipe', 'r'],
	1 => ['pipe', 'w'],
	2 => ['pipe', 'w'],
];
$env = array_merge($_ENV, [
	'PYTHONIOENCODING' => 'utf-8',
	'REQUEST_METHOD' => 'POST',
	'CONTENT_TYPE' => 'application/x-www-form-urlencoded',
	'CONTENT_LENGTH' => (string) strlen($postBody),
]);
$process = proc_open(
	[escapeshellcmd($python), $api],
	$descriptorSpec,
	$pipes,
	$pyprices,
	$env
);
if (!is_resource($process)) {
	echo json_encode([
		'status' => false,
		'message' => 'Could not start pyprices process',
		'imap_status' => null,
		'list_to_handle' => [],
		'list_to_handle_incorrect' => [],
		'list_to_handle_email' => new stdClass(),
		'errors_general_list' => [],
	]);
	exit;
}
fwrite($pipes[0], $postBody);
fclose($pipes[0]);
$stdout = stream_get_contents($pipes[1]);
$stderr = stream_get_contents($pipes[2]);
fclose($pipes[1]);
fclose($pipes[2]);
proc_close($process);

$output = (string) $stdout . "\n" . (string) $stderr;
if (trim($output) === '') {
	echo json_encode([
		'status' => false,
		'message' => 'pyprices returned empty output',
		'imap_status' => null,
		'list_to_handle' => [],
		'list_to_handle_incorrect' => [],
		'list_to_handle_email' => new stdClass(),
		'errors_general_list' => [],
	]);
	exit;
}

$output = preg_replace('/Content-Type:[^\n]*\n\s*/i', '', $output);
$trimmed = trim((string) $output);
if (str_contains($trimmed, 'ModuleNotFoundError') || str_contains($trimmed, 'Traceback')) {
	echo json_encode([
		'status' => false,
		'message' => 'Python dependencies are missing on the server. Install python3-pip/python3-venv to enable price processing.',
		'imap_status' => null,
		'list_to_handle' => [],
		'list_to_handle_incorrect' => [],
		'list_to_handle_email' => new stdClass(),
		'errors_general_list' => [$trimmed],
	]);
	exit;
}

$jsonStart = strpos($trimmed, '{');
$jsonEnd = strrpos($trimmed, '}');
if ($jsonStart !== false && $jsonEnd !== false && $jsonEnd >= $jsonStart) {
	echo substr($trimmed, $jsonStart, $jsonEnd - $jsonStart + 1);
	exit;
}

echo $trimmed;

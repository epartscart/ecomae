<?php
declare(strict_types=1);

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit('Forbidden');
}

header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$pyprices = __DIR__;
$candidates = [
	$pyprices . '/venv/bin/python',
	$pyprices . '/bin/python',
	'/usr/bin/python3',
	'/usr/local/bin/python3',
	'python3',
	'python',
];

$python = null;
foreach ($candidates as $candidate) {
	$output = [];
	$code = 1;
	@exec(escapeshellcmd($candidate) . ' -V 2>&1', $output, $code);
	if ($code === 0 && isset($output[0]) && preg_match('/Python 3\./', $output[0])) {
		$python = $candidate;
		echo "Using {$candidate}: {$output[0]}\n";
		break;
	}
}

if ($python === null) {
	exit("No Python 3 found\n");
}

$commands = [
	escapeshellcmd($python) . ' -m pip --version || ' . escapeshellcmd($python) . ' -c ' . escapeshellarg('import urllib.request; urllib.request.urlretrieve("https://bootstrap.pypa.io/get-pip.py", "/tmp/get-pip.py")') . ' && ' . escapeshellcmd($python) . ' /tmp/get-pip.py --user --break-system-packages',
	escapeshellcmd($python) . ' -m pip install --user --break-system-packages mysql-connector-python==8.0.28 imap-tools patool rarfile py7zr price_parser openpyxl xlrd==1.2.0',
];

foreach ($commands as $cmd) {
	echo "\n$ {$cmd}\n";
	$output = [];
	$code = 0;
	exec('cd ' . escapeshellarg($pyprices) . ' && ' . $cmd . ' 2>&1', $output, $code);
	echo implode("\n", array_slice($output, -30)) . "\n";
	echo "exit={$code}\n";
	if ($code !== 0) {
		exit("Failed\n");
	}
}

echo "Dependencies installed\n";

<?php
// Runs one real content/sms/handlers/<handler>/send_sms.php inside namespace H, where curl_*, PDO,
// DP_Config and the translator are stubs, and prints the provider requests plus the handler's answer.
// Usage: php harness.php <repo root> <cases.json> <case name>
namespace H;

$GLOBALS['H_CASE'] = null;
foreach (json_decode(file_get_contents($argv[2]), true) as $c) {
	if ($c['name'] === $argv[3]) {
		$GLOBALS['H_CASE'] = $c;
	}
}
$GLOBALS['H_REQUESTS'] = array();

const CURLOPT_URL = 10002;
const CURLOPT_RETURNTRANSFER = 19913;
const CURLOPT_SSL_VERIFYHOST = 81;
const CURLOPT_SSL_VERIFYPEER = 64;
const CURLOPT_POST = 47;
const CURLOPT_POSTFIELDS = 10015;
const CURLOPT_HTTPHEADER = 10023;
const CURLOPT_CONNECTTIMEOUT = 78;
const CURLOPT_TIMEOUT = 13;
const CURLOPT_FOLLOWLOCATION = 52;
const CURLOPT_ENCODING = 10102;
const CURLOPT_MAXREDIRS = 68;
const CURLOPT_HTTP_VERSION = 84;
const CURLOPT_CUSTOMREQUEST = 10036;
const CURLOPT_USERPWD = 10005;
const CURLOPT_USERAGENT = 10018;
const CURLOPT_HEADER = 42;
const CURL_HTTP_VERSION_1_1 = 2;
const CURLINFO_HTTP_CODE = 2097154;
const CURLINFO_HEADER_OUT = 2;

class DP_Config
{
	public $host = 'h';
	public $db = 'd';
	public $user = 'u';
	public $password = 'p';
	public $secret_succession = 'sec-key';
	public $domain_path = 'https://shop.example.com/';
}

class PDOStatement
{
	public function execute($args = array())
	{
		$GLOBALS['H_HANDLER_QUERIED'] = $args[0] ?? '';
		return true;
	}

	public function fetch($mode = 0)
	{
		$c = $GLOBALS['H_CASE'];
		if (!array_key_exists('params', $c) || $c['params'] === null) {
			return false;
		}
		return array('handler' => $c['handler'], 'parameters_values' => json_encode($c['params']));
	}
}

class PDO
{
	const FETCH_ASSOC = 2;
	const ATTR_ERRMODE = 3;
	const ERRMODE_EXCEPTION = 2;

	public function __construct(...$a) {}
	public function query($sql) { return true; }
	public function prepare($sql) { return new PDOStatement(); }
}

function multilang_init() { return array(); }
function translate_str_by_id($id) { return 'T' . $id; }
function fopen(...$a) { return 'stub'; }
function fwrite(...$a) { return 0; }
function fclose(...$a) { return true; }

function curl_init($url = null)
{
	$GLOBALS['H_REQUESTS'][] = array('opts' => array());
	return count($GLOBALS['H_REQUESTS']) - 1;
}
function curl_setopt($h, $opt, $value)
{
	$GLOBALS['H_REQUESTS'][$h]['opts'][$opt] = $value;
	return true;
}
function curl_setopt_array($h, $opts)
{
	foreach ($opts as $k => $v) {
		curl_setopt($h, $k, $v);
	}
	return true;
}
function curl_escape($h, $s) { return rawurlencode($s); }
function curl_error($h) { return ''; }
function curl_close($h) {}
function curl_getinfo($h, $what = 0)
{
	if ($what === CURLINFO_HTTP_CODE) {
		return (int) ($GLOBALS['H_CASE']['response']['status'] ?? 200);
	}
	return '';
}
function curl_exec($h)
{
	$r = $GLOBALS['H_CASE']['response'] ?? null;
	if ($r === null) {
		return false;
	}
	$body = (string) $r['body'];
	$opts = $GLOBALS['H_REQUESTS'][$h]['opts'];
	if (!empty($opts[CURLOPT_HEADER])) {
		$body = 'HTTP/1.1 ' . (int) $r['status'] . ' ' . ($r['reason'] ?? 'OK') . "\r\nContent-Type: text/plain\r\n\r\n" . $body;
	}
	if (empty($opts[CURLOPT_RETURNTRANSFER])) {
		echo $body;
		return true;
	}
	return $body;
}

register_shutdown_function(function () {
	$out = ob_get_clean();
	$requests = array();
	foreach ($GLOBALS['H_REQUESTS'] as $r) {
		$o = $r['opts'];
		$headers = array();
		foreach ((array) ($o[CURLOPT_HTTPHEADER] ?? array()) as $line) {
			$headers[] = $line;
		}
		if (isset($o[CURLOPT_USERPWD])) {
			$headers[] = 'Authorization: Basic ' . base64_encode($o[CURLOPT_USERPWD]);
		}
		if (isset($o[CURLOPT_USERAGENT])) {
			$headers[] = 'User-Agent: ' . $o[CURLOPT_USERAGENT];
		}
		$post = !empty($o[CURLOPT_POST]) || isset($o[CURLOPT_POSTFIELDS]);
		$requests[] = array(
			'method' => isset($o[CURLOPT_CUSTOMREQUEST]) ? $o[CURLOPT_CUSTOMREQUEST] : ($post ? 'POST' : 'GET'),
			'url' => $o[CURLOPT_URL] ?? '',
			'body' => isset($o[CURLOPT_POSTFIELDS]) ? (string) $o[CURLOPT_POSTFIELDS] : null,
			'headers' => $headers,
		);
	}
	\fwrite(STDOUT, json_encode(array('requests' => $requests, 'output' => $out), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
});

$root = $argv[1];
$c = $GLOBALS['H_CASE'];
$_POST = $c['post'];
$docRoot = sys_get_temp_dir() . '/sms-harness-root';
@mkdir($docRoot . '/lang', 0700, true);
@mkdir($docRoot . '/content/sms', 0700, true);
\file_put_contents($docRoot . '/config.php', '<?php ');
\file_put_contents($docRoot . '/lang/dp_lang.php', '<?php ');
\file_put_contents($docRoot . '/content/sms/epc_sms_helpers.php', '<?php ');
$_SERVER['DOCUMENT_ROOT'] = $docRoot;
$src = \file_get_contents($root . '/content/sms/handlers/' . $c['handler'] . '/send_sms.php');
$src = preg_replace('/^<\?php/', '', $src);
$src = preg_replace('/\?>\s*$/', '', $src);
ob_start();
eval('namespace H; ' . $src);

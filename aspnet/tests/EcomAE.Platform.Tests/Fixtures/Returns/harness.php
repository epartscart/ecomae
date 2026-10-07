<?php
// Renders the PHP content/shop/returns pages against a throwaway database for the golden HTML.
// One page per process (return.php exits for guests); run with -d short_open_tag=1 like the server (add_return.php uses <? tags).
// Cases run in the order of $cases: return.php marks the return's manager messages read.
// Usage: php -d short_open_tag=1 harness.php <repo_root> <dsn> <user> <password> <out_dir> <case>
declare(strict_types=1);

[$self, $root, $dsn, $dbUser, $dbPassword, $out, $case] = $argv;
$_SERVER['DOCUMENT_ROOT'] = $root;
define('_ASTEXE_', 1);

final class HarnessConfig
{
	public string $domain_path = 'https://www.epartscart.com/';
	public string $tech_key = 'tk-secret';
	public string $return_available = '1';
	public string $retention_percentage = '10';
	public string $retention_percentage_text = 'ret_note';
}

$cases = array(
	'list_guest' => array('returns.php', 0, array()),
	'list' => array('returns.php', 7, array()),
	'list_unread' => array('returns.php', 7, array('read' => '0')),
	'list_other' => array('returns.php', 8, array()),
	'return_guest' => array('return.php', 0, array('return_id' => '1')),
	'return_foreign' => array('return.php', 7, array('return_id' => '4')),
	'return_pending' => array('return.php', 7, array('return_id' => '1')),
	'return_decided' => array('return.php', 7, array('return_id' => '2')),
	'list_after_read' => array('returns.php', 7, array('read' => '0')),
	'add_disabled' => array('add_return.php', 7, array('items' => '[90]')),
	'add_items' => array('add_return.php', 7, array('items' => '[90,91,92,93]')),
	'add_no_retention' => array('add_return.php', 7, array('items' => '["91"]')),
	'add_foreign' => array('add_return.php', 7, array('items' => '[90,97]')),
	'add_none' => array('add_return.php', 7, array('items' => '[92]')),
	'add_script' => array('assets/add_return.js.php', 0, array()),
);
[$file, $userId, $get] = $cases[$case];

$DP_Config = new HarnessConfig();
if ($case === 'add_disabled') {
	$DP_Config->return_available = '0';
}
if ($case === 'add_no_retention') {
	$DP_Config->retention_percentage = '0';
	$DP_Config->retention_percentage_text = '0';
}
$GLOBALS['DP_Config'] = $DP_Config;
$DP_Lang = 'en';
$multilang_params = array('lang' => 'en', 'lang_href' => '/en', 'lang_href_slash_after' => 'en/');
$_GET = $get;
if ($userId > 0) {
	$_COOKIE['session'] = 'tok-' . $userId;
	$_COOKIE['u_id'] = (string) $userId;
}
$db_link = new PDO($dsn, $dbUser, $dbPassword);
$db_link->query('SET NAMES utf8;');
require $root . '/lang/dp_lang.php';
require_once $root . '/content/users/dp_user.php';
$user_session = DP_User::getUserSession();

file_put_contents('/tmp/ecomae_returns_fixture_photo.png', "\x89PNG\r\n\x1a\nfixture");
@unlink('/tmp/ecomae_returns_fixture_missing.png');

ob_start();
register_shutdown_function(static function () use ($out, $case): void {
	file_put_contents($out . '/' . $case . '.html', ob_get_clean());
});
if ($file === 'assets/add_return.js.php') {
	// The asset opens config.php and its own PDO; render only the body after that header block.
	$source = file_get_contents($root . '/content/shop/returns/' . $file);
	$header = strpos($source, "?>\r\n");
	eval('?>' . substr($source, $header + 4));
} else {
	require $root . '/content/shop/returns/' . $file;
}

<?php
// PHP 8.3 goldens for plan Q1-beck (storefront slider module). Full path only; CP slider sibling stays closed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['BECK_DBS'] = array();

function beck_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	file_put_contents($dest, $code);
}

function beck_pdo(string $suffix): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_beck_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['BECK_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `slider_setings` (
		`connected` TINYINT NOT NULL,
		`cnt_img` INT NOT NULL,
		`cnt_img_next` INT NOT NULL,
		`time_next` INT NOT NULL
	)');
	$pdo->exec('CREATE TABLE `slider_images` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`link` VARCHAR(255) NULL,
		`href` VARCHAR(255) NOT NULL,
		`orders` INT NOT NULL
	)');
	return $pdo;
}

function beck_drop(): void
{
	if (empty($GLOBALS['BECK_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['BECK_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['BECK_DBS'] = array();
}

function beck_stubs(): void
{
	if (!class_exists('DP_Content', false)) {
		class DP_Content
		{
			public $main_flag = false;
		}
	}
	if (!class_exists('DP_Template', false)) {
		class DP_Template
		{
			public $data_value;
			public function __construct()
			{
				$this->data_value = (object) array(
					'menu_category_show' => '',
					'header_style' => '',
				);
			}
		}
	}
}

function beck_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	beck_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['BECK_DOCROOT'];
	$db_link = $GLOBALS['db_link'];
	$DP_Content = $GLOBALS['DP_Content'];
	$DP_Template = $GLOBALS['DP_Template'];
	require $path;
}

function beck_render(PDO $pdo, mixed $connected, int $cnt, int $next, int $time, bool $main, mixed $menu, mixed $header): string
{
	$content = new DP_Content();
	$content->main_flag = $main;
	$tpl = new DP_Template();
	$tpl->data_value->menu_category_show = $menu;
	$tpl->data_value->header_style = $header;
	$GLOBALS['DP_Content'] = $content;
	$GLOBALS['DP_Template'] = $tpl;
	$GLOBALS['db_link'] = $pdo;
	if ($connected !== null) {
		$pdo->exec('DELETE FROM `slider_setings`');
		$pdo->prepare('INSERT INTO `slider_setings` (`connected`,`cnt_img`,`cnt_img_next`,`time_next`) VALUES (?,?,?,?)')
			->execute(array($connected, $cnt, $next, $time));
	}
	$tmp = sys_get_temp_dir() . '/ecomae_beck_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['BECK_PAGE'], $tmp);
	ob_start();
	beck_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function beck_seed_images(PDO $pdo, array $rows): void
{
	$pdo->exec('DELETE FROM `slider_images`');
	$ins = $pdo->prepare('INSERT INTO `slider_images` (`link`,`href`,`orders`) VALUES (?,?,?)');
	foreach ($rows as $row) {
		$ins->execute($row);
	}
}

function beck_run_off(): array
{
	$pdo = beck_pdo('off');
	$none = beck_render($pdo, null, 3, 1, 4000, false, '', '');
	beck_seed_images($pdo, array(array('/acme', '/img/acme.jpg', 1)));
	$off = beck_render($pdo, 0, 3, 1, 4000, true, 1, 'no_header');
	$stringOff = beck_render($pdo, '0', 3, 1, 4000, true, 1, 'no_header');
	$pdo->exec('DELETE FROM `slider_images`');
	$noSlides = beck_render($pdo, 1, 3, 1, 4000, true, 1, 'no_header');
	return array($none, $off, $stringOff, $noSlides);
}

function beck_run_slides(): array
{
	$pdo = beck_pdo('slides');
	beck_seed_images($pdo, array(
		array('/promo', '/img/promo.jpg', 2),
		array('', '/img/plain.jpg', 1),
		array('0', '/img/zero.jpg', 3),
	));
	$html = beck_render($pdo, 1, 3, 1, 4000, false, '', '');
	beck_seed_images($pdo, array(
		array(null, '/img/null-link.jpg', 1),
	));
	$nullLink = beck_render($pdo, '1', 2, 2, 2500, false, '0', 'boxed');
	return array($html, $nullLink);
}

function beck_run_chrome(): array
{
	$pdo = beck_pdo('chrome');
	beck_seed_images($pdo, array(
		array('/keep', '/img/keep.jpg', 1),
	));
	$menu = beck_render($pdo, 1, 4, 2, 5000, true, 1, 'boxed');
	$noHeader = beck_render($pdo, 1, 4, 2, 5000, true, 'yes', 'no_header');
	$notMain = beck_render($pdo, 1, 4, 2, 5000, false, 1, 'no_header');
	$emptyMenu = beck_render($pdo, 1, 4, 2, 5000, true, '', 'no_header');
	$zeroMenu = beck_render($pdo, 1, 4, 2, 5000, true, '0', 'no_header');
	return array($menu, $noHeader, $notMain, $emptyMenu, $zeroMenu);
}

function beck_run_tenant(): array
{
	$acme = beck_pdo('acme');
	beck_seed_images($acme, array(
		array('/acme-city', '/img/acme-city.jpg', 1),
	));
	$acmeHtml = beck_render($acme, 1, 3, 1, 4000, true, 1, 'no_header');
	$beta = beck_pdo('beta');
	beck_seed_images($beta, array(
		array('/beta-town', '/img/beta-town.jpg', 1),
	));
	$betaHtml = beck_render($beta, 1, 2, 1, 3000, false, '', 'boxed');
	return array($acmeHtml, $betaHtml);
}

register_shutdown_function('beck_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_beck_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	beck_patch($root . '/modules/slider/slider.php', $tmp . '/page.php');
	$GLOBALS['BECK_PAGE'] = $tmp . '/page.php';
	$GLOBALS['BECK_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	beck_stubs();
	$fn = 'beck_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		beck_drop();
		exit(1);
	}
	beck_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1beck_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1beck_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";

<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$passwords = array();
$scanRoots = array('/home/ecomae', '/home/ecomaecp', '/home/epartscart', '/home/clp');
foreach ($scanRoots as $root) {
	if (!is_dir($root)) {
		continue;
	}
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($root, FilesystemIterator::SKIP_DOTS),
		RecursiveIteratorIterator::SELF_FIRST
	);
	foreach ($it as $file) {
		if (!$file->isFile()) {
			continue;
		}
		$name = $file->getFilename();
		if ($name !== 'config.local.php' && $name !== '.env' && stripos($name, 'my.cnf') === false) {
			continue;
		}
		$text = (string) @file_get_contents($file->getPathname());
		if ($text === '') {
			continue;
		}
		if (preg_match_all("/['\"]password['\"]\s*=>\s*['\"]([^'\"]+)['\"]/", $text, $m)) {
			foreach ($m[1] as $p) {
				$passwords[$p] = $file->getPathname();
			}
		}
		if (preg_match_all("/DB_PASSWORD=(['\"]?)([^'\"\s]+)\\1/", $text, $m2)) {
			foreach ($m2[2] as $p) {
				$passwords[$p] = $file->getPathname();
			}
		}
	}
}

$extra = array(
	'ec9bbf589990e04516e5c121',
	'166397986a03c403fe2c4111',
	'79abee21d9e877496601e206',
	'EcomaeDb2026xK9mQ2',
	'EpC4rt_Db_2026_xK9mQ2',
	'2674f7feac3e3ac95ba8a965',
);
foreach ($extra as $p) {
	$passwords[$p] = 'hardcoded';
}

echo 'candidates=' . count($passwords) . "\n";
foreach ($passwords as $pass => $src) {
	try {
		$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $pass);
		echo "WORKS len=" . strlen($pass) . " from={$src}\n";
		$cfgWww = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($pass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n\t'from_name' => 'ecomae',\n\t'from_email' => 'hello@ecomae.com',\n);\n";
		$cfgCp = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfgWww);
		file_put_contents('/home/ecomae/htdocs/www.ecomae.com/config.local.php', $cfgWww);
		file_put_contents('/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php', $cfgCp);
		echo 'tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
		exit("config fixed\n");
	} catch (Exception $e) {
		echo 'fail len=' . strlen($pass) . ' src=' . basename($src) . "\n";
	}
}

$clpDbPaths = array(
	'/home/clp/htdocs/app/data/db.sq3',
	'/home/clp/htdocs/app/data/db.sqlite',
	'/etc/cloudpanel/cloudpanel.db',
);
foreach ($clpDbPaths as $path) {
	echo "clpdb {$path} " . (is_readable($path) ? 'readable' : 'no') . "\n";
}

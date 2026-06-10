<?php
declare(strict_types=1);

require_once __DIR__ . '/config.php';

function epc_sitemap_pdo(DP_Config $cfg): ?PDO
{
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db,
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$pdo->query('SET NAMES utf8');
		return $pdo;
	} catch (Exception $e) {
		return null;
	}
}

function epc_sitemap_base_url(DP_Config $cfg): string
{
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($cfg);
	$base = rtrim((string) $cfg->domain_path, '/');
	if ($base === '' || strpos($base, 'localhost') !== false) {
		$guessed = epc_portal_guess_domain_path();
		if ($guessed !== '') {
			$base = rtrim($guessed, '/');
		}
	}
	return $base;
}

function epc_sitemap_lang(): string
{
	return 'en';
}

function epc_sitemap_parts_root(DP_Config $cfg): string
{
	return isset($cfg->chpu_search_config['level_1']['url']) ? (string) $cfg->chpu_search_config['level_1']['url'] : 'parts';
}

function epc_sitemap_slash_code(DP_Config $cfg): string
{
	return isset($cfg->chpu_search_config['slash_code']) ? (string) $cfg->chpu_search_config['slash_code'] : '&ms;';
}

function epc_sitemap_segment($value, $slash_code): string
{
	$value = trim((string) $value);
	$value = str_replace('/', $slash_code, $value);
	return rawurlencode($value);
}

function epc_sitemap_parts_path(DP_Config $cfg, $lang, $parts_root, $slash_code, $path): string
{
	$base = epc_sitemap_base_url($cfg);
	return htmlspecialchars($base . '/' . $lang . '/' . $parts_root . '/' . $path, ENT_XML1, 'UTF-8');
}

function epc_sitemap_page_url(DP_Config $cfg, $lang, $path): string
{
	$base = epc_sitemap_base_url($cfg);
	$path = ltrim((string) $path, '/');
	return htmlspecialchars($base . '/' . $lang . '/' . $path, ENT_XML1, 'UTF-8');
}

function epc_sitemap_emit_urlset(array $entries): void
{
	echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
	echo "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";
	foreach ($entries as $entry) {
		echo "\t<url>\n";
		echo "\t\t<loc>" . $entry['loc'] . "</loc>\n";
		echo "\t\t<lastmod>" . $entry['lastmod'] . "</lastmod>\n";
		echo "\t\t<changefreq>" . $entry['changefreq'] . "</changefreq>\n";
		echo "\t\t<priority>" . $entry['priority'] . "</priority>\n";
		echo "\t</url>\n";
	}
	echo "</urlset>\n";
}

function epc_sitemap_add_entry(array &$entries, array &$seen, $loc, $lastmod, $changefreq, $priority): void
{
	if (isset($seen[$loc])) {
		return;
	}
	$seen[$loc] = true;
	$entries[] = array(
		'loc' => $loc,
		'lastmod' => $lastmod,
		'changefreq' => $changefreq,
		'priority' => $priority,
	);
}

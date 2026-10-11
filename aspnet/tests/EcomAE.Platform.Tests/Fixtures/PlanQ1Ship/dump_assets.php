<?php
// Dump static catalogs for PhpPlanQ1ShipJson.cs (no leftover sibling paths).
define('_ASTEXE_', 1);
$root = rtrim($argv[1] ?? '/workspace', '/');
$_SERVER['DOCUMENT_ROOT'] = $root;
$tmp = sys_get_temp_dir() . '/ecomae_cpw_q1s_dump_' . substr(md5(uniqid('', true)), 0, 8);
@mkdir($tmp . '/content/social_media', 0777, true);
file_put_contents($tmp . '/content/social_media/epc_social_media_helpers.php', "<?php\nfunction epc_social_adapt_text(string \$text, array \$brand): string {\n\t\$replacements = array(\n\t\t'ECOM AE' => (string) \$brand['brand_name'],\n\t\t'ecomae.official' => (string) \$brand['handle'],\n\t\t'ecomae.com' => (string) \$brand['domain'],\n\t\t'https://www.ecomae.com' => (string) \$brand['website'],\n\t\t'www.ecomae.com' => (string) \$brand['domain'],\n\t\t'#ECOMAE' => '#' . strtoupper(preg_replace('/[^A-Z0-9]/', '', strtoupper((string) \$brand['brand_name']))),\n\t);\n\treturn str_replace(array_keys(\$replacements), array_values(\$replacements), \$text);\n}\n");
$_SERVER['DOCUMENT_ROOT'] = $tmp;
copy($root . '/content/social_media/epc_social_media_pack_data.php', $tmp . '/content/social_media/epc_social_media_pack_data.php');
require $tmp . '/content/social_media/epc_social_media_pack_data.php';
require $root . '/content/shop/price_engine/epc_electronics_taxonomy.php';
echo json_encode(array(
	'platforms' => epc_social_pack_platforms(),
	'posts' => array(
		'linkedin' => epc_social_pack_posts('linkedin'),
		'instagram' => epc_social_pack_posts('instagram'),
		'facebook' => epc_social_pack_posts('facebook'),
		'x' => epc_social_pack_posts('x'),
		'tiktok' => epc_social_pack_posts('tiktok'),
		'nope' => epc_social_pack_posts('nope'),
	),
	'reels' => epc_social_instagram_reels_ideas(),
	'tiktok_specs' => epc_social_tiktok_specs(),
	'videos' => epc_social_video_library(),
	'thread' => epc_social_x_thread_starter(),
	'tax_tree' => epc_tax_seed_tree(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($tmp, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
foreach ($it as $file) {
	$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
}
@rmdir($tmp);

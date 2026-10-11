<?php
// PHP 8.3 goldens for plan Q1-atoll (social media hub panel). Leftover dp_user stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function atoll_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/social_media\/epc_social_media_helpers\.php[\'"]\s*;/', '// helpers injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/social_media\/epc_social_media_pack_data\.php[\'"]\s*;/', '// pack injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/social_media\/epc_social_publish\.php[\'"]\s*;/', '// publish injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_cp_page_frame\.php[\'"]\s*;/', '// frame injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/users\/dp_user\.php[\'"]\s*;/', '// leftover user injected', $code);
	file_put_contents($dest, $code);
}

function atoll_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function atoll_stubs(): void
{
	if (!function_exists('epc_social_h')) {
		function epc_social_h($v): string
		{
			return atoll_h($v);
		}
	}
	if (!function_exists('epc_social_pdo')) {
		function epc_social_pdo($fallback = null)
		{
			return $GLOBALS['ATOLL_PDO'] ?? $fallback;
		}
	}
	if (!function_exists('epc_social_ensure_schema')) {
		function epc_social_ensure_schema($pdo): void
		{
		}
	}
	if (!function_exists('epc_social_resolve_site_key')) {
		function epc_social_resolve_site_key($pdo): string
		{
			return (string) ($GLOBALS['ATOLL_SITE'] ?? 'acme_parts');
		}
	}
	if (!function_exists('epc_social_brand_context')) {
		function epc_social_brand_context($siteKey, $pdo): array
		{
			return (array) ($GLOBALS['ATOLL_BRAND'][$siteKey] ?? array(
				'brand_name' => 'Acme Parts',
				'industry' => 'auto_parts',
				'market' => "O'Reilly UAE",
				'country' => 'AE',
				'handle' => '@acme',
			));
		}
	}
	if (!function_exists('epc_social_list_accounts')) {
		function epc_social_list_accounts($pdo, $siteKey): array
		{
			return (array) ($GLOBALS['ATOLL_ACCOUNTS'][$siteKey] ?? array());
		}
	}
	if (!function_exists('epc_social_list_drafts')) {
		function epc_social_list_drafts($pdo, $siteKey): array
		{
			return (array) ($GLOBALS['ATOLL_DRAFTS'][$siteKey] ?? array());
		}
	}
	if (!function_exists('epc_social_trending_formats')) {
		function epc_social_trending_formats(): array
		{
			return (array) ($GLOBALS['ATOLL_TRENDS'] ?? array(array('name' => 'Reel', 'platforms' => 'IG/TT', 'tip' => 'Keep it under 15s')));
		}
	}
	if (!function_exists('epc_social_industry_hooks')) {
		function epc_social_industry_hooks($industry, $brand): array
		{
			return (array) ($GLOBALS['ATOLL_HOOKS'] ?? array('Same-day delivery in ' . $industry));
		}
	}
	if (!function_exists('epc_social_backend')) {
		function epc_social_backend(): string
		{
			return (string) ($GLOBALS['ATOLL_BACKEND'] ?? 'cp');
		}
	}
	if (!function_exists('epc_social_hub_url')) {
		function epc_social_hub_url(string $tab = 'pack', ?string $siteKey = null): string
		{
			$base = '/' . epc_social_backend() . '/control/portal/epc_social_media_hub';
			$params = array();
			if ($tab !== '' && $tab !== 'pack') {
				$params['tab'] = $tab;
			}
			if ($siteKey !== null && $siteKey !== '' && $siteKey !== 'platform') {
				$params['site_key'] = $siteKey;
			}
			return $params ? $base . '?' . http_build_query($params) : $base;
		}
	}
	if (!function_exists('epc_social_tenant_hub_url')) {
		function epc_social_tenant_hub_url(string $tab = 'social'): string
		{
			return '/' . epc_social_backend() . '/shop/tenant_hub/tenant_hub?tab=' . rawurlencode($tab);
		}
	}
	if (!function_exists('epc_social_csrf_token')) {
		function epc_social_csrf_token(): string
		{
			return (string) ($GLOBALS['ATOLL_CSRF'] ?? 'tok-1');
		}
	}
	if (!function_exists('epc_social_verify_csrf')) {
		function epc_social_verify_csrf(): bool
		{
			return !empty($GLOBALS['ATOLL_CSRF_OK']);
		}
	}
	if (!function_exists('epc_social_save_account')) {
		function epc_social_save_account($pdo, $site, $post): array
		{
			return array('ok' => true, 'message' => 'saved:' . $site . ':' . ($post['platform'] ?? ''));
		}
	}
	if (!function_exists('epc_social_test_account')) {
		function epc_social_test_account($pdo, $site, $plat): array
		{
			return array('ok' => true, 'message' => 'tested:' . $plat);
		}
	}
	if (!function_exists('epc_social_delete_account')) {
		function epc_social_delete_account($pdo, $site, $plat): array
		{
			return array('ok' => true, 'message' => 'deleted:' . $plat);
		}
	}
	if (!function_exists('epc_social_save_draft')) {
		function epc_social_save_draft($pdo, $site, $post): array
		{
			return array('ok' => true, 'message' => 'draft');
		}
	}
	if (!function_exists('epc_social_publish_draft')) {
		function epc_social_publish_draft($pdo, $site, $id): array
		{
			return array('ok' => true, 'message' => 'pub:' . $id);
		}
	}
	if (!function_exists('epc_social_publish_now')) {
		function epc_social_publish_now($pdo, $site, $post): array
		{
			return array('ok' => true, 'message' => 'now');
		}
	}
	if (!function_exists('epc_social_pack_posts')) {
		function epc_social_pack_posts(string $plat): array
		{
			return (array) ($GLOBALS['ATOLL_PACK'][$plat] ?? array());
		}
	}
	if (!function_exists('epc_social_pack_posts_for_brand')) {
		function epc_social_pack_posts_for_brand(string $plat, array $brand): array
		{
			return (array) ($GLOBALS['ATOLL_PACK_BRAND'][$plat] ?? array());
		}
	}
	if (!function_exists('epc_social_pack_platforms')) {
		function epc_social_pack_platforms(): array
		{
			return array(
				'linkedin' => array('label' => 'LinkedIn', 'intro' => 'B2B', 'hashtags' => array('#Auto {brand}')),
				'instagram' => array('label' => 'Instagram', 'intro' => 'Visual', 'hashtags' => array()),
				'facebook' => array('label' => 'Facebook', 'intro' => 'Reach', 'hashtags' => array()),
				'x' => array('label' => 'X', 'intro' => 'Short', 'hashtags' => array()),
				'tiktok' => array('label' => 'TikTok', 'intro' => 'Reels', 'hashtags' => array()),
			);
		}
	}
	if (!function_exists('epc_social_adapt_text')) {
		function epc_social_adapt_text(string $text, array $brand): string
		{
			return str_replace('{brand}', (string) ($brand['brand_name'] ?? ''), $text);
		}
	}
	if (!function_exists('epc_social_x_thread_starter')) {
		function epc_social_x_thread_starter(): string
		{
			return "Thread for {brand}\n1/ Why stock";
		}
	}
	if (!function_exists('epc_social_tiktok_specs')) {
		function epc_social_tiktok_specs(): array
		{
			return array('Ratio' => '9:16', 'Length' => '15-30s');
		}
	}
	if (!function_exists('epc_social_video_library')) {
		function epc_social_video_library(): array
		{
			return (array) ($GLOBALS['ATOLL_VIDEOS'] ?? array(
				array('title' => "O'Reilly reel", 'url' => 'https://cdn.test/a.mp4', 'kind' => 'reel', 'blurb' => 'Hook'),
				array('title' => 'Guide clip', 'url' => 'https://cdn.test/g.mp4', 'kind' => 'guide', 'blurb' => ''),
			));
		}
	}
	if (!function_exists('epc_social_instagram_reels_ideas')) {
		function epc_social_instagram_reels_ideas(): array
		{
			return array(array('title' => 'Unbox', 'caption' => 'New {brand} drop'));
		}
	}
	if (!function_exists('epc_social_platforms')) {
		function epc_social_platforms(): array
		{
			return array(
				'instagram' => array('label' => 'Instagram', 'icon' => 'fa-instagram', 'color' => '#e1306c'),
				'facebook' => array('label' => 'Facebook', 'icon' => 'fa-facebook', 'color' => '#1877f2'),
			);
		}
	}
	if (!function_exists('epc_social_account_public_meta')) {
		function epc_social_account_public_meta($pdo, $site, $key): array
		{
			return (array) ($GLOBALS['ATOLL_META'][$key] ?? array(
				'page_id' => 'p1', 'ig_user_id' => 'ig1', 'open_id' => '', 'privacy_level' => 'SELF_ONLY', 'has_token' => true,
			));
		}
	}
	if (!function_exists('epc_cp_page_frame_open')) {
		function epc_cp_page_frame_open(array $opts = array()): void
		{
			echo 'FRAME_OPEN:' . (string) ($opts['class'] ?? '') . ':' . (string) ($opts['hero']['badge'] ?? '') . ':' . (string) ($opts['hero']['title'] ?? '');
		}
	}
	if (!function_exists('epc_cp_page_frame_close')) {
		function epc_cp_page_frame_close(): void
		{
			echo 'FRAME_CLOSE';
		}
	}
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['ATOLL_SUPER_HOST']);
		}
	}
	if (!function_exists('epc_portal_list_tenants')) {
		function epc_portal_list_tenants($pdo): array
		{
			return (array) ($GLOBALS['ATOLL_TENANTS'] ?? array(
				array('site_key' => 'acme_parts'),
				array('site_key' => ''),
				array('site_key' => 'beta'),
			));
		}
	}
	if (!class_exists('DP_User', false)) {
		class DP_User
		{
			public static function isAdmin(): bool
			{
				return !empty($GLOBALS['ATOLL_ADMIN']);
			}
		}
	}
}

function atoll_include(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	atoll_stubs();
	require_once $GLOBALS['ATOLL_PAGE'];
}

function atoll_capture(callable $fn): string
{
	ob_start();
	$fn();
	return (string) ob_get_clean();
}

function atoll_pdo(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function atoll_reset(): void
{
	$GLOBALS['ATOLL_PDO'] = null;
	$GLOBALS['ATOLL_SITE'] = 'acme_parts';
	$GLOBALS['ATOLL_ADMIN'] = 0;
	$GLOBALS['ATOLL_SUPER_HOST'] = 0;
	$GLOBALS['ATOLL_CSRF_OK'] = 0;
	$GLOBALS['ATOLL_CSRF'] = 'tok-1';
	$GLOBALS['ATOLL_BACKEND'] = 'cp';
	$GLOBALS['ATOLL_PACK'] = array('linkedin' => array(array('title' => 'A')), 'instagram' => array(), 'facebook' => array(), 'x' => array());
	$GLOBALS['ATOLL_PACK_BRAND'] = array(
		'linkedin' => array(array('title' => "O'Reilly post", 'caption' => 'Stock {brand}')),
		'instagram' => array(),
		'facebook' => array(),
		'x' => array(),
		'tiktok' => array(array('title' => 'Reel 1', 'caption' => 'Fast clip')),
	);
	$GLOBALS['ATOLL_ACCOUNTS'] = array();
	$GLOBALS['ATOLL_DRAFTS'] = array();
	$GLOBALS['db_link'] = null;
	$_GET = array();
	$_POST = array();
	$_SERVER['REQUEST_METHOD'] = 'GET';
}

function atoll_run_names(): array
{
	atoll_include();
	$hint = epc_social_media_explore_hint_html();
	$empty = atoll_capture(static function () {
		epc_social_render_video_card(array('url' => '', 'title' => 'Nope'));
	});
	$vert = atoll_capture(static function () {
		epc_social_render_video_card(array(
			'title' => "O'Reilly reel",
			'url' => 'https://cdn.test/a.mp4?q=1&x=2',
			'blurb' => 'Hook & cut',
		), true);
	});
	$flat = atoll_capture(static function () {
		epc_social_render_video_card(array('title' => 'Guide', 'url' => 'https://cdn.test/g.mp4'));
	});
	return array($hint, $empty, $vert, $flat);
}

function atoll_run_gates(): array
{
	atoll_include();
	atoll_reset();
	$superNo = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => 1));
	});
	$tenantNo = atoll_capture(static function () {
		epc_social_media_render_hub(array());
	});
	$GLOBALS['ATOLL_ADMIN'] = 1;
	$noDb = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => 1));
	});
	$zeroSuper = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => '0'));
	});
	return array($superNo, $tenantNo, $noDb, $zeroSuper);
}

function atoll_run_hub(): array
{
	atoll_include();
	atoll_reset();
	$pdo = atoll_pdo();
	$GLOBALS['ATOLL_PDO'] = $pdo;
	$GLOBALS['ATOLL_ADMIN'] = 1;
	$GLOBALS['ATOLL_SITE'] = 'acme_parts';
	$_GET['tab'] = 'Pack!';
	$acme = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => 1));
	});
	$GLOBALS['ATOLL_SITE'] = 'beta';
	$GLOBALS['ATOLL_BRAND']['beta'] = array(
		'brand_name' => 'Beta Demo',
		'industry' => 'electronics',
		'market' => 'PK',
		'country' => 'PK',
		'handle' => '@beta',
	);
	$_GET['tab'] = 'ai';
	$beta = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => 1));
	});
	$GLOBALS['ATOLL_SITE'] = 'acme_parts';
	$_GET = array('sub' => 'guide');
	$embed = atoll_capture(static function () {
		epc_social_media_render_hub(array('embed_tenant_hub' => 1));
	});
	$_SERVER['REQUEST_METHOD'] = 'POST';
	$_POST['epc_social_action'] = 'save_account';
	$_POST['platform'] = 'instagram';
	$GLOBALS['ATOLL_CSRF_OK'] = 0;
	$_GET = array();
	$csrf = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => 1));
	});
	$GLOBALS['ATOLL_CSRF_OK'] = 1;
	$okPost = atoll_capture(static function () {
		epc_social_media_render_hub(array('is_super' => 1));
	});
	return array($acme, $beta, $embed, $csrf, $okPost);
}

function atoll_run_tabs(): array
{
	atoll_include();
	atoll_reset();
	$brand = array('brand_name' => 'Acme Parts', 'industry' => 'auto_parts', 'country' => 'AE', 'handle' => '@acme', 'market' => 'UAE');
	$pack = atoll_capture(static function () use ($brand) {
		epc_social_render_pack_tab($brand);
	});
	$tt = atoll_capture(static function () use ($brand) {
		epc_social_render_tiktok_tab($brand);
	});
	$ig = atoll_capture(static function () use ($brand) {
		epc_social_render_instagram_tab($brand);
	});
	$ai = atoll_capture(static function () use ($brand) {
		epc_social_render_ai_tab($brand, array(array('name' => 'Reel', 'platforms' => 'IG/TT', 'tip' => 'Keep it under 15s')), array('Same-day delivery'), 'tok-1');
	});
	$emptyDrafts = atoll_capture(static function () use ($brand) {
		epc_social_render_drafts_tab($brand, array(), 'tok-1');
	});
	$drafts = atoll_capture(static function () use ($brand) {
		epc_social_render_drafts_tab($brand, array(
			array(
				'id' => 9,
				'title' => "O'Reilly drop",
				'platform' => 'instagram',
				'status' => 'draft',
				'updated_at' => 1700000000,
				'caption' => 'Buy now',
				'media_url' => 'https://cdn.test/very-long-media-name-that-should-clip-after-sixty-chars.mp4',
			),
			array(
				'id' => 8,
				'title' => 'Done',
				'platform' => 'facebook',
				'status' => 'published',
				'updated_at' => 1700000000,
				'caption' => 'Live',
				'external_post_id' => 'fb-1',
			),
		), 'tok-1');
	});
	$guide = atoll_capture(static function () use ($brand) {
		epc_social_render_guide_tab($brand, '/cp/control/portal/epc_integrations_hub', '/cp/control/portal/epc_social_media_hub?tab=guide');
	});
	$accounts = atoll_capture(static function () use ($brand) {
		$GLOBALS['ATOLL_PDO'] = atoll_pdo();
		$GLOBALS['db_link'] = $GLOBALS['ATOLL_PDO'];
		epc_social_render_accounts_tab($brand, 'acme_parts', array(
			'instagram' => array('status' => 'verified', 'account_label' => "O'Reilly IG", 'username' => '@acme', 'last_test_at' => 1700000000),
		), '/cp/control/portal/epc_integrations_hub', 'tok-1');
	});
	return array($pack, $tt, $ig, $ai, $emptyDrafts, $drafts, $guide, $accounts);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_atoll_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	atoll_patch($root . '/cp/content/control/portal/epc_social_media_hub_panel.php', $tmp . '/page.php');
	$GLOBALS['ATOLL_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	atoll_reset();
	$fn = 'atoll_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1atoll_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1atoll_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";

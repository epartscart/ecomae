<?php
/**
 * Social Media Hub — shared panel (Super CP + Tenant CP + Tenant hub tab).
 * eval()-safe: parent loads via DOCUMENT_ROOT (never __DIR__ in CP eval context).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/social_media/epc_social_media_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/social_media/epc_social_media_pack_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_social_media_explore_hint_html(): string
{
	return '<div class="epc-social-explore-hint" role="status">'
		. '<span class="epc-social-explore-hint__icon" aria-hidden="true"><i class="fa fa-hand-o-down"></i></span>'
		. '<span class="epc-social-explore-hint__text">Click the tabs below — explore your marketing pack, video templates &amp; AI advisor</span>'
		. '</div>';
}

function epc_social_media_render_hub(array $opts = array()): void
{
	global $db_link, $DP_Config;

	$isSuper = !empty($opts['is_super']) || (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host());
	$embedInTenantHub = !empty($opts['embed_tenant_hub']);
	$tenantPdo = ($db_link instanceof PDO) ? $db_link : null;
	$platformPdo = epc_social_pdo($tenantPdo);

	if ($isSuper) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		if (!DP_User::isAdmin()) {
			echo '<div class="alert alert-warning">Please log in to Super CP to use Social media hub.</div>';
			return;
		}
	} else {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		if (!DP_User::isAdmin()) {
			echo '<div class="alert alert-warning">Admin login required.</div>';
			return;
		}
	}

	if (!$platformPdo instanceof PDO) {
		echo '<div class="alert alert-danger">Database unavailable.</div>';
		return;
	}

	epc_social_ensure_schema($platformPdo);
	$siteKey = epc_social_resolve_site_key($platformPdo);
	$brand = epc_social_brand_context($siteKey, $platformPdo);
	$tab = preg_replace('/[^a-z_]/', '', strtolower((string) ($_GET['tab'] ?? ($opts['tab'] ?? 'pack'))));
	if ($tab === '') {
		$tab = 'pack';
	}

	$accounts = epc_social_list_accounts($platformPdo, $siteKey);
	$drafts = epc_social_list_drafts($platformPdo, $siteKey);
	$trends = epc_social_trending_formats();
	$hooks = epc_social_industry_hooks((string) $brand['industry'], $brand);
	$backend = epc_social_backend();
	$hubBase = epc_social_hub_url('', $isSuper ? $siteKey : null);
	$csrf = epc_social_csrf_token();
	$integrationsUrl = '/' . $backend . '/control/portal/epc_integrations_hub';
	$guideUrl = epc_social_hub_url('guide', $isSuper ? $siteKey : null);

	$flash = null;
	if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_social_action'])) {
		if (!epc_social_verify_csrf()) {
			$flash = array('ok' => false, 'message' => 'CSRF validation failed. Refresh and try again.');
		} else {
			$action = (string) $_POST['epc_social_action'];
			if ($action === 'save_account') {
				$flash = epc_social_save_account($platformPdo, $siteKey, $_POST);
				$accounts = epc_social_list_accounts($platformPdo, $siteKey);
			} elseif ($action === 'test_account') {
				$flash = epc_social_test_account($platformPdo, $siteKey, (string) ($_POST['platform'] ?? ''));
				$accounts = epc_social_list_accounts($platformPdo, $siteKey);
			} elseif ($action === 'delete_account') {
				$flash = epc_social_delete_account($platformPdo, $siteKey, (string) ($_POST['platform'] ?? ''));
				$accounts = epc_social_list_accounts($platformPdo, $siteKey);
			} elseif ($action === 'save_draft') {
				$flash = epc_social_save_draft($platformPdo, $siteKey, $_POST);
				$drafts = epc_social_list_drafts($platformPdo, $siteKey);
			}
		}
	}

	$accountMap = array();
	foreach ($accounts as $acc) {
		$accountMap[(string) $acc['platform']] = $acc;
	}

	if (!$embedInTenantHub) {
		epc_cp_page_frame_open(array(
			'class' => 'epc-social-hub',
			'hero' => array(
				'badge' => $isSuper ? 'Super CP · Social Marketing' : 'Social media hub',
				'title' => $brand['brand_name'] . ' — Social Media Marketing',
				'sub' => 'Ready-to-post pack, TikTok & Instagram templates, encrypted account vault, and AI caption advisor for ' . epc_social_h($brand['market']) . '.',
				'actions' => array(
					array('label' => 'Guide', 'icon' => 'fa-book', 'url' => $guideUrl, 'primary' => true),
					array('label' => 'Integrations', 'icon' => 'fa-plug', 'url' => $integrationsUrl),
				),
			),
		));
		echo epc_social_media_explore_hint_html();
	} else {
		echo '<div class="epc-social-hub">';
		echo '<div class="epc-th-hero epc-social-hub__hero-tenant" style="margin-bottom:16px"><span class="epc-th-hero__badge">Social media</span>';
		echo '<h4><i class="fa fa-share-alt"></i> Marketing hub — ' . epc_social_h($brand['brand_name']) . '</h4>';
		echo '<p class="epc-th-hero__sub">Content pack, video templates, connected accounts, and AI advisor. ';
		echo '<a href="' . epc_social_h(epc_social_hub_url('', 'platform')) . '">Open full hub</a></p></div>';
		echo epc_social_media_explore_hint_html();
	}

	if ($flash !== null) {
		echo '<div class="alert alert-' . (!empty($flash['ok']) ? 'success' : 'danger') . '">' . epc_social_h($flash['message'] ?? '') . '</div>';
	}

	if ($isSuper && !$embedInTenantHub) {
		echo '<div class="alert alert-info"><i class="fa fa-cloud"></i> Platform scope: <strong>' . epc_social_h($siteKey) . '</strong>. ';
		echo 'Tenant credentials are isolated per <code>site_key</code>. Switch: ';
		$tenants = function_exists('epc_portal_list_tenants') ? epc_portal_list_tenants($platformPdo) : array();
		echo '<a href="' . epc_social_h(epc_social_hub_url($tab, 'platform')) . '">platform</a>';
		foreach (array_slice($tenants, 0, 8) as $t) {
			$sk = (string) ($t['site_key'] ?? '');
			if ($sk === '') {
				continue;
			}
			echo ' · <a href="' . epc_social_h(epc_social_hub_url($tab, $sk)) . '">' . epc_social_h($sk) . '</a>';
		}
		echo '</div>';
	}

	$tabs = array(
		'pack' => array('label' => 'Marketing pack', 'icon' => 'fa-file-text-o'),
		'tiktok' => array('label' => 'TikTok', 'icon' => 'fa-music'),
		'instagram' => array('label' => 'Instagram', 'icon' => 'fa-instagram'),
		'accounts' => array('label' => 'Connected accounts', 'icon' => 'fa-link'),
		'ai' => array('label' => 'AI advisor', 'icon' => 'fa-magic'),
		'drafts' => array('label' => 'Drafts', 'icon' => 'fa-pencil'),
		'guide' => array('label' => 'Guide', 'icon' => 'fa-book'),
	);

	echo '<div class="epc-social-tabs">';
	foreach ($tabs as $tk => $meta) {
		$url = $embedInTenantHub
			? epc_social_tenant_hub_url('social') . '&amp;sub=' . rawurlencode($tk)
			: epc_social_hub_url($tk, ($isSuper && $siteKey !== 'platform') ? $siteKey : null);
		$active = ($tab === $tk) ? ' btn-primary' : ' btn-default';
		echo '<a class="btn btn-sm' . $active . '" href="' . $url . '"><i class="fa ' . epc_social_h($meta['icon']) . '"></i> ' . epc_social_h($meta['label']) . '</a>';
	}
	echo '</div>';

	if ($embedInTenantHub && isset($_GET['sub'])) {
		$tab = preg_replace('/[^a-z_]/', '', strtolower((string) $_GET['sub']));
	}

	// KPI row
	echo '<div class="epc-social-kpi">';
	$readyPosts = count(epc_social_pack_posts('linkedin'))
		+ count(epc_social_pack_posts('instagram'))
		+ count(epc_social_pack_posts('facebook'))
		+ count(epc_social_pack_posts('x'));
	echo '<div class="epc-social-kpi__item"><div class="epc-social-kpi__val">' . $readyPosts . '</div><div class="epc-social-kpi__label">Ready posts</div></div>';
	echo '<div class="epc-social-kpi__item"><div class="epc-social-kpi__val">' . count($accounts) . '</div><div class="epc-social-kpi__label">Connected</div></div>';
	echo '<div class="epc-social-kpi__item"><div class="epc-social-kpi__val">' . count($drafts) . '</div><div class="epc-social-kpi__label">Drafts</div></div>';
	echo '<div class="epc-social-kpi__item"><div class="epc-social-kpi__val">' . epc_social_h($brand['industry']) . '</div><div class="epc-social-kpi__label">Industry</div></div>';
	echo '</div>';

	if ($tab === 'pack') {
		epc_social_render_pack_tab($brand);
	} elseif ($tab === 'tiktok') {
		epc_social_render_tiktok_tab($brand);
	} elseif ($tab === 'instagram') {
		epc_social_render_instagram_tab($brand);
	} elseif ($tab === 'accounts') {
		epc_social_render_accounts_tab($brand, $siteKey, $accountMap, $integrationsUrl, $csrf);
	} elseif ($tab === 'ai') {
		epc_social_render_ai_tab($brand, $trends, $hooks, $csrf);
	} elseif ($tab === 'drafts') {
		epc_social_render_drafts_tab($brand, $drafts, $csrf);
	} else {
		epc_social_render_guide_tab($brand, $integrationsUrl, $guideUrl);
	}

	if (!$embedInTenantHub) {
		epc_cp_page_frame_close();
	} else {
		echo '</div>';
	}
}

function epc_social_render_pack_tab(array $brand): void
{
	$platforms = array('linkedin', 'instagram', 'facebook', 'x');
	foreach ($platforms as $plat) {
		$meta = epc_social_pack_platforms()[$plat];
		$posts = epc_social_pack_posts_for_brand($plat, $brand);
		echo '<div class="panel panel-default"><div class="panel-heading"><strong><i class="fa fa-share-alt"></i> ' . epc_social_h($meta['label']) . '</strong></div>';
		echo '<div class="panel-body"><p class="text-muted">' . epc_social_h($meta['intro']) . '</p>';
		echo '<div class="epc-social-grid">';
		foreach ($posts as $i => $post) {
			$caption = (string) $post['caption'];
			echo '<div class="epc-social-post">';
			echo '<div class="epc-social-post__head">' . epc_social_h($post['title']) . '</div>';
			echo '<div class="epc-social-post__body">' . epc_social_h($caption) . '</div>';
			echo '<div class="epc-social-post__bar"><span class="text-muted small">' . epc_social_h($meta['label']) . '</span>';
			echo '<button type="button" class="btn btn-xs btn-primary epc-social-copy" data-caption="' . epc_social_h($caption) . '"><i class="fa fa-copy"></i> Copy</button></div>';
			echo '</div>';
		}
		echo '</div>';
		if (!empty($meta['hashtags'])) {
			echo '<div style="margin-top:14px"><strong class="small">Hashtag bank</strong><br>';
			foreach ($meta['hashtags'] as $tag) {
				$tag = epc_social_adapt_text($tag, $brand);
				echo '<span class="epc-social-tag epc-social-copy" data-caption="' . epc_social_h($tag) . '">' . epc_social_h($tag) . '</span>';
			}
			echo '</div>';
		}
		echo '</div></div>';
	}
	$thread = epc_social_adapt_text(epc_social_x_thread_starter(), $brand);
	echo '<div class="panel panel-default"><div class="panel-heading"><strong>X thread starter</strong></div><div class="panel-body">';
	echo '<pre style="white-space:pre-line;font-size:12px;background:#f8fafc;padding:12px;border-radius:8px">' . epc_social_h($thread) . '</pre>';
	echo '<button type="button" class="btn btn-sm btn-primary epc-social-copy" data-caption="' . epc_social_h($thread) . '"><i class="fa fa-copy"></i> Copy thread</button>';
	echo '</div></div>';
}

function epc_social_render_tiktok_tab(array $brand): void
{
	$meta = epc_social_pack_platforms()['tiktok'];
	$posts = epc_social_pack_posts_for_brand('tiktok', $brand);
	$specs = epc_social_tiktok_specs();
	echo '<div class="epc-social-intro"><strong>' . epc_social_h($meta['label']) . '</strong> — ' . epc_social_h($meta['intro']) . '</div>';
	echo '<div class="row"><div class="col-md-4"><div class="panel panel-default"><div class="panel-heading"><strong>Video specs</strong></div><div class="panel-body"><dl class="epc-social-specs">';
	foreach ($specs as $k => $v) {
		echo '<dt>' . epc_social_h($k) . '</dt><dd>' . epc_social_h($v) . '</dd>';
	}
	echo '</dl></div></div>';
	echo '<div class="panel panel-default"><div class="panel-heading"><strong>Upload / link</strong></div><div class="panel-body">';
	echo '<p class="text-muted small">Paste a draft video URL (Google Drive, Dropbox, or CDN) when saving a draft. Direct upload to TikTok requires API keys — connect under <em>Connected accounts</em>.</p>';
	echo '<input type="url" class="form-control input-sm" id="epc_social_video_url" placeholder="https://…/your-reel.mp4">';
	echo '</div></div></div>';
	echo '<div class="col-md-8"><div class="epc-social-grid">';
	foreach ($posts as $post) {
		$caption = (string) $post['caption'];
		echo '<div class="epc-social-post"><div class="epc-social-post__head">' . epc_social_h($post['title']) . '</div>';
		echo '<div class="epc-social-post__body">' . epc_social_h($caption) . '</div>';
		echo '<div class="epc-social-post__bar"><button type="button" class="btn btn-xs btn-primary epc-social-copy" data-caption="' . epc_social_h($caption) . '"><i class="fa fa-copy"></i> Copy caption</button>';
		echo '<button type="button" class="btn btn-xs btn-default epc-social-save-draft" data-platform="tiktok" data-title="' . epc_social_h($post['title']) . '" data-caption="' . epc_social_h($caption) . '"><i class="fa fa-save"></i> Save draft</button></div></div>';
	}
	echo '</div></div></div>';
}

function epc_social_render_instagram_tab(array $brand): void
{
	$meta = epc_social_pack_platforms()['instagram'];
	$posts = epc_social_pack_posts_for_brand('instagram', $brand);
	$reels = epc_social_instagram_reels_ideas();
	echo '<div class="epc-social-intro"><strong>Instagram</strong> — Posts, Reels &amp; carousels for ' . epc_social_h($brand['brand_name']) . '.</div>';
	echo '<div class="epc-social-grid">';
	foreach ($posts as $post) {
		$caption = (string) $post['caption'];
		echo '<div class="epc-social-post"><div class="epc-social-post__head">' . epc_social_h($post['title']) . '</div>';
		echo '<div class="epc-social-post__body">' . epc_social_h($caption) . '</div>';
		echo '<div class="epc-social-post__bar"><button type="button" class="btn btn-xs btn-primary epc-social-copy" data-caption="' . epc_social_h($caption) . '"><i class="fa fa-copy"></i> Copy</button></div></div>';
	}
	echo '</div>';
	echo '<div class="panel panel-default" style="margin-top:18px"><div class="panel-heading"><strong>Reels &amp; carousel ideas</strong></div><div class="panel-body"><div class="epc-social-grid">';
	foreach ($reels as $idea) {
		$cap = epc_social_adapt_text((string) $idea['caption'], $brand);
		echo '<div class="epc-social-post"><div class="epc-social-post__head">' . epc_social_h($idea['title']) . '</div>';
		echo '<div class="epc-social-post__body">' . epc_social_h($cap) . '</div>';
		echo '<div class="epc-social-post__bar"><button type="button" class="btn btn-xs btn-primary epc-social-copy" data-caption="' . epc_social_h($cap) . '"><i class="fa fa-copy"></i> Copy</button></div></div>';
	}
	echo '</div></div></div>';
}

function epc_social_render_accounts_tab(array $brand, string $siteKey, array $accountMap, string $integrationsUrl, string $csrf): void
{
	echo '<div class="alert alert-warning"><i class="fa fa-lock"></i> <strong>Secure vault</strong> — passwords and tokens are AES-256 encrypted per tenant. Never shown after save. Full OAuth flows: add API keys in <a href="' . epc_social_h($integrationsUrl) . '">Integrations hub</a>.</div>';
	foreach (epc_social_platforms() as $key => $plat) {
		$existing = $accountMap[$key] ?? null;
		$status = $existing ? (string) ($existing['status'] ?? 'pending') : 'not_connected';
		echo '<div class="epc-social-account-card">';
		echo '<div class="epc-social-account-card__head">';
		echo '<span class="epc-social-platform-icon" style="background:' . epc_social_h($plat['color']) . '"><i class="fa ' . epc_social_h($plat['icon']) . '"></i></span>';
		echo '<div><strong>' . epc_social_h($plat['label']) . '</strong><br><span class="label label-' . ($status === 'verified' ? 'success' : 'default') . '">' . epc_social_h($status) . '</span></div>';
		if ($existing && !empty($existing['last_test_at'])) {
			echo '<span class="text-muted small pull-right">Last test: ' . epc_social_h(date('Y-m-d H:i', (int) $existing['last_test_at'])) . '</span>';
		}
		echo '</div>';
		echo '<form method="post" class="form-horizontal">';
		echo '<input type="hidden" name="csrf_token" value="' . epc_social_h($csrf) . '">';
		echo '<input type="hidden" name="epc_social_action" value="save_account">';
		echo '<input type="hidden" name="platform" value="' . epc_social_h($key) . '">';
		echo '<div class="row"><div class="col-md-3 form-group"><label class="control-label">Account label</label>';
		echo '<input class="form-control input-sm" name="account_label" value="' . epc_social_h($existing['account_label'] ?? $brand['brand_name']) . '"></div>';
		echo '<div class="col-md-3 form-group"><label class="control-label">Username / page</label>';
		echo '<input class="form-control input-sm" name="username" value="' . epc_social_h($existing['username'] ?? $brand['handle']) . '" autocomplete="off"></div>';
		echo '<div class="col-md-3 form-group"><label class="control-label">Access token</label>';
		echo '<input class="form-control input-sm" name="access_token" type="password" placeholder="' . ($existing ? '•••••••• (unchanged if empty)' : 'Paste token') . '" autocomplete="new-password"></div>';
		echo '<div class="col-md-3 form-group"><label class="control-label">API key / App ID</label>';
		echo '<input class="form-control input-sm" name="api_key" type="password" placeholder="Optional" autocomplete="new-password"></div></div>';
		echo '<div class="row"><div class="col-md-3 form-group"><label class="control-label">API secret</label>';
		echo '<input class="form-control input-sm" name="api_secret" type="password" placeholder="Optional" autocomplete="new-password"></div>';
		echo '<div class="col-md-3 form-group"><label class="control-label">Page / Business ID</label>';
		echo '<input class="form-control input-sm" name="page_id" autocomplete="off"></div>';
		echo '<div class="col-md-6 form-group" style="padding-top:24px">';
		echo '<button type="submit" class="btn btn-sm btn-primary"><i class="fa fa-save"></i> Save securely</button>';
		echo '</div></div></form>';
		echo '<form method="post" style="display:inline"><input type="hidden" name="csrf_token" value="' . epc_social_h($csrf) . '">';
		echo '<input type="hidden" name="epc_social_action" value="test_account"><input type="hidden" name="platform" value="' . epc_social_h($key) . '">';
		echo '<button type="submit" class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Test connection</button></form> ';
		if ($existing) {
			echo '<form method="post" style="display:inline" onsubmit="return confirm(\'Remove credentials?\')"><input type="hidden" name="csrf_token" value="' . epc_social_h($csrf) . '">';
			echo '<input type="hidden" name="epc_social_action" value="delete_account"><input type="hidden" name="platform" value="' . epc_social_h($key) . '">';
			echo '<button type="submit" class="btn btn-sm btn-danger"><i class="fa fa-trash"></i></button></form>';
		}
		echo '</div></div>';
	}
}

function epc_social_render_ai_tab(array $brand, array $trends, array $hooks, string $csrf): void
{
	echo '<div class="panel panel-default"><div class="panel-heading"><strong><i class="fa fa-line-chart"></i> Trending formats this week</strong></div><div class="panel-body">';
	foreach ($trends as $t) {
		echo '<div class="epc-social-ai-card"><strong>' . epc_social_h($t['name']) . '</strong> <span class="text-muted">(' . epc_social_h($t['platforms']) . ')</span><br>' . epc_social_h($t['tip']) . '</div>';
	}
	echo '</div></div>';
	echo '<div class="panel panel-default"><div class="panel-heading"><strong>Industry hooks — ' . epc_social_h($brand['industry']) . ' (' . epc_social_h($brand['country']) . ')</strong></div><div class="panel-body"><ul>';
	foreach ($hooks as $hook) {
		echo '<li>' . epc_social_h($hook) . '</li>';
	}
	echo '</ul></div></div>';
	echo '<div class="panel panel-default"><div class="panel-heading"><strong>Generate caption</strong></div><div class="panel-body">';
	echo '<div class="row"><div class="col-md-3"><select class="form-control" id="epc_social_gen_platform"><option value="instagram">Instagram</option><option value="tiktok">TikTok</option><option value="linkedin">LinkedIn</option><option value="facebook">Facebook</option><option value="x">X</option></select></div>';
	echo '<div class="col-md-5"><input class="form-control" id="epc_social_gen_product" placeholder="Product line or promo (optional)"></div>';
	echo '<div class="col-md-4"><button type="button" class="btn btn-primary" id="epc_social_gen_btn"><i class="fa fa-magic"></i> Generate</button></div></div>';
	echo '<div id="epc_social_gen_result" style="margin-top:14px;display:none"><pre class="epc-social-post__body" style="max-height:none;background:#f8fafc;padding:12px;border-radius:8px" id="epc_social_gen_caption"></pre>';
	echo '<p id="epc_social_gen_tags" class="text-muted"></p>';
	echo '<button type="button" class="btn btn-sm btn-primary epc-social-copy" id="epc_social_gen_copy"><i class="fa fa-copy"></i> Copy</button></div>';
	echo '<p class="text-muted small" style="margin-top:10px">AI advisor uses industry + country rules. Connect LLM agent in Integrations for richer suggestions.</p>';
	echo '</div></div>';
}

function epc_social_render_drafts_tab(array $brand, array $drafts, string $csrf): void
{
	echo '<div class="panel panel-default"><div class="panel-heading"><strong>Saved drafts</strong></div><div class="panel-body">';
	if (count($drafts) === 0) {
		echo '<p class="text-muted">No drafts yet. Copy a caption from Marketing pack or TikTok and click <em>Save draft</em>.</p>';
	}
	echo '<table class="table table-striped"><thead><tr><th>Title</th><th>Platform</th><th>Updated</th><th></th></tr></thead><tbody>';
	foreach ($drafts as $d) {
		echo '<tr><td>' . epc_social_h($d['title']) . '</td><td>' . epc_social_h($d['platform']) . '</td><td>' . epc_social_h(date('Y-m-d H:i', (int) $d['updated_at'])) . '</td>';
		echo '<td><button type="button" class="btn btn-xs btn-default epc-social-copy" data-caption="' . epc_social_h($d['caption'] ?? '') . '"><i class="fa fa-copy"></i></button></td></tr>';
	}
	echo '</tbody></table></div></div>';
}

function epc_social_render_guide_tab(array $brand, string $integrationsUrl, string $guideUrl): void
{
	$steps = array(
		array('title' => 'Connect accounts', 'body' => 'Open <strong>Connected accounts</strong>. Save username + API token (encrypted). Use <em>Test connection</em> to verify vault storage. For Meta/TikTok OAuth apps, register keys in <a href="' . epc_social_h($integrationsUrl) . '">Integrations hub</a>.'),
		array('title' => 'Pick content from pack', 'body' => 'Marketing pack includes 16 ECOM AE posts (LinkedIn, Instagram, Facebook, X). Tenant CP auto-adapts brand name, domain, and hashtags for ' . epc_social_h($brand['brand_name']) . '.'),
		array('title' => 'TikTok & Instagram video', 'body' => 'Use TikTok tab for 9:16 specs and caption scripts. Instagram tab adds Reels/carousel storyboards. Paste video URL into draft — schedule manually until API posting is enabled.'),
		array('title' => 'AI advisor', 'body' => 'Check weekly trending formats and industry hooks. Generate captions for your product line. GCC: mix English + Arabic hashtags. Pakistan: Urdu/English captions perform well on Facebook.'),
		array('title' => 'Post & measure', 'body' => 'Copy → native app or Meta Business Suite. Track link-in-bio clicks. Super CP operators manage platform + per-tenant accounts; tenants only see their own <code>site_key</code> credentials.'),
	);
	echo '<div class="alert alert-info"><i class="fa fa-book"></i> Guide URL: <a href="' . epc_social_h($guideUrl) . '"><code>' . epc_social_h($guideUrl) . '</code></a></div>';
	foreach ($steps as $i => $step) {
		echo '<div class="epc-social-guide-step"><h5 style="margin:0 0 6px">Step ' . ($i + 1) . ' — ' . epc_social_h($step['title']) . '</h5><div>' . $step['body'] . '</div></div>';
	}
	echo '<div class="panel panel-default"><div class="panel-heading"><strong>GCC &amp; Pakistan best practices</strong></div><div class="panel-body"><ul>';
	echo '<li><strong>UAE/GCC:</strong> Post Sun–Thu 10am–1pm GST; compliance content (VAT, e-invoice) builds B2B trust.</li>';
	echo '<li><strong>Pakistan:</strong> Facebook + WhatsApp status repurposing; Urdu captions for retail; English for B2B LinkedIn.</li>';
	echo '<li><strong>All markets:</strong> Never post raw credentials; use CP vault only. Enable 2FA on social accounts.</li>';
	echo '</ul></div></div>';
}

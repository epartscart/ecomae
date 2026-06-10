<?php
/**
 * CP catalogue/products — industry product line tree (Auto Price AI taxonomy).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_industry_taxonomy.php';
require_once __DIR__ . '/epc_auto_price_categories.php';

function epc_apai_cp_resolve_site_key(): string
{
	if (function_exists('epc_apai_resolve_storefront_site_key')) {
		require_once __DIR__ . '/epc_auto_price_storefront.php';
		$sk = epc_apai_resolve_storefront_site_key();
		if ($sk !== '') {
			return $sk;
		}
	}
	$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	foreach (array_keys(epc_apai_tenant_industry_map()) as $tenantKey) {
		if (strpos($host, $tenantKey) !== false) {
			return $tenantKey;
		}
	}
	if (function_exists('epc_portal_site_key_from_hostname') && $host !== '') {
		require_once __DIR__ . '/../../general_pages/epc_portal_tenant_intro.php';
		$sk = epc_portal_site_key_from_hostname($host);
		if ($sk !== '') {
			return $sk;
		}
	}
	return '';
}

/**
 * @return array{site_key:string,industry_key:string,industry_label:string,root_category_id:int,category_map_count:int,tree:array,flat:array,enabled:bool}
 */
function epc_apai_cp_catalogue_filter_ctx(PDO $pdo, int $currentCategoryId = 0): array
{
	$siteKey = epc_apai_cp_resolve_site_key();
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$profiles = epc_apai_industry_profiles();
	$industryLabel = (string) (($profiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))));

	$enabled = ($siteKey !== 'epartscart');
	$rootCategoryId = 0;
	$categoryMapCount = 0;
	$tree = array();
	$flat = array();

	if ($enabled) {
		epc_apai_category_map_schema($pdo);
		$categoryMapCount = epc_apai_category_count($pdo, $siteKey, $industryKey);
		$rootAlias = epc_apai_category_slug($industryKey, 'apai-root');
		$rootChk = $pdo->prepare('SELECT `id` FROM `shop_catalogue_categories` WHERE `alias` = ? AND `parent` = 0 LIMIT 1');
		$rootChk->execute(array($rootAlias));
		$rootCategoryId = (int) $rootChk->fetchColumn();

		$stmt = $pdo->prepare(
			'SELECT m.`category_id`, m.`taxonomy_node_id`, c.`parent`, c.`value`, c.`url`, c.`count`, c.`level`, c.`alias`,
			        n.`slug`, n.`name_en`, n.`sort`
			 FROM `epc_taxonomy_category_map` m
			 INNER JOIN `shop_catalogue_categories` c ON c.`id` = m.`category_id`
			 LEFT JOIN `epc_product_taxonomy_nodes` n ON n.`id` = m.`taxonomy_node_id`
			 WHERE m.`site_key` = ? AND m.`industry_key` = ?
			 ORDER BY c.`level`, COALESCE(n.`sort`, 100), c.`order`, c.`value`'
		);
		$stmt->execute(array($siteKey, $industryKey));
		$flat = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

		$byParent = array();
		foreach ($flat as $row) {
			$pid = (int) ($row['parent'] ?? 0);
			if (!isset($byParent[$pid])) {
				$byParent[$pid] = array();
			}
			$byParent[$pid][] = $row;
		}
		$build = function (int $parentId) use (&$build, $byParent): array {
			$nodes = array();
			foreach ((array) ($byParent[$parentId] ?? array()) as $row) {
				$cid = (int) ($row['category_id'] ?? 0);
				$row['children'] = $build($cid);
				$nodes[] = $row;
			}
			return $nodes;
		};
		$tree = $rootCategoryId > 0 ? $build($rootCategoryId) : $build(0);
	}

	return array(
		'site_key' => $siteKey,
		'industry_key' => $industryKey,
		'industry_label' => $industryLabel,
		'root_category_id' => $rootCategoryId,
		'category_map_count' => $categoryMapCount,
		'current_category_id' => $currentCategoryId,
		'tree' => $tree,
		'flat' => $flat,
		'enabled' => $enabled,
	);
}

function epc_apai_cp_catalogue_filter_render(array $ctx, string $backendDir): void
{
	if (empty($ctx['enabled'])) {
		return;
	}
	global $DP_Config;
	$backend = trim($backendDir, '/');
	$base = '/' . $backend . '/shop/catalogue/products';
	$industryLabel = htmlspecialchars((string) ($ctx['industry_label'] ?? 'Industry'), ENT_QUOTES, 'UTF-8');
	$mapCount = (int) ($ctx['category_map_count'] ?? 0);
	$rootId = (int) ($ctx['root_category_id'] ?? 0);
	$currentId = (int) ($ctx['current_category_id'] ?? 0);

	$renderNodes = function (array $nodes, int $depth = 0) use (&$renderNodes, $base, $currentId) {
		foreach ($nodes as $n) {
			$cid = (int) ($n['category_id'] ?? 0);
			$name = htmlspecialchars((string) ($n['name_en'] ?? $n['value'] ?? ''), ENT_QUOTES, 'UTF-8');
			$isLeaf = ((int) ($n['count'] ?? 0)) === 0;
			$active = ($cid === $currentId) ? ' epc-apai-cat-tree__link--active' : '';
			$pad = 8 + ($depth * 14);
			$icon = $isLeaf ? 'fa-tag' : 'fa-folder-open';
			echo '<li style="padding-left:' . $pad . 'px"><a class="epc-apai-cat-tree__link' . $active . '" href="' . $base . '?category_id=' . $cid . '">';
			echo '<i class="fa ' . $icon . '" aria-hidden="true"></i> ' . $name . '</a></li>';
			if (!empty($n['children'])) {
				$renderNodes($n['children'], $depth + 1);
			}
		}
	};
	?>
	<div class="row epc-apai-cat-row">
	<style>
	.epc-apai-cat-banner{margin:0 0 12px;padding:10px 14px;background:#f0f7ff;border:1px solid #c5dff8;border-radius:4px;font-size:13px}
	.epc-apai-cat-tree{list-style:none;margin:0;padding:0;max-height:520px;overflow:auto;font-size:13px}
	.epc-apai-cat-tree__link{display:block;padding:4px 6px;color:#333;border-radius:3px;text-decoration:none}
	.epc-apai-cat-tree__link:hover,.epc-apai-cat-tree__link--active{background:#e8f4fc;color:#1a6fa8}
	.epc-apai-cat-tree__link--active{font-weight:600}
	</style>
	<div class="col-lg-3 col-md-4 hidden-sm hidden-xs">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<i class="fa fa-sitemap"></i> <?php echo $industryLabel; ?> product lines
			</div>
			<div class="panel-body">
				<div class="epc-apai-cat-banner">
					Product lines synced from <strong>Auto Price AI</strong> taxonomy
					(<?php echo $mapCount; ?> categories).
					<?php if ($rootId > 0) { ?>
					<br><a href="<?php echo htmlspecialchars($base . '?category_id=' . $rootId, ENT_QUOTES, 'UTF-8'); ?>">Browse all product lines</a>
					<?php } ?>
				</div>
				<ul class="epc-apai-cat-tree">
					<?php
					if ($rootId > 0) {
						$rootActive = ($currentId === $rootId) ? ' epc-apai-cat-tree__link--active' : '';
						echo '<li><a class="epc-apai-cat-tree__link' . $rootActive . '" href="' . $base . '?category_id=' . $rootId . '"><i class="fa fa-industry"></i> All ' . $industryLabel . '</a></li>';
					}
					$renderNodes((array) ($ctx['tree'] ?? array()));
					?>
				</ul>
			</div>
		</div>
	</div>
	<div class="col-lg-9 col-md-8 col-sm-12 col-xs-12 epc-apai-cat-main">
		<div class="hpanel" style="margin-bottom:8px">
			<div class="panel-body" style="padding:8px 12px">
				<form method="get" action="<?php echo htmlspecialchars($base, ENT_QUOTES, 'UTF-8'); ?>" class="form-inline">
					<label class="control-label" style="margin-right:8px"><i class="fa fa-filter"></i> Product line</label>
					<select name="category_id" class="form-control input-sm" onchange="this.form.submit()" style="min-width:240px">
						<option value="0">— All catalogue categories —</option>
						<?php if ($rootId > 0) { ?>
						<option value="<?php echo $rootId; ?>"<?php echo $currentId === $rootId ? ' selected' : ''; ?>>[Root] <?php echo $industryLabel; ?></option>
						<?php }
						foreach ((array) ($ctx['flat'] ?? array()) as $row) {
							$cid = (int) ($row['category_id'] ?? 0);
							$lvl = max(0, (int) ($row['level'] ?? 1) - 2);
							$prefix = $lvl > 0 ? str_repeat('— ', $lvl) : '';
							$label = $prefix . (string) ($row['name_en'] ?? $row['value'] ?? '');
							echo '<option value="' . $cid . '"' . ($currentId === $cid ? ' selected' : '') . '>' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</option>';
						}
						?>
					</select>
				</form>
			</div>
		</div>
	<?php
}

function epc_apai_cp_catalogue_filter_close(): void
{
	echo '</div><!-- .epc-apai-cat-main --></div><!-- .epc-apai-cat-row -->';
}

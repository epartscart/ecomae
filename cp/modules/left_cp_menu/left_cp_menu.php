<?php
defined('_ASTEXE_') or die('No access');
/*
Скрипт модуля для левого меню панели управления.

Меню состоит из следующих частей:
- кнопка на главную страницу панели управления
- категории товаров каталога
- задачи панели управления
*/

// Top mega-menu is primary — skip left-rail build (ACL + DOM) for speed.
// Do NOT `return` here: module PHP is eval()'d inside the CP template
// (dp_core.php); a return aborts the whole shell (truncated HTML, no #wrapper).
if (!empty($GLOBALS['epc_cp_topnav_only'])) {
	echo '<!-- epc: left_cp_menu skipped (topnav-only) -->';
} else {

// Catalogue tree is expensive — only load on catalogue/stock CP pages (not every /cp request).
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/modules/left_cp_menu/catalogue_menu_helper.php");

//ДЛЯ ВЫВОДА ЗАДАЧ ПАНЕЛИ УПРАВЛЕНИЯ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_cp_menu.php");
$epcCpHomeUrl = function_exists('epc_cp_control_url') ? epc_cp_control_url() : ('/' . $DP_Config->backend_dir . '/control');
//Определение функции проверки доступа к странице
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/control_helper.php");




?>
<div class="epc-cp-menu-search" id="epc-cp-menu-search">
	<label class="sr-only" for="epc-cp-menu-q">Search menu</label>
	<input type="search" id="epc-cp-menu-q" placeholder="Search menu…" autocomplete="off" />
</div>
<ul class="nav metismenu" id="side-menu">
	<?php
	//1. Кнопка главной страницы панели управления.
	?>
	<li class="epc-cp-nav-item epc-cp-nav-item--home">
		<a href="<?php echo htmlspecialchars($epcCpHomeUrl, ENT_QUOTES, 'UTF-8'); ?>" class="epc-cp-nav-item__link">
			<span class="nav-label"><?php echo translate_str_by_id(3992); ?></span>
		</a>
	</li>
	
	
	
	
	<?php
	//ВЫВОД КАТЕГОРИЙ ТОВАРОВ - только для для страниц, связанных с каталогом: Редактирование каталога и Кладовщики
	if( isset($module_modes_map[(string)$DP_Content->url]) )
	{
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");
		?>
		<li>
			<a href="javascript:void(0);"><span class="nav-label"><?php echo translate_str_by_id(3994); ?></span><span class="fa arrow"></span> </a>
			<?php
			$catalogue_tree_dump_PHP = json_decode($catalogue_tree_dump_JSON, true);
			
			printCatalogueBranch($catalogue_tree_dump_PHP);
			?>
		</li>
		<?php
	}
	
	



/*ВЫВОД ЗАДАЧ**/
//Для работы с пользователями - для определения доступа к страницам
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Массив для блоков и страниц по блокам
$tabs = array();

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
if (function_exists('epc_cp_trace')) { epc_cp_trace('left_menu: before menu cache'); }
$epcCpMenuCache = ($db_link instanceof PDO) ? epc_cp_menu_cache($db_link) : array('groups' => array(), 'items' => array());
if (function_exists('epc_cp_trace')) { epc_cp_trace('left_menu: after menu cache groups=' . count((array) ($epcCpMenuCache['groups'] ?? array())) . ' items=' . count((array) ($epcCpMenuCache['items'] ?? array()))); }

//Получаем перечнь групп задач панели управления (cached 5 min):
foreach ((array) ($epcCpMenuCache['groups'] ?? array()) as $group)
{
    $tabs[(string)$group["id"]] = array(
		"caption" => translate_str_by_id($group["caption"]),
		"caption_key" => (string) $group["caption"],
		"items" => array(),
	);
}
if (function_exists('epc_cp_trace')) { epc_cp_trace('left_menu: after group captions'); }


// Resolve backend once, normalize URLs, then batch-preload ACL maps (avoids N+1 is_anable queries).
$epcCpNavBackend = function_exists('epc_cp_nav_url_prefix')
	? ltrim(epc_cp_nav_url_prefix(), '/')
	: (string) $DP_Config->backend_dir;
$epcCpMenuItems = array();
foreach ((array) ($epcCpMenuCache['items'] ?? array()) as $item) {
	$item['url'] = str_replace(array('<backend>'), $epcCpNavBackend, $item['url']);
	$epcCpMenuItems[] = $item;
}
if (function_exists('epc_cp_trace')) { epc_cp_trace('left_menu: before acl preload'); }
if (function_exists('epc_cp_acl_preload')) {
	epc_cp_acl_preload($epcCpMenuItems);
}
if (function_exists('epc_cp_trace')) { epc_cp_trace('left_menu: after acl preload'); }
$epcCpSuperHost = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$epcCpSuperAdmin = $epcCpSuperHost && DP_User::isAdmin();

//Получаем перечень всех задач (cached 5 min):
$__epcAclMs = 0.0;
$__epcVisMs = 0.0;
$__epcAclCalls = 0;
$__epcVisCalls = 0;
foreach ($epcCpMenuItems as $item)
{
	//Добавляем, если у пользователя есть доступ или пункт помечен show_anyway (works on Super CP + tenants).
	$showAnyway = (int) (isset($item['show_anyway']) ? $item['show_anyway'] : 0) === 1;
	$a0 = microtime(true);
	$aclOk = $epcCpSuperAdmin ? true : is_anable($item);
	$__epcAclMs += (microtime(true) - $a0) * 1000;
	$__epcAclCalls++;
	$mayShow = $aclOk || $showAnyway;
	if (!$mayShow) {
		continue;
	}
	$v0 = microtime(true);
	$visOk = epc_portal_cp_item_visible_enhanced($item);
	$__epcVisMs += (microtime(true) - $v0) * 1000;
	$__epcVisCalls++;
	if ($visOk)
	{
		$groupKey = (string) $item["items_group"];
		if (!isset($tabs[$groupKey]) || !is_array($tabs[$groupKey]["items"])) {
			continue;
		}
		array_push($tabs[$groupKey]["items"], $item);
	}
}
if (function_exists('epc_cp_trace')) {
	epc_cp_trace('left_menu: after acl+visibility filter acl_ms=' . (int) round($__epcAclMs) . ' acl_n=' . $__epcAclCalls . ' vis_ms=' . (int) round($__epcVisMs) . ' vis_n=' . $__epcVisCalls);
}



// Dedupe items, then tenant-friendly order: primary daily groups first, advanced after.
foreach ($tabs as $key => $tab) {
	$tab['items'] = epc_portal_cp_menu_dedupe_items($tab['items']);
	$tabs[$key] = $tab;
}

$epcPrimaryKeys = function_exists('epc_portal_cp_primary_group_keys')
	? epc_portal_cp_primary_group_keys()
	: array('744', 'epc_cp_group_customers', 'epc_cp_group_documents', 'epc_cp_group_erp');
$epcAdvancedKeys = function_exists('epc_portal_cp_advanced_group_keys')
	? epc_portal_cp_advanced_group_keys()
	: array();
$epcIsSuperHost = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();

$epcRenderNavGroup = static function ($key, $tab, $extraClass = '') {
	if (count($tab['items']) === 0) {
		return;
	}
	$captionKey = (string) ($tab['caption_key'] ?? '');
	$groupSubtitle = function_exists('epc_portal_cp_group_subtitle')
		? epc_portal_cp_group_subtitle($captionKey)
		: '';
	$groupLiClass = trim('epc-cp-nav-section ' . $extraClass);
	if ($captionKey === 'epc_cp_group_operator') {
		$groupLiClass .= ' epc-cp-menu-group--operator';
	}
	?>
	<li class="<?php echo htmlspecialchars($groupLiClass, ENT_QUOTES, 'UTF-8'); ?>" data-epc-group="<?php echo htmlspecialchars($captionKey, ENT_QUOTES, 'UTF-8'); ?>">
		<a href="javascript:void(0);" class="epc-cp-nav-section__toggle">
			<span class="nav-label-wrap">
				<span class="nav-label epc-cp-nav-section__label"><?php echo $tab['caption']; ?></span>
				<?php if ($groupSubtitle !== '') { ?>
				<span class="epc-cp-group-subtitle epc-cp-nav-section__sub"><?php echo htmlspecialchars($groupSubtitle, ENT_QUOTES, 'UTF-8'); ?></span>
				<?php } ?>
			</span>
			<span class="fa arrow"></span>
		</a>
		<ul class="nav nav-second-level epc-cp-nav-section__children">
			<?php for ($i = 0; $i < count($tab['items']); $i++) { ?>
			<li class="epc-cp-nav-item epc-cp-nav-item--sub">
				<a href="<?php echo $tab['items'][$i]['url']; ?>" class="epc-cp-nav-item__link">
					<?php if (!empty($tab['items'][$i]['fontawesome_class'])) { ?>
					<i class="<?php echo $tab['items'][$i]['fontawesome_class']; ?>"></i>
					<?php } ?>
					<?php echo translate_str_by_id($tab['items'][$i]['caption']); ?>
				</a>
			</li>
			<?php } ?>
		</ul>
	</li>
	<?php
};

$rendered = array();
// 1) Primary groups in preferred order
foreach ($epcPrimaryKeys as $pkey) {
	foreach ($tabs as $key => $tab) {
		$ck = (string) ($tab['caption_key'] ?? '');
		if ($ck === $pkey || (string) $key === $pkey) {
			$epcRenderNavGroup($key, $tab, 'epc-cp-nav-section--primary');
			$rendered[$key] = true;
			break;
		}
	}
}
// 2) Other non-advanced groups (legacy leftovers) still as primary-ish
foreach ($tabs as $key => $tab) {
	if (!empty($rendered[$key])) {
		continue;
	}
	$ck = (string) ($tab['caption_key'] ?? '');
	if (in_array($ck, $epcAdvancedKeys, true)) {
		continue;
	}
	$epcRenderNavGroup($key, $tab, 'epc-cp-nav-section--primary');
	$rendered[$key] = true;
}
// 3) Advanced divider + groups (tenants only — Super CP keeps flat density)
$advancedToShow = array();
foreach ($tabs as $key => $tab) {
	if (!empty($rendered[$key])) {
		continue;
	}
	if (count($tab['items']) === 0) {
		continue;
	}
	$advancedToShow[$key] = $tab;
}
if (!$epcIsSuperHost && count($advancedToShow) > 0) {
	?>
	<li class="epc-cp-nav-divider" aria-hidden="true">
		<span>More modules</span>
	</li>
	<?php
	foreach ($epcAdvancedKeys as $akey) {
		foreach ($advancedToShow as $key => $tab) {
			$ck = (string) ($tab['caption_key'] ?? '');
			if ($ck === $akey || (string) $key === $akey) {
				$epcRenderNavGroup($key, $tab, 'epc-cp-nav-section--advanced');
				unset($advancedToShow[$key]);
				break;
			}
		}
	}
	foreach ($advancedToShow as $key => $tab) {
		$epcRenderNavGroup($key, $tab, 'epc-cp-nav-section--advanced');
	}
} elseif (count($advancedToShow) > 0) {
	foreach ($advancedToShow as $key => $tab) {
		$epcRenderNavGroup($key, $tab, '');
	}
}
?>
</ul>
<script>
(function () {
	var input = document.getElementById('epc-cp-menu-q');
	var menu = document.getElementById('side-menu');
	if (!input || !menu) return;
	function norm(s) { return String(s || '').toLowerCase().replace(/\s+/g, ' ').trim(); }
	input.addEventListener('input', function () {
		var q = norm(input.value);
		menu.classList.toggle('is-filtering', q.length > 0);
		Array.prototype.forEach.call(menu.querySelectorAll('li.epc-cp-nav-section'), function (section) {
			var label = section.querySelector('.epc-cp-nav-section__label');
			var sub = section.querySelector('.epc-cp-nav-section__sub');
			var sectionText = norm((label ? label.textContent : '') + ' ' + (sub ? sub.textContent : ''));
			var anyItem = false;
			Array.prototype.forEach.call(section.querySelectorAll('li.epc-cp-nav-item--sub'), function (li) {
				var t = norm(li.textContent);
				var show = !q || t.indexOf(q) !== -1 || sectionText.indexOf(q) !== -1;
				li.style.display = show ? '' : 'none';
				if (show) anyItem = true;
			});
			var showSection = !q || anyItem || sectionText.indexOf(q) !== -1;
			section.style.display = showSection ? '' : 'none';
			if (q && showSection) {
				section.classList.add('active');
			}
		});
		var home = menu.querySelector('li.epc-cp-nav-item--home');
		if (home) {
			var ht = norm(home.textContent);
			home.style.display = (!q || ht.indexOf(q) !== -1) ? '' : 'none';
		}
		var div = menu.querySelector('li.epc-cp-nav-divider');
		if (div) {
			div.style.display = q ? 'none' : '';
		}
	});
})();
</script>
<?php
} // !epc_cp_topnav_only — eval-safe (no return)
?>

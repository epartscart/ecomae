<?php
defined('_ASTEXE_') or die('No access');
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_pricing.php");

function epc_pm_h($value)
{
	return htmlspecialchars((string)$value, ENT_QUOTES, 'UTF-8');
}

function epc_pm_group_name($row)
{
	return function_exists('translate_str_by_key') ? translate_str_by_key($row['value']) : translate_str_by_id($row['value']);
}

function epc_pm_profile_code($value)
{
	$value = strtolower(trim((string)$value));
	$value = preg_replace('/[^a-z0-9_\\-]+/', '_', $value);
	$value = trim($value, '_-');
	return $value;
}

function epc_pm_ensure_profile_schema($db_link)
{
	try
	{
		$column_query = $db_link->prepare("SHOW COLUMNS FROM `epc_price_profiles` LIKE 'vat_percent';");
		$column_query->execute();
		if(!$column_query->fetch())
		{
			$db_link->exec("ALTER TABLE `epc_price_profiles` ADD `vat_percent` DECIMAL(10,2) NULL DEFAULT NULL AFTER `group_id`;");
		}
		$margin_query = $db_link->prepare("SHOW COLUMNS FROM `epc_price_profiles` LIKE 'margin_percent';");
		$margin_query->execute();
		if(!$margin_query->fetch())
		{
			$db_link->exec("ALTER TABLE `epc_price_profiles` ADD `margin_percent` DECIMAL(10,2) NOT NULL DEFAULT 0.00 AFTER `group_id`;");
		}
		$db_link->exec("CREATE TABLE IF NOT EXISTS `epc_price_profile_article_rules` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`group_id` int(11) NOT NULL,
			`manufacturer` varchar(255) NOT NULL,
			`article` varchar(64) NOT NULL,
			`margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
			`visible` tinyint(1) NOT NULL DEFAULT 1,
			`updated_at` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `x_group_brand_article` (`group_id`, `manufacturer`, `article`),
			KEY `x_article` (`article`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
	}
	catch(Exception $e)
	{
	}
}

function epc_pm_format_breakdown_steps($breakdown)
{
	if(empty($breakdown['steps']))
	{
		return 'No extra margin';
	}
	$parts = array();
	foreach($breakdown['steps'] as $step)
	{
		$parts[] = $step['label'].' +'.number_format((float)$step['percent'], 2, '.', '').'% → '.number_format((float)$step['price_after'], 2, '.', '');
	}
	return implode('; ', $parts);
}

function epc_pm_build_demo_scenarios($db_link, array $profiles)
{
	$guest_group_id = 0;
	try
	{
		$guest_group_id = (int)$db_link->query("SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1;")->fetchColumn();
	}
	catch(Exception $e)
	{
	}

	$profile_by_code = array();
	foreach($profiles as $profile)
	{
		$profile_by_code[$profile['code']] = $profile;
	}

	$base = 100.00;
	$scenarios = array(
		array('key' => 'guest', 'title' => 'Guest visitor (not logged in)', 'group_id' => $guest_group_id, 'brand' => 'TOYOTA', 'article' => ''),
		array('key' => 'retail_plain', 'title' => 'Retail profile — generic brand (TOYOTA)', 'group_id' => isset($profile_by_code['retail']['id']) ? (int)$profile_by_code['retail']['id'] : 0, 'brand' => 'TOYOTA', 'article' => ''),
		array('key' => 'retail_mazda', 'title' => 'Retail profile — MAZDA brand rule', 'group_id' => isset($profile_by_code['retail']['id']) ? (int)$profile_by_code['retail']['id'] : 0, 'brand' => 'MAZDA', 'article' => ''),
		array('key' => 'wholesale_mazda', 'title' => 'Wholesale profile — MAZDA brand rule', 'group_id' => isset($profile_by_code['wholesale']['id']) ? (int)$profile_by_code['wholesale']['id'] : 0, 'brand' => 'MAZDA', 'article' => ''),
		array('key' => 'retail_article', 'title' => 'Retail profile — article-level rule (if configured)', 'group_id' => isset($profile_by_code['retail']['id']) ? (int)$profile_by_code['retail']['id'] : 0, 'brand' => 'TOYOTA', 'article' => '1140051020'),
	);

	$out = array();
	foreach($scenarios as $scenario)
	{
		$group_id = (int)$scenario['group_id'];
		if($group_id <= 0)
		{
			continue;
		}
		$result = epc_pricing_apply_price_rules($db_link, $group_id, $scenario['brand'], $base, 0.0, $scenario['article']);
		$profile_label = 'Group '.$group_id;
		foreach($profiles as $profile)
		{
			if((int)$profile['id'] === $group_id)
			{
				$profile_label = epc_pm_group_name($profile).' ('.$profile['code'].')';
				break;
			}
		}
		if($group_id === $guest_group_id)
		{
			$profile_label = 'Guest / non-login group';
		}
		$out[] = array(
			'title' => $scenario['title'],
			'profile_label' => $profile_label,
			'brand' => $scenario['brand'],
			'article' => $scenario['article'] !== '' ? $scenario['article'] : '—',
			'base_price' => $base,
			'visible' => !empty($result['visible']),
			'final_price' => !empty($result['visible']) ? (float)$result['breakdown']['final_price'] : null,
			'total_margin_percent' => !empty($result['visible']) ? (float)$result['breakdown']['total_margin_percent'] : null,
			'breakdown_text' => !empty($result['visible']) ? epc_pm_format_breakdown_steps($result['breakdown']) : (string)($result['hidden_reason'] ?? 'Hidden'),
		);
	}
	return $out;
}

$message = '';
$error = '';

epc_pm_ensure_profile_schema($db_link);

if($_SERVER['REQUEST_METHOD'] === 'POST')
{
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	$action = isset($_POST['action']) ? $_POST['action'] : '';
	try
	{
		if($action === 'save_vat')
		{
			$vat = (float)str_replace(',', '.', $_POST['vat_percent']);
			if($vat < 0 || $vat > 100)
			{
				throw new Exception('VAT must be from 0 to 100');
			}
			$stmt = $db_link->prepare("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES ('vat_percent', ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`);");
			$stmt->execute(array(number_format($vat, 2, '.', '')));
			$message = 'VAT setting saved';
		}
		else if($action === 'save_profile_margin')
		{
			$group_id = (int)$_POST['group_id'];
			$profile_margin = (float)str_replace(',', '.', $_POST['profile_margin_percent']);
			if($group_id <= 0)
			{
				throw new Exception('Select profile');
			}
			if($profile_margin < 0 || $profile_margin > 1000)
			{
				throw new Exception('Profile margin must be from 0 to 1000');
			}
			$stmt = $db_link->prepare("UPDATE `epc_price_profiles` SET `margin_percent` = ? WHERE `group_id` = ?;");
			$stmt->execute(array(number_format($profile_margin, 2, '.', ''), $group_id));
			$message = 'Profile overall margin saved';
		}
		else if($action === 'save_guest_margin')
		{
			$guest_margin = (float)str_replace(',', '.', $_POST['guest_margin_percent']);
			if($guest_margin < 0 || $guest_margin > 1000)
			{
				throw new Exception('Guest margin must be from 0 to 1000');
			}
			$stmt = $db_link->prepare("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES ('guest_margin_percent', ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`);");
			$stmt->execute(array(number_format($guest_margin, 2, '.', '')));
			$message = 'Guest / non-login margin saved';
		}
		else if($action === 'save_profile_vat')
		{
			$group_id = (int)$_POST['group_id'];
			$profile_vat = trim((string)$_POST['profile_vat_percent']);
			if($group_id <= 0)
			{
				throw new Exception('Select profile');
			}
			if($profile_vat === '')
			{
				$stmt = $db_link->prepare("UPDATE `epc_price_profiles` SET `vat_percent` = NULL WHERE `group_id` = ?;");
				$stmt->execute(array($group_id));
			}
			else
			{
				$profile_vat_value = (float)str_replace(',', '.', $profile_vat);
				if($profile_vat_value < 0 || $profile_vat_value > 100)
				{
					throw new Exception('Profile VAT must be from 0 to 100');
				}
				$stmt = $db_link->prepare("UPDATE `epc_price_profiles` SET `vat_percent` = ? WHERE `group_id` = ?;");
				$stmt->execute(array(number_format($profile_vat_value, 2, '.', ''), $group_id));
			}
			$message = 'Profile VAT saved';
		}
		else if($action === 'create_profile')
		{
			$profile_name = trim($_POST['profile_name']);
			$profile_code = epc_pm_profile_code($_POST['profile_code'] !== '' ? $_POST['profile_code'] : $profile_name);
			if($profile_name === '' || $profile_code === '')
			{
				throw new Exception('Enter profile name');
			}
			$existing_profile = $db_link->prepare("SELECT `id` FROM `epc_price_profiles` WHERE `code` = ? LIMIT 1;");
			$existing_profile->execute(array($profile_code));
			if($existing_profile->fetchColumn())
			{
				throw new Exception('This profile code already exists');
			}
			$str_key = 'EPC_PROFILE_' . strtoupper(preg_replace('/[^A-Z0-9_]+/', '_', strtoupper($profile_code)));
			$db_link->prepare("INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1);")->execute(array($str_key, $profile_name));
			$db_link->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, 'en', ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);")->execute(array($str_key, $profile_name));
			$db_link->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, 'ru', ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);")->execute(array($str_key, $profile_name));
			$max_group_id = (int)$db_link->query("SELECT IFNULL(MAX(`id`), 0) FROM `groups`;")->fetchColumn();
			$new_group_id = $max_group_id + 1;
			$max_order = (int)$db_link->query("SELECT IFNULL(MAX(`order`), 0) FROM `groups` WHERE `parent` = 1;")->fetchColumn();
			$db_link->prepare("INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`) VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, ?);")->execute(array($new_group_id, $str_key, $str_key, $max_order + 1));
			$db_link->prepare("INSERT INTO `epc_price_profiles` (`code`, `group_id`, `created_at`) VALUES (?, ?, ?);")->execute(array($profile_code, $new_group_id, time()));
			$db_link->prepare("UPDATE `groups` SET `count` = (SELECT COUNT(*) FROM (SELECT `id` FROM `groups` WHERE `parent` = 1) AS x) WHERE `id` = 1;")->execute();
			$message = 'Customer price profile created';
		}
		else if($action === 'save_bulk_visibility')
		{
			$group_id = (int)$_POST['group_id'];
			$visible = (int)$_POST['visible'];
			$selected_brands = isset($_POST['brands']) && is_array($_POST['brands']) ? $_POST['brands'] : array();
			if($group_id <= 0 || empty($selected_brands))
			{
				throw new Exception('Select profile and at least one brand');
			}
			$stmt = $db_link->prepare("INSERT INTO `epc_price_profile_brand_rules` (`group_id`, `manufacturer`, `margin_percent`, `visible`) VALUES (?, ?, 0, ?) ON DUPLICATE KEY UPDATE `visible` = VALUES(`visible`);");
			$updated_count = 0;
			foreach($selected_brands as $brand)
			{
				$brand = epc_pricing_normalize_brand($brand);
				if($brand === '')
				{
					continue;
				}
				$stmt->execute(array($group_id, $brand, $visible ? 1 : 0));
				$updated_count++;
			}
			$message = 'Brand visibility updated for '.$updated_count.' brand(s)';
		}
		else if($action === 'save_rule')
		{
			$group_id = (int)$_POST['group_id'];
			$manufacturer = epc_pricing_normalize_brand($_POST['manufacturer']);
			$margin = (float)str_replace(',', '.', $_POST['margin_percent']);
			$visible = (int)$_POST['visible'];
			if($group_id <= 0 || $manufacturer === '')
			{
				throw new Exception('Select profile and enter brand');
			}
			$stmt = $db_link->prepare("INSERT INTO `epc_price_profile_brand_rules` (`group_id`, `manufacturer`, `margin_percent`, `visible`) VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`);");
			$stmt->execute(array($group_id, $manufacturer, $margin, $visible ? 1 : 0));
			$message = 'Brand rule saved';
		}
		else if($action === 'save_article_rule')
		{
			$group_id = (int)$_POST['group_id'];
			$manufacturer = epc_pricing_normalize_brand($_POST['manufacturer']);
			$article = epc_pricing_normalize_article($_POST['article']);
			$margin = (float)str_replace(',', '.', $_POST['margin_percent']);
			$visible = (int)$_POST['visible'];
			if($group_id <= 0 || $manufacturer === '' || $article === '')
			{
				throw new Exception('Select profile, brand, and article');
			}
			$stmt = $db_link->prepare("INSERT INTO `epc_price_profile_article_rules` (`group_id`, `manufacturer`, `article`, `margin_percent`, `visible`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`), `updated_at` = VALUES(`updated_at`);");
			$stmt->execute(array($group_id, $manufacturer, $article, $margin, $visible ? 1 : 0, time()));
			$message = 'Article rule saved';
		}
		else if($action === 'delete_article_rule')
		{
			$stmt = $db_link->prepare("DELETE FROM `epc_price_profile_article_rules` WHERE `id` = ?;");
			$stmt->execute(array((int)$_POST['rule_id']));
			$message = 'Article rule deleted';
		}
		else if($action === 'delete_rule')
		{
			$stmt = $db_link->prepare("DELETE FROM `epc_price_profile_brand_rules` WHERE `id` = ?;");
			$stmt->execute(array((int)$_POST['rule_id']));
			$message = 'Brand rule deleted';
		}
		else if($action === 'assign_profile')
		{
			$user_id = (int)$_POST['user_id'];
			$group_id = (int)$_POST['group_id'];
			if($user_id <= 0 || $group_id <= 0)
			{
				throw new Exception('Select customer and profile');
			}
			$profile_groups = $db_link->query("SELECT `group_id` FROM `epc_price_profiles`;")->fetchAll(PDO::FETCH_COLUMN);
			if(!empty($profile_groups))
			{
				$placeholders = str_repeat('?,', count($profile_groups) - 1) . '?';
				$args = array_merge(array($user_id), $profile_groups);
				$db_link->prepare("DELETE FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN ($placeholders);")->execute($args);
			}
			$stmt = $db_link->prepare("INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?);");
			$stmt->execute(array($user_id, $group_id));
			$message = 'Customer profile assigned';
		}
	}
	catch(Exception $e)
	{
		$error = $e->getMessage();
	}
}

$profiles = array();
$profiles_query = $db_link->prepare("SELECT `groups`.*, `epc_price_profiles`.`code`, `epc_price_profiles`.`vat_percent`, `epc_price_profiles`.`margin_percent` FROM `epc_price_profiles` INNER JOIN `groups` ON `groups`.`id` = `epc_price_profiles`.`group_id` ORDER BY `epc_price_profiles`.`id` ASC;");
$profiles_query->execute();
while($row = $profiles_query->fetch())
{
	$profiles[] = $row;
}

$vat_percent = epc_pricing_get_setting($db_link, 'vat_percent', '5.00');
$guest_margin_percent = epc_pricing_get_setting($db_link, 'guest_margin_percent', '0.00');

$users = array();
$users_query = $db_link->prepare("SELECT `user_id`, MAX(CASE WHEN `data_key`='email' THEN `data_value` ELSE '' END) AS `email`, MAX(CASE WHEN `data_key`='name' THEN `data_value` ELSE '' END) AS `name`, MAX(CASE WHEN `data_key`='surname' THEN `data_value` ELSE '' END) AS `surname` FROM `users_profiles` GROUP BY `user_id` ORDER BY `user_id` DESC LIMIT 300;");
$users_query->execute();
while($row = $users_query->fetch())
{
	$users[] = $row;
}

$brands = array();
try
{
	$brands_query = $db_link->prepare("
		SELECT DISTINCT `brand` FROM (
			SELECT `name` AS `brand` FROM `shop_docpart_manufacturers` WHERE `name` IS NOT NULL AND `name` != ''
			UNION
			SELECT `manufacturer` AS `brand` FROM `shop_docpart_prices_data` WHERE `manufacturer` IS NOT NULL AND `manufacturer` != ''
			UNION
			SELECT `manufacturer` AS `brand` FROM `epc_price_profile_brand_rules` WHERE `manufacturer` IS NOT NULL AND `manufacturer` != ''
		) AS `brands`
		ORDER BY `brand` ASC
		LIMIT 3000;
	");
	$brands_query->execute();
	while($row = $brands_query->fetch())
	{
		$brand = epc_pricing_normalize_brand($row['brand']);
		if($brand !== '')
		{
			$brands[$brand] = $brand;
		}
	}
}
catch(Exception $e)
{
}

$rules = array();
$rules_query = $db_link->prepare("SELECT `epc_price_profile_brand_rules`.*, `groups`.`value` AS `group_name` FROM `epc_price_profile_brand_rules` INNER JOIN `groups` ON `groups`.`id` = `epc_price_profile_brand_rules`.`group_id` ORDER BY `groups`.`order`, `manufacturer`;");
$rules_query->execute();
while($row = $rules_query->fetch())
{
	$rules[] = $row;
}

$article_rules = array();
$article_rules_query = $db_link->prepare("SELECT `epc_price_profile_article_rules`.*, `groups`.`value` AS `group_name` FROM `epc_price_profile_article_rules` INNER JOIN `groups` ON `groups`.`id` = `epc_price_profile_article_rules`.`group_id` ORDER BY `groups`.`order`, `manufacturer`, `article`;");
$article_rules_query->execute();
while($row = $article_rules_query->fetch())
{
	$article_rules[] = $row;
}

$demo_scenarios = epc_pm_build_demo_scenarios($db_link, $profiles);
$preview_result = null;
if(isset($_GET['preview']) && $_GET['preview'] === '1')
{
	$preview_group_id = isset($_GET['group_id']) ? (int)$_GET['group_id'] : 0;
	$preview_brand = isset($_GET['brand']) ? (string)$_GET['brand'] : 'TOYOTA';
	$preview_article = isset($_GET['article']) ? (string)$_GET['article'] : '';
	$preview_base = isset($_GET['base_price']) ? (float)$_GET['base_price'] : 100.0;
	if($preview_group_id > 0 && $preview_base > 0)
	{
		$preview_result = epc_pricing_apply_price_rules($db_link, $preview_group_id, $preview_brand, $preview_base, 0.0, $preview_article);
	}
}

$rules_count = count($rules);
$article_rules_count = count($article_rules);
$profiles_count = count($profiles);
$backend = $DP_Config->backend_dir;
?>

<?php if($message !== '') { ?><div class="alert alert-success epc-pm-alert"><?=epc_pm_h($message);?></div><?php } ?>
<?php if($error !== '') { ?><div class="alert alert-danger epc-pm-alert"><?=epc_pm_h($error);?></div><?php } ?>

<style>
.epc-pm { max-width: 1280px; margin: 0 auto; font-size: 14px; }
.epc-pm-hero {
	background: linear-gradient(135deg, #0c4a6e 0%, #0369a1 50%, #0ea5e9 100%);
	border-radius: 12px; color: #fff; padding: 28px 32px; margin-bottom: 20px;
	box-shadow: 0 12px 40px rgba(3, 105, 161, .25);
}
.epc-pm-hero h1 { margin: 0 0 8px; font-size: 26px; font-weight: 700; color: #fff; }
.epc-pm-hero p { margin: 0; opacity: .92; line-height: 1.55; max-width: 820px; }
.epc-pm-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin-bottom: 22px; }
.epc-pm-stat {
	background: #fff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 16px 18px;
	box-shadow: 0 2px 8px rgba(15, 23, 42, .04);
}
.epc-pm-stat strong { display: block; font-size: 24px; line-height: 1.2; color: #0f172a; }
.epc-pm-stat span { font-size: 12px; color: #64748b; text-transform: uppercase; letter-spacing: .03em; }
.epc-pm-flow {
	display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 0;
	background: #fff; border: 1px solid #e2e8f0; border-radius: 10px; overflow: hidden;
	margin-bottom: 24px; box-shadow: 0 2px 8px rgba(15, 23, 42, .04);
}
.epc-pm-flow-step {
	padding: 14px 12px; text-align: center; border-right: 1px solid #e2e8f0; position: relative;
}
.epc-pm-flow-step:last-child { border-right: none; }
.epc-pm-flow-step a { color: inherit; text-decoration: none; display: block; }
.epc-pm-flow-step a:hover { color: #0369a1; }
.epc-pm-flow-num {
	display: inline-flex; align-items: center; justify-content: center;
	width: 28px; height: 28px; border-radius: 50%; background: #0369a1; color: #fff;
	font-size: 13px; font-weight: 700; margin-bottom: 6px;
}
.epc-pm-flow-label { font-size: 11px; font-weight: 600; color: #334155; line-height: 1.3; }
.epc-pm-section {
	background: #fff; border: 1px solid #e2e8f0; border-radius: 10px;
	margin-bottom: 20px; overflow: hidden; box-shadow: 0 2px 8px rgba(15, 23, 42, .04);
}
.epc-pm-section-head {
	background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 16px 20px;
	display: flex; align-items: flex-start; gap: 14px;
}
.epc-pm-section-num {
	flex: 0 0 36px; width: 36px; height: 36px; border-radius: 8px;
	background: #0369a1; color: #fff; font-weight: 700; font-size: 16px;
	display: flex; align-items: center; justify-content: center;
}
.epc-pm-section-head h2 { margin: 0 0 4px; font-size: 17px; font-weight: 700; color: #0f172a; }
.epc-pm-section-head p { margin: 0; font-size: 13px; color: #64748b; line-height: 1.45; }
.epc-pm-section-body { padding: 20px; }
.epc-pm-table { margin-bottom: 0; }
.epc-pm-table thead th { background: #f1f5f9; font-size: 12px; text-transform: uppercase; letter-spacing: .04em; color: #475569; border-bottom: 2px solid #e2e8f0 !important; }
.epc-pm-table td { vertical-align: middle !important; }
.epc-pm-inline-form { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
.epc-pm-inline-form input[type=number] { width: 88px; }
.epc-pm-form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 14px; }
.epc-pm-form-grid label { display: block; font-size: 12px; font-weight: 600; color: #475569; margin-bottom: 4px; }
.epc-pm-form-grid .form-control { margin-bottom: 0; }
.epc-pm-form-actions { margin-top: 16px; padding-top: 16px; border-top: 1px solid #e2e8f0; }
.epc-pm-callout {
	background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 12px 14px;
	font-size: 13px; color: #1e40af; line-height: 1.5; margin-bottom: 16px;
}
.epc-pm-callout-warn { background: #fffbeb; border-color: #fde68a; color: #92400e; }
.epc-pm-demo-card {
	background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px;
	margin-bottom: 10px;
}
.epc-pm-demo-card:last-child { margin-bottom: 0; }
.epc-pm-demo-title { font-weight: 600; color: #0f172a; margin-bottom: 6px; font-size: 13px; }
.epc-pm-demo-meta { font-size: 12px; color: #64748b; margin-bottom: 8px; }
.epc-pm-demo-price { font-size: 22px; font-weight: 700; color: #059669; }
.epc-pm-demo-price.hidden-price { color: #dc2626; font-size: 16px; }
.epc-pm-demo-breakdown { font-size: 12px; color: #475569; margin-top: 6px; line-height: 1.5; }
.epc-pm-preview-box {
	margin-top: 16px; padding: 16px 18px; background: linear-gradient(135deg, #ecfdf5, #f0fdf4);
	border: 1px solid #86efac; border-radius: 8px;
}
.epc-pm-preview-box.hidden-box { background: linear-gradient(135deg, #fef2f2, #fff1f2); border-color: #fca5a5; }
.epc-pm-badge { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 11px; font-weight: 600; }
.epc-pm-badge-show { background: #dcfce7; color: #166534; }
.epc-pm-badge-hide { background: #fee2e2; color: #991b1b; }
.epc-pm-two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
@media (max-width: 991px) { .epc-pm-two-col { grid-template-columns: 1fr; } }
.epc-pm-advanced { border-top: 2px dashed #e2e8f0; margin-top: 8px; padding-top: 20px; }
.epc-pm-advanced summary { cursor: pointer; font-weight: 600; color: #475569; margin-bottom: 12px; }
.epc-pm-nav-top { margin-bottom: 12px; font-size: 13px; }
.epc-pm-nav-top a { margin-right: 14px; }
</style>

<div class="epc-pm">

<div class="epc-pm-hero">
	<h1>Price Management</h1>
	<p>
		Control how customers see prices on the storefront. Set profiles (Retail, Wholesale…),
		margins at profile / brand / article level, guest pricing, and invoice VAT — then verify with the live calculator below.
	</p>
</div>

<div class="epc-pm-stats">
	<div class="epc-pm-stat"><strong><?=(int)$profiles_count;?></strong><span>Profiles</span></div>
	<div class="epc-pm-stat"><strong><?=epc_pm_h($guest_margin_percent);?>%</strong><span>Guest margin</span></div>
	<div class="epc-pm-stat"><strong><?=epc_pm_h($vat_percent);?>%</strong><span>Default VAT</span></div>
	<div class="epc-pm-stat"><strong><?=(int)$rules_count;?></strong><span>Brand rules</span></div>
	<div class="epc-pm-stat"><strong><?=(int)$article_rules_count;?></strong><span>Article rules</span></div>
</div>

<div class="epc-pm-callout">
	<strong>How margins stack:</strong> Warehouse base price → <em>Profile overall %</em> → <em>Brand %</em> → <em>Article %</em> → <em>Guest %</em> (visitors only). Each step adds on top of the previous price.
	<br><strong>Policy:</strong> Guest + Retail default to <strong>40%</strong> markup. B2B (wholesale / CIS / GCC) prices apply only after manager approval and profile assignment. Checkout blocks any line with no positive margin.
</div>

<div class="epc-pm-callout" style="border-left-color:#0f766e;background:#f0fdfa;">
	<strong>VAT policy (no double tax):</strong>
	Price uploads and catalogue / warehouse prices are <strong>excluding VAT (ex VAT)</strong>.
	Margins above stay on the ex-VAT base.
	UAE output VAT (default <?=epc_pm_h($vat_percent);?>%) is applied <strong>once at invoice / e-invoice level</strong> in FTA format:
	unit net → line net → VAT amount → total incl. VAT.
	B2C storefront may <em>display</em> prices incl. VAT; e-invoice still splits that back to net + VAT (never adds 5% on top again).
	B2B / export keep ex-VAT shelf prices and add VAT (or zero-rate) only on the tax invoice.
</div>

<div class="epc-pm-flow">
	<div class="epc-pm-flow-step"><a href="#epc-pm-step1"><span class="epc-pm-flow-num">1</span><div class="epc-pm-flow-label">Profiles &amp;<br>overall margin</div></a></div>
	<div class="epc-pm-flow-step"><a href="#epc-pm-step2"><span class="epc-pm-flow-num">2</span><div class="epc-pm-flow-label">Guest &amp;<br>VAT settings</div></a></div>
	<div class="epc-pm-flow-step"><a href="#epc-pm-step3"><span class="epc-pm-flow-num">3</span><div class="epc-pm-flow-label">Assign<br>customer</div></a></div>
	<div class="epc-pm-flow-step"><a href="#epc-pm-step4"><span class="epc-pm-flow-num">4</span><div class="epc-pm-flow-label">Brand<br>rules</div></a></div>
	<div class="epc-pm-flow-step"><a href="#epc-pm-step5"><span class="epc-pm-flow-num">5</span><div class="epc-pm-flow-label">Article<br>rules</div></a></div>
	<div class="epc-pm-flow-step"><a href="#epc-pm-step6"><span class="epc-pm-flow-num">6</span><div class="epc-pm-flow-label">Live<br>calculator</div></a></div>
	<div class="epc-pm-flow-step"><a href="#epc-pm-step7"><span class="epc-pm-flow-num">7</span><div class="epc-pm-flow-label">Verify on<br>storefront</div></a></div>
</div>

<!-- STEP 1 -->
<div class="epc-pm-section" id="epc-pm-step1">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">1</div>
		<div>
			<h2>Customer price profiles</h2>
			<p>Each profile is a pricing tier (Retail, Wholesale, CIS, GCC). Set an <strong>overall margin %</strong> that applies to every brand for that profile.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<div class="table-responsive">
			<table class="table table-striped epc-pm-table">
				<thead>
					<tr>
						<th>Profile</th>
						<th>Code</th>
						<th>Overall margin %</th>
						<th>Invoice VAT %</th>
					</tr>
				</thead>
				<tbody>
					<?php foreach($profiles as $profile) { ?>
					<tr>
						<td><strong><?=epc_pm_h(epc_pm_group_name($profile));?></strong><br><span class="text-muted" style="font-size:11px;">Group ID <?=epc_pm_h($profile['id']);?></span></td>
						<td><code><?=epc_pm_h($profile['code']);?></code></td>
						<td>
							<form method="post" class="epc-pm-inline-form">
								<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
								<input type="hidden" name="action" value="save_profile_margin" />
								<input type="hidden" name="group_id" value="<?=epc_pm_h($profile['id']);?>" />
								<input class="form-control input-sm" name="profile_margin_percent" type="number" step="0.01" value="<?=epc_pm_h(isset($profile['margin_percent']) ? $profile['margin_percent'] : '0');?>" />
								<button class="btn btn-sm btn-primary" type="submit">Save</button>
							</form>
						</td>
						<td>
							<form method="post" class="epc-pm-inline-form">
								<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
								<input type="hidden" name="action" value="save_profile_vat" />
								<input type="hidden" name="group_id" value="<?=epc_pm_h($profile['id']);?>" />
								<input class="form-control input-sm" name="profile_vat_percent" type="number" step="0.01" value="<?=epc_pm_h($profile['vat_percent']);?>" placeholder="<?=epc_pm_h($vat_percent);?>" />
								<button class="btn btn-sm btn-default" type="submit">Save</button>
							</form>
						</td>
					</tr>
					<?php } ?>
				</tbody>
			</table>
		</div>
		<details class="epc-pm-advanced" style="border:none;padding-top:16px;margin-top:0;">
			<summary>Create a new profile</summary>
			<form method="post" class="epc-pm-form-grid" style="margin-top:14px;">
				<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
				<input type="hidden" name="action" value="create_profile" />
				<div><label>Profile name</label><input class="form-control" name="profile_name" placeholder="e.g. Fleet, VIP, Dealer" /></div>
				<div><label>Profile code (optional)</label><input class="form-control" name="profile_code" placeholder="e.g. fleet" /></div>
				<div class="epc-pm-form-actions" style="grid-column:1/-1;border:none;padding-top:0;">
					<button class="btn btn-success" type="submit"><i class="fa fa-plus"></i> Create profile</button>
				</div>
			</form>
		</details>
	</div>
</div>

<!-- STEP 2 -->
<div class="epc-pm-section" id="epc-pm-step2">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">2</div>
		<div>
			<h2>Guest margin &amp; default VAT</h2>
			<p>Guest margin applies to visitors who are <strong>not logged in</strong>. Uploaded and catalogue prices stay <strong>ex VAT</strong>. UAE sales apply <?=epc_pm_h($vat_percent);?>% output VAT <strong>once on the tax invoice / e-invoice</strong> (FTA: net + VAT + total) — never twice.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<div class="epc-pm-two-col">
			<div>
				<h4 style="margin-top:0;font-size:15px;">Guest / non-login margin</h4>
				<form method="post">
					<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
					<input type="hidden" name="action" value="save_guest_margin" />
					<label style="font-size:12px;color:#64748b;">Extra margin for visitors, %</label>
					<div class="input-group">
						<input class="form-control" name="guest_margin_percent" type="number" step="0.01" value="<?=epc_pm_h($guest_margin_percent);?>" />
						<span class="input-group-btn"><button class="btn btn-primary" type="submit">Save</button></span>
					</div>
					<p class="help-block" style="margin-top:8px;">Example: 40 means a guest sees prices 40% higher than the logged-in base for their group.</p>
				</form>
			</div>
			<div>
				<h4 style="margin-top:0;font-size:15px;">Default invoice VAT</h4>
				<form method="post">
					<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
					<input type="hidden" name="action" value="save_vat" />
					<label style="font-size:12px;color:#64748b;">VAT % (when profile has no own VAT)</label>
					<div class="input-group">
						<input class="form-control" name="vat_percent" type="number" step="0.01" value="<?=epc_pm_h($vat_percent);?>" />
						<span class="input-group-btn"><button class="btn btn-primary" type="submit">Save VAT</button></span>
					</div>
					<p class="help-block" style="margin-top:8px;">UAE default: 5% output VAT on customer tax invoices / e-invoices. Price uploads must be entered <strong>without VAT</strong>; the invoice layer adds VAT once.</p>
				</form>
			</div>
		</div>
	</div>
</div>

<!-- STEP 3 -->
<div class="epc-pm-section" id="epc-pm-step3">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">3</div>
		<div>
			<h2>Assign customer to profile</h2>
			<p>Link a registered customer to a pricing profile. They will see prices with that profile's margins when logged in.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<form method="post">
			<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
			<input type="hidden" name="action" value="assign_profile" />
			<div class="epc-pm-form-grid">
				<div>
					<label>Customer</label>
					<select class="form-control" name="user_id">
						<?php foreach($users as $customer) { ?>
						<option value="<?=epc_pm_h($customer['user_id']);?>">ID <?=epc_pm_h($customer['user_id']);?> — <?=epc_pm_h(trim($customer['email'].' '.$customer['name'].' '.$customer['surname']));?></option>
						<?php } ?>
					</select>
				</div>
				<div>
					<label>Price profile</label>
					<select class="form-control" name="group_id">
						<?php foreach($profiles as $profile) { ?>
						<option value="<?=epc_pm_h($profile['id']);?>"><?=epc_pm_h(epc_pm_group_name($profile));?> (<?=epc_pm_h($profile['code']);?>)</option>
						<?php } ?>
					</select>
				</div>
			</div>
			<div class="epc-pm-form-actions">
				<button class="btn btn-primary" type="submit"><i class="fa fa-user"></i> Assign profile to customer</button>
			</div>
		</form>
	</div>
</div>

<!-- STEP 4 -->
<div class="epc-pm-section" id="epc-pm-step4">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">4</div>
		<div>
			<h2>Brand-level rules</h2>
			<p>Add extra margin or hide/show an entire brand for a specific profile. Example: Retail + MAZDA +15%.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<form method="post">
			<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
			<input type="hidden" name="action" value="save_rule" />
			<div class="epc-pm-form-grid">
				<div><label>Profile</label>
					<select class="form-control" name="group_id">
						<?php foreach($profiles as $profile) { ?>
						<option value="<?=epc_pm_h($profile['id']);?>"><?=epc_pm_h(epc_pm_group_name($profile));?></option>
						<?php } ?>
					</select>
				</div>
				<div><label>Brand / manufacturer</label><input class="form-control" name="manufacturer" placeholder="TOYOTA, MAZDA, AISIN…" /></div>
				<div><label>Extra margin %</label><input class="form-control" name="margin_percent" type="number" step="0.01" value="0" /></div>
				<div><label>Visibility</label>
					<select class="form-control" name="visible"><option value="1">Show brand</option><option value="0">Hide brand</option></select>
				</div>
			</div>
			<div class="epc-pm-form-actions">
				<button class="btn btn-primary" type="submit"><i class="fa fa-tag"></i> Save brand rule</button>
			</div>
		</form>
		<?php if(!empty($rules)) { ?>
		<hr style="margin:24px 0 16px;">
		<h4 style="font-size:14px;margin-bottom:12px;">Active brand rules</h4>
		<div class="table-responsive">
			<table class="table table-striped epc-pm-table">
				<thead><tr><th>Profile</th><th>Brand</th><th>Margin</th><th>Status</th><th></th></tr></thead>
				<tbody>
					<?php foreach($rules as $rule) { ?>
					<tr>
						<td><?=epc_pm_h(function_exists('translate_str_by_key') ? translate_str_by_key($rule['group_name']) : translate_str_by_id($rule['group_name']));?></td>
						<td><strong><?=epc_pm_h($rule['manufacturer']);?></strong></td>
						<td>+<?=epc_pm_h($rule['margin_percent']);?>%</td>
						<td><span class="epc-pm-badge <?=((int)$rule['visible'] === 1) ? 'epc-pm-badge-show' : 'epc-pm-badge-hide';?>"><?=((int)$rule['visible'] === 1) ? 'Visible' : 'Hidden';?></span></td>
						<td class="text-right">
							<form method="post" style="display:inline;">
								<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
								<input type="hidden" name="action" value="delete_rule" />
								<input type="hidden" name="rule_id" value="<?=epc_pm_h($rule['id']);?>" />
								<button class="btn btn-xs btn-danger" type="submit">Remove</button>
							</form>
						</td>
					</tr>
					<?php } ?>
				</tbody>
			</table>
		</div>
		<?php } else { ?>
		<p class="text-muted" style="margin-top:16px;margin-bottom:0;">No brand rules yet. Add one above.</p>
		<?php } ?>
	</div>
</div>

<!-- STEP 5 -->
<div class="epc-pm-section" id="epc-pm-step5">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">5</div>
		<div>
			<h2>Article-level rules (most specific)</h2>
			<p>Target one exact part number under a brand. Example: Retail + TOYOTA + 1140051020 +20%.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<form method="post">
			<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
			<input type="hidden" name="action" value="save_article_rule" />
			<div class="epc-pm-form-grid">
				<div><label>Profile</label>
					<select class="form-control" name="group_id">
						<?php foreach($profiles as $profile) { ?>
						<option value="<?=epc_pm_h($profile['id']);?>"><?=epc_pm_h(epc_pm_group_name($profile));?></option>
						<?php } ?>
					</select>
				</div>
				<div><label>Brand</label><input class="form-control" name="manufacturer" placeholder="TOYOTA" /></div>
				<div><label>Article / part number</label><input class="form-control" name="article" placeholder="1140051020" /></div>
				<div><label>Extra margin %</label><input class="form-control" name="margin_percent" type="number" step="0.01" value="0" /></div>
				<div><label>Visibility</label>
					<select class="form-control" name="visible"><option value="1">Show part</option><option value="0">Hide part</option></select>
				</div>
			</div>
			<div class="epc-pm-form-actions">
				<button class="btn btn-primary" type="submit"><i class="fa fa-barcode"></i> Save article rule</button>
			</div>
		</form>
		<?php if(!empty($article_rules)) { ?>
		<hr style="margin:24px 0 16px;">
		<h4 style="font-size:14px;margin-bottom:12px;">Active article rules</h4>
		<div class="table-responsive">
			<table class="table table-striped epc-pm-table">
				<thead><tr><th>Profile</th><th>Brand</th><th>Article</th><th>Margin</th><th>Status</th><th></th></tr></thead>
				<tbody>
					<?php foreach($article_rules as $rule) { ?>
					<tr>
						<td><?=epc_pm_h(function_exists('translate_str_by_key') ? translate_str_by_key($rule['group_name']) : translate_str_by_id($rule['group_name']));?></td>
						<td><?=epc_pm_h($rule['manufacturer']);?></td>
						<td><code><?=epc_pm_h($rule['article']);?></code></td>
						<td>+<?=epc_pm_h($rule['margin_percent']);?>%</td>
						<td><span class="epc-pm-badge <?=((int)$rule['visible'] === 1) ? 'epc-pm-badge-show' : 'epc-pm-badge-hide';?>"><?=((int)$rule['visible'] === 1) ? 'Visible' : 'Hidden';?></span></td>
						<td class="text-right">
							<form method="post" style="display:inline;">
								<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
								<input type="hidden" name="action" value="delete_article_rule" />
								<input type="hidden" name="rule_id" value="<?=epc_pm_h($rule['id']);?>" />
								<button class="btn btn-xs btn-danger" type="submit">Remove</button>
							</form>
						</td>
					</tr>
					<?php } ?>
				</tbody>
			</table>
		</div>
		<?php } else { ?>
		<p class="text-muted" style="margin-top:16px;margin-bottom:0;">No article rules yet.</p>
		<?php } ?>
	</div>
</div>

<!-- STEP 6 -->
<div class="epc-pm-section" id="epc-pm-step6">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">6</div>
		<div>
			<h2>Live price calculator</h2>
			<p>Test any combination before checking the storefront. Uses your saved settings from Steps 1–5.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<form method="get" class="epc-pm-form-grid">
			<input type="hidden" name="preview" value="1" />
			<div><label>Profile</label>
				<select class="form-control" name="group_id">
					<?php foreach($profiles as $profile) { ?>
					<option value="<?=epc_pm_h($profile['id']);?>" <?=(isset($_GET['group_id']) && (int)$_GET['group_id'] === (int)$profile['id']) ? 'selected' : '';?>><?=epc_pm_h(epc_pm_group_name($profile));?></option>
					<?php } ?>
				</select>
			</div>
			<div><label>Brand</label><input class="form-control" name="brand" value="<?=epc_pm_h(isset($_GET['brand']) ? $_GET['brand'] : 'MAZDA');?>" /></div>
			<div><label>Article (optional)</label><input class="form-control" name="article" value="<?=epc_pm_h(isset($_GET['article']) ? $_GET['article'] : '');?>" placeholder="Leave empty for brand-only" /></div>
			<div><label>Base warehouse price</label><input class="form-control" name="base_price" type="number" step="0.01" value="<?=epc_pm_h(isset($_GET['base_price']) ? $_GET['base_price'] : '100');?>" /></div>
			<div class="epc-pm-form-actions" style="grid-column:1/-1;border:none;padding-top:0;">
				<button class="btn btn-primary btn-lg" type="submit"><i class="fa fa-calculator"></i> Calculate price</button>
			</div>
		</form>

		<?php if(is_array($preview_result)) { ?>
		<div class="epc-pm-preview-box <?=empty($preview_result['visible']) ? 'hidden-box' : '';?>">
			<?php if(!empty($preview_result['visible'])) { ?>
			<div style="font-size:13px;color:#64748b;margin-bottom:4px;">Calculated storefront price</div>
			<div class="epc-pm-demo-price"><?=number_format((float)$preview_result['breakdown']['final_price'], 2, '.', '');?></div>
			<div style="font-size:13px;margin-top:6px;">Total margin: <strong><?=number_format((float)$preview_result['breakdown']['total_margin_percent'], 2, '.', '');?>%</strong> from base <?=epc_pm_h(isset($_GET['base_price']) ? $_GET['base_price'] : '100');?></div>
			<div class="epc-pm-demo-breakdown"><?=epc_pm_h(epc_pm_format_breakdown_steps($preview_result['breakdown']));?></div>
			<?php } else { ?>
			<div class="epc-pm-demo-price hidden-price">Part hidden for this profile</div>
			<div class="epc-pm-demo-breakdown"><?=epc_pm_h($preview_result['hidden_reason'] ?? 'Hidden by visibility rule');?></div>
			<?php } ?>
		</div>
		<?php } ?>

		<h4 style="font-size:14px;margin:28px 0 14px;">Quick scenarios (base 100.00)</h4>
		<div class="row">
			<?php foreach($demo_scenarios as $demo) { ?>
			<div class="col-md-6 col-lg-4" style="margin-bottom:12px;">
				<div class="epc-pm-demo-card">
					<div class="epc-pm-demo-title"><?=epc_pm_h($demo['title']);?></div>
					<div class="epc-pm-demo-meta"><?=epc_pm_h($demo['profile_label']);?> · <?=epc_pm_h($demo['brand']);?><?php if($demo['article'] !== '—') { ?> · <?=epc_pm_h($demo['article']);?><?php } ?></div>
					<?php if($demo['visible']) { ?>
					<div class="epc-pm-demo-price"><?=number_format((float)$demo['final_price'], 2, '.', '');?></div>
					<div class="epc-pm-demo-breakdown">+<?=number_format((float)$demo['total_margin_percent'], 1, '.', '');?>% total · <?=epc_pm_h($demo['breakdown_text']);?></div>
					<?php } else { ?>
					<div class="epc-pm-demo-price hidden-price">Hidden</div>
					<div class="epc-pm-demo-breakdown"><?=epc_pm_h($demo['breakdown_text']);?></div>
					<?php } ?>
				</div>
			</div>
			<?php } ?>
		</div>
	</div>
</div>

<!-- STEP 7 -->
<div class="epc-pm-section" id="epc-pm-step7">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">7</div>
		<div>
			<h2>Verify on the storefront</h2>
			<p>After saving settings, confirm prices appear correctly for each customer type.</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<ol style="line-height:1.8;margin-bottom:0;padding-left:20px;">
			<li><strong>Guest test</strong> — log out of the storefront, search a part (e.g. MAZDA). Price should include guest margin (currently <?=epc_pm_h($guest_margin_percent);?>%).</li>
			<li><strong>Logged-in test</strong> — log in as the customer you assigned in Step 3. Search the same part — price should reflect their profile + brand/article rules.</li>
			<li><strong>Compare</strong> — use <a href="/<?=epc_pm_h($backend);?>/shop/prices/prices_edit">Prices edit</a> to preview site price per profile against warehouse base.</li>
			<li><strong>Hidden brands</strong> — if a brand is set to Hide, the part should not appear in search results for that profile.</li>
		</ol>
	</div>
</div>

<!-- Advanced: bulk brand visibility -->
<details class="epc-pm-section epc-pm-advanced" style="padding:0;border-style:solid;">
	<summary style="padding:16px 20px;background:#f8fafc;margin:0;">Advanced: bulk brand visibility (hide/show many brands at once)</summary>
	<div class="epc-pm-section-body" style="border-top:1px solid #e2e8f0;">
		<form method="post">
			<input type="hidden" name="csrf_guard_key" value="<?=$user_session["csrf_guard_key"];?>" />
			<input type="hidden" name="action" value="save_bulk_visibility" />
			<div class="row">
				<div class="col-md-4">
					<label>Profile</label>
					<select class="form-control" name="group_id">
						<?php foreach($profiles as $profile) { ?>
						<option value="<?=epc_pm_h($profile['id']);?>"><?=epc_pm_h(epc_pm_group_name($profile));?></option>
						<?php } ?>
					</select>
					<br>
					<label>Action</label>
					<select class="form-control" name="visible">
						<option value="0">Hide selected brands</option>
						<option value="1">Show selected brands</option>
					</select>
					<br>
					<button class="btn btn-default" type="submit">Apply to selected brands</button>
				</div>
				<div class="col-md-8">
					<label>Brands (Ctrl+click to select multiple)</label>
					<input class="form-control" id="epc_brand_visibility_filter" placeholder="Filter brands…" onkeyup="epcFilterBrandVisibilityList();" style="margin-bottom:8px;" />
					<select class="form-control" id="epc_brand_visibility_select" name="brands[]" multiple="multiple" size="12">
						<?php foreach($brands as $brand) { ?>
						<option value="<?=epc_pm_h($brand);?>"><?=epc_pm_h($brand);?></option>
						<?php } ?>
					</select>
				</div>
			</div>
		</form>
	</div>
</details>

<p class="text-muted epc-pm-nav-top" style="margin-top:20px;">
	<a href="/<?=epc_pm_h($backend);?>/control/cp-guideline"><i class="fa fa-book"></i> Full CP guideline</a>
	<a href="/<?=epc_pm_h($backend);?>/shop/prices/guide"><i class="fa fa-upload"></i> Price upload guide</a>
	<a href="/<?=epc_pm_h($backend);?>/shop/prices/prices_edit"><i class="fa fa-list"></i> Prices edit</a>
</p>

</div><!-- .epc-pm -->

<script>
function epcFilterBrandVisibilityList() {
	var filter = document.getElementById('epc_brand_visibility_filter').value.toUpperCase();
	var select = document.getElementById('epc_brand_visibility_select');
	for (var i = 0; i < select.options.length; i++) {
		var option = select.options[i];
		option.style.display = option.text.toUpperCase().indexOf(filter) !== -1 ? '' : 'none';
	}
}
</script>

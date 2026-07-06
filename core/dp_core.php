<?php
/*
 * @ https://EasyToYou.eu - IonCube v11 Decoder Online
 * @ PHP 5.0 & 5.6
 * @ Decoder version: 1.1.5
 * @ Release: 12/09/2024
 */

// Decoded file for php version 56.
defined("_ASTEXE_") or exit("No access");
$DP_Core = true;
if (!isset($DP_Config) || !($DP_Config instanceof DP_Config)) {
    $DP_Config = new DP_Config();
    $epcPortalFile = $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_portal.php";
    if (is_file($epcPortalFile)) {
        require_once $epcPortalFile;
        if (function_exists("epc_portal_apply_config") && empty($GLOBALS["epc_portal_config_applied"])) {
            epc_portal_apply_config($DP_Config);
            $GLOBALS["epc_portal_config_applied"] = true;
        }
    } elseif (is_file($_SERVER["DOCUMENT_ROOT"] . "/config.local.php")) {
        $epc_config_local = null;
        require $_SERVER["DOCUMENT_ROOT"] . "/config.local.php";
        if (isset($epc_config_local) && is_array($epc_config_local)) {
            foreach ($epc_config_local as $key => $value) {
                if (property_exists($DP_Config, $key)) {
                    $DP_Config->$key = $value;
                }
            }
        }
    }
}
if (!function_exists("epc_portal_apply_config")) {
    $epcPortalBootstrap = $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_portal.php";
    if (is_file($epcPortalBootstrap)) {
        require_once $epcPortalBootstrap;
    }
}
if (function_exists("epc_portal_apply_config") && empty($GLOBALS["epc_portal_config_applied"])) {
    epc_portal_apply_config($DP_Config);
    $GLOBALS["epc_portal_config_applied"] = true;
}
$domain_to_check = parse_url($DP_Config->domain_path, PHP_URL_HOST);
$request_host = isset($_SERVER["HTTP_HOST"]) ? (string) $_SERVER["HTTP_HOST"] : "";
if ($request_host !== "" && strpos($request_host, ":") !== false) {
	$request_host = explode(":", $request_host, 2)[0];
}
if ($request_host === "" && isset($_SERVER["SERVER_NAME"])) {
	$request_host = (string) $_SERVER["SERVER_NAME"];
}
// Model C: client domains are nginx aliases on the platform vhost — SERVER_NAME stays the primary host.
$epcAliasHost = $request_host !== "" && isset($_SERVER["SERVER_NAME"])
	&& strcasecmp($request_host, (string) $_SERVER["SERVER_NAME"]) !== 0;
$epcDomainMatchesRequest = $request_host !== "" && is_string($domain_to_check) && $domain_to_check !== ""
	&& strcasecmp($domain_to_check, $request_host) === 0;
if (!$epcAliasHost && !$epcDomainMatchesRequest && $domain_to_check != $_SERVER["SERVER_NAME"]) {
    exit("License error 1.01: Wrong value of domain_path field");
}
if ($domain_to_check != $request_host && strcasecmp((string) $domain_to_check, $request_host) !== 0) {
    exit("License error 1.02: Wrong value of domain_path field");
}
$headers_array = function_exists("getallheaders") ? array_change_key_case(getallheaders(), CASE_LOWER) : array();
$host_by_getallheaders = isset($headers_array["host"]) ? (string) $headers_array["host"] : $request_host;
if ($host_by_getallheaders !== "" && strpos($host_by_getallheaders, ":") !== false) {
	$host_by_getallheaders = explode(":", $host_by_getallheaders, 2)[0];
}
if ($domain_to_check != $host_by_getallheaders && strcasecmp((string) $domain_to_check, $host_by_getallheaders) !== 0 && !$epcAliasHost) {
    exit("License error 1.03: Wrong value of domain_path field");
}
if(isset($DP_Config->domain_lics)) {
    $domain_in_list = false;
    foreach ($DP_Config->domain_lics as $key => $domain_str) {
        if($domain_to_check != $domain_str) {
            if(strpos($domain_str, "*.") === 0) {
                $domain_to_check_parts = explode(".", $domain_to_check);
                $domain_to_check_up = "*";
                for ($i = 1; $i < count($domain_to_check_parts); $i++) {
                    $domain_to_check_up = $domain_to_check_up . "." . $domain_to_check_parts[$i];
                }
                if($domain_to_check_up == $domain_str) {
                    $domain_in_list = true;
                    break;
                }
            }
        } else {
            $domain_in_list = true;
            break;
        }
    }
    if(!$domain_in_list) {
        exit("License error 1.04: Wrong value of domain_path field");
    }
    $domain_to_check = "";
    foreach ($DP_Config->domain_lics as $key => $domain_str) {
        if($domain_to_check != "") {
            $domain_to_check = $domain_to_check . ";";
        }
        $domain_to_check = $domain_to_check . $domain_str;
    }
}
$defined_vars = 5;
if(isset($db_link) || file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_pages.php") || file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_pages_tree.php") || file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_brends.php") || file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_marks.php") || isset($array_pages_url) || isset($REQUEST_URI) || isset($generator_pages_settings) || isset($settings) || isset($page_exists)) {
    $modules_found = array();
    if(file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_pages.php")) {
        array_push($modules_found, array("id" => "40", "key" => md5("SomE vALue BEFore 2020.11.30 " . $domain_to_check . " some-valueafter")));
    }
    if(file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_brends.php")) {
        array_push($modules_found, array("id" => "52", "key" => md5("SomE BraNdS vALue BEFore 2020.11.30 " . $domain_to_check . " some-valueafter HeRe")));
    }
    if(file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_marks.php")) {
        array_push($modules_found, array("id" => "53", "key" => md5("2020.11.30 SomE vALue 53 BEFore " . $domain_to_check . " vALue 53 some-valueafter vALue 53")));
    }
    if(file_exists($_SERVER["DOCUMENT_ROOT"] . "/generator_pages_tree.php")) {
        array_push($modules_found, array("id" => "87", "key" => md5("SomE vALue 878787 BEFore 2020.11.30 " . $domain_to_check . " some-valueafter 87878787 vALue")));
    }
    if(count($modules_found) == 0) {
        exit("License error 0.0.1");
    }
    foreach ($modules_found as $key => $module) {
        $module_license_file = @fopen($_SERVER["DOCUMENT_ROOT"] . "/license/module_" . $module["id"] . "_license.lic", "r");
        if($module_license_file == false) {
            exit("License error 0." . $module["id"] . ".1");
        }
        $record = fgets($module_license_file, 4096);
        fclose($module_license_file);
        $record = str_replace("\n", "", $record);
        if($record != $module["key"]) {
            exit("License error 0." . $module["id"] . ".2");
        }
    }
    unset($modules_found);
    unset($defined_vars);
}
$DP_Module_array = array();
if (!isset($DP_Config) || !($DP_Config instanceof DP_Config)) {
    $DP_Config = new DP_Config();
}
if (!function_exists("epc_portal_apply_config")) {
    require_once $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_portal.php";
}
if (empty($GLOBALS["epc_portal_config_applied"])) {
	epc_portal_apply_config($DP_Config);
	$GLOBALS["epc_portal_config_applied"] = true;
}
if (function_exists('epc_portal_demo_finalize_runtime')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	epc_portal_demo_finalize_runtime($DP_Config);
}
if (isset($GLOBALS['epc_db_link']) && $GLOBALS['epc_db_link'] instanceof PDO) {
	$db_link = $GLOBALS['epc_db_link'];
} else {
try {
    $db_link = new PDO("mysql:host=" . $DP_Config->host . ";dbname=" . $DP_Config->db, $DP_Config->user, $DP_Config->password, array(PDO::ATTR_TIMEOUT => 5));
	$GLOBALS['epc_db_link'] = $db_link;
} catch (PDOException $e) {
    if (!empty($GLOBALS['epc_demo_cp_context']) && function_exists('epc_portal_demo_cp_exit_db_error')) {
        epc_portal_demo_cp_exit_db_error($e);
    }
    exit("No DB connect");
}
}
$db_link->query("SET NAMES utf8;");
$multilang_params = multilang_init();
if (function_exists('epc_portal_demo_finalize_runtime')) {
	epc_portal_demo_finalize_runtime($DP_Config, $multilang_params);
}
$DP_Lang = $multilang_params["lang"];
$license_file = @fopen($_SERVER["DOCUMENT_ROOT"] . "/license/license.lic", "r");
if($license_file == false) {
    exit("License error 2: License not found");
}
$license_id = "";
$lisense_key = "";
$lisense_expired = "";
while (!feof($license_file)) {
    $record = fgets($license_file, 4096);
    $record = str_replace("\n", "", $record);
    $record = explode(":", $record);
    switch ($record[0]) {
        case "license":
            $license_id = $record[1];
            break;
        case "key":
            $lisense_key = $record[1];
            break;
        case "expired":
            $lisense_expired = $record[1];
            break;
    }
}
//if($license_id == "" || $lisense_key == "" || $lisense_expired == "") {
//    exit("License error 3: License has incorrect structure");
//}
if(isset($DP_Config->products_table_mode)) {
    $docpart_type = "";
    if(isset($DP_Config->wholesaler)) {
        $docpart_type = "wholesaler";
    } else {
        $suppliers_dir_content = scandir($_SERVER["DOCUMENT_ROOT"] . "/content/shop/docpart/suppliers_handlers");
        foreach ($suppliers_dir_content as $key => $file_folder_name) {
            if($file_folder_name != "." && $file_folder_name != ".." && $file_folder_name != "prices" && $file_folder_name != "treelax_catalogue") {
                $docpart_type = "standart";
                break;
            }
        }
        if($docpart_type == "") {
            $docpart_type = "base";
        }
    }
    switch ($docpart_type) {
        case "base":
            $key1 = md5(strlen($domain_to_check) . $license_id . $lisense_expired . " а тут теперь у нас отдельно формируется лицензия для пакета Базовый. HeRe bASe 2*02!106--02. ThE the cheapest TyPe");
            $key2 = md5($domain_to_check . " NOt StanDArt. basE. NOT wHoLeSaLeR here " . $domain_to_check . strlen($domain_to_check));
            $key3 = md5(strlen($domain_to_check) . $domain_to_check . " 7400 rub");
            break;
        case "standart":
            $key1 = md5(strlen($domain_to_check) . $license_id . $lisense_expired . "последовательность ast random Ltd");
            $key2 = md5($domain_to_check . "другая последовательность" . $domain_to_check . strlen($domain_to_check));
            $key3 = md5(strlen($domain_to_check) . $domain_to_check);
            break;
        case "wholesaler":
            $key1 = md5(strlen($domain_to_check) . $license_id . $lisense_expired . "здесь теперь у нас отдельно формируется лицензия для пакета Оптовик. HeRe wholeSAler KeYYYY 2021.06_02");
            $key2 = md5($domain_to_check . " NOt StanDArt Not basE. wHoLeSaLeR " . $domain_to_check . strlen($domain_to_check));
            $key3 = md5(strlen($domain_to_check) . $domain_to_check . "WoW");
            break;
        default:
            exit("License error 3.1: CMS has incorrect structure");
    }
} elseif(isset($DP_Config->product_url)) {
    $key1 = md5(strlen($domain_to_check) . $license_id . $lisense_expired . "последовательность intask for expancart 123 5 ok");
    $key2 = md5($domain_to_check . "еще последовательность 4 5 for экспанкарт 1" . $domain_to_check . strlen($domain_to_check));
    $key3 = md5(strlen($domain_to_check) . "e1x1p 5an7ca1 rt" . $domain_to_check);
} else {
    $key1 = md5(strlen($domain_to_check) . $license_id . $lisense_expired . "последовательность ast cms random Ltd");
    $key2 = md5($domain_to_check . "для cms" . $domain_to_check . strlen($domain_to_check));
    $key3 = md5(strlen($domain_to_check) . $domain_to_check . $lisense_expired . $license_id . $license_id);
}
//if($key1 . $key2 . $key3 != $lisense_key) {
//  exit("License error 4: License key not valid");
//}
if((int) $lisense_expired != 0 && $lisense_expired < time()) {
    exit("License error 5: License expired");
}
$current_template_query = $db_link->prepare("SELECT * FROM `templates` WHERE `current` = ? AND `is_frontend` = ?;");
$current_template_query->execute(array(1, $isFrontMode));
$current_template_record = $current_template_query->fetch();
$DP_Template = new DP_Template();
$epcCpLightBootstrapEarly = !empty($GLOBALS['epc_cp_light_bootstrap']) && (int) $isFrontMode === 0;
if ($current_template_record === false) {
	if ((int) $isFrontMode === 1 && function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_router.php';
		if (epc_ecomae_platform_render_standalone()) {
			exit;
		}
	}
	if ($epcCpLightBootstrapEarly) {
		$current_template_record = array(
			'id' => 1,
			'name' => 'bootstrap_admin',
			'positions' => '[]',
			'phone_support' => 0,
			'tablet_support' => 0,
			'data_value' => '{}',
		);
	} elseif ((int) $isFrontMode === 0 && !empty($GLOBALS['epc_demo_cp_context']) && function_exists('epc_portal_demo_cp_exit_schema_error')) {
		epc_portal_demo_cp_exit_schema_error();
	} elseif ((int) $isFrontMode === 0 && function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		$current_template_record = array(
			'id' => 1,
			'name' => 'bootstrap_admin',
			'positions' => '[]',
			'phone_support' => 0,
			'tablet_support' => 0,
			'data_value' => '{}',
		);
	} elseif ((int) $isFrontMode === 0 && function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		$current_template_record = array(
			'id' => 1,
			'name' => 'bootstrap_admin',
			'positions' => '[]',
			'phone_support' => 0,
			'tablet_support' => 0,
			'data_value' => '{}',
		);
	} else {
		exit("No DB connect");
	}
}
$DP_Template->id = (int) $current_template_record["id"];
$DP_Template->name = $current_template_record["name"];
$DP_Template->positions = json_decode($current_template_record["positions"], true);
$DP_Template->html = "";
$DP_Template->phone_support = $current_template_record["phone_support"];
$DP_Template->tablet_support = $current_template_record["tablet_support"];
$DP_Template->data_value = json_decode($current_template_record["data_value"]);
$backendDir = trim((string) $DP_Config->backend_dir, '/');
if ((int) $isFrontMode === 0 && $backendDir === '') {
	$backendDir = 'cp';
}
if ((int) $isFrontMode === 0 && $backendDir !== '') {
	$epcTplRoot = $_SERVER['DOCUMENT_ROOT'] . '/' . $backendDir . '/templates/';
	$epcTplDir = $epcTplRoot . $DP_Template->name;
	if (!is_dir($epcTplDir) && is_dir($epcTplRoot . 'bootstrap_admin')) {
		$DP_Template->name = 'bootstrap_admin';
	}
}
$epcUseErpShellTpl = false;
$epcCpLightBootstrap = !empty($GLOBALS['epc_cp_light_bootstrap']) && (int) $isFrontMode === 0;
if ((int) $isFrontMode === 0 && !$epcCpLightBootstrap) {
	$epcShellFile = $_SERVER["DOCUMENT_ROOT"] . "/content/shop/finance/epc_erp_cp_shell.php";
	if (is_file($epcShellFile)) {
		require_once $epcShellFile;
		epc_erp_cp_shell_maybe_set_cookie();
		epc_erp_cp_shell_maybe_redirect();
		$epcUseErpShellTpl = epc_erp_cp_shell_use_template();
	}
}
if ($epcCpLightBootstrap) {
	$DP_Template->html = '<!DOCTYPE html><html><head><docpart type="head" name="head" /></head><body><docpart type="main" name="main" /></body></html>';
} elseif ((int) $isFrontMode === 0 && $backendDir !== '') {
	if ($epcUseErpShellTpl) {
		$tpl_file_path = $backendDir . '/templates/' . $DP_Template->name . '/erp_desktop.php';
		if (!is_file($_SERVER["DOCUMENT_ROOT"] . "/" . $tpl_file_path)) {
			$tpl_file_path = $backendDir . '/templates/' . $DP_Template->name . '/desktop.php';
		}
	} else {
		$tpl_file_path = $backendDir . '/templates/' . $DP_Template->name . '/desktop.php';
	}
} else {
	$tpl_file_path = 'templates/' . $DP_Template->name . '/desktop.php';
}
if (!$epcCpLightBootstrap) {
if (!is_file($tpl_file_path)) {
	$tpl_file_path = $_SERVER['DOCUMENT_ROOT'] . '/' . ltrim(str_replace('\\', '/', $tpl_file_path), '/');
}
$tpl_file = @fopen($tpl_file_path, 'r');
if ($tpl_file === false && (int) $isFrontMode === 0 && $backendDir !== '' && $DP_Template->name === 'bootstrap') {
	$DP_Template->name = 'bootstrap_admin';
	if ($epcUseErpShellTpl) {
		$tpl_file_path = $backendDir . '/templates/bootstrap_admin/erp_desktop.php';
		if (!is_file($_SERVER['DOCUMENT_ROOT'] . '/' . $tpl_file_path)) {
			$tpl_file_path = $backendDir . '/templates/bootstrap_admin/desktop.php';
		}
	} else {
		$tpl_file_path = $backendDir . '/templates/bootstrap_admin/desktop.php';
	}
	if (!is_file($tpl_file_path)) {
		$tpl_file_path = $_SERVER['DOCUMENT_ROOT'] . '/' . ltrim(str_replace('\\', '/', $tpl_file_path), '/');
	}
	$tpl_file = @fopen($tpl_file_path, 'r');
}
if ($tpl_file === false) {
	exit('CP template missing');
}
$tpl_size = @filesize($tpl_file_path);
$tpl_file_string = ($tpl_size !== false && $tpl_size > 0) ? fread($tpl_file, $tpl_size) : '';
fclose($tpl_file);
$DP_Template->html = $tpl_file_string;
}
$url_route = urldecode(getPageRoute());
if($isFrontMode == 0) {
    $backend_prefix = (string)$DP_Config->backend_dir;
    if($backend_prefix !== '') {
        if(strpos($url_route, $backend_prefix . '/') === 0) {
            $url_route = substr($url_route, strlen($backend_prefix) + 1);
        } else if($url_route === $backend_prefix) {
            $url_route = '';
        } else if(strpos($url_route, '/' . $backend_prefix . '/') === 0) {
            $url_route = substr($url_route, strlen($backend_prefix) + 2);
        } else if($url_route === '/' . $backend_prefix) {
            $url_route = '';
        }
    }
    if(!empty($url_route) && $url_route[0] == "/") {
        $url_route = substr_replace($url_route, "", 0, 1);
    }
    if (!empty($GLOBALS['epc_demo_cp_context']) && !empty($GLOBALS['epc_demo_cp_site_key'])) {
        $demoRoute = 'demo/' . preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_cp_site_key']);
        if ($url_route === $demoRoute) {
            $url_route = '';
        } elseif (strpos($url_route, $demoRoute . '/') === 0) {
            $url_route = substr($url_route, strlen($demoRoute) + 1);
        }
    }
} elseif($multilang_params["multilang"] == true && !$multilang_params["is_just_domain"] && $multilang_params["is_lang_url_exists"]) {
    $url_route = substr($url_route, strlen($multilang_params["lang"]));
    if(!empty($url_route) && $url_route[0] == "/") {
        $url_route = substr_replace($url_route, "", 0, 1);
    }
}
if($url_route == "") {
    $page_record_query = $db_link->prepare("SELECT * FROM `content` WHERE `main_flag` = ? AND `published_flag` = ? AND `is_frontend` = ?;");
    $page_record_query->execute(array(1, 1, $isFrontMode));
} else {
    $page_record_query = $db_link->prepare("SELECT * FROM `content` WHERE `url` = ? AND `published_flag` = ? AND `is_frontend` = ?;");
    $page_record_query->execute(array($url_route, 1, $isFrontMode));
}
$page_record_array = $page_record_query->fetch();
if($page_record_array == false || isset($multilang_params["is_lang_url_exists"]) && $multilang_params["is_lang_url_exists"] == false) {
    $DP_Content = new DP_Content();
    $category_id = 0;
    $product_id = 0;
    if(!(isset($multilang_params["is_lang_url_exists"]) && $multilang_params["is_lang_url_exists"] == false)) {
        get_alternative_page();
    }
    if($DP_Content->alternative_flag != true) {
        if (!function_exists('epc_ecomae_platform_absorb_route') || !epc_ecomae_platform_absorb_route($url_route, $DP_Content, $isFrontMode)) {
            $DP_Content->content_type = "text";
            $DP_Content->title_tag = translate_str_by_id(3);
            $DP_Content->value = translate_str_by_id(3);
            $DP_Content->description_tag = translate_str_by_id(4755);
            $DP_Content->keywords_tag = translate_str_by_id(4755);
            $DP_Content->author_tag = translate_str_by_id(4755);
            $DP_Content->content = translate_str_by_id(4755);
            $DP_Content->service_data["error_page"] = 404;
        }
    }
} else {
    $DP_Content = new DP_Content();
    $DP_Content->id = $page_record_array["id"];
    $DP_Content->value = translate_str_by_id($page_record_array["value"]);
    $DP_Content->url = $page_record_array["url"];
    $DP_Content->content_type = $page_record_array["content_type"];
    $DP_Content->description = translate_str_by_id($page_record_array["description"]);
    $DP_Content->title_tag = translate_str_by_id($page_record_array["title_tag"]);
    if($DP_Content->title_tag == "") {
        $DP_Content->title_tag = $DP_Content->value;
    }
    $DP_Content->description_tag = translate_str_by_id($page_record_array["description_tag"]);
    $DP_Content->keywords_tag = translate_str_by_id($page_record_array["keywords_tag"]);
    $DP_Content->author_tag = translate_str_by_id($page_record_array["author_tag"]);
    $DP_Content->robots_tag = $page_record_array["robots_tag"];
    $DP_Content->main_flag = (bool) $page_record_array["main_flag"];
    if (
        $DP_Content->url === 'shop/part_search'
        && empty($DP_Content->service_data['article_search_chpu'])
        && $DP_Content->robots_tag === ''
    ) {
        $DP_Content->robots_tag = 'noindex, follow';
    }
    if (
        $DP_Content->url === 'shop/part_search'
        && !empty($DP_Config->chpu_search_config['chpu_search_on'])
        && isset($_GET['article'])
        && trim((string) $_GET['article']) !== ''
        && (!isset($_GET['brend']) || trim((string) $_GET['brend']) === '')
    ) {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
        $epc_article_raw = trim((string) $_GET['article']);
        $epc_lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? $multilang_params['lang_href'] : '/en';
        $epc_single_brand_url = epc_chpu_single_brand_redirect_url($db_link, $DP_Config, $epc_article_raw, $epc_lang_href);
        if ($epc_single_brand_url !== '') {
            header('Location: ' . $epc_single_brand_url, true, 302);
            exit;
        }
        $epc_brands_seg = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url'])
            ? (string) $DP_Config->chpu_search_config['level_2']['mode_1']['url']
            : 'brands';
        $epc_picker_url = epc_chpu_build_part_url($DP_Config, $epc_lang_href, '', $epc_article_raw);
        if ($epc_picker_url !== '') {
            header('Location: ' . $epc_picker_url, true, 302);
            exit;
        }
    }
    if($DP_Content->content_type == "php") {
        $php_path = str_replace(array("<backend_dir>"), $DP_Config->backend_dir, $_SERVER["DOCUMENT_ROOT"] . $page_record_array["content"]);
        if(file_exists($php_path)) {
            $php_file = fopen($php_path, "r");
            $DP_Content->content = fread($php_file, filesize($php_path));
            fclose($php_file);
        } else {
            $DP_Content->content = "<div class=\"error_message\">" . translate_str_by_id(4756) . "</div>";
        }
    } elseif($DP_Content->content_type == "text") {
        $DP_Content->content = translate_str_by_id($page_record_array["content"]);
    }
    if($page_record_array["modules_array"] != "") {
        $DP_Content->modules_array = json_decode($page_record_array["modules_array"], true);
    }
    $DP_Content->css_js = str_replace("<template_dir>", $DP_Template->name, $page_record_array["css_js"]);
}
if(true) {
    if (!$epcCpLightBootstrap) {
    $for_all_modules_query = $db_link->prepare("SELECT `id` FROM `modules` WHERE `for_all` = ? AND `activated`=? AND `is_frontend` = ?;");
    $for_all_modules_query->execute(array(1, 1, $isFrontMode));
    while ($for_all_module = $for_all_modules_query->fetch()) {
        if(array_search($for_all_module["id"], $DP_Content->modules_array) === false) {
            array_push($DP_Content->modules_array, $for_all_module["id"]);
        }
    }
    // Batch-load all modules in a single query instead of N+1 per-module SELECTs
    $epc_module_ids = array_values(array_unique(array_map('intval', $DP_Content->modules_array)));
    if (count($epc_module_ids) > 0) {
        $epc_mod_placeholders = implode(',', array_fill(0, count($epc_module_ids), '?'));
        $epc_mod_params = $epc_module_ids;
        $epc_mod_params[] = 1;
        $epc_mod_params[] = $isFrontMode;
        $epc_batch_query = $db_link->prepare("SELECT * FROM `modules` WHERE `id` IN (" . $epc_mod_placeholders . ") AND `activated`=? AND `is_frontend` = ?;");
        $epc_batch_query->execute($epc_mod_params);
        $epc_modules_map = array();
        while ($epc_mod_row = $epc_batch_query->fetch(PDO::FETCH_ASSOC)) {
            $epc_modules_map[(int) $epc_mod_row['id']] = $epc_mod_row;
        }
        for ($i = 0; $i < count($epc_module_ids); $i++) {
            $module_record = isset($epc_modules_map[$epc_module_ids[$i]]) ? $epc_modules_map[$epc_module_ids[$i]] : null;
            if ($module_record === null) {
                continue;
            }
            $DP_Module = new DP_Module();
            $DP_Module->id = $module_record["id"];
            $DP_Module->caption = translate_str_by_id($module_record["caption"]);
            $DP_Module->content_type = $module_record["content_type"];
            $DP_Module->position = $module_record["position"];
            $DP_Module->show_caption = $module_record["show_caption"];
            $DP_Module->css_js = $module_record["css_js"];
            $DP_Module->order = $module_record["order"];
            if($DP_Module->content_type == "php") {
                $php_path = str_replace(array("<backend_dir>"), $DP_Config->backend_dir, $_SERVER["DOCUMENT_ROOT"] . $module_record["content"]);
                if(file_exists($php_path)) {
                    $php_file = fopen($php_path, "r");
                    $DP_Module->content = fread($php_file, filesize($php_path));
                    fclose($php_file);
                } else {
                    $DP_Module->content = "<div class=\"error_message\">" . translate_str_by_id(4757) . "</div>";
                }
            } elseif($DP_Module->content_type == "text") {
                $DP_Module->content = $module_record["content"];
            }
            if((bool) $DP_Module->show_caption == true) {
                $DP_Module->content = "<h3 class=\"module_caption\">" . $DP_Module->caption . "</h3>" . $DP_Module->content;
            }
            $DP_Module->content = str_replace("<module_id>", $DP_Module->id, $DP_Module->content);
            array_push($DP_Module_array, $DP_Module);
        }
        unset($epc_modules_map, $epc_module_ids, $epc_mod_placeholders, $epc_mod_params, $epc_batch_query);
    }
    usort($DP_Module_array, "sort_by_order");
    }
}
$plugins_query = $db_link->prepare("SELECT * FROM `plugins` WHERE `activated`=? AND `is_frontend`=? ORDER BY `order`;");
$plugins_query->execute(array(1, $isFrontMode));
while ($plugin_record = $plugins_query->fetch()) {
    $plugin_content = "";
    $php_path = str_replace(array("<backend_dir>"), $DP_Config->backend_dir, $_SERVER["DOCUMENT_ROOT"] . $plugin_record["source"]);
    if(file_exists($php_path)) {
        $php_file = fopen($php_path, "r");
        $plugin_content = fread($php_file, filesize($php_path));
        fclose($php_file);
    }
    eval(" ?>" . $plugin_content . "<?php ");
}
if(isset($DP_Content->service_data["error_page"]) && $DP_Content->content_type == "php") {
    $php_path = str_replace(array("<backend_dir>"), $DP_Config->backend_dir, $_SERVER["DOCUMENT_ROOT"] . $DP_Content->content);
    if(file_exists($php_path)) {
        $php_file = fopen($php_path, "r");
        $DP_Content->content = fread($php_file, filesize($php_path));
        fclose($php_file);
    } else {
        $DP_Content->content = "<div class=\"error_message\">" . translate_str_by_id(4756) . "</div>";
    }
}
if (
    function_exists('epc_portal_electronics_retail_enabled')
    && epc_portal_electronics_retail_enabled()
) {
    $epc_er_helpers_early = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_helpers.php';
    if (is_file($epc_er_helpers_early)) {
        require_once $epc_er_helpers_early;
        if (function_exists('epc_electronics_retail_apply_seo')) {
            epc_electronics_retail_apply_seo($DP_Content);
        }
    }
}
if (
    function_exists('epc_portal_consulting_primeinvest_enabled')
    && epc_portal_consulting_primeinvest_enabled()
) {
    $epc_cpi_helpers_early = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_helpers.php';
    if (is_file($epc_cpi_helpers_early)) {
        require_once $epc_cpi_helpers_early;
        if (function_exists('epc_cpi_apply_seo')) {
            epc_cpi_apply_seo($DP_Content);
        }
    }
}
if (
    function_exists('epc_portal_fashion_retail_namshi_enabled')
    && epc_portal_fashion_retail_namshi_enabled()
) {
    $epc_frn_helpers_early = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_fashion_retail_namshi_helpers.php';
    if (is_file($epc_frn_helpers_early)) {
        require_once $epc_frn_helpers_early;
        if (function_exists('epc_fashion_retail_namshi_apply_seo')) {
            epc_fashion_retail_namshi_apply_seo($DP_Content);
        }
    }
}
if (
    function_exists('epc_portal_jewellery_retail_kiyasha_enabled')
    && epc_portal_jewellery_retail_kiyasha_enabled()
) {
    $epc_jrk_helpers_early = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_helpers.php';
    if (is_file($epc_jrk_helpers_early)) {
        require_once $epc_jrk_helpers_early;
        if (function_exists('epc_jewellery_retail_kiyasha_apply_seo')) {
            epc_jewellery_retail_kiyasha_apply_seo($DP_Content);
        }
    }
}
global $db_link;
$epcSeoIndexingFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';
if (is_file($epcSeoIndexingFile) && $db_link instanceof PDO) {
    require_once $epcSeoIndexingFile;
    if (function_exists('epc_seo_apply_storefront_content_meta')) {
        epc_seo_apply_storefront_content_meta($DP_Content, $db_link);
    }
}
$css_js_tags = "";
$epcCpScriptRelocate = ((int) $isFrontMode === 0);
if ($epcCpScriptRelocate) {
    $epcCpScriptRelocateFile = $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_cp_script_relocate.php";
    if (is_file($epcCpScriptRelocateFile)) {
        require_once $epcCpScriptRelocateFile;
        if (function_exists("epc_cp_footer_scripts_reset")) {
            epc_cp_footer_scripts_reset();
        }
        if (function_exists("epc_cp_prepare_cp_page_content")) {
            $DP_Content->content = epc_cp_prepare_cp_page_content((string) $DP_Content->content);
        }
    } else {
        $epcCpScriptRelocate = false;
    }
}
$DP_Template->html = str_replace("<docpart type=\"main\" name=\"main\" />", $DP_Content->content, $DP_Template->html);
$css_js_tags .= $DP_Content->css_js;
$title = "";
// Operator console (super-CP host) is the platform's Business Operation System,
// not a tenant storefront — present it as "ECOM AE · BOS" instead of inheriting
// the tenant site name. Tenant storefronts/CPs keep their own name untouched.
$epc_title_site_name = translate_str_by_id($DP_Config->site_name);
if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
    $epc_title_site_name = "ECOM AE \xC2\xB7 BOS";
}
if(filter_var($DP_Config->show_page_title, FILTER_VALIDATE_BOOLEAN) && filter_var($DP_Config->show_site_name, FILTER_VALIDATE_BOOLEAN)) {
    if(filter_var($DP_Config->page_title_first, FILTER_VALIDATE_BOOLEAN)) {
        $title = $DP_Content->title_tag . " - " . $epc_title_site_name;
    } else {
        $title = $epc_title_site_name . " - " . $DP_Content->title_tag;
    }
} elseif(filter_var($DP_Config->show_page_title, FILTER_VALIDATE_BOOLEAN)) {
    $title = $DP_Content->title_tag;
} elseif(filter_var($DP_Config->show_site_name, FILTER_VALIDATE_BOOLEAN)) {
    $title = $epc_title_site_name;
}
if($title != "") {
    $title = "<title>" . $title . "</title>\n";
}
$keywords = "";
if($DP_Content->keywords_tag != "") {
    $keywords = "<meta name=\"keywords\" content=\"" . $DP_Content->keywords_tag . "\">\n";
} elseif(translate_str_by_id($DP_Config->keywords_tag) != "") {
    $keywords = "<meta name=\"keywords\" content=\"" . translate_str_by_id($DP_Config->keywords_tag) . "\">\n";
}
$description = "";
if($DP_Content->description_tag != "") {
    $description = "<meta name=\"description\" content=\"" . $DP_Content->description_tag . "\">\n";
} elseif(translate_str_by_id($DP_Config->description_tag) != "") {
    $description = "<meta name=\"description\" content=\"" . translate_str_by_id($DP_Config->description_tag) . "\">\n";
}
$author = "";
if($DP_Content->author_tag != "") {
    $author = "<meta name=\"author\" content=\"" . $DP_Content->author_tag . "\">";
}
$robots = "";
if (
    isset($DP_Content->service_data['error_page'])
    && (int) $DP_Content->service_data['error_page'] === 404
    && $DP_Content->robots_tag === ''
) {
    $DP_Content->robots_tag = 'noindex, nofollow';
}
if($DP_Content->robots_tag != "") {
    $robots = "<meta name=\"robots\" content=\"" . $DP_Content->robots_tag . "\">";
}
if (!empty($DP_Content->service_data['epc_seo_page_title'])) {
    $title = "<title>" . htmlspecialchars((string) $DP_Content->service_data['epc_seo_page_title'], ENT_QUOTES, 'UTF-8') . "</title>\n";
}
$epc_seo_head_extras = '';
if (function_exists('epc_seo_head_extras_html')) {
    $epc_seo_head_extras = epc_seo_head_extras_html($DP_Content, $DP_Config);
}
$css_js_tags = $title . $keywords . $description . $author . $robots . "\n" . $epc_seo_head_extras . $css_js_tags;
$positions_html_array = array();
for ($i = 0; $i < count($DP_Module_array); $i++) {
    if(!isset($positions_html_array[$DP_Module_array[$i]->position])) {
        $positions_html_array[$DP_Module_array[$i]->position] = "";
    }
    $positions_html_array[$DP_Module_array[$i]->position] .= $DP_Module_array[$i]->content;
    $css_js_array = explode("\n", $DP_Module_array[$i]->css_js);
    for ($cj = 0; $cj < count($css_js_array); $cj++) {
        $css_js_array[$cj] = str_replace("<template_dir>", $DP_Template->name, $css_js_array[$cj]);
        if($css_js_array[$cj] == "") {
            continue;
        }
        if(stristr($css_js_tags, $css_js_array[$cj]) == false) {
            $css_js_tags .= $css_js_array[$cj] . "\n";
        }
    }
}
if ($epcCpScriptRelocate && function_exists("epc_cp_prepare_cp_modules")) {
    epc_cp_prepare_cp_modules($positions_html_array);
}
foreach ($positions_html_array as $position => $modules_html) {
    $DP_Template->html = str_replace("<docpart type=\"module\" name=\"" . $position . "\" />", $modules_html, $DP_Template->html);
}
$DP_Template->html = str_replace("<docpart type=\"head\" name=\"head\" />", $css_js_tags, $DP_Template->html);
for ($i = 0; $i < count($DP_Template->positions); $i++) {
    $DP_Template->html = str_replace("<docpart type=\"" . $DP_Template->positions[$i]["type"] . "\" name=\"" . $DP_Template->positions[$i]["name"] . "\" />", "", $DP_Template->html);
}
$DP_Template->html = str_replace(array("<backend_dir>"), $DP_Config->backend_dir, $DP_Template->html);
if($DP_Content->id == 0 && $DP_Content->alternative_flag != true) {
    header("HTTP/1.0 404 Not Found");
}
// Defaults for template/eval'd code that may use pagination variables
if (!isset($p)) {
    $p = (isset($DP_Config->list_page_limit) && $DP_Config->list_page_limit !== '') ? (int)$DP_Config->list_page_limit : 20;
}
if (!isset($s_page)) {
    $s_page = (isset($_GET['s_page']) && $_GET['s_page'] !== '') ? (int)$_GET['s_page'] : 0;
}
if (
    function_exists('epc_portal_electronics_retail_enabled')
    && epc_portal_electronics_retail_enabled()
) {
    $epc_er_helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_helpers.php';
    if (is_file($epc_er_helpers)) {
        require_once $epc_er_helpers;
        if (function_exists('epc_electronics_retail_apply_seo')) {
            epc_electronics_retail_apply_seo($DP_Content);
        }
        if (function_exists('epc_electronics_retail_patch_template_html')) {
            $DP_Template->html = epc_electronics_retail_patch_template_html($DP_Template->html);
        }
    }
}
if (
    function_exists('epc_portal_consulting_primeinvest_enabled')
    && epc_portal_consulting_primeinvest_enabled()
) {
    $epc_cpi_helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_helpers.php';
    if (is_file($epc_cpi_helpers)) {
        require_once $epc_cpi_helpers;
        if (function_exists('epc_cpi_apply_seo')) {
            epc_cpi_apply_seo($DP_Content);
        }
        if (function_exists('epc_cpi_patch_template_html')) {
            $DP_Template->html = epc_cpi_patch_template_html($DP_Template->html);
        }
    }
}
if (
    function_exists('epc_portal_fashion_retail_namshi_enabled')
    && epc_portal_fashion_retail_namshi_enabled()
) {
    $epc_frn_helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_fashion_retail_namshi_helpers.php';
    if (is_file($epc_frn_helpers)) {
        require_once $epc_frn_helpers;
        if (function_exists('epc_fashion_retail_namshi_apply_seo')) {
            epc_fashion_retail_namshi_apply_seo($DP_Content);
        }
        if (function_exists('epc_fashion_retail_namshi_patch_template_html')) {
            $DP_Template->html = epc_fashion_retail_namshi_patch_template_html($DP_Template->html);
        }
    }
}
if (
    function_exists('epc_portal_jewellery_retail_kiyasha_enabled')
    && epc_portal_jewellery_retail_kiyasha_enabled()
) {
    $epc_jrk_helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_helpers.php';
    if (is_file($epc_jrk_helpers)) {
        require_once $epc_jrk_helpers;
        if (function_exists('epc_jewellery_retail_kiyasha_apply_seo')) {
            epc_jewellery_retail_kiyasha_apply_seo($DP_Content);
        }
        if (function_exists('epc_jewellery_retail_kiyasha_patch_template_html')) {
            $DP_Template->html = epc_jewellery_retail_kiyasha_patch_template_html($DP_Template->html);
        }
    }
}
// No longer needed — splash page uses simple meta-refresh, no sessionStorage counter
if ($epcCpScriptRelocate && function_exists("epc_cp_finalize_cp_html")) {
    ob_start();
    eval(" ?>" . $DP_Template->html . "<?php ");
    $epcCpEvalHtml = ob_get_clean();
    echo epc_cp_finalize_cp_html($epcCpEvalHtml);
} else {
    eval(" ?>" . $DP_Template->html . "<?php ");
}
$db_link = NULL;

?>
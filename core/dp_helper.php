<?php
/*
 * @ https://EasyToYou.eu - IonCube v11 Decoder Online
 * @ PHP 5.0 & 5.6
 * @ Decoder version: 1.1.5
 * @ Release: 12/09/2024
 */

// Decoded file for php version 56.
defined("_ASTEXE_") or exit("No access");
require_once $_SERVER["DOCUMENT_ROOT"] . "/lang/dp_lang.php";
/**
 * Copyright © 2015, AST Ltd. All Rights Reserved.
 * 
 * Вспомогательные функции
*/
function getPageUrl()
{
    $pageURL = "http";
    if(isset($_SERVER["HTTPS"]) && $_SERVER["HTTPS"] == "on") {
        $pageURL .= "s";
    }
    $pageURL .= "://";
    if($_SERVER["SERVER_PORT"] != "80") {
        $pageURL .= $_SERVER["SERVER_NAME"] . ":" . $_SERVER["SERVER_PORT"] . $_SERVER["REQUEST_URI"];
    } else {
        $pageURL .= $_SERVER["SERVER_NAME"] . $_SERVER["REQUEST_URI"];
    }
    return $pageURL;
}
function getPageRoute()
{
    $page_path = parse_url(getpageurl(), PHP_URL_PATH);
    $page_path = str_replace("index.php/", "", $page_path);
    if($page_path[0] == "/") {
        $page_path = substr_replace($page_path, "", 0, 1);
    }
    if(0 < strlen($page_path) && $page_path[strlen($page_path) - 1] == "/") {
        $page_path = substr_replace($page_path, "", strlen($page_path) - 1, 1);
    }
    return $page_path;
}
function sort_by_order($f1, $f2)
{
    if($f1->order < $f2->order) {
        return -1;
    }
    if($f2->order < $f1->order) {
        return 1;
    }
    return 0;
}
function get_alternative_page()
{
    global $DP_Content;
    global $DP_Config;
    global $db_link;
    global $url_route;
    global $category_id;
    global $product_id;
    if (is_file($_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_epartscart_storefront.php")) {
        require_once $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_epartscart_storefront.php";
        if ($db_link instanceof PDO && function_exists("epc_epartscart_apai_category_redirect")) {
            $epcEpcApaiRedirect = epc_epartscart_apai_category_redirect($db_link, (string) $url_route);
            if ($epcEpcApaiRedirect !== "") {
                header("Location: " . $epcEpcApaiRedirect, true, 301);
                exit;
            }
        }
    }
    $category_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `url` = ?;");
    $category_query->execute(array($url_route));
    $category = $category_query->fetch();
    if($category != false) {
        $DP_Content->value = translate_str_by_id($category["value"]);
        $DP_Content->url = $category["url"];
        $DP_Content->content_type = "category";
        $DP_Content->title_tag = translate_str_by_id($category["title_tag"]);
        if($DP_Content->title_tag == "") {
            $DP_Content->title_tag = $DP_Content->value;
        }
        $DP_Content->description_tag = translate_str_by_id($category["description_tag"]);
        $DP_Content->keywords_tag = translate_str_by_id($category["keywords_tag"]);
        if(isset($category["author_tag"])) {
            $DP_Content->author_tag = $category["author_tag"];
        } else {
            $DP_Content->author_tag = "";
        }
        $DP_Content->robots_tag = $category["robots_tag"];
        $DP_Content->modules_array = array(31, 34);
        $DP_Content->alternative_flag = true;
        $category_id = $category["id"];
        $php_file = fopen("content/shop/catalogue/catalogue_for_customer.php", "r");
        $DP_Content->content = fread($php_file, filesize("content/shop/catalogue/catalogue_for_customer.php"));
        fclose($php_file);
    } else {
        $url_route_components = explode("/", $url_route);
        $product = false;
        $category = false;
        $product_url = "";
        if (is_file($_SERVER["DOCUMENT_ROOT"] . "/content/shop/price_engine/epc_auto_price_categories.php")) {
            require_once $_SERVER["DOCUMENT_ROOT"] . "/content/shop/price_engine/epc_auto_price_categories.php";
            $resolved = epc_apai_resolve_catalogue_product_route(
                $db_link,
                $url_route,
                (string) ($DP_Config->product_url ?? "alias")
            );
            if (is_array($resolved)) {
                $category = $resolved["category"];
                $product = $resolved["product"];
                $alias = (string) ($product["alias"] ?? "");
                $product_url = $alias !== "" ? $alias : (string) ($product["id"] ?? "");
            }
        }
        if ($product == false) {
            $category_url = "";
            for ($i = 0; $i < count($url_route_components) - 1; $i++) {
                if(0 < $i) {
                    $category_url .= "/";
                }
                $category_url .= $url_route_components[$i];
            }
            $product_url = $url_route_components[count($url_route_components) - 1];
            $category_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `url` = ?;");
            $category_query->execute(array($category_url));
            $category = $category_query->fetch();
            if($category != false) {
                $category_id = $category["id"];
                if($DP_Config->product_url == "id") {
                    $product_query = $db_link->prepare("SELECT * FROM `shop_catalogue_products` WHERE `category_id` = ? AND `id`=?;");
                    $product_query->execute(array($category_id, $product_url));
                } else {
                    $product_query = $db_link->prepare("SELECT * FROM `shop_catalogue_products` WHERE `category_id` = ? AND `alias`=?;");
                    $product_query->execute(array($category_id, $product_url));
                }
                $product = $product_query->fetch();
            }
        }
            if($product != false) {
                $DP_Content->value = translate_str_by_id($product["caption"]);
                $DP_Content->url = $product_url;
                $DP_Content->content_type = "product";
                $DP_Content->title_tag = translate_str_by_id($product["title_tag"]);
                if($DP_Content->title_tag == "") {
                    $DP_Content->title_tag = $DP_Content->value;
                }
                $DP_Content->description_tag = translate_str_by_id($product["description_tag"]);
                $DP_Content->keywords_tag = translate_str_by_id($product["keywords_tag"]);
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = $product["robots_tag"];
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $product_id = $product["id"];
                $product_page_path = "content/shop/catalogue/product_page_for_customer.php";
                if (is_file($_SERVER["DOCUMENT_ROOT"] . "/content/shop/price_engine/epc_auto_price_storefront.php")) {
                    require_once $_SERVER["DOCUMENT_ROOT"] . "/content/shop/price_engine/epc_auto_price_storefront.php";
                    if ($db_link instanceof PDO && function_exists("epc_apai_is_warehouse_auto_parts_storefront") && epc_apai_is_warehouse_auto_parts_storefront($db_link)) {
                        $product_page_path = "content/shop/catalogue/product_page_warehouse_supplier.php";
                    }
                }
                $php_file = fopen($product_page_path, "r");
                $DP_Content->content = fread($php_file, filesize($product_page_path));
                fclose($php_file);
                return NULL;
            }
        if($DP_Config->chpu_search_config["chpu_search_on"] == true && 0 < count($url_route_components) && $url_route_components[0] == $DP_Config->chpu_search_config["level_1"]["url"]) {
            require_once $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_seo_indexing.php";
            if(count($url_route_components) == 1) {
                $DP_Content->value = translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]);
                $DP_Content->url = $url_route;
                $DP_Content->content_type = $DP_Config->chpu_search_config["level_1"]["content_type"];
                $DP_Content->title_tag = translate_str_by_id($DP_Config->chpu_search_config["level_1"]["title_tag"]);
                $DP_Content->description_tag = translate_str_by_id($DP_Config->chpu_search_config["level_1"]["description_tag"]);
                $DP_Content->keywords_tag = translate_str_by_id($DP_Config->chpu_search_config["level_1"]["keywords_tag"]);
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = "";
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $DP_Content->service_data = array("article_search_chpu" => true);
                $DP_Content->service_data["bread_crumbs"] = array();
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                $lvl1_content_cfg = $DP_Config->chpu_search_config["level_1"]["content"];
                if ($DP_Content->content_type == "php" && is_string($lvl1_content_cfg) && strlen($lvl1_content_cfg) > 0 && $lvl1_content_cfg[0] === "/") {
                    $DP_Content->content = $lvl1_content_cfg;
                } else {
                    $DP_Content->content = translate_str_by_id($lvl1_content_cfg);
                }
                if($DP_Content->content_type == "php") {
                    $php_file = fopen($_SERVER["DOCUMENT_ROOT"] . $DP_Content->content, "r");
                    $DP_Content->content = fread($php_file, filesize($_SERVER["DOCUMENT_ROOT"] . $DP_Content->content));
                    fclose($php_file);
                }
                epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "parts_index");
                return NULL;
            }
            if(count($url_route_components) == 2) {
                if($url_route_components[1] == $DP_Config->chpu_search_config["level_2"]["mode_1"]["url"]) {
                    $DP_Content->value = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_1"]["h1"]);
                    $DP_Content->url = $url_route;
                    $DP_Content->content_type = $DP_Config->chpu_search_config["level_2"]["mode_1"]["content_type"];
                    $DP_Content->title_tag = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_1"]["title_tag"]);
                    $DP_Content->description_tag = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_1"]["description_tag"]);
                    $DP_Content->keywords_tag = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_1"]["keywords_tag"]);
                    $DP_Content->author_tag = "";
                    $DP_Content->robots_tag = "";
                    $DP_Content->content = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_1"]["content"]);
                    if($DP_Content->content_type == "php") {
                        $php_file = fopen($_SERVER["DOCUMENT_ROOT"] . $DP_Content->content, "r");
                        $DP_Content->content = fread($php_file, filesize($_SERVER["DOCUMENT_ROOT"] . $DP_Content->content));
                        fclose($php_file);
                    }
                    $DP_Content->modules_array = array(31, 34);
                    $DP_Content->alternative_flag = true;
                    $DP_Content->service_data = array("article_search_chpu" => true);
                    $DP_Content->service_data["bread_crumbs"] = array();
                    $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                    $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => $DP_Content->value);
                    epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "parts_brands_hub");
                    return NULL;
                }
                if($url_route_components[1] == $DP_Config->chpu_search_config["level_2"]["mode_2"]["url"]) {
                    $DP_Content->value = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_2"]["h1"]);
                    $DP_Content->url = $url_route;
                    $DP_Content->content_type = $DP_Config->chpu_search_config["level_2"]["mode_2"]["content_type"];
                    $DP_Content->title_tag = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_2"]["title_tag"]);
                    $DP_Content->description_tag = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_2"]["description_tag"]);
                    $DP_Content->keywords_tag = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_2"]["keywords_tag"]);
                    $DP_Content->author_tag = "";
                    $DP_Content->robots_tag = "";
                    $DP_Content->content = translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_2"]["content"]);
                    if($DP_Content->content_type == "php") {
                        $php_file = fopen($_SERVER["DOCUMENT_ROOT"] . $DP_Content->content, "r");
                        $DP_Content->content = fread($php_file, filesize($_SERVER["DOCUMENT_ROOT"] . $DP_Content->content));
                        fclose($php_file);
                    }
                    $DP_Content->modules_array = array(31, 34);
                    $DP_Content->alternative_flag = true;
                    $DP_Content->service_data = array("article_search_chpu" => true);
                    $DP_Content->service_data["bread_crumbs"] = array();
                    $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                    $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => $DP_Content->value);
                    epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "parts_all_hub");
                    return NULL;
                }
                $manufacturer = mb_strtoupper(htmlentities(str_replace($DP_Config->chpu_search_config["slash_code"], "/", $url_route_components[1]), ENT_QUOTES, "UTF-8"), "UTF-8");
                $DP_Content->value = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_3"]["h1"]));
                $DP_Content->url = $url_route;
                $DP_Content->content_type = $DP_Config->chpu_search_config["level_2"]["mode_3"]["content_type"];
                $DP_Content->title_tag = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_3"]["title_tag"]));
                $DP_Content->description_tag = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_3"]["description_tag"]));
                $DP_Content->keywords_tag = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_3"]["keywords_tag"]));
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = "";
                // /parts/{BRAND} without article: show all rows for this manufacturer from local price lists
                $DP_Content->content_type = "php";
                $php_path = $_SERVER["DOCUMENT_ROOT"] . "/content/shop/docpart/part_search_manufacturer_browse.php";
                $php_file = fopen($php_path, "r");
                $DP_Content->content = fread($php_file, filesize($php_path));
                fclose($php_file);
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $DP_Content->service_data = array("article_search_chpu" => true);
                $DP_Content->service_data["search_type"] = "manufacturer_browse";
                $DP_Content->service_data["manufacturer"] = html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, "UTF-8");
                $DP_Content->service_data["bread_crumbs"] = array();
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => $DP_Content->value);
                epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "manufacturer_browse", html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, "UTF-8"));
                return NULL;
            }
            if(count($url_route_components) == 3 && $url_route_components[1] == $DP_Config->chpu_search_config["level_2"]["mode_1"]["url"]) {
                $article = mb_strtoupper(htmlentities($url_route_components[2], ENT_QUOTES, "UTF-8"), "UTF-8");
                require_once $_SERVER["DOCUMENT_ROOT"] . "/content/shop/docpart/docpart_article_match.php";
                global $multilang_params;
                $epc_lang_href = (isset($multilang_params["lang_href"]) && $multilang_params["lang_href"] !== "") ? $multilang_params["lang_href"] : "/en";
                $epc_single_brand_url = epc_chpu_single_brand_redirect_url($db_link, $DP_Config, html_entity_decode($article, ENT_QUOTES | ENT_XML1, "UTF-8"), $epc_lang_href);
                if ($epc_single_brand_url !== "") {
                    header("Location: " . $epc_single_brand_url, true, 302);
                    exit;
                }
                $DP_Content->value = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_1"]["h1"]));
                $DP_Content->url = $url_route;
                $DP_Content->content_type = "php";
                $DP_Content->title_tag = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_1"]["title_tag"]));
                $DP_Content->description_tag = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_1"]["description_tag"]));
                $DP_Content->keywords_tag = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_1"]["keywords_tag"]));
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = "";
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $DP_Content->service_data = array("article_search_chpu" => true, "search_type" => "all_brands_by_article", "article" => $article);
                $DP_Content->service_data["bread_crumbs"] = array();
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"] . "/" . $DP_Config->chpu_search_config["level_2"]["mode_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_1"]["h1"]));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => $DP_Content->value);
                $php_file = fopen("content/shop/docpart/part_search_page.php", "r");
                $DP_Content->content = fread($php_file, filesize("content/shop/docpart/part_search_page.php"));
                fclose($php_file);
                epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "all_brands_by_article", "", $article);
                return NULL;
            }
            if(count($url_route_components) == 3 && $url_route_components[1] == $DP_Config->chpu_search_config["level_2"]["mode_2"]["url"]) {
                $article = mb_strtoupper(htmlentities($url_route_components[2], ENT_QUOTES, "UTF-8"), "UTF-8");
                $DP_Content->value = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_2"]["h1"]));
                $DP_Content->url = $url_route;
                $DP_Content->content_type = "php";
                $DP_Content->title_tag = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_2"]["title_tag"]));
                $DP_Content->description_tag = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_2"]["description_tag"]));
                $DP_Content->keywords_tag = str_replace("<article>", $article, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_2"]["keywords_tag"]));
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = "";
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $DP_Content->service_data = array("article_search_chpu" => true, "search_type" => "prices_by_article_and_manufacturer", "article" => $article, "manufacturer" => NULL);
                $DP_Content->service_data["bread_crumbs"] = array();
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"] . "/" . $DP_Config->chpu_search_config["level_2"]["mode_2"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_2"]["h1"]));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => $DP_Content->value);
                $php_file = fopen("content/shop/docpart/part_search_page.php", "r");
                $DP_Content->content = fread($php_file, filesize("content/shop/docpart/part_search_page.php"));
                fclose($php_file);
                epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "prices_by_article_and_manufacturer", "", $article);
                return NULL;
            }
            if(1) {
                $article = mb_strtoupper(htmlentities($url_route_components[count($url_route_components) - 1], ENT_QUOTES, "UTF-8"), "UTF-8");
                $manufacturer = "";
                for ($i = 1; $i < count($url_route_components) - 1; $i++) {
                    if($manufacturer != "") {
                        $manufacturer = $manufacturer . "/";
                    }
                    $manufacturer = $manufacturer . $url_route_components[$i];
                }
                $manufacturer = str_replace($DP_Config->chpu_search_config["slash_code"], "/", $manufacturer);
                $manufacturer = htmlentities(mb_strtoupper($manufacturer, "UTF-8"), ENT_QUOTES, "UTF-8");
                $DP_Content->value = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_3"]["h1"]));
                $DP_Content->value = str_replace("<article>", $article, $DP_Content->value);
                $DP_Content->url = $url_route;
                $DP_Content->content_type = "php";
                $DP_Content->title_tag = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_3"]["title_tag"]));
                $DP_Content->title_tag = str_replace("<article>", $article, $DP_Content->title_tag);
                $DP_Content->description_tag = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_3"]["description_tag"]));
                $DP_Content->description_tag = str_replace("<article>", $article, $DP_Content->description_tag);
                $DP_Content->keywords_tag = str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_3"]["mode_3"]["keywords_tag"]));
                $DP_Content->keywords_tag = str_replace("<article>", $article, $DP_Content->keywords_tag);
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = "";
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $DP_Content->service_data = array("article_search_chpu" => true, "search_type" => "prices_by_article_and_manufacturer", "article" => $article, "manufacturer" => $manufacturer);
                $manufacturer_url = str_replace("/", $DP_Config->chpu_search_config["slash_code"], $manufacturer);
                $DP_Content->service_data["bread_crumbs"] = array();
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"], "caption" => translate_str_by_id($DP_Config->chpu_search_config["level_1"]["h1"]));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $DP_Config->chpu_search_config["level_1"]["url"] . "/" . $manufacturer_url, "caption" => str_replace("<manufacturer>", $manufacturer, translate_str_by_id($DP_Config->chpu_search_config["level_2"]["mode_3"]["h1"])));
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => $DP_Content->value);
                $php_file = fopen("content/shop/docpart/part_search_page.php", "r");
                $DP_Content->content = fread($php_file, filesize("content/shop/docpart/part_search_page.php"));
                fclose($php_file);
                epc_seo_apply_chpu_meta($DP_Content, $db_link, $DP_Config, "prices_by_article_and_manufacturer", $manufacturer, $article);
                return NULL;
            }
        }
        $sp_check_query = $db_link->prepare("SELECT * FROM `shop_special_searches` WHERE `active` = ? AND `alias` = ?;");
        $sp_check_query->execute(array(1, $url_route_components[0]));
        $sp_check_record = $sp_check_query->fetch();
        if($sp_check_record != false) {
            if(count($url_route_components) == 1) {
                $DP_Content->service_data["sp"] = true;
                $DP_Content->service_data["sp_alias"] = $url_route_components[0];
                $DP_Content->service_data["bread_crumbs"] = array();
                $DP_Content->service_data["bread_crumbs"][] = array("url" => $url_route, "caption" => translate_str_by_id($sp_check_record["caption"]));
                $DP_Content->value = translate_str_by_id($sp_check_record["caption"]);
                $DP_Content->url = $url_route;
                $DP_Content->content_type = "special_search";
                $DP_Content->title_tag = translate_str_by_id($sp_check_record["title"]);
                if($DP_Content->title_tag == "") {
                    $DP_Content->title_tag = $DP_Content->value;
                }
                $DP_Content->description_tag = translate_str_by_id($sp_check_record["description"]);
                $DP_Content->keywords_tag = translate_str_by_id($sp_check_record["keywords"]);
                $DP_Content->author_tag = "";
                $DP_Content->robots_tag = $sp_check_record["robots"];
                $DP_Content->modules_array = array(31, 34);
                $DP_Content->alternative_flag = true;
                $steps = array();
                $steps_query = $db_link->prepare("SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = ? ORDER BY `order` ASC;");
                $steps_query->execute(array($sp_check_record["id"]));
                $step = $steps_query->fetch();
                if($step == false) {
                    return NULL;
                }
                $step["objects"] = json_decode($step["objects"], true);
                if($step["type"] == 2) {
                    $DP_Content->service_data["sp_step_type"] = 2;
                    $DP_Content->service_data["sp_step_tree_list_id"] = $step["objects"][0];
                    $DP_Content->service_data["sp_step_tree_list_parent"] = 0;
                } else {
                    $DP_Content->service_data["sp_step_type"] = 1;
                    $DP_Content->service_data["sp_step_categories"] = $step["objects"];
                }
                $php_file = fopen("content/shop/catalogue/special_searches_handler.php", "r");
                $DP_Content->content = fread($php_file, filesize("content/shop/catalogue/special_searches_handler.php"));
                fclose($php_file);
                return NULL;
            }
            $steps = array();
            $steps_query = $db_link->prepare("SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = ? ORDER BY `order` ASC;");
            $steps_query->execute(array($sp_check_record["id"]));
            while ($step = $steps_query->fetch()) {
                $step["objects"] = json_decode($step["objects"], true);
                $steps[] = $step;
            }
            $bread_crumbs = array();
            $bread_crumbs[] = array("url" => $url_route_components[0], "caption" => translate_str_by_id($sp_check_record["caption"]));
            $current_url = "";
            $current_step = 0;
            $current_step_level = 1;
            $prevs = array();
            $current_step_one_found = false;
            $i = 1;
            while ($i < count($url_route_components)) {
                if($current_url != "") {
                    $current_url = $current_url . "/";
                    $current_step_level++;
                } else {
                    $current_step_level = 1;
                    $current_step_one_found = false;
                }
                $current_url = $current_url . $url_route_components[$i];
                if($steps[$current_step]["type"] == 2) {
                    $tree_list_id = $steps[$current_step]["objects"][0];
                    $check_url_query = $db_link->prepare("SELECT * FROM `shop_tree_lists_items` WHERE `url` = ? AND `tree_list_id` = ?;");
                    $check_url_query->execute(array($current_url, $tree_list_id));
                    $check_url = $check_url_query->fetch();
                    if($check_url == false) {
                        if(0 < count($steps) - ($current_step + 1) && $current_step_one_found) {
                            $current_step++;
                            $current_url = "";
                            $i--;
                            $i++;
                        } else {
                            sp_404($bread_crumbs);
                            return NULL;
                        }
                    } else {
                        $current_step_one_found = true;
                        $bread_crumbs[] = array("url" => $bread_crumbs[count($bread_crumbs) - 1]["url"] . "/" . $url_route_components[$i], "caption" => translate_str_by_id($check_url["value"]));
                        $prevs[] = translate_str_by_id($check_url["value"]);
                        $DP_Content->service_data["sp_tl_" . $tree_list_id] = $check_url["id"];
                        if($i == count($url_route_components) - 1) {
                            $DP_Content->service_data["sp"] = true;
                            $DP_Content->service_data["sp_alias"] = $url_route_components[0];
                            $DP_Content->service_data["bread_crumbs"] = $bread_crumbs;
                            $page_metadata = get_sp_metadata($sp_check_record["id"], $steps[$current_step]["id"], $current_step_level, $check_url["value"], $sp_check_record["caption"], $prevs);
                            $DP_Content->service_data["bread_crumbs"][count($DP_Content->service_data["bread_crumbs"]) - 1]["caption"] = translate_str_by_id($page_metadata["h1"]);
                            $DP_Content->value = translate_str_by_id($page_metadata["h1"]);
                            $DP_Content->url = $url_route;
                            $DP_Content->content_type = "special_search";
                            $DP_Content->title_tag = translate_str_by_id($page_metadata["title"]);
                            $DP_Content->description_tag = translate_str_by_id($page_metadata["description"]);
                            $DP_Content->keywords_tag = translate_str_by_id($page_metadata["keywords"]);
                            $DP_Content->author_tag = "";
                            $DP_Content->robots_tag = $page_metadata["robots"];
                            $DP_Content->modules_array = array(31, 34);
                            $DP_Content->alternative_flag = true;
                            if($check_url["count"] == 0) {
                                if(count($steps) - ($current_step + 1) == 1) {
                                    $DP_Content->service_data["sp_step_type"] = 1;
                                    $DP_Content->service_data["sp_step_categories"] = $steps[$current_step + 1]["objects"];
                                } else {
                                    $DP_Content->service_data["sp_step_type"] = 2;
                                    $DP_Content->service_data["sp_step_tree_list_id"] = $steps[$current_step + 1]["objects"][0];
                                    $DP_Content->service_data["sp_step_tree_list_parent"] = 0;
                                }
                            } else {
                                $DP_Content->service_data["sp_step_type"] = 2;
                                $DP_Content->service_data["sp_step_tree_list_id"] = $check_url["tree_list_id"];
                                $DP_Content->service_data["sp_step_tree_list_parent"] = $check_url["id"];
                            }
                            $php_file = fopen("content/shop/catalogue/special_searches_handler.php", "r");
                            $DP_Content->content = fread($php_file, filesize("content/shop/catalogue/special_searches_handler.php"));
                            fclose($php_file);
                            return NULL;
                        }
                        continue;
                    }
                } else {
                    $check_url_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `url` = ?;");
                    $check_url_query->execute(array($current_url));
                    $check_url = $check_url_query->fetch();
                    if($check_url == false) {
                        sp_404($bread_crumbs);
                        return NULL;
                    }
                    $current_step_one_found = true;
                    $bread_crumbs[] = array("url" => $bread_crumbs[count($bread_crumbs) - 1]["url"] . "/" . $url_route_components[$i], "caption" => translate_str_by_id($check_url["value"]));
                    $prevs[] = translate_str_by_id($check_url["value"]);
                    if($i == count($url_route_components) - 1) {
                        if($check_url["count"] == 0) {
                            $DP_Content->service_data["sp_step_type"] = 3;
                            $DP_Content->service_data["sp_step_category_id"] = $check_url["id"];
                        } else {
                            $sub_categories_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `parent` = ? ORDER BY `order` ASC;");
                            $sub_categories_query->execute(array($check_url["id"]));
                            $DP_Content->service_data["sp_step_categories"] = array();
                            while ($category = $sub_categories_query->fetch()) {
                                $DP_Content->service_data["sp_step_categories"][] = $category["id"];
                            }
                            $DP_Content->service_data["sp_step_type"] = 1;
                        }
                        $DP_Content->service_data["sp"] = true;
                        $DP_Content->service_data["sp_alias"] = $url_route_components[0];
                        $DP_Content->service_data["bread_crumbs"] = $bread_crumbs;
                        $page_metadata = get_sp_metadata($sp_check_record["id"], $steps[$current_step]["id"], $current_step_level, $check_url["value"], $sp_check_record["caption"], $prevs);
                        $DP_Content->service_data["bread_crumbs"][count($DP_Content->service_data["bread_crumbs"]) - 1]["caption"] = translate_str_by_id($page_metadata["h1"]);
                        $DP_Content->value = translate_str_by_id($page_metadata["h1"]);
                        $DP_Content->url = $url_route;
                        $DP_Content->content_type = "category";
                        $DP_Content->title_tag = translate_str_by_id($page_metadata["title"]);
                        $DP_Content->description_tag = translate_str_by_id($page_metadata["description"]);
                        $DP_Content->keywords_tag = translate_str_by_id($page_metadata["keywords"]);
                        $DP_Content->author_tag = "";
                        $DP_Content->robots_tag = $page_metadata["robots"];
                        $DP_Content->modules_array = array(31, 34);
                        $DP_Content->alternative_flag = true;
                        $php_file = fopen("content/shop/catalogue/special_searches_handler.php", "r");
                        $DP_Content->content = fread($php_file, filesize("content/shop/catalogue/special_searches_handler.php"));
                        fclose($php_file);
                        return NULL;
                    }
                    continue;
                }
            }
        }
    }
}
function get_sp_metadata($sp_id, $step_id, $current_step_level, $for_empty, $sp_caption, $prevs)
{
    global $db_link;
    $page_metadata_query = $db_link->prepare("SELECT * FROM `shop_special_searches_metadata` WHERE `search_id` = ? AND `step_id` = ? AND `step_level` = ?;");
    $page_metadata_query->execute(array($sp_id, $step_id, $current_step_level));
    $page_metadata = $page_metadata_query->fetch();
    if($page_metadata == false) {
        $page_metadata = array();
        $page_metadata["h1"] = $for_empty;
        $page_metadata["title"] = $for_empty;
        $page_metadata["description"] = $for_empty;
        $page_metadata["keywords"] = $for_empty;
        $page_metadata["robots"] = "";
    } else {
        $metadata_fields = array("h1", "title", "description", "keywords");
        foreach ($metadata_fields as $key => $field) {
            if($page_metadata[$field] == "") {
                $page_metadata[$field] = $for_empty;
            } else {
                $page_metadata[$field] = translate_str_by_id($page_metadata[$field]);
            }
        }
    }
    $metadata_fields = array("h1", "title", "description", "keywords", "robots");
    foreach ($metadata_fields as $key => $field) {
        $page_metadata[$field] = str_replace("%item_name%", $for_empty, $page_metadata[$field]);
        $page_metadata[$field] = str_replace("%search_name%", $sp_caption, $page_metadata[$field]);
        $prev_n = 1;
        for ($i = count($prevs) - 2; 0 <= $i; $i--) {
            $page_metadata[$field] = str_replace("%prev" . $prev_n . "%", $prevs[$i], $page_metadata[$field]);
            $prev_n++;
        }
    }
    return $page_metadata;
}
function sp_404($bread_crumbs)
{
    global $DP_Content;
    global $url_route;
    $url_route_components = explode("/", $url_route);
    $need_404_aliases = count($url_route_components) - count($bread_crumbs);
    $start_from = count($url_route_components) - (count($url_route_components) - count($bread_crumbs));
    for ($i = $start_from; $i < count($url_route_components); $i++) {
        $bread_crumbs[] = array("url" => $bread_crumbs[count($bread_crumbs) - 1]["url"] . "/" . $url_route_components[$i], "caption" => "404 Страница не найдена");
    }
    $DP_Content->alternative_flag = true;
    $DP_Content->content_type = "text";
    $DP_Content->title_tag = translate_str_by_id(3);
    $DP_Content->value = translate_str_by_id(3);
    $DP_Content->description_tag = translate_str_by_id(4755);
    $DP_Content->keywords_tag = translate_str_by_id(4755);
    $DP_Content->author_tag = translate_str_by_id(4755);
    $DP_Content->content = translate_str_by_id(4755);
    $DP_Content->service_data["error_page"] = 404;
    $DP_Content->service_data["sp"] = true;
    $DP_Content->service_data["bread_crumbs"] = $bread_crumbs;
}

?>
<?php
/*
 * @ https://EasyToYou.eu - IonCube v11 Decoder Online
 * @ PHP 5.0 & 5.6
 * @ Decoder version: 1.1.5
 * @ Release: 12/09/2024
 */

// Decoded file for php version 56.
defined("_ASTEXE_") or exit("No access");
/**
 * Copyright © 2015, AST Ltd. All Rights Reserved.
 * 
 * Класс для описания основного содежимого страницы
 * Имена полей совпадают с именами колонок таблицы материалов
*/
class DP_Content
{
    public $id = NULL;
    public $content_type = NULL;
    public $content = NULL;
    public $value = NULL;
    public $url = NULL;
    public $modules_array = array();
    public $css_js = NULL;
    public $description = NULL;
    public $title_tag = NULL;
    public $description_tag = NULL;
    public $keywords_tag = NULL;
    public $author_tag = NULL;
    public $robots_tag = NULL;
    public $main_flag = NULL;
    public $alternative_flag = NULL;
    public $service_data = array();
    public function __construct()
    {
        $this->id = 0;
        $this->content_type = "";
        $this->content = "";
        $this->value = "";
        $this->url = "";
        $this->css_js = "";
        $this->description = "";
        $this->title_tag = "";
        $this->description_tag = "";
        $this->keywords_tag = "";
        $this->author_tag = "";
        $this->robots_tag = "";
        $this->main_flag = 0;
        $this->alternative_flag = 0;
        $this->service_data = array();
    }
}

?>
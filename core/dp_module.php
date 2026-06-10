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
 * Класс для описания модуля
 * Имена полей совпадают с именами колонок в таблице модулей
*/
class DP_Module
{
    public $id = NULL;
    public $caption = NULL;
    public $content_type = NULL;
    public $content = NULL;
    public $position = NULL;
    public $show_caption = NULL;
    public $css_js = NULL;
    public $order = NULL;
    public function __construct()
    {
        $this->id = 0;
        $this->caption = "";
        $this->content_type = "";
        $this->position = "";
        $this->show_caption = 1;
        $this->css_js = "";
        $this->order = 0;
    }
}

?>
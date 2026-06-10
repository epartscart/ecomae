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
 * Класс для описания шаблона страницы
*/
class DP_Template
{
    public $id = NULL;
    public $name = NULL;
    public $html = NULL;
    public $phone_support = NULL;
    public $tablet_support = NULL;
    public $positions = array();
    public $data_value = array();
    public function __construct()
    {
        $this->id = 0;
        $this->name = "";
        $this->html = "";
        $this->phone_support = false;
        $this->tablet_support = false;
    }
}

?>
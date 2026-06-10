<?php
/**
 * Created by PhpStorm.
 * User: applebred
 * Date: 21.05.18
 * Time: 9:34
 */

namespace guayaquil\language;


class LanguageTemplate
{
    public static $language_data = [];

    public static function getTemplateData() {
        return self::$language_data;
    }
}
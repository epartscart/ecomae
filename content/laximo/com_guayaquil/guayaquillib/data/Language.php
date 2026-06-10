<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 17.08.17
 * Time: 16:03
 */

namespace guayaquil\guayaquillib\data;

use guayaquil\Config;
use guayaquil\language\LanguageTemplateEn;
use guayaquil\language\LanguageTemplateRu;

class Language
{
    /**
     * @return array
     */
    public function getLocalizationsList() {
        return [
            'Русский'             => 'ru_RU',
            'English (USA)'       => 'en_US',
            'Chinese'             => 'zh_CN',
            'Turkish'             => 'tr_TR',
            'French'              => 'fr_FR',
            'German'              => 'de_DE',
            'Hindi'               => 'hi_IN',
            'Spanish'             => 'es_ES',
            'Japanese'            => 'ja_JP',
            'Dutch'               => 'nl_NL',
            'English (UK)'        => 'en_GB',
            'Greek'               => 'el_GR',
            'Italian'             => 'it_IT',
            'Korean'              => 'ko_KR',
            'Polish'              => 'pl_PL',
            'Português'           => 'pt_PT',
            'Svenska'             => 'sv_SE',
            'Thai'                => 'th_TH',
            'Traditional Chinese' => 'zh_TW',
            'Czech'               => 'cs_CZ',
            'Danish'              => 'da_DK',
            'Finnish'             => 'fi_FI',
            'Hungarian'           => 'hu_HU',
            'Romanian'            => 'ro_RO',
            'Croatian'            => 'hr_HR',
            'Estonian'            => 'et_EE',
            'Latvian'             => 'lv_LV',
            'Lithuanian'          => 'lt_LT',
            'Български'           => 'bg_BG',
            'Slovak'              => 'sk_SK',
        ];
    }

    public function setLocalization($code) {
        setcookie('interface_language', $code);
    }

    public function getLocalization() {

        if (!isset($_COOKIE['interface_language'])) {
            return false;
        }

        return $_COOKIE['interface_language'];
    }

    public function t($name, ...$args) {

        $name = (string) $name;

        $currentTemplateClass = 'guayaquil\language\LanguageTemplateEn';
        $cookieLocalization = $this->getLocalization();

        if (Config::$useEnvParams) {
            if (getenv('UUE_UI_LOCALE')) {
                $currentLang = base64_decode(getenv('UUE_UI_LOCALE'));
            } else {
                $currentLang = Config::$ui_localization;
            }
        } else {
            if ($cookieLocalization) {
                $currentLang = $cookieLocalization;
            } else {
                $currentLang = Config::$ui_localization;
                $currentTemplateClass = 'guayaquil\language\LanguageTemplate' . ucfirst($currentLang);
            }
        }



        switch ($currentLang) {
            case 'ru':
            case 'ru_RU':
                $langArr = LanguageTemplateRu::$language_data;
                break;
            case 'en':
            case 'en_GB':
                $langArr = LanguageTemplateEn::$language_data;
                break;
            default:
                $langArr = $currentTemplateClass::$language_data;
        }

        if (array_key_exists($name, $langArr) && $langArr[$name]) {
            $string =  (string) $langArr[$name];

            return sprintf($string, ...$args);
        } else {
            return $name;
        }
    }

    public function createUrl($task = null, $view = null, $format = null, array $params = [])
    {

        $paths = [];

        if ($task) {
            if (is_array($task)) {
                $paths = array_merge($paths, $task);
            } else {
                $paths['task'] = $task;
            }
        }

        if ($view) {
            if (is_array($view)) {
                $paths = array_merge($paths, $view);
            } else {
                $paths['view'] = $view;
            }
        }

        if ($format) {
            if (is_array($format)) {
                $paths = array_merge($paths, $format);
            } else {
                $paths['format'] = $format;
            }
        }

        foreach ($params as $key=>$param) {
            $params[$key] = trim($param);
        }

        if ($params) {
            $paths = array_merge($paths, $params);
        }

        $baseUrl = 'https://store.safeapp.ru/';

        if ($paths) {
            $url = $baseUrl . ('katalog-laximo?' . http_build_query($paths));
            if (strpos($url, $baseUrl) === false) {
                $url = $baseUrl . 'katalog-laximo?' . http_build_query($paths);
            }
        } else {
            $url = $baseUrl;
        }

        return urldecode($url);
    }

    public function noSpaces($name) {
        $name = (string) $name;

        $name = preg_replace('/\s+/', ' ', $name);

        return $name;

    }
}
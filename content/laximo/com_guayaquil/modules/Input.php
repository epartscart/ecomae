<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 17.08.17
 * Time: 16:03
 */

namespace guayaquil\modules;


class Input
{
    public function getString($arg, $default = false)
    {
        $result = isset($_GET[$arg]) ? $_GET[$arg] : $default;

        return $result;
    }

    public function getInt($arg, $default = false)
    {
        $result = (int)isset($_GET[$arg]) ? $_GET[$arg] : $default;

        return $result;
    }

    public function get($arg)
    {

        return $_GET[$arg];
    }

    public function getArray()
    {

        return (array)$_GET;
    }

    public function formData() {
        return (array) $_POST;
    }

}
<?php

namespace guayaquil;

use guayaquil\guayaquillib\data\Language;

class router
{

    public static function start()
    {
        $route = self::parse($_SERVER['REQUEST_URI']);

        if (isset($route['task']) && $route['task'] !== '') {

            $namespace = 'guayaquil\views\\' . $route['task'] . '\\';
            $task      = ucfirst($route['task']);
            $viewName  = $namespace . $task . 'Html';

            $view = new $viewName();
            $view->Display();
        } else {
            if (Config::$showWelcomePage) {
                $view = new View();
                $view->renderHead();
                $view->loadTwig('tmpl', 'index.twig');
                $view->renderFooter();
            } else {
                $language = new Language();
                $view     = new View();
                $view->redirect($language->createUrl('catalogs'));
            }
        }
    }

    public static function parse(&$segments)
    {

        $url = parse_url($segments);
        if (isset($url['query'])) {
            $query = $url['query'];
        }

        if (!empty($query)) {
            $values = explode('&', $query);
            foreach ($values as $key => $value) {
                if ($value === '') {
                    unset($values[$key]);
                }
                $parameter = explode('=', $value);

                if (!empty($parameter)) {
                    $parameters[$parameter[0]] = isset($parameter[1]) ? $parameter[1] : '';
                }


            }
            reset($values);

            $params = [];
            foreach ($values as $value) {
                $key = explode('=', $value);

                if (isset($key[0]) && isset($key[1])) {

                    $params[$key[0]] = $key[1];

                }

            }

            return [
                'task'   => isset($parameters['task']) ? $parameters['task'] : '',
                'params' => $params
            ];
        }

        return [];
    }

}

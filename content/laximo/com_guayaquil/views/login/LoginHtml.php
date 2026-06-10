<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 27.08.18
 * Time: 14:04
 */

namespace guayaquil\views\login;

use guayaquil\View;


class LoginHtml extends View
{
    /**
     * @var boolean
     */
    private $isAftermarket;

    public function Display($tpl = 'login', $view = 'view')
    {
        $view = $this->input->getString('view');
        switch ($view) {
            case 'login':
                $this->login();
                break;
            case 'logout':
                $user = $this->input->formData()['user'];
                $url  = parse_url($user['backurl']);
                parse_str($url['query'], $backurlParams);
                $task = $backurlParams['task'];

                if ($task === 'aftermarket') {
                    $this->logoutFromAftermarket();
                    break;
                }

                $this->logout();
                break;
        }

    }

    public function login()
    {
        $user = $this->input->formData()['user'];

        if (!$user) {
            return;
        }

        $login = trim($user['login']);
        $key = $user['password'];

        $url = parse_url($user['backurl']);
        parse_str($url['query'], $backurlParams);
        $task = $backurlParams['task'];

        if (!empty($task) && $task === 'aftermarket') {
            $this->isAftermarket = true;
            $this->loginByAftermarket($user);
        } else {
            $requests = [
                'appendListCatalogs' => []
            ];

            $this->getData($requests, [], $login, $key);

            if ($this->user) {
                $_SESSION['logged'] = true;
                $_SESSION['username'] = $login;
                $_SESSION['key'] = $key;
                $this->redirect($user['backurl'] . '&auth=true');
            } else {
                unset($_SESSION['logged']);
                unset($_SESSION['username']);
                unset($_SESSION['key']);
                $this->redirect($user['backurl'] . '&auth=false');
            }
        }
    }

    private function loginByAftermarket($user) {
        $request = [
            'appendFindOEM' => [
                'oem' => 'C110'
            ]
        ];

        $login = trim($user['login']);
        $key = $user['password'];

        $this->getAftermarketData($request, [], $login, $key);

        if ($this->amUser) {
            $_SESSION['logged_in_am'] = true;
            $_SESSION['am_username'] = $login;
            $_SESSION['am_key'] = $key;

            $this->redirect($user['backurl'] . '&auth=true');
        } else {
            unset($_SESSION['logged_in_am']);
            unset($_SESSION['am_username']);
            unset($_SESSION['am_key']);
            $this->redirect($user['backurl'] . '&auth=false');
        }
    }

    public function logoutFromAftermarket() {
        unset($_SESSION['logged_in_am']);
        unset($_SESSION['am_username']);
        unset($_SESSION['am_key']);

        $data = $this->input->formData();

        if (!$_SESSION['logged_in_am']) {
            $this->redirect($data['user']['backurl']);
        }
    }

    public function logout()
    {
        unset($_SESSION['logged']);
        unset($_SESSION['username']);
        unset($_SESSION['key']);

        $data = $this->input->formData();

        if (!$_SESSION['logged']) {
            $this->redirect($data['user']['backurl']);
        }
    }
}
<?php
namespace guayaquil\views\error;
use guayaquil\View;

/**
 * Created by Laximo
 * User: elnikov.a
 * Date: 03.04.18
 * Time: 14:43
 */
class ErrorHtml extends View
{
    public function Display($tpl = 'error', $view = 'error') {
        $type = $this->input->getString('type', 'error');

        parent::Display($tpl, $type);
    }
}
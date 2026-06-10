<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 17.08.17
 * Time: 16:03
 */

namespace guayaquil\views\unitfilter;

use guayaquil\guayaquillib\objects\UnitObject;
use guayaquil\View;

/**
 * @property array      filter_data
 * @property UnitObject unit
 * @property array      from
 * @property string     fromTask
 */
class UnitfilterHtml extends View
{
    public function Display($tpl = 'unitfilter', $view = 'view')
    {
        $c   = $this->input->getString('c');
        $ssd = $this->input->getString('ssd');
        $f   = $this->input->getString('f');
        $vid = $this->input->getString('vid');
        $uid = $this->input->getString('uid');

        $params      = ['c' => $c, 'ssd' => $ssd, ''];

        $requests = [
            'appendGetFilterByUnit' => [
                'f'   => $f,
                'vid' => $vid,
                'uid' => $uid,
            ],
            'appendGetUnitInfo'       => [
                'uid' => $uid
            ]
        ];

        $data = $this->getData($requests, $params);

        if ($data) {
            $filter_data = $data[0];
            $unit        = $data[1];
            $fromTask    = $this->input->getString('fromTask');

            $this->filter_data = $filter_data;
            $this->unit        = $unit;
            $this->from        = $this->input->getArray();
            $this->fromTask    = $fromTask;
        }

        parent::Display($tpl, $view);
    }
}
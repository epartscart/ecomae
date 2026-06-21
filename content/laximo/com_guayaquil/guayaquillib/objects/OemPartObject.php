<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 08.05.18
 * Time: 15:15
 */

namespace guayaquil\guayaquillib\objects;


use guayaquil\guayaquillib\BaseGuayaquilObject;

class OemPartObject extends BaseGuayaquilObject
{
    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $oem;

    protected function fromXml($data) {
        $this->name = (string) $data->name;
        $this->oem  = (string) $data->attributes()->oem;
    }
}
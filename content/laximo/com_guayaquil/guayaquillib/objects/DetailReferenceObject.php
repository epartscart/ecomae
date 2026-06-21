<?php
/**
 * Created by PhpStorm.
 * User: applebred
 * Date: 16.01.19
 * Time: 15:54
 */

namespace guayaquil\guayaquillib\objects;


use guayaquil\guayaquillib\BaseGuayaquilObject;
use SimpleXMLElement;

class DetailReferenceObject extends BaseGuayaquilObject
{
    /**
     * @var string $brand
     */
    public $brand;

    /**
     * @var string $code
     */
    public $code;

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $this->brand = (string)$data->attributes()->brand;
        $this->code  = (string)$data->attributes()->code;
    }

    protected function fromJSON($data) {
        $this->brand = (string) $data['catalog']->attributes()->brand;
        $this->code  = (string)$data['catalog']->attributes()->code;
    }
}
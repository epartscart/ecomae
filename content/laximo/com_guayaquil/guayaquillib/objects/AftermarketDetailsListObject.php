<?php
/**
 * Created by PhpStorm.
 * User: applebred
 * Date: 10.01.19
 * Time: 15:30
 */

namespace guayaquil\guayaquillib\objects;


use guayaquil\guayaquillib\BaseGuayaquilObject;
use SimpleXMLElement;

class AftermarketDetailsListObject extends BaseGuayaquilObject
{
    /**
     * @var array $oems
     */
    public $oems;

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        foreach ($data as $detail) {
            $detail = new AftermarketDetailObject($detail);
            $this->oems[] = $detail;
        }
    }
}
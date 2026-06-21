<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 08.05.18
 * Time: 15:04
 */

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\guayaquillib\objects\OemPartObject;
use SimpleXMLElement;

class PartsListObject extends BaseGuayaquilObject
{
    public $oemParts = [];

    protected function fromXml($data) {
        foreach ($data->OEMPart as $part) {
            $partObj = new OemPartObject($part);
            $this->oemParts[] = $partObj;
        }
    }

}
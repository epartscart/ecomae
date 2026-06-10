<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class UnitListObject extends BaseGuayaquilObject
{

    /**
     * @var UnitObject[]
     */
    public $units;

    protected function fromXml($data)
    {
        foreach ($data->row as $unit) {
            $current = new UnitObject($unit);
            $this->units[] = $current;
        }
    }
}
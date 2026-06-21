<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class FilterObject extends BaseGuayaquilObject
{
    public $fields;
    protected function fromXml($data)
    {
        foreach ($data->row as $filterField) {
            $this->fields[] = new FilterFieldObject($filterField);
        }
    }
}
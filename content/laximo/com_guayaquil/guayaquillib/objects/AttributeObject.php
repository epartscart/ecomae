<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class AttributeObject extends BaseGuayaquilObject
{
    public $key;

    public $name;

    public $value;

    protected function fromXml($data)
    {
        $this->key = (string)$data['key'];
        $this->name = (string)$data['name'];
        $this->value = (string)$data['value'];
    }

    protected function fromJSON($data)
    {
        $this->fromXml($data);
    }
}
<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use SimpleXMLElement;

class FilterFieldObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $type;

    /**
     * @var FilterFieldValueObject[]
     */
    public $values;



    protected function fromXml($data)
    {
        $this->name = (string)$data['name'];
        $this->type = (string)$data['type'];

        if ($data->values instanceof SimpleXMLElement) {
            foreach ($data->values->row as $value) {
                $this->values[] = new FilterFieldValueObject($value);
            }
        }
    }
}
<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use SimpleXMLElement;

class CatalogOperationObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $description;

    /**
     * @var string
     */
    public $kind;

    /**
     * @var string
     */
    public $name;

    /**
     * @var CatalogOperationFieldObject[]
     */
    public $fields;

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $this->description = (string)$data['description'];
        $this->kind = (string)$data['kind'];
        $this->name = (string)$data['name'];
        foreach ($data->field as $field) {
            $this->fields[] = new CatalogOperationFieldObject($field);
        }
    }
}
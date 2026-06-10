<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class FeatureObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $example;

    protected function fromXml($data)
    {
        $this->example = isset($data['example']) ? (string)$data['example'] : null;
        $this->name = (string)$data['name'];
    }
}
<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class WizardStepOptionObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $key;

    /**
     * @var string
     */
    public $value;

    protected function fromXml($data)
    {
        $this->key = (string)$data['key'];
        $this->value = html_entity_decode($data['value']);
    }
}
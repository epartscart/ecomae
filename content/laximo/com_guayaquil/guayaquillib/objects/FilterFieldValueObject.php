<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class FilterFieldValueObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $note;

    /**
     * @var string
     */
    public $ssdmodification;


    protected function fromXml($data)
    {
        $this->name            = (string)$data['name'];
        $this->note            = (string)$data['note'];
        $this->ssdmodification = (string)$data['ssdmodification'];
    }
}
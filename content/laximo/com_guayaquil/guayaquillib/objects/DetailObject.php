<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use SimpleXMLElement;

class DetailObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $oem;

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $codeonimage;

    /**
     * @var string
     */
    public $ssd;

    /**
     * var bool
     */
    public $match;

    /**
     * var bool
     */
    public $filter;

    /**
     * @var AttributeObject[]
     */
    public $attributes;

    /**
     * @var array
     */
    public $links = [];


    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $this->oem = (string)$data['oem'];
        $this->name = (string)$data['name'];
        $this->codeonimage = (string)$data['codeonimage'];
        $this->match = (string)$data['match'] == 't' ? 1 : 0;
        $this->filter = (string)$data['filter'];
        $this->ssd = (string)$data['ssd'];

        if (!empty($data->Links)) {
            foreach ($data->Links as $link) {
                $linkObject    = new DetailLink($link);
                $this->links[] = $linkObject;
            }
        }

        if ($data->attribute instanceof SimpleXMLElement) {
            foreach ($data->attribute as $attribute) {
                $this->attributes[] = new AttributeObject($attribute);
            }
        }
    }
}
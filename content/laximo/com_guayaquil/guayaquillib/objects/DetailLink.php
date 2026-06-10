<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 14.03.19
 * Time: 14:54
 */

namespace guayaquil\guayaquillib\objects;


use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\guayaquillib\data\Language;
use SimpleXMLElement;

class DetailLink extends BaseGuayaquilObject
{
    /**
     * @var string
     */
    public $type;

    /**
     * @var string
     */
    public $label;

    /**
     * @var string
     */
    public $command;

    /**
     * @var array
     */
    public $attributes;

    public function getLink()
    {
        $language = new Language();
        $link     = null;

        switch ($this->command) {
            case 'LISTUNITS':
                $params = [
                    'c'              => $this->attributes['Catalog'] ?? '',
                    'cid'            => $this->attributes['CategoryId'] ?? '',
                    'vid'            => $this->attributes['VehicleId'] ?? '',
                    'ssd'            => $this->attributes['ssd'] ?? '',
                    'linkedWithUnit' => 1
                ];

                $link = $language->createUrl('vehicle', '', '', $params);
                break;
        }

        return $link;
    }

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $serviceData = json_decode(json_encode($data));
        foreach ($serviceData->Link->{'@attributes'} as $key => $attribute) {
            if (strtolower($key) === 'type') {
                $this->type = $attribute;
            } elseif (strtolower($key) === 'label') {
                $this->label = $attribute;
            } elseif (strtolower($key) === 'command') {
                $this->command = $attribute;
            } else {
                $this->attributes[$key] = $attribute;
            }
        }
    }
}
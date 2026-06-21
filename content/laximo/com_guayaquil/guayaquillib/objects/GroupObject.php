<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\guayaquillib\ObjectFactory;
use SimpleXMLElement;
use guayaquil\guayaquillib\data\Language;

class GroupObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $contains;

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $synonyms;

    /**
     * @var string
     */
    public $quickgroupid;

    /**
     * @var bool
     */
    public $link;

    /**
     * @var GroupObject[]
     */
    public $childGroups;

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $this->contains     = (string)$data['contains'];
        $this->link         = (string)$data['link'] === 'true' ? 1 : 0;
        $this->name         = (string)$data['name'];
        $this->quickgroupid = (string)$data['quickgroupid'];
        $this->synonyms     = (string)$data['synonyms'];
        $children           = $data->children();

        foreach ($children->row as $child) {
            $this->childGroups[] = new GroupObject($child);
        }
    }

    /**
 * @param VehicleObject $vehicle
 * @param array $addParams
 *
 * @return string
 */
    public function getLink($vehicle,  $addParams = [])
    {
        $language = new Language();

        $addParams['c']   = $vehicle->catalog;
        $addParams['vid'] = $vehicle->vehicleid;
        $addParams['gid'] = $this->quickgroupid;
        $addParams['ssd'] = $vehicle->ssd;

        return $language->createUrl('qdetails.view', '', '', $addParams);
    }
}
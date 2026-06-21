<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\guayaquillib\data\Language;
use SimpleXMLElement;

class CategoryObject extends BaseGuayaquilObject
{

    /**
     * @var int
     */
    public $categoryid;

    /**
     * @var CategoryObject[]|null
     */
    public $childrens;

    /**
     * @var string
     */
    public $code;

    /**
     * @var string
     */
    public $name;

    /**
     * @var int
     */
    public $parentcategoryid;

    /**
     * @var string
     */
    public $ssd;

    /**
     * @var bool
     */
    public $selected;

    /**
     * @var UnitObject[]
     */
    public $units;

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $this->categoryid       = (string)$data['categoryid'];
        $this->code             = (string)$data['code'];
        $this->name             = (string)$data['name'];
        $this->parentcategoryid = (string)$data['parentcategoryid'];
        $this->ssd              = (string)$data['ssd'];
        $this->selected         = false;
        if ($data->Unit instanceof SimpleXMLElement) {
            foreach ($data->Unit as $unit) {
                $this->units[] = new UnitObject($unit);
            }
        }
    }

    /**
     * @param VehicleObject $vehicle
     * @param array $addParams
     *
     * @return string
     */
    public function getLink($vehicle, $addParams = [])
    {
        $language = new Language();

        $addParams['c']   = $vehicle->catalog;
        $addParams['vid'] = $vehicle->vehicleid;
        $addParams['cid'] = $this->categoryid;
        $addParams['ssd'] = $this->ssd;

        return $language->createUrl('vehicle.view', '', '', $addParams);
    }
}
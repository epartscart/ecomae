<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\guayaquillib\data\Language;
use SimpleXMLElement;

class UnitObject extends BaseGuayaquilObject
{

    /**
     * @var string
     */
    public $code;

    /**
     * @var string
     */
    public $unitid;

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $imageurl;

    /**
     * @var string
     */
    public $largeimageurl;

    /**
     * @var string
     */
    public $ssd;

    /**
     * @var string
     */
    public $filter;

    /**
     * @var DetailObject[]
     */
    public $details;

    /**
     * @var AttributeObject[];
     */
    public $attributes;

    protected function fromXml($data)
    {
        $this->code          = (string)$data['code'];
        $this->unitid        = (string)$data['unitid'];
        $this->name          = (string)$data['name'];
        $this->imageurl      = str_replace('http://' , 'https://', (string)$data['imageurl']);
        $this->largeimageurl = str_replace('http://' , 'https://', (string)$data['largeimageurl']);
        $this->ssd           = (string)$data['ssd'];
        $this->filter        = (string)$data['filter'];

        if ($data->attribute instanceof SimpleXMLElement) {
            foreach ($data->attribute as $attribute) {
                $this->attributes[] = new AttributeObject($attribute);
            }
        }

        if ($data->Detail instanceof SimpleXMLElement) {
            foreach ($data->Detail as $detail) {
                $this->details[] = new DetailObject($detail);
            }
        }

        $this->getDetailsByCode();
    }

    /**
     * @param VehicleObject $vehicle
     * @param CategoryObject $category
     * @param array $codesOnImage
     *
     * @return string
     */
    public function getLink($vehicle, $category, $codesOnImage = null, $addParams = []) {
        $language = new Language();
        $params = [
            'c' => $vehicle->catalog ?? '',
            'vid' => $vehicle->vehicleid ?? '',
            'uid' => $this->unitid ?? '',
            'cid'=> $category->categoryid ?? '',
            'ssd' => $this->ssd ?? '',
        ];

        if ($codesOnImage){
            $params['coi'] = implode(',',$codesOnImage);
        }

        if ($addParams) {
            $params = array_merge($params, $addParams);
        }

        return $language->createUrl('unit', '', '', $params);
    }

    private function getDetailsByCode()
    {
        $groups = [];

        if ($this->details) {
            foreach ($this->details as $detail) {
                if ($detail->codeonimage && $detail->codeonimage != '-') {
                    $groups['i' . $detail->codeonimage][] = $detail;
                } else {
                    $groups['-'][] = $detail;
                }
            }
        }

        $this->detailsByCode = $groups;
    }
}
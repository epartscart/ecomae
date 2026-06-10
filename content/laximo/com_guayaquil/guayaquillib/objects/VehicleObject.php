<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\Config;
use guayaquil\guayaquillib\data\Language;
use SimpleXMLElement;

class VehicleObject extends BaseGuayaquilObject
{

    protected static $mainAttributes = [
        'brand',
        'name',
        'grade',
        'transmission',
        'doors',
        'creationregion',
        'destinationregion',
        'date',
        'manufactured',
        'framecolor',
        'trimcolor',
        'datefrom',
        'dateto',
        'frame',
        'frames',
        'framefrom',
        'frameto',
        'engine',
        'engine1',
        'engine',
        'engineno',
        'options',
        'modelyearfrom',
        'modelyearto',
        'modification',
        'description',
    ];

    /**
     * @var string
     */
    public $catalog;

    /**
     * @var string
     */
    public $name;

    /**
     * @var string
     */
    public $ssd;

    /**
     * @var string
     */
    public $vehicleid;

    /**
     * @var AttributeObject[]
     */
    public $attributes;

    /**
     * @var string
     */
    public $brand;

    /**
     * @var string
     */
    public $grade;

    /**
     * @var string
     */
    public $transmission;

    /**
     * @var string
     */
    public $doors;

    /**
     * @var string
     */
    public $creationregion;

    /**
     * @var string
     */
    public $destinationregion;

    /**
     * @var string
     */
    public $date;

    /**
     * @var string
     */
    public $manufactured;

    /**
     * @var string
     */
    public $framecolor;

    /**
     * @var string
     */
    public $trimcolor;

    /**
     * @var string
     */
    public $datefrom;

    /**
     * @var string
     */
    public $dateto;

    /**
     * @var string
     */
    public $frame;

    /**
     * @var string
     */
    public $frames;

    /**
     * @var string
     */
    public $framefrom;

    /**
     * @var string
     */
    public $frameto;

    /**
     * @var string
     */
    public $engine;

    /**
     * @var string
     */
    public $engine1;

    /**
     * @var string
     */
    public $engine2;

    /**
     * @var string
     */
    public $engineno;

    /**
     * @var string
     */
    public $options;

    /**
     * @var string
     */
    public $modelyearfrom;

    /**
     * @var string
     */
    public $modelyearto;

    /**
     * @var string
     */
    public $modification;

    /**
     * @var string
     */
    public $description;

    /**
     * @var string
     */
    public $vehicleInfo;

    /**
     * @var string
     */
    public $pathData;

    /**
     * @var array
     */
    public $children;

    public function getPathData()
    {
        if (!$this->pathData) {
            $this->pathData = urlencode(strtr(base64_encode(substr($this->getVehicleInfo(), 0, 512)), '+/=', '-_,'));
        }

        return $this->pathData;
    }

    public function getVehicleInfo()
    {
        if (!$this->vehicleInfo) {
            $info = '';

            $columns = self::$mainAttributes;
            foreach ($columns as $column) {
                if ($this->$column) {
                    $info .= $column . ': ' . $this->$column . '; ';
                }
                if (!empty($this->attributes[$column])) {
                    $info .= $this->attributes[$column]->name . ': ' . $this->attributes[$column]->value . '; ';
                }
            }
            $this->vehicleInfo = $info;
        }

        return $this->vehicleInfo;
    }

    /**
     * @param null $pathId
     * @param array $addParams
     *
     * @return string
     */
    public function getVehicleLink($pathId = null, $addParams = [])
    {
        $addParams = $this->getLinkParams($pathId, $addParams);
        $addParams['checkQG'] = 1;
        $language  = new Language();

        return $language->createUrl('vehicle', '', '', $addParams);
    }

    /**
     * @param $pathId
     * @param $addParams
     *
     * @return mixed
     */
    protected function getLinkParams($pathId, $addParams)
    {
        $addParams['c']   = $this->catalog;
        $addParams['vid'] = $this->vehicleid;
        $addParams['ssd'] = $this->ssd;

        return $addParams;
    }

    /**
     * @param null $pathId
     * @param array $addParams
     *
     * @return string
     */
    public function getQGLink($pathId = null, $addParams = [])
    {
        $addParams = $this->getLinkParams($pathId, $addParams);
        $language  = new Language();

        return $language->createUrl('qgroups', '', '', $addParams);
    }

    /**
     * @param null $pathId
     * @param array $addParams
     *
     * @return string
     */
    public function getQDetailsLink($pathId = null, $addParams = [])
    {
        $addParams = $this->getLinkParams($pathId, $addParams);
        $language  = new Language();

        return $language->createUrl('qdetails', '', '', $addParams);
    }

    /**
     * @return array
     */
    public function getTooltip()
    {
        $tooltipArray = [];
        $columns      = self::$mainAttributes;
        $language     = new Language();
        foreach ($columns as $column) {
            if ($this->$column) {
                $attributeObject                     = new AttributeObject();
                $attributeObject->key                = $column;
                $attributeObject->name               = $column;
                $attributeObject->value              = $this->$column;
                $tooltipArray[$attributeObject->key] = $attributeObject;
            }
        }
        $tooltipArray = array_merge($tooltipArray, $this->attributes);

        return $tooltipArray;
    }

    /**
     * @param SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        $this->fillSimilar($data);

        if ($data->attribute instanceof SimpleXMLElement) {
            foreach ($data->attribute as $attribute) {
                $attributeObject                         = new AttributeObject($attribute);
                $this->attributes[$attributeObject->key] = $attributeObject;
            }
        }

    }

    /**
     * @param $data
     */
    private function fillSimilar($data)
    {
        $this->catalog           = (string)$data['catalog'];
        $this->name              = (string)$data['name'];
        $this->ssd               = (string)$data['ssd'];
        $this->vehicleid         = (string)$data['vehicleid'];
        $this->brand             = (string)$data['brand'];
        $this->grade             = (string)$data['grade'];
        $this->transmission      = (string)$data['transmission'];
        $this->doors             = (string)$data['doors'];
        $this->creationregion    = (string)$data['creationregion'];
        $this->destinationregion = (string)$data['destinationregion'];
        $this->date              = (string)$data['date'];
        $this->manufactured      = (string)$data['manufactured'];
        $this->framecolor        = (string)$data['framecolor'];
        $this->trimcolor         = (string)$data['trimcolor'];
        $this->datefrom          = (string)$data['datefrom'];
        $this->dateto            = (string)$data['dateto'];
        $this->frame             = (string)$data['frame'];
        $this->frames            = (string)$data['frames'];
        $this->framefrom         = (string)$data['framefrom'];
        $this->frameto           = (string)$data['frameto'];
        $this->engine            = (string)$data['engine'];
        $this->engine1           = (string)$data['engine1'];
        $this->engine2           = (string)$data['engine2'];
        $this->engineno          = (string)$data['engineno'];
        $this->options           = (string)$data['options'];
        $this->modelyearfrom     = (string)$data['modelyearfrom'];
        $this->modelyearto       = (string)$data['modelyearto'];
        $this->modification      = (string)$data['modification'];
        $this->description       = (string)$data['description'];
    }

    protected function fromJSON($data)
    {
        $this->fillSimilar($data);

        if (!empty($data['attributes'])) {
            foreach ($data['attributes'] as $attribute) {
                $attributeObject                         = new AttributeObject($attribute);
                $this->attributes[$attributeObject->key] = new AttributeObject($attribute);
            }
        }
    }


}
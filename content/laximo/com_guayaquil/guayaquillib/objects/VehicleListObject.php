<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;
use guayaquil\Config;
use guayaquil\guayaquillib\data\Language;

class VehicleListObject extends BaseGuayaquilObject
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
     * @var VehicleObject[]
     */
    public $vehicles;

    /**
     * @var array
     */
    public $tableHeaders;

    /**
     * @var array
     */
    public $tableColumns;

    /**
     * @var array
     */
    public $commonColumns;

    /**
     * @var array
     */
    public $groupedByName;


    /**
     * @param \SimpleXMLElement $data
     */
    protected function fromXml($data)
    {
        foreach ($data->row as $vehicle) {
            $this->vehicles[] = new VehicleObject($vehicle);
        }
    }

    protected function fromJSON($data)
    {
        if (!empty($data['IdentifiedVehiclesResponse']) && !empty($data['IdentifiedVehiclesResponse']['vehicles'])) {
            foreach ($data['IdentifiedVehiclesResponse']['vehicles'] as $vehicle) {
                $this->vehicles[] = new VehicleObject($vehicle);
            }
        }
    }

    public function groupColumnsByVehicles()
    {
        $columns      = self::$mainAttributes;
        $columnValues = [];
        $this->tableHeaders  = is_array($this->tableHeaders) ? $this->tableHeaders : [];
        $this->tableColumns  = [];
        $this->commonColumns = [];

        if ($this->vehicles) {
            foreach ($this->vehicles as $vehicle) {
                foreach ($columns as $column) {
                    if ($vehicle->$column) {
                        $columnValues[$column][$vehicle->$column] = isset($columnValues[$column][$vehicle->$column]) ? $columnValues[$column][$vehicle->$column] + 1 : 1;
                        $this->tableHeaders[$column]              = $column;
                    }
                }
                if ($vehicle->attributes) {
                    foreach ($vehicle->attributes as $column => $attribute) {
                        $columnValues[$column][$attribute->value] = isset($columnValues[$column][$attribute->value]) ? $columnValues[$column][$attribute->value] + 1 : 1;
                        $this->tableHeaders[$column]              = $attribute->name;
                    }
                }
            }
        }

        // Always keep identity columns visible — single-VIN roaming hits otherwise
        // collapse every field into commonColumns and the Twig table renders blank.
        $alwaysVisible = ['brand', 'name'];
        foreach ($alwaysVisible as $must) {
            if (!isset($this->tableHeaders[$must]) && $this->vehicles) {
                $this->tableHeaders[$must] = $must;
            }
        }

        foreach ($columnValues as $column => $values) {
            if (in_array($column, $alwaysVisible, true)) {
                $this->tableColumns[] = $column;
                continue;
            }
            if (count($values) > 1) {
                $this->tableColumns[] = $column;
            } else {
                foreach ($values as $value => $count) {
                    if ($count == count($this->vehicles)) {
                        $attributeObject = new AttributeObject([
                            'key'   => $column,
                            'name'  => $this->tableHeaders[$column],
                            'value' => $value
                        ]);

                        $this->commonColumns[] = $attributeObject;
                    } else {
                        $this->tableColumns[] = $column;
                    }
                }
            }
        }

        foreach ($alwaysVisible as $must) {
            if (!in_array($must, $this->tableColumns, true) && isset($this->tableHeaders[$must])) {
                array_unshift($this->tableColumns, $must);
            }
        }

        return $this;
    }

    public function groupVehiclesByName()
    {
        $vehicles = $this->vehicles;
        if (!$vehicles) {
            return false;
        }

        foreach ($vehicles as $vehicle) {
            if (!isset($this->groupedByName[$vehicle->brand . ' ' . $vehicle->name])) {
                $this->groupedByName[$vehicle->brand . ' ' . $vehicle->name] = $vehicle;
            } else {
                if ($this->groupedByName[$vehicle->brand . ' ' . $vehicle->name] !== $vehicle) {
                    $this->groupedByName[$vehicle->brand . ' ' . $vehicle->name]->children[] = $vehicle;
                }
            }
        }

        $this->groupColumnsByVehicles();

        return $this;
    }
}
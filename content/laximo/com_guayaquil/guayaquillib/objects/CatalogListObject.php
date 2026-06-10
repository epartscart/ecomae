<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\Config;
use guayaquil\guayaquillib\BaseGuayaquilObject;

class CatalogListObject extends BaseGuayaquilObject
{
    public $catalogs;

    public $carCatalogs;

    public $truckCatalogs;

    public $examples;

    protected function fromXml($data)
    {
        $carBrands = Config::$VehiclesColumns;
        foreach ($data as $catalog) {
            $catObj = new CatalogObject($catalog);
            $this->catalogs[] = $catObj;
            if (in_array($catObj->name, $carBrands)) {
                $this->carCatalogs[] = $catObj;
            } else {
                $this->truckCatalogs[] = $catObj;
            }
        }

        $this->examples = $this->getRandomExample();
    }

    private function getRandomExample()
    {
        if (!$this->catalogs) {
            $this->catalogs = [];
        }

        $rand = rand(1, count($this->catalogs));

        $count = 0;

        $vinExample   = 'WAUZZZ4M6JD010702';
        $frameExample = 'XZU423-0001026';

        foreach ($this->catalogs as $i => $catalog) {
            $count++;

            if ($count === $rand && isset($catalog->vinexample) && !empty($catalog->vinexample)) {
                $vinExample = $catalog->vinexample;

                break;
            }
        }

        $count = 0;
        $rand  = rand(1, count($this->catalogs));

        foreach ($this->catalogs as $i => $catalog) {
            $count++;

            if ($count === $rand && isset($catalog->frameexample) && !empty($catalog->frameexample)) {
                $frameExample = $catalog->frameexample;

                break;
            }
        }

        $examples = [$vinExample, $frameExample];

        return $examples;
    }
}
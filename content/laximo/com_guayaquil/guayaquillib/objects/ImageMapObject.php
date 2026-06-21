<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class ImageMapObject extends BaseGuayaquilObject
{
    public $mapObjects;

    protected function fromXml($data)
    {
        foreach ($data->row as $mapObject) {
            $this->mapObjects[] = new MapObject($mapObject);
        }
    }
}
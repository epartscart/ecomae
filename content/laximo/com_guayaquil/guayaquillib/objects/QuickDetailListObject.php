<?php
/**
 * Created by Laximo
 * User: altunint
 * Date: 4/13/18
 * Time: 3:08 PM
 * TasK:
 */

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class QuickDetailListObject extends BaseGuayaquilObject
{
    public $categories;

    protected function fromXml($data)
    {
        foreach ($data->Category as $category) {
            $this->categories[] = new CategoryObject($category);
        }
    }
}
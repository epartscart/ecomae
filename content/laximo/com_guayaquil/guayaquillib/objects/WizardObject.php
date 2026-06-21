<?php

namespace guayaquil\guayaquillib\objects;

use guayaquil\guayaquillib\BaseGuayaquilObject;

class WizardObject extends BaseGuayaquilObject
{

    public $steps;

    protected function fromXml($data)
    {
        foreach ($data->row as $step) {
            $this->steps[] = new WizardStepObject($step);
        }
    }
}
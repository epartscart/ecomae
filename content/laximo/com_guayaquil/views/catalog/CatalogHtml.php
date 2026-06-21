<?php

namespace guayaquil\views\catalog;

use guayaquil\guayaquillib\data\GuayaquilRequestOEM;
use guayaquil\Config;
use guayaquil\CommonExtender;
use guayaquil\modules\pathway\Pathway;
use guayaquil\View;

class CatalogHtml extends View
{
    public $operation;

    public function Display($tpl = 'catalog', $view = 'view')
    {
        if ($c = $this->input->getString('c')) {
            $ssd  = $this->input->getString('ssd', '');
            $spi2 = $this->input->getString('spi2', '');


            $requests = [
                'appendGetCatalogInfo' => [],
            ];

            if ($spi2 == 't') {
                $requests['appendGetWizard2'] = [];
            }


            $params = ['c' => $c, 'ssd' => $ssd, 'spi2' => $spi2];

            $data = $this->getData($requests, $params);

            $cataloginfo  = $data[0];
            $wizardFields = isset($data[1]) ? $data[1]->steps : [];

            $pathway = new Pathway();

            $pathway->addItem($cataloginfo->name, $cataloginfo->link);

            $this->pathway = $pathway->getPathway();

            $this->cataloginfo       = $cataloginfo;
            $this->brandName         = $cataloginfo->name;
            $this->useVin            = $cataloginfo->supportvinsearch;
            $this->useFrame          = $cataloginfo->supportframesearch;
            $this->otherSearch       = $cataloginfo->operations;
            $this->wizard            = $cataloginfo->supportparameteridentification2;
            $this->wizardFields      = $wizardFields;
            $this->example           = !empty($cataloginfo->vinFrameExample) ? $cataloginfo->vinFrameExample : '';
            $this->vinExample        = $cataloginfo->vinexample;
            $this->frameExample      = $cataloginfo->frameexample;
            $this->useApplicability  = $cataloginfo->supportdetailapplicability;
            $this->showApplicability = self::showApplicability();

        }

        parent::Display($tpl, $view);
    }

}




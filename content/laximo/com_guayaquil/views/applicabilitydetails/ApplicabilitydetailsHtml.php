<?php
/**
 * Created by Laximo
 * User: elnikov.a
 * Date: 10.04.18
 * Time: 12:54
 */

namespace guayaquil\views\applicabilitydetails;


use guayaquil\Config;
use guayaquil\guayaquillib\data\Language;
use guayaquil\modules\pathway\Pathway;
use guayaquil\View;

class ApplicabilitydetailsHtml extends View
{
    public function Display($tpl = 'applicabilitydetails', $view = 'view')
    {
        $this->displayApplicabilityDetails();

        parent::Display('qdetails', 'view');
    }

    public function displayApplicabilityDetails()
    {
        $c                   = $this->input->getString('c', '');
        $ssd                 = $this->input->getString('ssd', '');
        $oem                 = $this->input->getString('oem', '');
        $vid                 = $this->input->getString('vid');
        $this->showOems      = Config::$showOemsToGuest;
        $this->applicability = true;


        $requests = [
            'appendGetCatalogInfo'          => [],
            'appendGetOemPartApplicability' => [
                'oem' => $oem
            ],
            'appendGetVehicleInfo'          => [
                'vid' => $vid
            ]
        ];

        $params = ['c' => $c, 'ssd' => $ssd, ''];

        $data = $this->getData($requests, $params);
        if ($data) {
            $details     = isset($data[1]) ? $data[1] : [];
            $vehicle     = $data[2];
            $cataloginfo = $data[0];

            $pathway = new Pathway();

            if ($cataloginfo) {
                $pathway->addItem($cataloginfo->name,
                    '/katalog-laximo?task=catalog&c=' . $cataloginfo->code . ($cataloginfo->supportparameteridentification ? '&spi=t' : '') . ($cataloginfo->supportparameteridentification2 ? '&spi2=t' : '') . ('&ssd='));

            }
            if ($vehicle) {
                $language    = new Language();
                $vehicleLink = $language->createUrl('vehicle', '', '', [
                    'c'       => $cataloginfo->code,
                    'vid'     => $vehicle->vehicleid,
                    'ssd'     => $vehicle->ssd,
                    'checkQG' => true
                ]);

                $pathway->addItem($vehicle->brand . ' ' . $vehicle->name, $vehicleLink);
            }
            $pathway->addItem($oem);

            $this->pathway = $pathway->getPathway();

            $this->gid        = $this->input->getString('gid', '');
            $this->details    = $details;
            $this->vehicle    = $vehicle;
            $this->format     = $this->input->getString('format', '');
            $this->noimage    = Config::$imagePlaceholder;
            $this->domain     = $this->getBackUrl();
            $this->linkTarget = $this->getLinkTarget();
            $this->oem        = $oem;
        }
    }

}
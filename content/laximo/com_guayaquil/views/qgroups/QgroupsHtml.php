<?php

namespace guayaquil\views\qgroups;

use guayaquil\Config;
use guayaquil\guayaquillib\data\Language;
use guayaquil\guayaquillib\objects\CatalogObject;
use guayaquil\guayaquillib\objects\PartsListObject;
use guayaquil\guayaquillib\objects\VehicleObject;
use guayaquil\modules\pathway\Pathway;
use guayaquil\View;

/**
 * Created by laximo.
 * User: elnikov.a
 * Date: 16.08.17
 * Time: 10:20
 * @property array           pathway
 * @property VehicleObject   vehicle
 * @property array           groups
 * @property CatalogObject   cataloginfo
 * @property string          ssd
 * @property string          oem
 * @property int             useApplicability
 * @property PartsListObject partsList
 * @property int             totalParts
 * @property int             total
 * @property bool            showApplicability
 */
class QgroupsHtml extends View
{
    public function Display($tpl = 'qgroups', $view = 'view')
    {
        $catalogCode = $this->input->getString('c');
        $ssd         = $this->input->getString('ssd', '');
        $oem         = $this->input->getString('oem');
        $vid         = $this->input->getString('vid', '');
        $language    = new Language();
        $params      = ['c' => $catalogCode, 'ssd' => $ssd, ''];

        $requests = [
            'appendGetCatalogInfo' => [],
            'appendGetVehicleInfo' => [
                'vid' => $vid
            ]
        ];

        if (!$oem) {
            $requests['appendListQuickGroup'] = [
                'vid' => $vid
            ];
        } else {
            $linkToQdetails = $language->createUrl('qdetails', '', '', [
                'c'   => $catalogCode,
                'oem' => $oem,
                'vid' => $vid,
                'ssd' => $ssd
            ]);

            $this->redirect($linkToQdetails);
        }

        $params['ignore_error'] = true;
        
        $data = $this->getData($requests, $params);

        if (!isset($_SESSION['logged']) && !Config::$showGroupsToGuest) {
            $this->redirect($language->createUrl('vehicle', '', '', [
                'c' => $catalogCode,
                'ssd' => $ssd,
                'vid' => $vid,
            ]));
        }

        if ($data) {

            $vehicle     = $data[1];
            $groups      = $data[2]->childGroups[0]->childGroups;
            $catalogInfo = $data[0];
            $language    = new Language();

            if (!$groups) {
                $vehicleLink = $language->createUrl('vehicle', '', '', [
                    'c'   => $vehicle->catalog ?: $this->input->getString('c'),
                    'vid' => $vehicle->vehicleid ?: $this->input->getString('vid'),
                    'ssd' => $vehicle->ssd ?: $this->input->getString('ssd')
                ]);

                $this->redirect($vehicleLink);
            }

            $pathway = new Pathway();

            $pathway->addItem($catalogInfo->name, $catalogInfo->link);
            $pathway->addItem($vehicle->brand . ' ' . $vehicle->name);

            $this->pathway          = $pathway->getPathway();
            $this->vehicle          = $vehicle;
            $this->groups           = $groups;
            $this->cataloginfo      = $catalogInfo;
            $this->ssd              = $this->input->getString('ssd', '');
            $this->oem              = $oem;
            $this->useApplicability = $catalogInfo ? $catalogInfo->supportdetailapplicability : 0;
            $this->showApplicability = Config::$showApplicability;
            $this->partsList        = isset($data[3]) ? $data[3]->oemParts : null;
            $this->totalParts       = isset($data[3]) ? $this->total = count($data[3]->oemParts) : 0;
        }

        parent::Display($tpl, $view);
    }

}
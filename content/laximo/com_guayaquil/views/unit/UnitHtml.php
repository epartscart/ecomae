<?php
/**
 * Created by Laximo.
 * User: elnikov.a
 * Date: 17.08.17
 * Time: 14:04
 */

namespace guayaquil\views\unit;

use guayaquil\Config;
use guayaquil\modules\pathway\Pathway;
use guayaquil\guayaquillib\data\Language;
use guayaquil\View;


class UnitHtml extends View
{
    public function Display($tpl = 'unit', $view = 'view')
    {
        $catalogCode = $this->input->getString('c');
        $ssd         = $this->input->getString('ssd', '');
        $uid         = $this->input->getString('uid');
        $vid         = $this->input->getString('vid');
        $params      = ['c' => $catalogCode, 'ssd' => $ssd];
        $skipped     = $this->input->getString('skipped');

        $requests = [
            'appendGetUnitInfo'        => [
                'uid' => $uid
            ],
            'appendListDetailByUnit'   => [
                'uid' => $uid
            ],
            'appendListImageMapByUnit' => [
                'uid' => $uid
            ],
            'appendGetCatalogInfo'     => [],
            'appendGetVehicleInfo'     => [
                'vid' => $vid
            ]
        ];

        $data = $this->getData($requests, $params);
        if ($data) {
            $unit        = $data[0];
            $details     = $data[1];
            $imagemap    = $data[2]->mapObjects;
            $catalogInfo = $data[3];
            $vehicle     = $data[4];
            $detailCodes = [];

            if ($details->details) {
                $detailCodes = array_map(function ($detail) {
                    return $detail->codeonimage;
                }, $details->details);
            }

            $language = new Language();

            $pathway         = new Pathway();
            $fromTask        = $this->input->getString('fromTask');
            $fromCatalogTask = $this->input->getString('fromTask');

            $pathway->addItem($catalogInfo->name, $catalogInfo->link);

            $pathway->addItem($vehicle->name, !$skipped ? $vehicle->getQGLink(null) :  null);

            $pathway->addItem($unit->name);

            $this->pathway           = $pathway->getPathway();
            $this->vehicle           = $vehicle;
            $this->cataloginfo       = $catalogInfo;
            $this->unit              = $unit;
            $this->imagemap          = $imagemap;
            $this->detailCodes       = $detailCodes;
            $this->details           = $details->details;
            $this->catalog           = $this->input->getString('c');
            $this->vid               = $this->input->getString('vid', '');
            $this->gid               = $this->input->getString('gid', '');
            $this->cid               = $this->input->getString('cid', '');
            $this->selectedCoi       = $this->input->getString('coi', '');
            $this->domain            = $this->getBackUrl();
            $this->cois              = $this->input->getString('coi') ? explode(', ',
                $this->input->getString('coi')) : '';
            $this->noimage           = Config::$imagePlaceholder;
            $this->fromCatalogTask   = $fromCatalogTask;
            $this->corrected         = $this->input->getString('corrected');
            $this->useApplicability  = $catalogInfo->supportdetailapplicability;
            $this->showOems          = Config::$showOemsToGuest;
            $this->showApplicability = self::showApplicability();
            $this->linkTarget        = $this->getLinkTarget();
            $this->hideTarget        = $this->needHideTarget();
        }

        parent::Display($tpl, $view);
    }

}
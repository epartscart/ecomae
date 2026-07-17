<?php

namespace guayaquil;

class Config
{
    public static $oemServiceUrl = 'ws.laximo.net';
    public static $amServiceUrl = 'aws.laximo.net';

    public static $useLoginAuthorizationMethod = true;
    public static $defaultUserLogin = 'au308248';
    public static $defaultUserKey = '5HcskWnQ8FPhy4LNS';

    public static $defaultAmLogin = 'au216116';
    public static $defaultAmKey = 'Y34TRgYaUNV42rd';

    public static $catalog_data = 'en_US';
    public static $ui_localization = 'en';
    public static $dev = false;
    public static $showRequest = false;
    public static $showWelcomePage = false;
    public static $showToGuest = true;
    public static $showGroupsToGuest = true;
    public static $showOemsToGuest = true;
    public static $showApplicability = true;
    public static $showCatalogsLetters = true;
    public static $catalogColumns = 4;
    public static $VehiclesColumns = 4;
    public static $oemExample = '';
    public static $defaultVin = '';
    public static $defaultFrame = '';
    public static $groupVehicles = false;
    public static $vehiclesMaxField = 10;
    public static $imagePlaceholder = '';
    public static $useEnvParams = false;
    public static $hideFooter = true;
    public static $theme = 'guayaquil';
    public static $SiteDomain = '';
    public static $backurlError = '';
    public static $linkTarget = '_self';
    public static $toolbarPages = ['catalogs', 'catalog', 'vehicles', 'vehicle', 'unit', 'qgroups', 'qdetails', 'aftermarket', 'applicabilitydetails', 'wizard2'];
}

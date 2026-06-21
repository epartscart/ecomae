#Guayaquil v2.0
*Read the documentation for details: [wiki.technologytrade.ru](http://wiki.technologytrade.ru)*

**Requirements:**

* PHP 5.6 +
* php-soap
* php-xml
* php-zip
* php-openssl
* php-mbstring
* php-curl

###How to install?
**Demo:**
> 1. Place the files in a directory accessible to the web server.
> 2. Run "php composer.phar install" in library directory.
> 3. Use index.php like entry point to show demo.

###How to  use lib?
> 1. import GuayaquilRequestOEM or GuayaquilRequestAM classes.
> 2. Use instance of GuayaquilRequestOEM or GuayaquilRequestAM to create request. Add requests by "append" methods.

###Configuration
> 1. Copy ConfigExample.php and rename it to Config.php
> 2. Rename class of new file to Config
> 3. Set the required parameters inside the class

**Get list catalogs example:** 

    $request = new GuayaquilRequestOEM('', '', 'en_US');
    $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);

    $request->appendListCatalogs();

    $data = $request->query();

    if ($request->error) {
        /** handle error*/
    }

    /**
     * @var CatalogListObject $catalogList
     */
    $catalogList = array_shift($data);
    $allCatalogs = $catalogList->catalogs;

**Find by VIN example:**
    
    $catalogCode = 'AU1221';
    $vin = 'WAUZZZ4M0HD042149';
    
    $request = new GuayaquilRequestOEM($catalogCode, '', 'en_US');
    $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
    
    $request->appendFindVehicleByVIN($vin);
    $data = $request->query(); /** Now you can see VehicleListObject in $data[0] */

**Get catalog info example:**

    $catalogCode = 'AU1221';
    $request = new GuayaquilRequestOEM($catalogCode, '', 'en_US');
    $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
    
    $request->appendGetCatalogInfo();
    $data = $request->query(); /** Now you can see CatalogObject in $data[0] */
    
**List quick group or categories tree example:**

     $catalogCode = 'HONDA2017';
     $vin         = 'JHMCL96806C216721';

     $request = new GuayaquilRequestOEM('HONDA2017', '', 'en_US');
     $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
     
     $request->appendFindVehicle($vin);
     $data = $request->query();

     $allFoundedVehicles = array_shift($data);

     /**
      * @var VehicleObject $firstVehicleForExample
      */
     $firstVehicleForExample = array_shift($allFoundedVehicles->vehicles);

     $request = new GuayaquilRequestOEM($firstVehicleForExample->catalog, $firstVehicleForExample->ssd, 'en_US');
     $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);

     $request->appendListQuickGroup($firstVehicleForExample->vehicleid);
     $request->appendListCategories($firstVehicleForExample->vehicleid, -1, $firstVehicleForExample->ssd);

     $data = $request->query();

     if ($request->error) {
         /** handle error*/
     }

     $quickGroupsTree = $data[0];
     $categoriesTree  = $data[1];
  
**Multiple requests (You can use up to five at a time):**
    
    $catalogCode = 'AU1221';
    $vin = 'WAUZZZ4M0HD042149';
    $request = new GuayaquilRequestOEM($catalogCode, '', 'en_US');
    $request->setUserAuthorizationMethod(Config::$defaultUserLogin, Config::$defaultUserKey);
    
    $request->appendGetCatalogInfo();
    $request->appendFindVehicleByVIN($vin);
    $data = $request->query(); /** Now you can see CatalogObject in $data[0] and VehicleListObject in $data[1] */

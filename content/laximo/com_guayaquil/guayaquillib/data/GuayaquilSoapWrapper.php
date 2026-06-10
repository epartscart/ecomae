<?php

namespace guayaquil\guayaquillib\data;

use guayaquil\Config;

class GuayaquilSoapWrapper
{
    ///////////////////////////// Configuration

    public $authorized;
    private $authMethod;
    private $certificateFileName;
    private $certificateKeyFileName;
    private $certificatePassword;
    private $userLogin;

    //////////////////////////// Results

    // If error != '' then request processing error occured
    private $userSecretKey;
    private $error = '';

    // SimpleXML object
    private $errorTrace = '';
    private $data;
    private $textRequests;

    public function setCertificateAuthorizationMethod($certificateFolder = false, $certificatePassword = false)
    {
        if (!$certificateFolder) {
            $certificateFolder = dirname(__FILE__) . DIRECTORY_SEPARATOR . 'cert';
        }

        $this->authMethod             = 'certificate';
        $this->certificateFileName    = $certificateFolder . '/client.pem';
        $this->certificateKeyFileName = $certificateFolder . '/client.key';
        $this->certificatePassword    = $certificatePassword ? $certificatePassword : "123ertGHJ";
    }

    public function getTextRequests() {
        return $this->textRequests;
    }

    public function setUserAuthorizationMethod($login, $key, $oem = true)
    {
        if (Config::$useEnvParams) {
            $envCustomerLogin    = base64_decode(getenv('UUE_CUSTOMER_LOGIN'));
            $envCustomerPassword = base64_decode(getenv('UUE_CUSTOMER_PASSWORD'));
            $login               = $envCustomerLogin;
            $key                 = $envCustomerPassword;
        }

        if ($oem) {
            if (isset($_SESSION['key']) && isset($_SESSION['username'])) {
                $login = $_SESSION['username'];
                $key   = $_SESSION['key'];
            }
        } else {
            if (isset($_SESSION['am_key']) && isset($_SESSION['am_username'])) {
                $login = $_SESSION['am_username'];
                $key   = $_SESSION['am_key'];
            }
        }

        $this->authMethod    = 'login';
        $this->userLogin     = $login;
        $this->userSecretKey = $key;
    }

    function queryData($request, $oem_service = true)
    {
        try {
            $client = $this->getSoapClient($oem_service);
            if ($this->authMethod == 'certificate') {
                try {
                    $this->data = $client->QueryData($request);
                } catch (\Exception $exc) {
                    $this->error      = $this->parseError($exc->getMessage());
                    $this->errorTrace = $exc->getTrace();
                }

            } else {
                try {
                    $this->textRequests[] = $request;
                    $this->data = $client->QueryDataLogin($request, $this->userLogin,
                        md5($request . $this->userSecretKey));
                } catch (\Exception $exep) {
                    $this->error = $this->parseError($exep->getMessage());

                    return false;
                }
            }

            return $this->data;
        } catch (Exception $ex) {
            $this->error = $this->parseError($ex->getMessage());

            return false;
        }
    }

    function getSoapClient($laximoOem = true)
    {
        $options = array(
            'compression' => SOAP_COMPRESSION_ACCEPT | SOAP_COMPRESSION_GZIP,
        );

        if ($laximoOem) {
            $options['uri']      = 'http://WebCatalog.Kito.ec';
            $options['location'] = ($this->authMethod == 'certificate' ? 'https' : 'http') . '://' . Config::$oemServiceUrl . '/ec.Kito.WebCatalog/services/Catalog.CatalogHttpSoap11Endpoint/';
        } else {
            $options['uri']      = 'http://Aftermarket.Kito.ec';
            $options['location'] = ($this->authMethod == 'certificate' ? 'https' : 'http') . '://' . Config::$amServiceUrl . '/ec.Kito.Aftermarket/services/Catalog.CatalogHttpSoap11Endpoint/';
        }

        if ($this->authMethod == 'certificate') {
            $options['sslCertPath']   = $this->certificateFileName;
            $options['sslKeyPath']    = $this->certificateKeyFileName;
            $options['passphrase']    = $this->certificatePassword;
            $options['sslcertpasswd'] = $this->certificatePassword;
            $options['verifypeer']    = 0;
            $options['verifyhost']    = 0;
        }

        $client = new SSLSoapClient(null, $options);

        return $client;

    }

    function parseError($err)
    {
        if (strpos($err, "cURL ERROR: 35")) {
            return 'Not Connected';
        }

        if (strpos($err, "cURL ERROR: 58")) {
            return 'No Certificate';
        }

        if (strpos($err, "400 Bad Request")) {
            return 'Certificate expired';
        }

        $e   = explode("<br>", $err, 2);
        $err = $e[0];
        $pos = strrpos($err, 'E_');
        if ($pos === false) {
            $pos = strrpos($err, ':') + 1;
        }

        return substr($err, $pos);
    }

    public function getError()
    {
        return $this->error;
    }

    public function getErrorTrace()
    {
        return $this->errorTrace;
    }

    public function getData()
    {
        return $this->data;
    }
}

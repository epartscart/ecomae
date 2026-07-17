<?php
namespace guayaquil\guayaquillib\data;

use guayaquil\Config;
use guayaquil\guayaquillib\data\SSLSoapException;

/**
 * SoapClient that performs the HTTP transport via cURL.
 *
 * Flow:
 *  1) __doRequest stores the request + location and throws SSLSoapException
 *  2) __soapCall catches it, runs cURL, stores the response string
 *  3) Second __soapCall/__doRequest returns that string to the SoapClient parser
 *
 * Critical: __doRequest must NEVER return a non-string (PHP fatals /
 * "returned non string value" — which blanked the Laximo catalog page).
 */
class SSLSoapClient extends \SoapClient
{

    private $cookies = array();
    /** @var string|null */
    private $lastRequest;
    /** @var string|null */
    private $lastResponse;
    /** @var string */
    private $lastLocation = '';
    /** @var array */
    private $options = array();
    /** @var resource|\CurlHandle|null */
    private $curlHandle;
    public $error;


    public function __construct($wsdl, $options = array())
    {
        $this->options = $options;
        parent::__construct($wsdl, $this->options);
        $this->initCurl();
    }

    public function __destruct()
    {
        if (is_resource($this->curlHandle) || (is_object($this->curlHandle) && $this->curlHandle instanceof \CurlHandle)) {
            curl_close($this->curlHandle);
        }
    }

    protected function callCurl()
    {
        $url = $this->lastLocation !== '' ? $this->lastLocation : (string) ($this->location ?? '');
        if ($url === '') {
            throw new \SoapFault('HTTP', 'SOAP location URL is empty');
        }
        // Prefer HTTPS — Laximo endpoints reject/empty many plain HTTP calls.
        if (stripos($url, 'http://') === 0) {
            $url = 'https://' . substr($url, 7);
        }

        curl_setopt($this->curlHandle, CURLOPT_URL, $url);
        curl_setopt($this->curlHandle, CURLOPT_POSTFIELDS, $this->lastRequest);

        if ($cookies = $this->getCookieAsString()) {
            curl_setopt($this->curlHandle, CURLOPT_COOKIE, $cookies);
        }

        $response = curl_exec($this->curlHandle);
        $curlErr = curl_error($this->curlHandle);
        $httpCode = (int) curl_getinfo($this->curlHandle, CURLINFO_HTTP_CODE);

        if ($response === false || $response === null) {
            $this->lastResponse = null;
            $this->error = $curlErr !== '' ? $curlErr : 'Empty SOAP response';
            throw new \SoapFault('HTTP', 'cURL ERROR: ' . $this->error);
        }

        $this->lastResponse = (string) $response;

        if ($httpCode >= 400) {
            $xml = @simplexml_load_string($this->lastResponse, 'SimpleXMLElement', 0, 'soapenv', true);
            if ($xml) {
                $fault = $xml->Body->Fault->children('');
                throw new \SoapFault((string) $fault->faultcode, (string) $fault->faultstring);
            }
            throw new \SoapFault("Error code $httpCode", substr($this->lastResponse, 0, 500));
        }
    }

    public function __setCookie($name, $value = null)
    {
        if (is_null($value)) {
            unset($this->cookies[$name]);
        } else {
            $this->cookies[$name] = $value;
        }
    }

    private function getCookieAsString()
    {
        $out = null;
        if ($this->cookies) {
            $buf = array();
            foreach ($this->cookies as $name => $val) {
                $buf[] = urlencode($name) . '=' . urlencode($val);
            }
            $out = implode('; ', $buf);
        }
        return $out;
    }

    protected function initCurl()
    {
        $this->curlHandle = curl_init();
        curl_setopt($this->curlHandle, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($this->curlHandle, CURLOPT_HEADER, false);
        curl_setopt($this->curlHandle, CURLOPT_FOLLOWLOCATION, true);
        curl_setopt($this->curlHandle, CURLOPT_FORBID_REUSE, true);
        curl_setopt($this->curlHandle, CURLOPT_FRESH_CONNECT, true);
        curl_setopt($this->curlHandle, CURLOPT_ENCODING, 'gzip,deflate');
        curl_setopt($this->curlHandle, CURLOPT_HTTPHEADER, array('Content-Type: text/xml; charset=utf-8', 'Expect:'));
        curl_setopt($this->curlHandle, CURLOPT_POST, true);
        curl_setopt($this->curlHandle, CURLOPT_CONNECTTIMEOUT, 15);
        curl_setopt($this->curlHandle, CURLOPT_TIMEOUT, 60);
        curl_setopt($this->curlHandle, CURLOPT_SSL_VERIFYPEER, false);
        curl_setopt($this->curlHandle, CURLOPT_SSL_VERIFYHOST, false);

        if (!empty($this->options['sslCertPath']) && is_file((string) $this->options['sslCertPath'])) {
            $this->setSslCert((string) $this->options['sslCertPath']);
        }
        if (!empty($this->options['sslKeyPath']) && is_file((string) $this->options['sslKeyPath'])) {
            $this->setSslKey((string) $this->options['sslKeyPath']);
        }
        if (!empty($this->options['passphrase'])) {
            curl_setopt($this->curlHandle, CURLOPT_SSLCERTPASSWD, (string) $this->options['passphrase']);
        }
    }

    public function setSslCert($sslCertPath)
    {
        if (file_exists($sslCertPath)) {
            $this->setCurlOption(CURLOPT_SSLCERT, $sslCertPath);
        } else {
            throw new SSLSoapException("SSLCERT file {$sslCertPath} not found.");
        }
    }

    public function setSslKey($sslKeyPath)
    {
        if (file_exists($sslKeyPath)) {
            $this->setCurlOption(CURLOPT_SSLKEY, $sslKeyPath);
        } else {
            throw new SSLSoapException("SSLKEY file {$sslKeyPath} not foud.");
        }
    }

    public function setCurlOption($key, $value)
    {
        curl_setopt($this->curlHandle, $key, $value);
    }

    public function __call($functionName, $arguments)
    {
        $this->lastResponse = null;
        return $this->__soapCall($functionName, $arguments);
    }

    public function __getLastRequest()
    {
        return $this->lastRequest;
    }

    public function __getLastResponse()
    {
        return $this->lastResponse;
    }

    public function __soapCall(
        $function_name, $arguments, $options = null, $input_headers = null, &$output_headers = null
    ) {
        try {
            $this->lastResponse = null;
            return parent::__soapCall($function_name, $arguments, $options, $input_headers, $output_headers);
        } catch (SSLSoapException $e) {
            $this->error = $e->getMessage();
        }

        $this->callCurl();
        // Re-enter parser with cURL body already stored as string.
        return parent::__soapCall($function_name, $arguments, $options, $input_headers, $output_headers);
    }

    public function __doRequest($request, $location, $action, $version, $one_way = 0)
    {
        if (is_string($this->lastResponse) && $this->lastResponse !== '') {
            $out = $this->lastResponse;
            $this->lastResponse = null;
            return $out;
        }

        $this->lastRequest = (string) $request;
        $this->lastLocation = (string) $location;
        $this->lastResponse = null;

        throw new SSLSoapException('use-curl-transport');
    }
}

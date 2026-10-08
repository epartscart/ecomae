#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/epc_registration_enhanced.php (with the real
epc_uae_customer_vat.php and epc_einvoice.php it loads) behind `php -S`, so that the KYC documents are genuine HTTP
uploads. Each case posts its fields and files (b"x" * size) against a throwaway MariaDB schema; the result of every
operation, the users_profiles, epc_einvoice_buyer_profiles and epc_einvoice_settings rows and the stored KYC files are
recorded. Times are written as "T" and the date stamp of a KYC file name as "D".
Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, secrets, shutil, socket, subprocess, sys, tempfile, time, urllib.parse, urllib.request

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]

RUN_PHP = r"""<?php
define('_ASTEXE_', 1);
$case = json_decode(file_get_contents($_GET['case']), true);
$db_link = new PDO('mysql:host=127.0.0.1;dbname=' . $_GET['db'], 'ecomae', getenv('EPC_H_PW'));
$db_link->query('SET NAMES utf8mb4;');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_enhanced.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
$post = $_POST;
$results = array();
foreach ($case['ops'] as $a) {
    try {
        switch ($a[0]) {
            case 'validate': epc_reg_validate_enhanced_fields($post, $a[1]); $results[] = 'ok'; break;
            case 'validate_uae': epc_reg_validate_uae_fields($post); $results[] = 'ok'; break;
            case 'save': epc_reg_save_enhanced_profile($db_link, $a[1], $post, $a[2], $a[3]); $results[] = 'ok'; break;
            case 'save_uae': epc_reg_save_uae_buyer_profile($db_link, $a[1], $post, $a[2], $a[3]); $results[] = 'ok'; break;
            case 'vat_sync': $results[] = epc_uae_customer_vat_sync($db_link, $a[1]); break;
            case 'buyer':
                $b = epc_einvoice_buyer_profile($db_link, $a[1]);
                ksort($b);
                if (isset($b['time_updated']) && (int) $b['time_updated'] > 0) { $b['time_updated'] = 'T'; }
                $results[] = array_map(function ($v) { return $v === null ? null : (string) $v; }, $b);
                break;
            default: $results[] = 'unknown op';
        }
    } catch (Throwable $e) {
        $results[] = array('error' => $e->getMessage());
    }
}
function rows($db_link, $sql) {
    try {
        return array_map(function ($r) { return array_map(function ($v) { return $v === null ? null : (string) $v; }, $r); },
            $db_link->query($sql)->fetchAll(PDO::FETCH_NUM));
    } catch (Throwable $e) { return null; }
}
$buyers = rows($db_link, 'SELECT user_id, buyer_name, trn, tin, legal_reg_no, legal_reg_type, authority_name, address_line1, city, emirate, country_code, phone, email, electronic_id, peppol_endpoint, buyer_onboarded, time_updated FROM epc_einvoice_buyer_profiles ORDER BY user_id');
foreach ((array) $buyers as $i => $b) { $buyers[$i][16] = 'T'; }
$files = array();
$kyc = $_SERVER['DOCUMENT_ROOT'] . '/content/files/kyc';
if (is_dir($kyc)) {
    $it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($kyc, FilesystemIterator::SKIP_DOTS));
    foreach ($it as $f) {
        $files[] = array(preg_replace('/_\d{8}_\d{6}\./', '_D.', substr($f->getPathname(), strlen($_SERVER['DOCUMENT_ROOT']))), $f->getSize());
    }
    sort($files);
}
$profiles = rows($db_link, 'SELECT user_id, data_key, data_value FROM users_profiles ORDER BY user_id, data_key, id');
foreach ((array) $profiles as $i => $p) {
    if ($p[2] !== null) { $profiles[$i][2] = preg_replace('/_\d{8}_\d{6}\./', '_D.', $p[2]); }
}
echo json_encode(array(
    'helpers' => array(epc_reg_customer_type($post), epc_reg_country_code($post), epc_reg_trn_mode($post), epc_reg_uae_requested($post), epc_reg_extract_trn($post)),
    'results' => $results,
    'profiles' => $profiles,
    'buyers' => $buyers,
    'settings' => rows($db_link, 'SELECT setting_key, setting_value FROM epc_einvoice_settings ORDER BY setting_key'),
    'files' => $files,
), JSON_UNESCAPED_UNICODE);
"""


def multipart(fields, files):
    boundary = "----epcharness" + secrets.token_hex(8)
    parts = []
    for key, value in fields.items():
        parts.append(("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n" % (boundary, key)).encode() + value.encode("utf-8") + b"\r\n")
    for key, (name, size) in files.items():
        head = "--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: application/octet-stream\r\n\r\n" % (boundary, key, name)
        parts.append(head.encode() + b"x" * size + b"\r\n")
    parts.append(("--%s--\r\n" % boundary).encode())
    return b"".join(parts), "multipart/form-data; boundary=" + boundary


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    seed = open(os.path.join(here, "seed.sql"), encoding="utf-8").read()
    old = os.umask(0o077)
    work = tempfile.mkdtemp()
    cnf = os.path.join(work, "client.cnf")
    with open(cnf, "w") as f:
        f.write("[client]\nuser=ecomae\nhost=127.0.0.1\nport=3306\npassword=%s\ndefault-character-set=utf8mb4\n" % password)
    os.umask(old)
    os.chmod(work, 0o755)

    def sql(statement, db="mysql"):
        return subprocess.run(["mysql", "--defaults-extra-file=" + cnf, "-N", "-B", db, "-e", statement],
                              check=True, capture_output=True, text=True).stdout

    docroot = os.path.join(work, "root")
    os.makedirs(os.path.join(docroot, "content", "files"))
    for rel in ("content/users", "content/shop"):
        os.symlink(os.path.join(root, rel), os.path.join(docroot, rel))
    open(os.path.join(docroot, "run.php"), "w", encoding="utf-8").write(RUN_PHP)

    sock = socket.socket()
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()
    server = subprocess.Popen(["php", "-d", "upload_max_filesize=20M", "-d", "post_max_size=40M", "-d", "display_errors=stderr",
                               "-S", "127.0.0.1:%d" % port, "-t", docroot],
                              env=dict(os.environ, EPC_H_PW=password), stdout=subprocess.DEVNULL, stderr=open(os.path.join(work, "server.log"), "w"))
    time.sleep(0.8)

    goldens = {}
    try:
        for case in cases:
            db = "ecomae_cpw_" + secrets.token_hex(6)
            sql("CREATE DATABASE `%s` DEFAULT CHARACTER SET utf8mb4" % db)
            try:
                sql(seed, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                shutil.rmtree(os.path.join(docroot, "content", "files", "kyc"), ignore_errors=True)
                case_file = os.path.join(work, "case.json")
                json.dump(case, open(case_file, "w", encoding="utf-8"))
                body, ctype = multipart(case.get("post", {}), case.get("files", {}))
                url = "http://127.0.0.1:%d/run.php?%s" % (port, urllib.parse.urlencode({"case": case_file, "db": db}))
                req = urllib.request.Request(url, data=body, headers={"Content-Type": ctype})
                text = urllib.request.urlopen(req, timeout=60).read().decode("utf-8")
                try:
                    goldens[case["name"]] = json.loads(text)
                except ValueError:
                    print(case["name"], text[:2000], file=sys.stderr)
                    raise
            finally:
                sql("DROP DATABASE IF EXISTS `%s`" % db)
    finally:
        server.terminate()
        server.wait()
        log = open(os.path.join(work, "server.log")).read()
        if any(w in log for w in ("Warning", "Fatal", "Notice", "Deprecated")):
            print(log[-4000:], file=sys.stderr)
        shutil.rmtree(work, ignore_errors=True)

    out = os.path.join(here, "goldens.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(goldens, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())

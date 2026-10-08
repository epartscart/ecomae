#!/usr/bin/env python3
"""Regenerates goldens.json from the real lib/captcha scripts behind `php -S` (needs php-gd with FreeType, and
request_order=GP as in php.ini-production). check_captcha.php: the body for each case in cases.json. captcha.php: over
40 requests, the status, content type, the cache headers in order, the cookie attributes, the PNG size, and that the
cookie is the md5 of a 4 to 7 character code (checked by trying every code the image could hold is impractical, so the
cookie is only checked for shape). A "broken" shape is PHP picking lib/captcha/bg/index.html as a background
(a 500 TypeError about one request in 31), which ASP.NET never does. One PHP image is kept as sample_php.png for a visual comparison."""
import http.client, json, os, re, shutil, socket, subprocess, sys, tempfile, time, urllib.parse

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    work = tempfile.mkdtemp()
    docroot = os.path.join(work, "root")
    os.makedirs(os.path.join(docroot, "lib"))
    os.symlink(os.path.join(root, "lib", "captcha"), os.path.join(docroot, "lib", "captcha"))
    sock = socket.socket()
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()
    log = open(os.path.join(work, "server.log"), "w")
    server = subprocess.Popen(["php", "-d", "request_order=GP", "-d", "display_errors=0", "-d", "log_errors=1", "-d", "error_log=" + os.path.join(work, "php.log"),
                               "-S", "127.0.0.1:%d" % port, "-t", docroot], stdout=subprocess.DEVNULL, stderr=log)
    time.sleep(0.8)

    def request(method, path, body=None, headers=None):
        conn = http.client.HTTPConnection("127.0.0.1", port, timeout=30)
        conn.request(method, path, body=body, headers=headers or {})
        response = conn.getresponse()
        data = response.read()
        conn.close()
        return response, data

    goldens = {"check": {}, "image": None}
    try:
        for case in cases:
            path = "/lib/captcha/check_captcha.php"
            if case.get("query"):
                path += "?" + urllib.parse.urlencode(case["query"])
            headers = {}
            body = None
            if case.get("cookie") is not None:
                headers["Cookie"] = "captcha=" + case["cookie"]
            if case["method"] == "POST":
                body = urllib.parse.urlencode(case.get("post", {}))
                headers["Content-Type"] = "application/x-www-form-urlencoded"
            response, data = request(case["method"], path, body, headers)
            goldens["check"][case["name"]] = {"status": response.status, "body": data.decode("utf-8"), "type": response.getheader("Content-Type")}

        shapes = set()
        for i in range(40):
            response, data = request("GET", "/lib/captcha/captcha.php?rid=0.%d" % i)
            if not data.startswith(b"\x89PNG"):
                shapes.add(("broken", response.status))
                continue
            width, height = int.from_bytes(data[16:20], "big"), int.from_bytes(data[20:24], "big")
            cookie = response.getheader("Set-Cookie")
            match = re.fullmatch(r"captcha=([0-9a-f]{32}); expires=[^;]+; Max-Age=(\d+); path=(/)", cookie or "")
            shapes.add(("png", response.status, response.getheader("Content-Type"), width, height, bool(match), match.group(2) if match else None, match.group(3) if match else None))
            if i == 0:
                headers = [(k, v) for k, v in response.getheaders() if k in ("Expires", "Last-Modified", "Cache-Control", "Pragma")]
                goldens["image"] = {"headers": headers}
                open(os.path.join(work, "sample_php.png"), "wb").write(data)
                shutil.copy(os.path.join(work, "sample_php.png"), "/tmp/captcha_sample_php.png")
        goldens["image"]["shapes"] = sorted([list(s) for s in shapes], key=json.dumps)
    finally:
        server.terminate()
        server.wait()
        text = open(os.path.join(work, "php.log")).read() if os.path.exists(os.path.join(work, "php.log")) else ""
        if "Fatal" in text or "Warning" in text:
            print(text[-4000:], file=sys.stderr)
        shutil.rmtree(work, ignore_errors=True)

    with open(os.path.join(here, "goldens.json"), "w", encoding="utf-8") as f:
        json.dump(goldens, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("wrote goldens.json")


if __name__ == "__main__":
    sys.exit(main())

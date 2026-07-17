/**
 * Native push registration for the Capacitor CP/ERP app.
 *
 * Load inside the app WebView (the CP shell already runs in Capacitor). On a
 * real device it asks for permission, gets the FCM/APNs token, and registers it
 * with pyapi using the same admin session cookie. No-ops in a plain browser.
 *
 *   <script src="/pyapi/static/epc_push_register.js" defer></script>
 */
(function () {
  'use strict';

  function registerToken(token, platform) {
    return fetch('/pyapi/v1/push/register', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: token, platform: platform, app: 'cp' }),
    }).catch(function () {});
  }

  function init() {
    if (!window.Capacitor || !Capacitor.Plugins || !Capacitor.Plugins.PushNotifications) {
      return; // plain browser or plugin absent — nothing to do
    }
    var Push = Capacitor.Plugins.PushNotifications;
    var platform = (Capacitor.getPlatform && Capacitor.getPlatform()) || 'android';

    Push.addListener('registration', function (t) {
      if (t && t.value) registerToken(t.value, platform);
    });
    Push.addListener('registrationError', function () {});

    Push.requestPermissions().then(function (res) {
      if (res && res.receive === 'granted') {
        Push.register();
      }
    }).catch(function () {});
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  } else {
    init();
  }
})();

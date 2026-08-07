(() => {
  const KEY = 'lifeos-client-session';
  const $ = (id) => document.getElementById(id);
  const showErr = (msg) => {
    const el = $('lojoin-error');
    if (!el) return;
    el.hidden = !msg;
    el.textContent = msg || '';
  };
  const saveSession = (client) => {
    try {
      localStorage.setItem(KEY, JSON.stringify({
        clientId: client.clientId,
        token: client.joinToken,
        displayName: client.displayName,
        at: new Date().toISOString()
      }));
    } catch (_) {}
  };
  const detect = () => {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone || '';
    const locale = navigator.language || '';
    const platform = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || '';
    const source = window.matchMedia('(display-mode: standalone)').matches
      ? 'mobile-pwa'
      : (/Mobi|Android/i.test(navigator.userAgent) ? 'mobile-web' : 'web');
    return {
      timeZone: tz,
      locale,
      platform,
      joinSource: source,
      userAgent: navigator.userAgent || '',
      referrer: document.referrer || ''
    };
  };
  const paintResult = (data) => {
    const box = $('lojoin-result');
    if (!box || !data || !data.ok) return;
    box.hidden = false;
    $('lojoin-message').textContent = data.message || 'Joined.';
    const c = data.client || {};
    $('lojoin-details').innerHTML =
      '<li>Client: <strong>' + (c.displayName || '') + '</strong> · clone <strong>' + (c.cloneName || '') + '</strong></li>' +
      '<li>Id: <code>' + (c.clientId || '') + '</code></li>' +
      '<li>Country: <strong>' + (c.country || c.countryCode || '—') + '</strong>' + (c.city ? ' · ' + c.city : '') + '</li>' +
      '<li>Timezone: ' + (c.timeZone || '—') + ' · Source: ' + (c.joinSource || '—') + '</li>' +
      '<li>Token saved on this signed-in device (keep private)</li>';
    $('lojoin-open-companion').href = data.companionUrl || '#';
    $('lojoin-open-results').href = data.resultsUrl || '#';
    const ol = $('lojoin-next');
    ol.innerHTML = '';
    (data.nextSteps || []).forEach((s) => {
      const li = document.createElement('li');
      li.textContent = s;
      ol.appendChild(li);
    });
    saveSession(c);
    box.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };
  const join = async (payload) => {
    showErr('');
    const status = $('lojoin-status');
    const submit = $('lojoin-submit');
    if (status) status.textContent = 'Joining…';
    if (submit) submit.disabled = true;
    try {
      const res = await fetch('/lifeos/join', {
        method: 'POST',
        headers: { 'content-type': 'application/json', 'accept': 'application/json' },
        body: JSON.stringify(payload)
      });
      const data = await res.json();
      if (!res.ok || !data.ok) {
        throw new Error(data.message || data.error || ('Join failed (' + res.status + ')'));
      }
      if (status) status.textContent = 'Joined successfully.';
      paintResult(data);
    } catch (e) {
      showErr(e.message || String(e));
      if (status) status.textContent = '';
    } finally {
      if (submit) submit.disabled = false;
    }
  };

  const bind = () => {
    const form = $('lojoin-form');
    if (!form || form.dataset.bound === '1') return;
    form.dataset.bound = '1';
    form.addEventListener('submit', (ev) => {
      ev.preventDefault();
      const name = (($('lojoin-name') && $('lojoin-name').value) || '').trim();
      if (!name) { showErr('Please enter your name.'); return; }
      const countryRaw = (($('lojoin-country') && $('lojoin-country').value) || '');
      if (!countryRaw) { showErr('Please select your country.'); return; }
      const parts = countryRaw.split('|');
      const country = parts[0];
      const countryCode = parts[1];
      const d = detect();
      join({
        displayName: name,
        email: (($('lojoin-email') && $('lojoin-email').value) || '').trim() || null,
        country,
        countryCode,
        city: (($('lojoin-city') && $('lojoin-city').value) || '').trim() || null,
        timeZone: d.timeZone,
        locale: d.locale,
        platform: d.platform,
        userAgent: d.userAgent,
        referrer: d.referrer,
        joinSource: d.joinSource,
        useTestClient: false
      });
    });

    const testBtn = $('lojoin-test');
    if (testBtn) {
      testBtn.addEventListener('click', () => {
        const d = detect();
        join({
          useTestClient: true,
          timeZone: d.timeZone,
          locale: d.locale,
          platform: d.platform,
          userAgent: d.userAgent,
          joinSource: 'test'
        });
      });
    }

    let deferred;
    window.addEventListener('beforeinstallprompt', (e) => { e.preventDefault(); deferred = e; });
    const installBtn = $('lojoin-install-btn');
    if (installBtn) {
      installBtn.addEventListener('click', async () => {
        if (deferred) {
          deferred.prompt();
          try { await deferred.userChoice; } catch (_) {}
          deferred = null;
          return;
        }
        alert('On iPhone: Share → Add to Home Screen. On Android Chrome: menu → Install app.');
      });
    }
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register('/lifeos/sw.js', { scope: '/lifeos/' }).catch(() => {});
    }
  };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();

(() => {
  const KEY = 'lifeos-client-session';
  const params = new URLSearchParams(location.search);
  const loadSession = () => {
    let clientId = params.get('clientId') || '';
    let token = params.get('token') || '';
    if (!clientId || !token) {
      try {
        const s = JSON.parse(localStorage.getItem(KEY) || '{}');
        clientId = clientId || s.clientId || 'test-amina';
        token = token || s.token || 'lifeos-test-amina-join';
      } catch (_) {
        clientId = clientId || 'test-amina';
        token = token || 'lifeos-test-amina-join';
      }
    }
    return { clientId, token };
  };
  let session = loadSession();
  let human = 'Amina';
  let clone = 'Amina';
  const speak = (text) => {
    try {
      if (!('speechSynthesis' in window) || !text) return;
      window.speechSynthesis.cancel();
      const u = new SpeechSynthesisUtterance(text);
      u.rate = 1.02;
      window.speechSynthesis.speak(u);
    } catch (_) {}
  };
  const setBubble = (id, text, doSpeak) => {
    const el = document.getElementById(id);
    if (!el) return;
    el.hidden = !text;
    el.textContent = text || '';
    if (text && doSpeak) speak(text);
  };
  const renderFeed = (items) => {
    const ul = document.getElementById('locomp-track-feed');
    if (!ul) return;
    if (!items || !items.length) {
      ul.innerHTML = '<li class="muted">No events yet — tap Start walk.</li>';
      return;
    }
    ul.innerHTML = items.map((a) =>
      '<li><strong>' + (a.label || a.kind) + '</strong> · ' + (a.atUtc || a.at || '') +
      (a.cloneReply ? '<br><em>' + a.cloneReply + '</em>' : '') + '</li>'
    ).join('');
  };
  const refresh = async () => {
    const url = '/lifeos/companion?clientId=' + encodeURIComponent(session.clientId) +
      '&token=' + encodeURIComponent(session.token);
    const res = await fetch(url, { headers: { accept: 'application/json' } });
    const data = await res.json();
    const s = data.session || {};
    human = s.displayName || human;
    clone = s.cloneName || clone;
    const who = document.getElementById('locomp-who');
    if (who) {
      who.textContent = human + ' ↔ ' + clone + (s.isTest ? ' · test' : '') + (s.country ? ' · ' + s.country : '');
    }
    const results = '/lifeos/results?clientId=' + encodeURIComponent(session.clientId) +
      '&token=' + encodeURIComponent(session.token);
    const r1 = document.getElementById('locomp-results');
    const r2 = document.getElementById('locomp-foot-results');
    if (r1) r1.href = results;
    if (r2) r2.href = results;
    renderFeed(s.recentTracks || []);
    const beats = document.getElementById('locomp-beats');
    if (beats && Array.isArray(s.guideBeats)) {
      beats.innerHTML = s.guideBeats.map((b) =>
        '<li><strong>' + (b.title || '') + '</strong> — ' + (b.line || '') + '</li>'
      ).join('');
    }
    try {
      localStorage.setItem(KEY, JSON.stringify({
        clientId: session.clientId,
        token: session.token,
        displayName: human,
        at: new Date().toISOString()
      }));
    } catch (_) {}
  };
  const track = async (kind, label, value) => {
    const res = await fetch('/lifeos/companion/track', {
      method: 'POST',
      headers: { 'content-type': 'application/json', accept: 'application/json' },
      body: JSON.stringify({
        clientId: session.clientId,
        joinToken: session.token,
        kind,
        label,
        value
      })
    });
    const data = await res.json();
    setBubble('locomp-track-advice', data.cloneAdvice || '', true);
    renderFeed((data.session && data.session.recentTracks) || []);
  };
  const talk = async (utterance, mode) => {
    const res = await fetch('/lifeos/companion/talk', {
      method: 'POST',
      headers: { 'content-type': 'application/json', accept: 'application/json' },
      body: JSON.stringify({
        clientId: session.clientId,
        joinToken: session.token,
        utterance,
        mode
      })
    });
    const data = await res.json();
    const reply = data.reply || '';
    if (mode === 'guide') setBubble('locomp-guide-reply', reply, true);
    else setBubble('locomp-talk-reply', reply, true);
  };

  const bind = () => {
    const root = document.getElementById('lifeos-companion');
    if (!root || root.dataset.bound === '1') return;
    root.dataset.bound = '1';

    document.querySelectorAll('.locomp-tabs button').forEach((btn) => {
      btn.addEventListener('click', () => {
        const mode = btn.getAttribute('data-mode');
        document.querySelectorAll('.locomp-tabs button').forEach((b) => b.classList.toggle('is-on', b === btn));
        document.querySelectorAll('[data-panel]').forEach((p) => {
          p.hidden = p.getAttribute('data-panel') !== mode;
        });
      });
    });
    document.querySelectorAll('[data-track]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const parts = (btn.getAttribute('data-track') || 'walk|Walk|1').split('|');
        track(parts[0], parts[1], Number(parts[2] || 1)).catch((e) => alert(e.message || e));
      });
    });
    const send = document.getElementById('locomp-send');
    if (send) {
      send.addEventListener('click', () => {
        const ta = document.getElementById('locomp-utterance');
        const text = ((ta && ta.value) || '').trim() || (human + ', walk me through today.');
        talk(text, 'talk').catch((e) => alert(e.message || e));
      });
    }
    document.querySelectorAll('[data-guide]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const line = btn.getAttribute('data-guide') || 'Walk me through today.';
        talk(human + '… ' + line, 'guide').catch((e) => alert(e.message || e));
      });
    });

    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    const mic = document.getElementById('locomp-mic');
    if (mic && SR) {
      const rec = new SR();
      rec.lang = 'en-US';
      mic.addEventListener('click', () => {
        try { rec.start(); mic.textContent = 'Listening…'; } catch (_) {}
      });
      rec.onresult = (ev) => {
        const ta = document.getElementById('locomp-utterance');
        if (ta) ta.value = ev.results[0][0].transcript;
        mic.textContent = 'Hold to talk';
      };
      rec.onerror = rec.onend = () => { mic.textContent = 'Hold to talk'; };
    } else if (mic) {
      mic.textContent = 'Type below';
      mic.disabled = true;
    }

    const lines = () => ([
      clone + ': Good morning, ' + human + '. Priorities are ready.',
      clone + ': Protect a ninety minute focus block.',
      clone + ': Tracking on. I will coach your pace.',
      clone + ': Soft bend in the knees. Logging clean reps.',
      clone + ': Strong day. I will plan tomorrow and quiet sensors.'
    ]);
    let beatIdx = 0;
    const listenNext = document.getElementById('locomp-listen-next');
    if (listenNext) {
      listenNext.addEventListener('click', () => {
        const line = lines()[beatIdx % 5];
        beatIdx++;
        speak(line);
        talk(human + ', listen to the next guide.', 'listen').catch(() => {});
      });
    }
    const listenStop = document.getElementById('locomp-listen-stop');
    if (listenStop) {
      listenStop.addEventListener('click', () => {
        try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (_) {}
      });
    }

    let deferred;
    window.addEventListener('beforeinstallprompt', (e) => { e.preventDefault(); deferred = e; });
    const install = document.getElementById('locomp-install');
    if (install) {
      install.addEventListener('click', async () => {
        if (deferred) {
          deferred.prompt();
          try { await deferred.userChoice; } catch (_) {}
          deferred = null;
          return;
        }
        alert('Add to Home Screen from your browser menu for an app-like LifeOS companion.');
      });
    }
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register('/lifeos/sw.js', { scope: '/lifeos/' }).catch(() => {});
    }
    refresh().catch((e) => {
      const who = document.getElementById('locomp-who');
      if (who) who.textContent = 'Companion offline — ' + (e.message || e);
    });
  };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind);
  else bind();
})();

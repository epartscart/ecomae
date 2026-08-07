(() => {
  const KEY = 'lifeos-client-session';
  const clientEl = document.getElementById('lores-client');
  const tokenEl = document.getElementById('lores-token');
  const statusEl = document.getElementById('lores-status');
  const restore = document.getElementById('lores-restore');
  const form = document.getElementById('lores-form');
  const read = () => {
    try { return JSON.parse(localStorage.getItem(KEY) || 'null'); } catch (_) { return null; }
  };
  if (restore && form) {
    restore.addEventListener('click', () => {
      const s = read();
      if (!s || !s.clientId || !s.token) {
        if (statusEl) statusEl.textContent = 'No saved session on this device. Join first at /lifeos/join.';
        return;
      }
      if (clientEl) clientEl.value = s.clientId;
      if (tokenEl) tokenEl.value = s.token;
      form.submit();
    });
  }
  if (clientEl && tokenEl && (!clientEl.value || !tokenEl.value)) {
    const s = read();
    if (s && s.clientId && s.token) {
      clientEl.value = s.clientId;
      tokenEl.value = s.token;
    }
  }
})();

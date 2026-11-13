window.chatUi = {
  visible: () => document.visibilityState === 'visible' && document.hasFocus(),
  calendarScroll(hour) { const el = document.querySelector(".calendar-scroll"); if (el) el.scrollTop = hour * 64; },
  openDialog(id) { const el = document.getElementById(id); if (el && !el.open) el.showModal(); },
  mobile: () => matchMedia('(max-width: 700px)').matches,
  capture() {
    const el = document.getElementById('chat-history');
    if (!el) return true;
    const rect = el.getBoundingClientRect();
    const anchor = [...el.querySelectorAll('article[id]')].find(x => x.getBoundingClientRect().bottom > rect.top);
    this.position = { bottom: el.scrollHeight - el.scrollTop - el.clientHeight < 60,
      top: el.scrollTop, id: anchor?.id, offset: anchor ? anchor.getBoundingClientRect().top - rect.top : 0 };
    return this.position.bottom;
  },
  restore() {
    const el = document.getElementById('chat-history'), p = this.position;
    if (!el || !p) return;
    if (p.bottom) el.scrollTop = el.scrollHeight;
    else {
      const anchor = document.getElementById(p.id);
      if (anchor) el.scrollTop += anchor.getBoundingClientRect().top - el.getBoundingClientRect().top - p.offset;
      else el.scrollTop = p.top;
    }
    this.position = null;
  },
  visibleMessages() {
    const el = document.getElementById('chat-history');
    if (!el || !el.getClientRects().length) return [];
    const rect = el.getBoundingClientRect();
    return [...el.querySelectorAll('article[id]')].filter(x => {
      const r = x.getBoundingClientRect(); return r.bottom > rect.top && r.top < rect.bottom;
    }).map(x => x.id);
  },
  scroll(id) {
    requestAnimationFrame(() => {
      const target = document.getElementById(id), el = document.getElementById('chat-history');
      if (!target || !el) return;
      if (id === 'chat-bottom') el.scrollTop = el.scrollHeight;
      else { el.scrollTop += target.getBoundingClientRect().top - el.getBoundingClientRect().top - 24;
        target.classList.add('chat-message--target'); }
    });
  }
};
window.chatPush = {
  bind(reference, publicKey) {
    const button = document.getElementById('chat-push-enable');
    if (!button || button.dataset.bound) return;
    button.dataset.bound = 'true';
    button.addEventListener('click', async () => {
      button.disabled = true;
      try { const subscription = await window.chatPush.enable(publicKey); await reference.invokeMethodAsync('RegisterBrowserPush', subscription); }
      catch (error) { await reference.invokeMethodAsync('BrowserPushError', error.message || 'Не удалось подключить push.'); }
      finally { button.disabled = false; }
    });
  },
  async enable(publicKey) {
    if (!window.isSecureContext || !('serviceWorker' in navigator) || !('PushManager' in window)) throw new Error('Web Push недоступен. На iPhone сначала добавьте сайт на домашний экран и откройте оттуда.');
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') throw new Error('Разрешите уведомления в настройках браузера.');
    const registration = await navigator.serviceWorker.register('/chat-sw.js');
    await navigator.serviceWorker.ready;
    const key = publicKey.replace(/-/g, '+').replace(/_/g, '/');
    const bytes = Uint8Array.from(atob(key.padEnd(Math.ceil(key.length / 4) * 4, '=')), c => c.charCodeAt(0));
    const subscription = await registration.pushManager.getSubscription() || await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: bytes });
    const json = subscription.toJSON();
    return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
  },
  async current() {
    const registration = await navigator.serviceWorker.getRegistration('/');
    return (await registration?.pushManager.getSubscription())?.endpoint || '';
  },
  async disable() {
    const registration = await navigator.serviceWorker.getRegistration('/');
    const subscription = await registration?.pushManager.getSubscription();
    if (subscription) await subscription.unsubscribe();
  }
};

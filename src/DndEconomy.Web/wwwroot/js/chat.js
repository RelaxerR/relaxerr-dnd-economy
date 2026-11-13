window.chatUi = { visible: () => document.visibilityState === 'visible' && document.hasFocus(), scroll: id => requestAnimationFrame(() => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'center' })) };
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

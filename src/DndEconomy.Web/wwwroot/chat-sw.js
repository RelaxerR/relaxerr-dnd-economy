// No fetch handler: private pages and attachments are never cached by this worker.
self.addEventListener('push', event => {
  const data = event.data?.json() || {};
  event.waitUntil(self.registration.showNotification(data.title || 'Relaxerr', {
    tag: data.tag, body: 'Откройте сайт, чтобы посмотреть подробности.',
    data: { url: data.url || '/chat' }, icon: '/chat-icon.svg'
  }));
});
self.addEventListener('notificationclick', event => {
  event.notification.close();
  const requested = new URL(event.notification.data?.url || '/chat', self.location.origin);
  const url = requested.origin === self.location.origin && requested.pathname === '/chat' ? requested.href : '/chat';
  event.waitUntil(clients.openWindow(url));
});

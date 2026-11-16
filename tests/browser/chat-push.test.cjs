// Unit tests of browser glue with mocks; these do not claim device/provider acceptance.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('src/DndEconomy.Web/wwwroot/js/chat.js', 'utf8');

function setup({ permission = 'granted', existingKey = null } = {}) {
  const calls = [];
  const subscription = { options: { applicationServerKey: existingKey }, toJSON: () => ({ endpoint: 'https://fcm.googleapis.com/test', keys: { p256dh: 'public', auth: 'auth' } }) };
  const registration = { pushManager: { getSubscription: async () => existingKey ? subscription : null,
    subscribe: async options => { calls.push(['subscribe', options]); return subscription; } } };
  const button = { dataset: {}, addEventListener: (name, fn) => { button.click = fn; } };
  let visibleButton = null;
  const context = vm.createContext({ window: { isSecureContext: true, PushManager: {}, Notification: {} },
    Notification: { requestPermission: () => { calls.push(['permission']); return Promise.resolve(permission); } },
    navigator: { serviceWorker: { register: async path => { calls.push(['register', path]); return registration; }, ready: Promise.resolve(registration) } },
    document: { getElementById: () => visibleButton }, atob, Uint8Array });
  vm.runInContext(source, context);
  return { push: context.window.chatPush, calls, button, showButton() { visibleButton = button; } };
}

test('binding retries after async settings render and permission starts in the click', async () => {
  const s = setup(); const callbacks = [];
  const reference = { invokeMethodAsync: async (...args) => callbacks.push(args) };
  s.push.bind(reference, 'BAEC');
  s.showButton(); s.push.bind(reference, 'BAEC'); s.push.bind(reference, 'BAEC');
  const clicked = s.button.click();
  assert.equal(s.calls[0][0], 'permission');
  await clicked;
  assert.equal(s.calls.filter(x => x[0] === 'subscribe').length, 1);
  assert.equal(s.calls.find(x => x[0] === 'register')[1], '/chat-sw.js');
  assert.equal(callbacks[0][0], 'RegisterBrowserPush');
  assert.equal(s.button.disabled, false);
});

test('permission denial never creates a subscription', async () => {
  const s = setup({ permission: 'denied' });
  await assert.rejects(s.push.enable('BAEC'), /Разрешите/);
  assert.deepEqual(s.calls.map(x => x[0]), ['permission']);
});

test('rotated key gives explicit re-subscription instructions', async () => {
  const s = setup({ existingKey: new Uint8Array([4, 8, 9]).buffer });
  await assert.rejects(s.push.enable('BAEC'), /Ключ уведомлений изменился/);
});

test('service worker shows neutral body and restricts click destinations', async () => {
  const listeners = {}; const shown = []; const opened = [];
  vm.runInNewContext(fs.readFileSync('src/DndEconomy.Web/wwwroot/chat-sw.js', 'utf8'), {
    URL, clients: { openWindow: async url => opened.push(url) },
    self: { location: { origin: 'https://example.com' }, addEventListener: (name, fn) => { listeners[name] = fn; },
      registration: { showNotification: async (...args) => shown.push(args) } }
  });
  let pending;
  listeners.push({ data: { json: () => ({ title: 'Новое личное сообщение', tag: 'notice-1', url: '/chat?message=id' }) }, waitUntil: p => { pending = p; } });
  await pending;
  assert.equal(shown[0][1].body, 'Откройте сайт, чтобы посмотреть подробности.');
  assert.equal(shown[0][1].tag, 'notice-1');
  for (const url of ['https://attacker.invalid/chat', '/admin/users', '/chat?message=id']) {
    listeners.notificationclick({ notification: { close() {}, data: { url } }, waitUntil: p => { pending = p; } });
    await pending;
  }
  assert.deepEqual(opened, ['/chat', '/chat', 'https://example.com/chat?message=id']);
});

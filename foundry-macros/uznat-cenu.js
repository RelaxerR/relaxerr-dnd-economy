/**
 * Узнать цену предмета — DnD Economy (relaxerr-dnd-economy.ru)
 *
 * Спрашивает название предмета, ищет его через API сайта той же опечатко-устойчивой логикой,
 * что и каталог, и показывает окно с точным названием и ценой.
 *
 * НАСТРОЙКА: впишите ниже логин/пароль обычного игрока (создать/получить у админа). Эти данные
 * видит любой, кто может редактировать этот макрос — держите его в мире, а не в публичном
 * компендиуме.
 */
const API_BASE = "https://relaxerr-dnd-economy.ru";
const LOGIN = {
  email: "ВАШ_EMAIL_ИГРОКА",
  password: "ВАШ_ПАРОЛЬ_ИГРОКА"
};

function formatGp(value) {
  return value === null || value === undefined ? "—" : `${Number(value).toFixed(2)} зм`;
}

async function loginPlayer() {
  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(LOGIN)
  });

  if (res.status === 423) throw new Error("Учётная запись временно заблокирована после серии неудачных попыток входа.");
  if (!res.ok) throw new Error(`Не удалось войти (HTTP ${res.status}). Проверьте логин/пароль, вписанные в макрос.`);
}

async function searchItem(name) {
  const url = `${API_BASE}/api/items/search?name=${encodeURIComponent(name)}`;
  const res = await fetch(url, { method: "GET", credentials: "include" });

  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Ошибка поиска (HTTP ${res.status}).`);
  return res.json();
}

function renderItemCard(item) {
  return `
  <div style="font-family:'Signika',sans-serif;border:2px solid #8a6d3b;border-radius:8px;
              padding:12px 16px;background:linear-gradient(160deg,#fdf6e3,#f0e4c8);">
    <h2 style="margin:0 0 8px;color:#4b3a1a;border-bottom:1px solid #b89b5e;padding-bottom:4px;">
      ${foundry.utils.escapeHTML(item.nameRu)}
    </h2>
    ${item.nameEn ? `<div style="font-style:italic;color:#7a6641;margin-bottom:8px;">${foundry.utils.escapeHTML(item.nameEn)}</div>` : ""}
    <table style="width:100%;font-size:14px;border-collapse:collapse;">
      <tr>
        <td style="color:#5c4a24;padding:2px 0;">Покупка:</td>
        <td style="text-align:right;font-weight:bold;color:${item.isAvailable ? "#2d5a27" : "#a33333"};">
          ${item.isAvailable ? formatGp(item.buyPrice) : "Нет в наличии"}
        </td>
      </tr>
      <tr>
        <td style="color:#5c4a24;padding:2px 0;">Продажа:</td>
        <td style="text-align:right;font-weight:bold;color:#4b3a1a;">${formatGp(item.sellPrice)}</td>
      </tr>
    </table>
  </div>`;
}

async function promptForName() {
  return Dialog.prompt({
    title: "Узнать цену предмета",
    content: `<div class="form-group"><label>Название предмета:</label>
                 <input type="text" name="itemName" autofocus placeholder="например, кольчюга"></div>`,
    label: "Найти",
    callback: html => html.find('[name="itemName"]').val()?.trim(),
    rejectClose: false
  });
}

(async () => {
  try {
    const name = await promptForName();
    if (!name) return;

    await loginPlayer();
    const item = await searchItem(name);

    if (!item) {
      new Dialog({
        title: "Не найдено",
        content: `<p>Предмет «${foundry.utils.escapeHTML(name)}» не найден в каталоге экономики.</p>`,
        buttons: { ok: { label: "Ок" } }
      }).render(true);
      return;
    }

    new Dialog({
      title: "Результат поиска",
      content: renderItemCard(item),
      buttons: { ok: { label: "Закрыть" } }
    }).render(true);
  } catch (err) {
    ui.notifications.error(err.message);
    console.error("[DnD Economy] Ошибка поиска предмета:", err);
  }
})();

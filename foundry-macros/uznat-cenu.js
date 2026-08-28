/**
 * Узнать цену предмета — DnD Economy (relaxerr-dnd-economy.ru)
 *
 * Спрашивает название предмета, ищет его через API сайта той же опечатко-устойчивой логикой,
 * что и каталог, и показывает окно с до 5 ближайшими совпадениями (точное название и цена).
 *
 * НАСТРОЙКА: впишите ниже статичный API-ключ (выдаёт админ — MacroApi:PlayerKey в конфиге
 * сервера). Это НЕ пароль от личного аккаунта — ключ общий для макроса и не связан с конкретным
 * игроком. Эти данные видит любой, кто может редактировать этот макрос — держите его в мире,
 * а не в публичном компендиуме.
 */
const API_BASE = "https://relaxerr-dnd-economy.ru";
const API_KEY = "ВАШ_PLAYER_API_КЛЮЧ";
const RESULTS_LIMIT = 5;

function formatGp(value) {
  return value === null || value === undefined ? "—" : `${Number(value).toFixed(2)} зм`;
}

async function searchItems(name) {
  const url = `${API_BASE}/api/items/search?name=${encodeURIComponent(name)}&take=${RESULTS_LIMIT}`;
  const res = await fetch(url, { method: "GET", headers: { "X-Api-Key": API_KEY } });

  if (res.status === 401) throw new Error("Неверный API-ключ, вписанный в макрос.");
  if (!res.ok) throw new Error(`Ошибка поиска (HTTP ${res.status}).`);
  return res.json();
}

function renderItemsTable(items) {
  const rows = items.map(item => `
    <tr style="border-bottom:1px solid #ddc9a3;">
      <td style="padding:6px 8px;color:#4b3a1a;">
        <strong>${foundry.utils.escapeHTML(item.nameRu)}</strong>
        ${item.nameEn ? `<div style="font-size:11px;font-style:italic;color:#7a6641;">${foundry.utils.escapeHTML(item.nameEn)}</div>` : ""}
      </td>
      <td style="padding:6px 8px;text-align:right;font-weight:bold;white-space:nowrap;
                  color:${item.isAvailable ? "#2d5a27" : "#a33333"};">
        ${item.isAvailable ? formatGp(item.buyPrice) : "Нет в наличии"}
      </td>
      <td style="padding:6px 8px;text-align:right;font-weight:bold;white-space:nowrap;color:#4b3a1a;">
        ${formatGp(item.sellPrice)}
      </td>
    </tr>`).join("");

  return `
  <div style="font-family:'Signika',sans-serif;border:2px solid #8a6d3b;border-radius:8px;
              padding:12px 16px;background:linear-gradient(160deg,#fdf6e3,#f0e4c8);">
    <table style="width:100%;font-size:14px;border-collapse:collapse;">
      <thead>
        <tr style="border-bottom:2px solid #b89b5e;">
          <th style="text-align:left;padding:4px 8px;color:#5c4a24;">Предмет</th>
          <th style="text-align:right;padding:4px 8px;color:#5c4a24;">Покупка</th>
          <th style="text-align:right;padding:4px 8px;color:#5c4a24;">Продажа</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
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

    const items = await searchItems(name);

    if (!items.length) {
      new Dialog({
        title: "Не найдено",
        content: `<p>Предмет «${foundry.utils.escapeHTML(name)}» не найден в каталоге экономики.</p>`,
        buttons: { ok: { label: "Ок" } }
      }).render(true);
      return;
    }

    new Dialog({
      title: `Результаты поиска (${items.length})`,
      content: renderItemsTable(items),
      buttons: { ok: { label: "Закрыть" } }
    }).render(true);
  } catch (err) {
    ui.notifications.error(err.message);
    console.error("[DnD Economy] Ошибка поиска предмета:", err);
  }
})();

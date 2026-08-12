/**
 * Создать предмет — DnD Economy (relaxerr-dnd-economy.ru)
 *
 * Даёт выбрать один из предметов мира (Items directory), подсказывает вес/цену из полей Foundry
 * и создаёт по ним предмет в каталоге сайта. Категория всегда ставится "Homebrew" — Тип и Подтип
 * (русская таксономия сайта: "Оружие"/"Рукопашное" и т.п., не совпадает с системными полями
 * Foundry) вводятся вручную в форме подтверждения.
 *
 * НАСТРОЙКА: впишите ниже логин/пароль АДМИНА сайта (только у админа есть право создавать
 * предметы). Эти данные видит любой, кто может редактировать этот макрос — держите его в мире,
 * доступном только ГМ, а не в общем компендиуме.
 */
const API_BASE = "https://relaxerr-dnd-economy.ru";
const ADMIN_LOGIN = {
  email: "ВАШ_EMAIL_АДМИНА",
  password: "ВАШ_ПАРОЛЬ_АДМИНА"
};

// Курс конвертации в золотые монеты (зм) — стандартный для 5e.
const DENOMINATION_TO_GP = { pp: 10, gp: 1, ep: 0.5, sp: 0.1, cp: 0.01 };

function guessBaseCostGp(foundryItem) {
  const price = foundryItem.system?.price;
  if (!price?.value) return 0;
  const rate = DENOMINATION_TO_GP[price.denomination] ?? 1;
  return Math.round(price.value * rate * 100) / 100;
}

function guessWeight(foundryItem) {
  return foundryItem.system?.weight?.value ?? 0;
}

async function loginAdmin() {
  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(ADMIN_LOGIN)
  });

  if (res.status === 423) throw new Error("Учётная запись временно заблокирована после серии неудачных попыток входа.");
  if (!res.ok) throw new Error(`Не удалось войти админом (HTTP ${res.status}). Проверьте логин/пароль, вписанные в макрос.`);
}

async function createItem(payload) {
  const res = await fetch(`${API_BASE}/api/items`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  if (res.status === 403) throw new Error("Отказано — учётная запись из макроса не администратор.");
  if (!res.ok) throw new Error(`Сервер отклонил создание (HTTP ${res.status}): ${await res.text()}`);
  return res.json();
}

async function pickWorldItem() {
  const items = game.items.contents
    .slice()
    .sort((a, b) => a.name.localeCompare(b.name, "ru"));

  if (!items.length) {
    ui.notifications.warn("В мире нет ни одного предмета (Items directory пуст) — сначала перетащите предмет туда.");
    return null;
  }

  const options = items
    .map(i => `<option value="${i.id}">${foundry.utils.escapeHTML(i.name)} (${i.type})</option>`)
    .join("");

  const itemId = await Dialog.prompt({
    title: "Выберите предмет Foundry",
    content: `<div class="form-group"><label>Предмет:</label><select name="itemId">${options}</select></div>`,
    label: "Далее",
    callback: html => html.find('[name="itemId"]').val(),
    rejectClose: false
  });

  return itemId ? game.items.get(itemId) : null;
}

async function confirmDetails(foundryItem) {
  const guessedCost = guessBaseCostGp(foundryItem);
  const guessedWeight = guessWeight(foundryItem);

  const content = `
    <div class="form-group"><label>Тип (рус., напр. "Оружие"):</label>
      <input type="text" name="type" placeholder="Оружие, Доспехи, Инструменты..."></div>
    <div class="form-group"><label>Подтип (рус., напр. "Рукопашное"):</label>
      <input type="text" name="subtype" placeholder="Рукопашное, Лёгкие..."></div>
    <div class="form-group"><label>Название (рус.):</label>
      <input type="text" name="nameRu" value="${foundry.utils.escapeHTML(foundryItem.name)}"></div>
    <div class="form-group"><label>Название (англ., опц.):</label>
      <input type="text" name="nameEn" value=""></div>
    <div class="form-group"><label>Базовая стоимость (зм):</label>
      <input type="number" step="0.01" min="0" name="baseCost" value="${guessedCost}"></div>
    <div class="form-group"><label>Вес:</label>
      <input type="number" step="0.01" min="0" name="weight" value="${guessedWeight}"></div>
    <p style="font-size:12px;color:#777;">Категория будет проставлена автоматически: Homebrew.</p>
  `;

  return Dialog.prompt({
    title: `Создать предмет: ${foundryItem.name}`,
    content,
    label: "Создать",
    callback: html => ({
      category: "Homebrew",
      type: html.find('[name="type"]').val()?.trim() ?? "",
      subtype: html.find('[name="subtype"]').val()?.trim() ?? "",
      nameRu: html.find('[name="nameRu"]').val()?.trim() ?? "",
      nameEn: html.find('[name="nameEn"]').val()?.trim() || null,
      baseCost: Number(html.find('[name="baseCost"]').val()),
      weight: Number(html.find('[name="weight"]').val())
    }),
    rejectClose: false
  });
}

(async () => {
  try {
    const foundryItem = await pickWorldItem();
    if (!foundryItem) return;

    const payload = await confirmDetails(foundryItem);
    if (!payload) return;

    if (!payload.type || !payload.subtype || !payload.nameRu) {
      ui.notifications.warn("Создание отменено — Тип, Подтип и Название обязательны.");
      return;
    }

    await loginAdmin();
    const created = await createItem(payload);
    ui.notifications.info(`Предмет «${payload.nameRu}» создан в экономике сайта (id ${created.itemId}).`);
  } catch (err) {
    ui.notifications.error(err.message);
    console.error("[DnD Economy] Ошибка создания предмета:", err);
  }
})();

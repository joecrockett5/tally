// Fills a fresh Tally with made-up demo data through its public API, for the
// README screenshots. All people and restaurants are fictional.
// Usage: node tools/media/seed-demo.mjs http://localhost:8080
const base = process.argv[2] ?? 'http://localhost:8080';

async function api(method, path, body, profile) {
  const response = await fetch(`${base}/api${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(profile ? { 'X-Profile-Id': profile } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${await response.text()}`);
  return response.json();
}

const { today } = await api('GET', '/config');
const addDays = (date, n) => {
  const d = new Date(`${date}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};

// A small deterministic random generator, so every run produces the same diary.
let seed = 20261106;
const random = () => ((seed = (seed * 1664525 + 1013904223) % 4294967296) / 4294967296);
const pick = (items) => items[Math.floor(random() * items.length)];
const vary = (amount) => Math.round((amount * (0.85 + random() * 0.3)) / 5) * 5;

const sam = (await api('POST', '/profiles', { name: 'Sam', avatar: '🦊' })).id;
await api('POST', '/profiles', { name: 'Alex', avatar: '🐢' });
await api('POST', '/profiles', { name: 'Robin', avatar: '🌻' });

const cache = new Map();
async function food(name) {
  if (cache.has(name)) return cache.get(name);
  const results = await api('GET', `/foods/search?q=${encodeURIComponent(name.replaceAll(',', ''))}&limit=60`, undefined, sam);
  const match = results.find((f) => f.name.startsWith(name));
  if (!match) throw new Error(`No CoFID food starting with "${name}"`);
  cache.set(name, match.id);
  return match.id;
}

const log = async (date, meal, foodId, quantity, unit = 'Gram') =>
  api('POST', '/diary/entries', { date, meal, foodId, unit, quantity }, sam);

// Custom foods: one shared with the household, and a fictional restaurant's menu.
const flapjack = await api('POST', '/foods', {
  name: 'Homemade oat flapjack', basis: 'Per100g', isShared: true,
  nutrients: { energyKcal: 430, fat: 19, saturates: 9.5, carbohydrate: 58, sugars: 28, fibre: 5, protein: 6.5, salt: 0.4 },
  servings: [{ name: '1 bar', amount: 60 }],
}, sam);
const bowl = await api('POST', '/foods', {
  name: 'Chicken burrito bowl', restaurant: 'The Corner Kitchen', basis: 'PerPortion', portionName: '1 bowl', isShared: true,
  nutrients: { energyKcal: 640, fat: 21, saturates: 6.5, carbohydrate: 72, sugars: 6, fibre: 9, protein: 38, salt: 2.3 },
}, sam);
await api('POST', '/foods', {
  name: 'Halloumi wrap', restaurant: 'The Corner Kitchen', basis: 'PerPortion', portionName: '1 wrap', isShared: true,
  nutrients: { energyKcal: 590, fat: 29, saturates: 14, carbohydrate: 52, sugars: 7, fibre: 5, protein: 27, salt: 3.1 },
}, sam);

const f = {
  oats: await food('Porridge oats, unfortified'),
  milk: await food('Milk, semi-skimmed, pasteurised, average'),
  banana: await food('Bananas, flesh only'),
  blueberries: await food('Blueberries'),
  toast: await food('Bread, wholemeal, toasted'),
  eggs: await food('Eggs, chicken, whole'),
  beans: await food('Baked beans, canned in tomato sauce'),
  chicken: await food('Chicken, breast, grilled'),
  broccoli: await food('Broccoli, green, steamed'),
  pasta: await food('Pasta, white, dried'),
  salmon: await food('Salmon, wild, grilled'),
  cheddar: await food('Cheese, Cheddar, English'),
  apple: await food('Apples, eating, raw'),
  yogurt: await food('Yogurt, low fat, plain'),
  almonds: await food('Almonds, whole kernels'),
  oil: await food('Oil, olive'),
  carrots: await food('Carrots, young, raw'),
};

const stirFry = await api('POST', '/recipes', {
  name: 'Chicken and broccoli pasta', servings: 4, cookedWeightGrams: 1400,
  ingredients: [
    { foodId: f.chicken, unit: 'Gram', quantity: 500 },
    { foodId: f.pasta, unit: 'Gram', quantity: 320 },
    { foodId: f.broccoli, unit: 'Gram', quantity: 300 },
    { foodId: f.carrots, unit: 'Gram', quantity: 150 },
    { foodId: f.oil, unit: 'Gram', quantity: 20 },
    { foodId: f.cheddar, unit: 'Gram', quantity: 80 },
  ],
}, sam);

const breakfasts = [
  [[f.oats, 50], [f.milk, 250], [f.banana, 100]],
  [[f.oats, 50], [f.milk, 250], [f.blueberries, 80]],
  [[f.toast, 80], [f.eggs, 110]],
  [[f.toast, 80], [f.beans, 200]],
];
const lunches = [
  [[f.toast, 90], [f.cheddar, 45], [f.apple, 120]],
  [[f.beans, 300], [f.toast, 90], [f.cheddar, 30]],
  [[f.chicken, 150], [f.toast, 70], [f.carrots, 80], [f.yogurt, 150]],
];
const dinners = [
  [[f.salmon, 170], [f.broccoli, 120], [f.pasta, 90], [f.oil, 10], [f.yogurt, 120]],
  [[f.chicken, 180], [f.pasta, 100], [f.broccoli, 100], [f.cheddar, 25], [f.oil, 10]],
];
const snacks = [[[f.apple, 120]], [[f.almonds, 30]], [[f.yogurt, 150]], [[f.banana, 100], [f.almonds, 25]]];

async function logAll(date, meal, items) {
  for (const [id, grams] of items) await log(date, meal, id, vary(grams));
}

// Four weeks of history, with a few unlogged days.
for (let back = 27; back >= 1; back--) {
  const date = addDays(today, -back);
  if (back % 11 === 5) continue;
  await logAll(date, 'Breakfast', pick(breakfasts));
  await logAll(date, 'Lunch', pick(lunches));
  if (back % 6 === 2) await log(date, 'Dinner', bowl.id, 1, 'Portion');
  else if (back % 3 === 0) await api('POST', '/diary/entries', { date, meal: 'Dinner', recipeId: stirFry.id, unit: 'Portion', quantity: 1 }, sam);
  else await logAll(date, 'Dinner', pick(dinners));
  await logAll(date, 'Snacks', pick(snacks));
  if (back % 2 === 0) await log(date, 'Snacks', flapjack.id, 60);
}

// Today: dinner is left empty for the demo recording to fill in.
await logAll(today, 'Breakfast', breakfasts[0]);
await logAll(today, 'Lunch', lunches[0]);
await log(today, 'Snacks', flapjack.id, 60);

await api('POST', '/saved-meals', {
  name: 'Porridge with banana',
  items: breakfasts[0].map(([foodId, quantity]) => ({ foodId, unit: 'Gram', quantity })),
}, sam);

console.log(`Seeded ${base}: Sam has four weeks of diary, plus a recipe, a saved meal and custom foods.`);

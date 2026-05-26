// ============================================================================
// COLORES POR GRUPO
// Cada grupo tiene un color único que se aplica a todos sus pins.
// ============================================================================

const GROUP_COLORS = {
  locations: '#74a8e0',   // azul apagado
  loot:      '#c8b560',   // dorado/ocre
  enemies:   '#c05050',   // rojo oscuro
};

// ============================================================================
// CONFIGURACIÓN DE CATEGORÍAS
// group: a qué grupo pertenece (define el color del pin)
// icon:  emoji o símbolo Unicode
// ============================================================================

const PIN_TYPES = {
  // — LOCATIONS —
  save_zone:    { label: 'Save Zone',    group: 'locations', icon: '⬢' },
  door:         { label: 'Door',         group: 'locations', icon: '⬡' },
  transit:      { label: 'Transit',      group: 'locations', icon: '▶' },
  environment:  { label: 'Environment',  group: 'locations', icon: '◈' },
  interactable: { label: 'Interactable', group: 'locations', icon: '⚙' },
  // — LOOT —
  item:         { label: 'Item',         group: 'loot',      icon: '◆' },
  key_item:     { label: 'Key Item',     group: 'loot',      icon: '★' },
  note:         { label: 'Note',         group: 'loot',      icon: '✎' },
  // — ENEMIES —
  boss:         { label: 'Boss',         group: 'enemies',   icon: '☠' },
  consumed:     { label: 'Consumed',     group: 'enemies',   icon: '✕' },
};

// Agrega la propiedad color derivada del grupo, para que el resto del código
// siga funcionando igual (accede a def.color sin cambios).
Object.values(PIN_TYPES).forEach(def => {
  def.color = GROUP_COLORS[def.group];
});

// Grupos para renderizar en el panel, en orden
const PIN_GROUPS = [
  { key: 'locations', label: 'Locations' },
  { key: 'loot',      label: 'Loot'      },
  { key: 'enemies',   label: 'Enemies'   },
];

// Legacy map para saves anteriores
const LEGACY_MAP = {
  puerta:  'door',
  objeto:  'item',
  ambiente:'environment',
  enemies: 'consumed',
};

function resolveType(type) {
  return LEGACY_MAP[type] || (PIN_TYPES[type] ? type : 'item');
}

// Expose for app.js
const LABELS = Object.fromEntries(Object.entries(PIN_TYPES).map(([k,v]) => [k, v.label]));

let pins = [];

function getPins()          { return pins; }
function setPins(newPins)   { pins = newPins.map(p => ({ ...p, type: resolveType(p.type) })); }
function addPin(pin)        { pins.push(pin); }
function removePin(id)      { pins = pins.filter(p => p.id !== id); }
function findPin(id)        { return pins.find(p => p.id === id); }
function clearPins()        { pins = []; }

function pinSVG(type) {
  const def = PIN_TYPES[type] || PIN_TYPES.item;
  const c   = def.color;
  const ico = def.icon;
  return `<svg width="40" height="56" viewBox="0 0 40 56" fill="none" xmlns="http://www.w3.org/2000/svg">
    <path d="M20 0C8.954 0 0 8.954 0 20c0 15 20 36 20 36S40 35 40 20C40 8.954 31.046 0 20 0z"
      fill="${c}" fill-opacity="0.95"/>
    <path d="M20 0C8.954 0 0 8.954 0 20c0 15 20 36 20 36S40 35 40 20C40 8.954 31.046 0 20 0z"
      fill="none" stroke="rgba(0,0,0,0.4)" stroke-width="1.5"/>
    <text x="20" y="26" text-anchor="middle" font-size="20" font-weight="bold"
      font-family="'Courier New',Consolas,monospace" fill="#0e0e0e" opacity="0.95">${ico}</text>
  </svg>`;
}

function createPinEl(pin, mapContainer, onClickPin) {
  const el = document.createElement('div');
  el.className = 'pin';
  if (pin.hidden) el.classList.add('pin-hidden');
  el.dataset.id = pin.id;
  el.innerHTML = pinSVG(pin.type);
  el.style.left = pin.x + 'px';
  el.style.top  = pin.y + 'px';
  
  // Tooltip hover
  const tooltip = document.createElement('div');
  tooltip.className = 'pin-tooltip';
  tooltip.textContent = pin.title || (PIN_TYPES[pin.type] || {}).label;
  el.appendChild(tooltip);
  
  el.addEventListener('click', e => { e.stopPropagation(); onClickPin(pin.id); });
  mapContainer.appendChild(el);
}

function updatePinScales(mapContainer, scale) {
  const inverseScale = 1 / scale;
  mapContainer.querySelectorAll('.pin').forEach(el => {
    el.style.transform = `translate(-50%, -100%) scale(${inverseScale})`;
  });
}

function renderPins(mapContainer, onClickPin) {
  mapContainer.querySelectorAll('.pin').forEach(p => p.remove());
  pins.forEach(pin => createPinEl(pin, mapContainer, onClickPin));
}

function updatePinCount(el) {
  el.textContent = `${pins.length} pin${pins.length !== 1 ? 's' : ''}`;
}

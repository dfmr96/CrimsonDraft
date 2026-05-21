const LS_PINS            = 'mappinner_pins';
const LS_MAP             = 'mappinner_map';
const LS_MODE            = 'mappinner_mode';
const LS_EXPLORE_PRESETS = 'mappinner_explore_presets';
const LS_EXPLORE_ACTIVE  = 'mappinner_explore_active';

// ── Pins ──────────────────────────────────────────────────────
function savePins(pins) {
  localStorage.setItem(LS_PINS, JSON.stringify(pins));
}
function loadPins() {
  try { return JSON.parse(localStorage.getItem(LS_PINS)) || []; }
  catch { return []; }
}

// ── Map ───────────────────────────────────────────────────────
function saveMap(dataUrl) {
  try { localStorage.setItem(LS_MAP, dataUrl); }
  catch { showToast('Imagen muy grande para guardar localmente'); }
}
function loadMap() {
  return localStorage.getItem(LS_MAP);
}

// ── Mode ──────────────────────────────────────────────────────
function saveMode(mode) { localStorage.setItem(LS_MODE, mode); }
function loadMode()     { return localStorage.getItem(LS_MODE) || 'dev'; }

// ── Explore presets (guardan filtros + hidden state) ──────────
function loadExplorePresets() {
  try { return JSON.parse(localStorage.getItem(LS_EXPLORE_PRESETS)) || []; }
  catch { return []; }
}
function saveExplorePresets(list) {
  localStorage.setItem(LS_EXPLORE_PRESETS, JSON.stringify(list));
}
function loadExploreActiveId() {
  return localStorage.getItem(LS_EXPLORE_ACTIVE) || null;
}
function saveExploreActiveId(id) {
  if (id) localStorage.setItem(LS_EXPLORE_ACTIVE, id);
  else    localStorage.removeItem(LS_EXPLORE_ACTIVE);
}

// ── Export / Import — DEV (mapa + pins) ──────────────────────
function exportSave(pins, mapDataUrl) {
  const payload = {
    version: 1,
    exportedAt: new Date().toISOString(),
    map: mapDataUrl,
    pins: pins
  };
  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = `mappinner-${Date.now()}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

function importSave(file, onSuccess) {
  const reader = new FileReader();
  reader.onload = ev => {
    try {
      const data = JSON.parse(ev.target.result);
      if (!data.map || !Array.isArray(data.pins)) throw new Error('Formato inválido');
      onSuccess(data);
    } catch {
      showToast('Archivo inválido o corrupto');
    }
  };
  reader.readAsText(file);
}

// ── Export / Import — EXPLORE vistas (lista completa, sin mapa ni pins) ──
function exportExploreViews(sets) {
  const payload = {
    version:      1,
    exploreViews: true,
    exportedAt:   new Date().toISOString(),
    sets:         sets
  };
  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = `mappinner-vistas-${Date.now()}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

function importExploreViews(file, onSuccess) {
  const reader = new FileReader();
  reader.onload = ev => {
    try {
      const data = JSON.parse(ev.target.result);
      if (!data.exploreViews || !Array.isArray(data.sets)) throw new Error('Formato inválido');
      onSuccess(data.sets);
    } catch {
      showToast('Archivo inválido — usá un .json de vistas exportado desde Explore');
    }
  };
  reader.readAsText(file);
}

// ── Exportar proyecto completo (DEV → bundle para Explore) ────
function exportProjectBundle(pins, mapDataUrl) {
  const payload = {
    version:       2,
    projectBundle: true,
    exportedAt:    new Date().toISOString(),
    map:           mapDataUrl,
    pins:          pins
  };
  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = `mappinner-bundle-${Date.now()}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

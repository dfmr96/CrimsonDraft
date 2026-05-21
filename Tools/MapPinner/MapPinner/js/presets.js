// DEV presets — usan su propio namespace
const LS_PRESETS      = 'mappinner_presets';
const LS_ACTIVE_PRESET = 'mappinner_active_preset';

let presets = [];
let activePresetId = null;

function loadPresets() {
  try {
    presets = JSON.parse(localStorage.getItem(LS_PRESETS)) || [];
    activePresetId = localStorage.getItem(LS_ACTIVE_PRESET);
    return presets;
  } catch { return []; }
}

function savePresets() {
  localStorage.setItem(LS_PRESETS, JSON.stringify(presets));
}

function setActivePreset(id) {
  activePresetId = id;
  if (id) localStorage.setItem(LS_ACTIVE_PRESET, id);
  else    localStorage.removeItem(LS_ACTIVE_PRESET);
}

function getActivePresetId() { return activePresetId; }
function getActivePreset()   { return presets.find(p => p.id === activePresetId); }

function createPreset(name, mapDataUrl, pins) {
  const preset = {
    id: Date.now() + Math.random(),
    name, map: mapDataUrl, pins,
    createdAt: new Date().toISOString()
  };
  presets.push(preset);
  savePresets();
  return preset;
}

function updatePreset(id, name, mapDataUrl, pins) {
  const preset = presets.find(p => p.id === id);
  if (!preset) return null;
  preset.name = name;
  preset.map  = mapDataUrl;
  preset.pins = pins;
  preset.updatedAt = new Date().toISOString();
  savePresets();
  return preset;
}

function deletePreset(id) {
  presets = presets.filter(p => p.id != id);
  savePresets();
  if (activePresetId == id) {
    activePresetId = null;
    localStorage.removeItem(LS_ACTIVE_PRESET);
  }
}

function getPresets() { return presets; }

function movePresetUp(id) {
  const idx = presets.findIndex(p => p.id == id);
  if (idx <= 0) return false;
  [presets[idx], presets[idx-1]] = [presets[idx-1], presets[idx]];
  savePresets();
  return true;
}

function movePresetDown(id) {
  const idx = presets.findIndex(p => p.id == id);
  if (idx < 0 || idx >= presets.length - 1) return false;
  [presets[idx], presets[idx+1]] = [presets[idx+1], presets[idx]];
  savePresets();
  return true;
}

// ── EXPLORE presets — guardan filtros activos + hidden overrides ──
// Estructura: { id, name, hiddenTypes: [], hiddenPins: [] }

let exploreSets    = [];
let activeExploreId = null;

function loadExploreSets() {
  exploreSets    = loadExplorePresets();
  activeExploreId = loadExploreActiveId();
  return exploreSets;
}

function getExploreSets()      { return exploreSets; }
function getActiveExploreId()  { return activeExploreId; }
function getActiveExploreSet() { return exploreSets.find(s => s.id == activeExploreId); }

function setActiveExploreSet(id) {
  activeExploreId = id;
  saveExploreActiveId(id);
}

function createExploreSet(name, hiddenTypes, hiddenPins) {
  const s = {
    id: Date.now() + Math.random(),
    name, hiddenTypes, hiddenPins,
    createdAt: new Date().toISOString()
  };
  exploreSets.push(s);
  saveExplorePresets(exploreSets);
  return s;
}

function updateExploreSet(id, name, hiddenTypes, hiddenPins) {
  const s = exploreSets.find(s => s.id == id);
  if (!s) return null;
  s.name = name; s.hiddenTypes = hiddenTypes; s.hiddenPins = hiddenPins;
  s.updatedAt = new Date().toISOString();
  saveExplorePresets(exploreSets);
  return s;
}

function deleteExploreSet(id) {
  exploreSets = exploreSets.filter(s => s.id != id);
  saveExplorePresets(exploreSets);
  if (activeExploreId == id) {
    activeExploreId = null;
    saveExploreActiveId(null);
  }
}

function moveExploreSetUp(id) {
  const idx = exploreSets.findIndex(s => s.id == id);
  if (idx <= 0) return false;
  [exploreSets[idx], exploreSets[idx-1]] = [exploreSets[idx-1], exploreSets[idx]];
  saveExplorePresets(exploreSets);
  return true;
}

function moveExploreSetDown(id) {
  const idx = exploreSets.findIndex(s => s.id == id);
  if (idx < 0 || idx >= exploreSets.length - 1) return false;
  [exploreSets[idx], exploreSets[idx+1]] = [exploreSets[idx+1], exploreSets[idx]];
  saveExplorePresets(exploreSets);
  return true;
}

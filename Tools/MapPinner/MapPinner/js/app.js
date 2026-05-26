// ─── DOM refs ────────────────────────────────────────────────
const workspace    = document.getElementById('workspace');
const mapContainer = document.getElementById('map-container');
const mapImg       = document.getElementById('map-img');
const uploadPrompt = document.getElementById('upload-prompt');
const uploadBox    = document.getElementById('upload-box');
const fileInput    = document.getElementById('file-input');
const importInput  = document.getElementById('import-input');
const pinPanel     = document.getElementById('pin-panel');
const pinModal     = document.getElementById('pin-modal');
const overlay      = document.getElementById('overlay');
const pinCountEl   = document.getElementById('pin-count');
const toastEl      = document.getElementById('toast');
const presetsPanel = document.getElementById('presets-panel');
const presetsList  = document.getElementById('presets-list');
const actionsPanel = document.getElementById('actions-panel');

let pendingPos  = null;
let activeType  = null;
let selectedId  = null;
let editingId   = null;   // id del pin que se está editando

// ─── Toast ───────────────────────────────────────────────────
function showToast(msg) {
  toastEl.textContent = msg;
  toastEl.classList.add('show');
  setTimeout(() => toastEl.classList.remove('show'), 2200);
}

// ════════════════════════════════════════════════════════════
// MODO (DEV / EXPLORE)
// ════════════════════════════════════════════════════════════
let currentMode = loadMode(); // 'dev' | 'explore'

function applyMode(mode) {
  currentMode = mode;
  saveMode(mode);
  document.body.classList.toggle('mode-dev',     mode === 'dev');
  document.body.classList.toggle('mode-explore', mode === 'explore');
  document.getElementById('mode-badge').textContent = mode.toUpperCase();

  // Botones toggle
  document.getElementById('btn-mode-dev').classList.toggle('active',     mode === 'dev');
  document.getElementById('btn-mode-explore').classList.toggle('active', mode === 'explore');

  // Cursor: en explore no hay creación
  workspace.style.cursor = mode === 'dev' ? 'crosshair' : 'default';

  // Presets: mostrar sección correspondiente
  document.getElementById('dev-presets-section').style.display     = mode === 'dev'     ? '' : 'none';
  document.getElementById('explore-presets-section').style.display = mode === 'explore' ? '' : 'none';

  if (mode === 'explore') {
    loadExploreMap();
    renderExploreSetsList();
  } else {
    renderPresetsList();
  }

  renderFilters();
}

// Botones de modo
document.getElementById('btn-mode-dev').addEventListener('click', () => {
  if (currentMode === 'dev') return;
  if (!confirm('¿Cambiar a modo DEV? Los pins de explore no se modificarán.')) return;
  applyMode('dev');
  // Recargar mapa y pins DEV desde localStorage
  const savedMap = loadMap();
  if (savedMap) {
    initMap(savedMap, workspace, mapContainer, mapImg, uploadPrompt, onMapLoaded);
  } else {
    clearPins();
    renderPins(mapContainer, openModal);
    updatePinCount(pinCountEl);
    renderFilters();
  }
});

document.getElementById('btn-mode-explore').addEventListener('click', () => {
  if (currentMode === 'explore') return;
  applyMode('explore');
});

function loadExploreMap() {
  const raw = localStorage.getItem('mappinner_explore_bundle');
  if (!raw) {
    document.getElementById('explore-no-map').style.display = '';
    return;
  }
  document.getElementById('explore-no-map').style.display = 'none';
  try {
    const data = JSON.parse(raw);
    setPins(data.pins || []);
    initMap(data.map, workspace, mapContainer, mapImg, uploadPrompt, () => {
      renderPins(mapContainer, openModal);
      const t = getTransform();
      updatePinScales(mapContainer, t.scale);
      updatePinCount(pinCountEl);
      applyFilterVisibility();
      renderFilters();
    });
  } catch { /* nada */ }
}

// ════════════════════════════════════════════════════════════
// PRESETS DEV
// ════════════════════════════════════════════════════════════
loadPresets();

let lastSavedState = null;

function captureState() {
  return { map: getMapDataUrl(), pins: JSON.stringify(getPins()) };
}
function hasUnsavedChanges() {
  if (!lastSavedState || !getActivePresetId()) return false;
  const c = captureState();
  return c.map !== lastSavedState.map || c.pins !== lastSavedState.pins;
}

function renderPresetsList() {
  const presets  = getPresets();
  const activeId = getActivePresetId();

  if (presets.length === 0) {
    presetsList.innerHTML = '<div class="presets-empty">Sin presets.<br>Crea uno nuevo.</div>';
    return;
  }

  presetsList.innerHTML = presets.map((p, idx) => `
    <div class="preset-item ${p.id === activeId ? 'active' : ''}" data-id="${p.id}">
      <div class="preset-item-reorder">
        <button class="btn-reorder btn-reorder-up"   data-id="${p.id}" ${idx === 0 ? 'disabled' : ''}>▲</button>
        <button class="btn-reorder btn-reorder-down" data-id="${p.id}" ${idx === presets.length-1 ? 'disabled' : ''}>▼</button>
      </div>
      <div class="preset-item-name">${p.name}</div>
      <div class="preset-item-meta">${p.pins.length} pin${p.pins.length!==1?'s':''}</div>
      <div class="preset-item-controls">
        <button class="preset-item-delete" data-id="${p.id}">×</button>
      </div>
    </div>
  `).join('');

  presetsList.querySelectorAll('.preset-item').forEach(item => {
    item.addEventListener('click', e => {
      if (e.target.classList.contains('preset-item-delete') ||
          e.target.classList.contains('btn-reorder')) return;
      const tid = item.dataset.id;
      if (tid == getActivePresetId()) return;
      if (hasUnsavedChanges() && !confirm('Cambios sin guardar. ¿Continuar?')) return;
      loadDevPresetById(tid);
    });
  });

  presetsList.querySelectorAll('.preset-item-delete').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      const pid = btn.dataset.id;
      const preset = getPresets().find(p => p.id == pid);
      if (!preset || !confirm(`Eliminar preset "${preset.name}"?`)) return;
      const wasActive = pid == getActivePresetId();
      deletePreset(pid);
      loadPresets();
      if (wasActive) {
        clearPins(); mapImg.style.display = 'none';
        uploadPrompt.classList.remove('hidden');
        setActivePreset(null); lastSavedState = null;
        updatePinCount(pinCountEl);
      }
      renderPresetsList();
      showToast('Preset eliminado');
    });
  });

  presetsList.querySelectorAll('.btn-reorder-up').forEach(btn => {
    btn.addEventListener('click', e => { e.stopPropagation(); if (movePresetUp(btn.dataset.id)) renderPresetsList(); });
  });
  presetsList.querySelectorAll('.btn-reorder-down').forEach(btn => {
    btn.addEventListener('click', e => { e.stopPropagation(); if (movePresetDown(btn.dataset.id)) renderPresetsList(); });
  });
}

function loadDevPresetById(id) {
  const preset = getPresets().find(p => p.id == id);
  if (!preset) return;
  setActivePreset(preset.id);
  setPins(preset.pins);
  initMap(preset.map, workspace, mapContainer, mapImg, uploadPrompt, () => {
    renderPins(mapContainer, openModal);
    const t = getTransform();
    updatePinScales(mapContainer, t.scale);
    updatePinCount(pinCountEl);
    renderPresetsList();
    lastSavedState = captureState();
    showToast(`Preset "${preset.name}" cargado`);
    applyFilterVisibility();
    renderFilters();
  });
}

document.getElementById('btn-new-preset').addEventListener('click', () => {
  const name = prompt('Nombre del nuevo preset:');
  if (!name || !name.trim()) return;
  if (!getMapLoaded()) { showToast('Cargá un mapa primero'); return; }
  const preset = createPreset(name.trim(), getMapDataUrl(), getPins());
  setActivePreset(preset.id);
  lastSavedState = captureState();
  renderPresetsList();
  showToast(`Preset "${preset.name}" creado`);
});

document.getElementById('btn-save-preset').addEventListener('click', () => {
  const activeId = getActivePresetId();
  if (!activeId) { showToast('Seleccioná o creá un preset primero'); return; }
  if (!getMapLoaded()) { showToast('Cargá un mapa primero'); return; }
  const preset = getActivePreset();
  const name = prompt('Nombre del preset:', preset.name);
  if (!name || !name.trim()) return;
  updatePreset(activeId, name.trim(), getMapDataUrl(), getPins());
  lastSavedState = captureState();
  renderPresetsList();
  showToast(`Preset "${name}" guardado`);
});

// ════════════════════════════════════════════════════════════
// PRESETS EXPLORE (vistas: filtros + hidden)
// ════════════════════════════════════════════════════════════
loadExploreSets();

function renderExploreSetsList() {
  const list      = document.getElementById('explore-sets-list');
  const sets      = getExploreSets();
  const activeId  = getActiveExploreId();

  if (sets.length === 0) {
    list.innerHTML = '<div class="presets-empty">Sin vistas guardadas.</div>';
    return;
  }

  list.innerHTML = sets.map((s, idx) => `
    <div class="preset-item ${s.id == activeId ? 'active' : ''}" data-id="${s.id}">
      <div class="preset-item-reorder">
        <button class="btn-reorder btn-reorder-up"   data-id="${s.id}" ${idx===0?'disabled':''}>▲</button>
        <button class="btn-reorder btn-reorder-down" data-id="${s.id}" ${idx===sets.length-1?'disabled':''}>▼</button>
      </div>
      <div class="preset-item-name">${s.name}</div>
      <div class="preset-item-meta">${s.hiddenTypes.length} filtros</div>
      <div class="preset-item-controls">
        <button class="preset-item-delete" data-id="${s.id}">×</button>
      </div>
    </div>
  `).join('');

  list.querySelectorAll('.preset-item').forEach(item => {
    item.addEventListener('click', e => {
      if (e.target.classList.contains('preset-item-delete') ||
          e.target.classList.contains('btn-reorder')) return;
      loadExploreSetById(item.dataset.id);
    });
  });

  list.querySelectorAll('.preset-item-delete').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      const s = getExploreSets().find(x => x.id == btn.dataset.id);
      if (!s || !confirm(`Eliminar vista "${s.name}"?`)) return;
      deleteExploreSet(btn.dataset.id);
      renderExploreSetsList();
      showToast('Vista eliminada');
    });
  });

  list.querySelectorAll('.btn-reorder-up').forEach(btn => {
    btn.addEventListener('click', e => { e.stopPropagation(); if (moveExploreSetUp(btn.dataset.id)) renderExploreSetsList(); });
  });
  list.querySelectorAll('.btn-reorder-down').forEach(btn => {
    btn.addEventListener('click', e => { e.stopPropagation(); if (moveExploreSetDown(btn.dataset.id)) renderExploreSetsList(); });
  });
}

function loadExploreSetById(id) {
  const s = getExploreSets().find(x => x.id == id);
  if (!s) return;
  setActiveExploreSet(s.id);
  // Restaurar filtros
  hiddenTypes = new Set(s.hiddenTypes);
  // Restaurar hidden por pin
  getPins().forEach(p => { p.hidden = (s.hiddenPins || []).includes(String(p.id)); });
  savePins(getPins());
  renderPins(mapContainer, openModal);
  const t = getTransform();
  updatePinScales(mapContainer, t.scale);
  applyFilterVisibility();
  renderFilters();
  renderExploreSetsList();
  showToast(`Vista "${s.name}" cargada`);
}

document.getElementById('btn-new-explore-set').addEventListener('click', () => {
  const name = prompt('Nombre de la nueva vista:');
  if (!name || !name.trim()) return;
  const hiddenPins = getPins().filter(p => p.hidden).map(p => String(p.id));
  const s = createExploreSet(name.trim(), [...hiddenTypes], hiddenPins);
  setActiveExploreSet(s.id);
  renderExploreSetsList();
  showToast(`Vista "${s.name}" creada`);
});

document.getElementById('btn-save-explore-set').addEventListener('click', () => {
  const aid = getActiveExploreId();
  if (!aid) { showToast('Seleccioná o creá una vista primero'); return; }
  const s    = getActiveExploreSet();
  const name = prompt('Nombre de la vista:', s.name);
  if (!name || !name.trim()) return;
  const hiddenPins = getPins().filter(p => p.hidden).map(p => String(p.id));
  updateExploreSet(aid, name.trim(), [...hiddenTypes], hiddenPins);
  renderExploreSetsList();
  showToast(`Vista "${name}" guardada`);
});

// ════════════════════════════════════════════════════════════
// PANEL DE ACCIONES (izquierdo)
// ════════════════════════════════════════════════════════════
document.getElementById('btn-toggle-actions').addEventListener('click', () => {
  actionsPanel.classList.toggle('collapsed');
});
document.getElementById('btn-show-actions').addEventListener('click', () => {
  actionsPanel.classList.remove('collapsed');
});

// Panel derecho
document.getElementById('btn-toggle-presets').addEventListener('click', () => {
  presetsPanel.classList.toggle('collapsed');
});
document.getElementById('btn-show-presets').addEventListener('click', () => {
  presetsPanel.classList.remove('collapsed');
});

// ════════════════════════════════════════════════════════════
// ACCIONES DEV
// ════════════════════════════════════════════════════════════
document.getElementById('btn-export').addEventListener('click', () => {
  if (!getMapLoaded()) { showToast('Cargá un mapa primero'); return; }
  exportSave(getPins(), getMapDataUrl());
  showToast('Exportado correctamente');
});

document.getElementById('btn-import').addEventListener('click', () =>
  document.getElementById('import-input').click()
);
document.getElementById('import-input').addEventListener('change', e => {
  const file = e.target.files[0];
  if (!file) return;
  importSave(file, data => {
    if (!confirm(`Importar ${data.pins.length} pin(s)? Reemplaza el mapa y pins actuales.`)) return;
    saveMap(data.map);
    setPins(data.pins);
    savePins(getPins());
    initMap(data.map, workspace, mapContainer, mapImg, uploadPrompt, () => {
      renderPins(mapContainer, openModal);
      const t = getTransform();
      updatePinScales(mapContainer, t.scale);
      updatePinCount(pinCountEl);
      applyFilterVisibility();
      renderFilters();
      showToast(`${getPins().length} pin(s) importados`);
    });
  });
  e.target.value = '';
});

document.getElementById('btn-change-map').addEventListener('click', () => fileInput.click());

document.getElementById('btn-clear').addEventListener('click', () => {
  if (!confirm('Eliminar todos los pins del mapa?')) return;
  clearPins(); savePins(getPins());
  renderPins(mapContainer, openModal);
  const t = getTransform();
  updatePinScales(mapContainer, t.scale);
  updatePinCount(pinCountEl);
  renderFilters();
  showToast('Pins eliminados');
});

document.getElementById('btn-export-bundle').addEventListener('click', () => {
  if (!getMapLoaded()) { showToast('Cargá un mapa primero'); return; }
  exportProjectBundle(getPins(), getMapDataUrl());
  showToast('Bundle exportado — importalo en Explore');
});

// ════════════════════════════════════════════════════════════
// ACCIONES EXPLORE
// ════════════════════════════════════════════════════════════

function loadExploreBundle(file) {
  const reader = new FileReader();
  reader.onload = ev => {
    try {
      const data = JSON.parse(ev.target.result);
      if (!data.map || !Array.isArray(data.pins)) throw new Error('Formato inválido');
      if (!confirm(`Importar bundle con ${data.pins.length} pin(s)? Reemplaza el mapa y pins actuales.`)) return;
      localStorage.setItem('mappinner_explore_bundle', JSON.stringify(data));
      setPins(data.pins);
      savePins(getPins());
      initMap(data.map, workspace, mapContainer, mapImg, uploadPrompt, () => {
        document.getElementById('explore-no-map').style.display = 'none';
        renderPins(mapContainer, openModal);
        const t = getTransform();
        updatePinScales(mapContainer, t.scale);
        updatePinCount(pinCountEl);
        applyFilterVisibility();
        renderFilters();
        showToast(`${getPins().length} pin(s) importados`);
      });
    } catch { showToast('Archivo inválido o corrupto'); }
  };
  reader.readAsText(file);
}

// Caja central de carga (explore sin bundle)
const exploreBundleInput = document.getElementById('explore-bundle-input');
if (exploreBundleInput) {
  exploreBundleInput.addEventListener('change', e => {
    const file = e.target.files[0];
    if (file) loadExploreBundle(file);
    e.target.value = '';
  });
  const exploreLoadBox = document.getElementById('explore-load-box');
  exploreLoadBox.addEventListener('dragover', e => e.preventDefault());
  exploreLoadBox.addEventListener('drop', e => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (file) loadExploreBundle(file);
  });
}

// — Bundle: importar desde panel de acciones —
document.getElementById('btn-import-explore-bundle').addEventListener('click', () =>
  document.getElementById('import-bundle-input').click()
);
document.getElementById('import-bundle-input').addEventListener('change', e => {
  const file = e.target.files[0];
  if (file) loadExploreBundle(file);
  e.target.value = '';
});

// — Vistas: exportar lista completa / importar y agregar a la lista —
document.getElementById('btn-export-views').addEventListener('click', () => {
  const sets = getExploreSets();
  if (sets.length === 0) { showToast('No hay vistas guardadas para exportar'); return; }
  exportExploreViews(sets);
  showToast(`${sets.length} vista(s) exportadas`);
});

document.getElementById('btn-import-views').addEventListener('click', () =>
  document.getElementById('import-views-input').click()
);
document.getElementById('import-views-input').addEventListener('change', e => {
  const file = e.target.files[0];
  if (!file) return;
  importExploreViews(file, importedSets => {
    if (!confirm(`Importar ${importedSets.length} vista(s)? Se agregarán a las existentes.`)) return;
    importedSets.forEach(s => {
      createExploreSet(s.name, s.hiddenTypes || [], s.hiddenPins || []);
    });
    renderExploreSetsList();
    showToast(`${importedSets.length} vista(s) importadas`);
  });
  e.target.value = '';
});

// Upload
uploadBox.addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', e => {
  const f = e.target.files[0];
  if (f) loadImageFile(f, workspace, mapContainer, mapImg, uploadPrompt, onMapLoaded);
});
uploadBox.addEventListener('dragover', e => e.preventDefault());
uploadBox.addEventListener('drop', e => {
  e.preventDefault();
  const f = e.dataTransfer.files[0];
  if (f && f.type.startsWith('image/')) loadImageFile(f, workspace, mapContainer, mapImg, uploadPrompt, onMapLoaded);
});

// ════════════════════════════════════════════════════════════
// TYPE GRID
// ════════════════════════════════════════════════════════════
const typeGrid = document.getElementById('type-grid');
Object.entries(PIN_TYPES).forEach(([key, def]) => {
  const btn = document.createElement('div');
  btn.className = 'type-btn';
  btn.dataset.type = key;
  btn.innerHTML = `<span class="type-icon">${def.icon}</span><span class="type-label">${def.label}</span>`;
  btn.style.setProperty('--type-color', def.color);
  btn.addEventListener('click', () => {
    activeType = key;
    document.querySelectorAll('.type-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
  });
  typeGrid.appendChild(btn);
});

// ════════════════════════════════════════════════════════════
// MAP LOAD CALLBACK
// ════════════════════════════════════════════════════════════
function onMapLoaded() {
  const loaded = loadPins();
  setPins(loaded);
  renderPins(mapContainer, openModal);
  const t = getTransform();
  updatePinScales(mapContainer, t.scale);
  updatePinCount(pinCountEl);
  applyFilterVisibility();
  renderFilters();
}

// ════════════════════════════════════════════════════════════
// PANEL CREAR / EDITAR PIN
// ════════════════════════════════════════════════════════════
// Right-click → abrir panel (solo en DEV)
workspace.addEventListener('contextmenu', e => {
  e.preventDefault();
  if (currentMode !== 'dev') return;
  if (!getMapLoaded()) return;
  editingId  = null;
  pendingPos = getMapCoords(e.clientX, e.clientY, workspace);
  openPanel();
});

function openPanel(preselect) {
  activeType = preselect || null;
  document.querySelectorAll('.type-btn').forEach(b => {
    b.classList.remove('active');
    if (b.dataset.type === activeType) b.classList.add('active');
  });
  document.getElementById('pin-title').value = '';
  document.getElementById('pin-desc').value  = '';
  document.getElementById('pin-panel-title').textContent = editingId ? 'Editar pin' : 'Nuevo pin';
  document.getElementById('btn-confirm').textContent     = editingId ? 'Guardar cambios' : 'Confirmar';

  // Si estamos editando, pre-cargar datos
  if (editingId) {
    const pin = findPin(editingId);
    if (pin) {
      document.getElementById('pin-title').value = pin.title || '';
      document.getElementById('pin-desc').value  = pin.desc  || '';
      activeType = pin.type;
      document.querySelectorAll('.type-btn').forEach(b => {
        b.classList.toggle('active', b.dataset.type === activeType);
      });
      resetImageSlots();
      (pin.images || []).forEach((src, i) => {
        if (i >= 2) return;
        pendingImages[i] = src;
        renderSlotPreview(i, src);
        if (i === 0) lockSlot(1, false);
      });
      // Unlocks
      setUnlocksToggle(!!pin.unlocks);
      // Requirements
      pendingRequirements = new Set(pin.requirements || []);
    }
  } else {
    resetImageSlots();
    setUnlocksToggle(false);
    pendingRequirements = new Set();
  }

  renderRequirementsList();

  overlay.classList.add('visible');
  pinPanel.classList.add('visible');
  setTimeout(() => document.getElementById('pin-title').focus(), 80);
}

function closePanel() {
  pinPanel.classList.remove('visible');
  overlay.classList.remove('visible');
  pendingPos          = null;
  activeType          = null;
  editingId           = null;
  pendingRequirements = new Set();
}

// ── Unlocks / Requirements ─────────────────────────────────
let pendingRequirements = new Set();

function setUnlocksToggle(val) {
  const btn = document.getElementById('btn-toggle-unlocks');
  btn.setAttribute('aria-pressed', val ? 'true' : 'false');
  btn.textContent = val ? 'ON' : 'OFF';
}

function getUnlocksToggle() {
  return document.getElementById('btn-toggle-unlocks').getAttribute('aria-pressed') === 'true';
}

document.getElementById('btn-toggle-unlocks').addEventListener('click', () => {
  setUnlocksToggle(!getUnlocksToggle());
});

function renderRequirementsList() {
  const container = document.getElementById('req-list');
  // Pins con unlocks=true, excluyendo el pin que se está editando
  const candidates = getPins().filter(p =>
    p.unlocks && String(p.id) !== String(editingId)
  );

  if (candidates.length === 0) {
    container.innerHTML = '<div class="req-list-empty">No hay pins con "Unlocks something" creados.</div>';
    return;
  }

  container.innerHTML = '';
  candidates.forEach(p => {
    const def      = PIN_TYPES[p.type] || { icon: '?', label: p.type };
    const selected = pendingRequirements.has(String(p.id));
    const item     = document.createElement('div');
    item.className = `req-item${selected ? ' selected' : ''}`;
    item.innerHTML = `
      <div class="req-item-check">${selected ? '✓' : ''}</div>
      <span class="req-item-icon" style="color:${def.color}">${def.icon}</span>
      <span class="req-item-name">${p.title || def.label}</span>
      <span class="req-item-type">${def.label}</span>
    `;
    item.addEventListener('click', () => {
      const sid = String(p.id);
      if (pendingRequirements.has(sid)) pendingRequirements.delete(sid);
      else pendingRequirements.add(sid);
      renderRequirementsList();
    });
    container.appendChild(item);
  });
}

// ─── Imágenes ─────────────────────────────────────────────
const pendingImages = [null, null];

function resetImageSlots() {
  pendingImages[0] = null;
  pendingImages[1] = null;
  [0, 1].forEach(i => {
    const slot = document.getElementById(`pin-img-slot-${i}`);
    slot.querySelector('.pin-img-placeholder').style.display = '';
    const prev = slot.querySelector('.pin-img-preview');
    if (prev) prev.remove();
    const rem  = slot.querySelector('.pin-img-remove');
    if (rem)  rem.remove();
    const inp  = slot.querySelector('input[type="file"]');
    inp.value = '';
  });
  lockSlot(1, true);
}

function lockSlot(i, locked) {
  const slot = document.getElementById(`pin-img-slot-${i}`);
  const inp  = slot.querySelector('input[type="file"]');
  slot.classList.toggle('pin-img-slot-locked', locked);
  inp.disabled = locked;
}

function setupImageSlot(i) {
  const inp = document.getElementById(`pin-img-${i}`);
  inp.addEventListener('change', e => {
    const file = e.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = ev => {
      pendingImages[i] = ev.target.result;
      renderSlotPreview(i, ev.target.result);
      if (i === 0) lockSlot(1, false);
    };
    reader.readAsDataURL(file);
  });
}

function renderSlotPreview(i, dataUrl) {
  const slot = document.getElementById(`pin-img-slot-${i}`);
  slot.querySelector('.pin-img-placeholder').style.display = 'none';
  const old = slot.querySelector('.pin-img-preview');
  if (old) old.remove();
  const oldRem = slot.querySelector('.pin-img-remove');
  if (oldRem) oldRem.remove();
  const img = document.createElement('img');
  img.className = 'pin-img-preview';
  img.src = dataUrl;
  slot.appendChild(img);
  const rem = document.createElement('button');
  rem.className = 'pin-img-remove';
  rem.textContent = '×';
  rem.addEventListener('click', e => {
    e.stopPropagation(); e.preventDefault();
    pendingImages[i] = null;
    img.remove(); rem.remove();
    slot.querySelector('.pin-img-placeholder').style.display = '';
    slot.querySelector('input[type="file"]').value = '';
    if (i === 0) {
      pendingImages[1] = null;
      const slot1 = document.getElementById('pin-img-slot-1');
      const prev1 = slot1.querySelector('.pin-img-preview');
      if (prev1) prev1.remove();
      const rem1 = slot1.querySelector('.pin-img-remove');
      if (rem1) rem1.remove();
      slot1.querySelector('.pin-img-placeholder').style.display = '';
      slot1.querySelector('input[type="file"]').value = '';
      lockSlot(1, true);
    }
  });
  slot.appendChild(rem);
}

setupImageSlot(0);
setupImageSlot(1);
resetImageSlots();

document.getElementById('panel-close').addEventListener('click', closePanel);
document.getElementById('btn-cancel').addEventListener('click', closePanel);
overlay.addEventListener('click', closePanel);

document.getElementById('btn-confirm').addEventListener('click', () => {
  if (!activeType) { showToast('Seleccioná una categoría'); return; }
  const def = PIN_TYPES[activeType];

  if (editingId) {
    // ── EDITAR pin existente ──
    const pin = findPin(editingId);
    if (!pin) return;
    pin.type         = activeType;
    pin.title        = document.getElementById('pin-title').value.trim() || def.label;
    pin.desc         = document.getElementById('pin-desc').value.trim();
    pin.images       = pendingImages.filter(Boolean);
    pin.unlocks      = getUnlocksToggle();
    pin.requirements = [...pendingRequirements];
    savePins(getPins());
    renderPins(mapContainer, openModal);
    const t = getTransform();
    updatePinScales(mapContainer, t.scale);
    applyFilterVisibility();
    renderFilters();
    closePanel();
    showToast('Pin actualizado');
  } else {
    // ── NUEVO pin ──
    if (!pendingPos) return;
    const pin = {
      id:           Date.now() + Math.random(),
      type:         activeType,
      x:            pendingPos.x,
      y:            pendingPos.y,
      title:        document.getElementById('pin-title').value.trim() || def.label,
      desc:         document.getElementById('pin-desc').value.trim(),
      images:       pendingImages.filter(Boolean),
      unlocks:      getUnlocksToggle(),
      requirements: [...pendingRequirements]
    };
    addPin(pin);
    savePins(getPins());
    createPinEl(pin, mapContainer, openModal);
    const t = getTransform();
    updatePinScales(mapContainer, t.scale);
    updatePinCount(pinCountEl);
    applyFilterVisibility();
    renderFilters();
    closePanel();
    showToast('Pin guardado');
  }
});

document.getElementById('pin-title').addEventListener('keydown', e => {
  if (e.key === 'Enter') document.getElementById('btn-confirm').click();
});

// ════════════════════════════════════════════════════════════
// MODAL DE LECTURA
// ════════════════════════════════════════════════════════════
function openModal(id) {
  const pin = findPin(id);
  if (!pin) return;
  const def = PIN_TYPES[pin.type] || { label: pin.type, color: '#888', icon: '?' };
  selectedId = id;

  // Imágenes
  const imgContainer = document.getElementById('modal-images');
  imgContainer.innerHTML = '';
  const imgs = (pin.images || []).filter(Boolean);
  if (imgs.length > 0) {
    imgContainer.classList.add('has-images');
    imgContainer.classList.toggle('single', imgs.length === 1);
    imgContainer.classList.toggle('dual',   imgs.length === 2);
    const track = document.createElement('div');
    track.className = 'modal-images-track';
    imgs.forEach(src => {
      const img = document.createElement('img');
      img.src = src; img.alt = '';
      track.appendChild(img);
    });
    imgContainer.appendChild(track);
  } else {
    imgContainer.classList.remove('has-images', 'single', 'dual');
  }

  document.getElementById('modal-pin-icon').textContent = def.icon;
  document.getElementById('modal-pin-icon').style.color = def.color;
  document.getElementById('modal-title').textContent    = pin.title || '(sin nombre)';
  document.getElementById('modal-tag').textContent      = def.label;
  document.getElementById('modal-desc').textContent     = pin.desc  || '';

  // ── Relaciones ──────────────────────────────────────────
  // Requirements: pins que este pin necesita
  const reqSection = document.getElementById('modal-requirements');
  const reqList    = document.getElementById('modal-req-list');
  const reqs = (pin.requirements || [])
    .map(rid => getPins().find(p => String(p.id) === String(rid)))
    .filter(Boolean);

  if (reqs.length > 0) {
    reqList.innerHTML = '';
    reqs.forEach(rp => {
      const rd   = PIN_TYPES[rp.type] || { icon: '?', color: '#888', label: rp.type };
      const chip = document.createElement('div');
      chip.className = 'modal-rel-chip';
      chip.innerHTML = `
        <span class="modal-rel-chip-icon" style="color:${rd.color}">${rd.icon}</span>
        <span class="modal-rel-chip-name">${rp.title || rd.label}</span>
        <span class="modal-rel-chip-arrow">→</span>
      `;
      chip.addEventListener('click', () => navigateToPin(rp.id));
      reqList.appendChild(chip);
    });
    reqSection.style.display = '';
  } else {
    reqSection.style.display = 'none';
  }

  // Used-in: pins que usan este como requirement
  const usedSection = document.getElementById('modal-used-in');
  const usedList    = document.getElementById('modal-usedin-list');
  const usedIn = getPins().filter(p =>
    (p.requirements || []).some(rid => String(rid) === String(id))
  );

  if (usedIn.length > 0) {
    usedList.innerHTML = '';
    usedIn.forEach(up => {
      const ud   = PIN_TYPES[up.type] || { icon: '?', color: '#888', label: up.type };
      const chip = document.createElement('div');
      chip.className = 'modal-rel-chip';
      chip.innerHTML = `
        <span class="modal-rel-chip-icon" style="color:${ud.color}">${ud.icon}</span>
        <span class="modal-rel-chip-name">${up.title || ud.label}</span>
        <span class="modal-rel-chip-arrow">→</span>
      `;
      chip.addEventListener('click', () => navigateToPin(up.id));
      usedList.appendChild(chip);
    });
    usedSection.style.display = '';
  } else {
    usedSection.style.display = 'none';
  }

  const hideBtn = document.getElementById('btn-hide');
  hideBtn.textContent = pin.hidden ? 'Unhide' : 'Hide';
  hideBtn.classList.toggle('active', pin.hidden);

  pinModal.classList.add('visible');
}

document.getElementById('btn-close-modal').addEventListener('click', () => {
  pinModal.classList.remove('visible'); selectedId = null;
});

// ── Navegación a un pin desde el modal ────────────────────
function navigateToPin(targetId) {
  const pin = findPin(targetId);
  if (!pin) return;

  // Cerrar modal actual
  pinModal.classList.remove('visible');
  selectedId = null;

  // Calcular posición centrada en el viewport
  const { scale } = getTransform();
  const ww = workspace.clientWidth;
  const wh = workspace.clientHeight;
  const newOffsetX = ww / 2 - pin.x * scale;
  const newOffsetY = wh / 2 - pin.y * scale;

  // Animar el desplazamiento
  const mapContainer = document.getElementById('map-container');
  const mapImg       = document.getElementById('map-img');
  mapContainer.style.transition = 'transform 0.5s cubic-bezier(0.4,0,0.2,1)';
  // Usar la API interna de map.js para aplicar el nuevo offset
  setMapOffset(newOffsetX, newOffsetY);

  setTimeout(() => {
    mapContainer.style.transition = '';
    // Highlight del pin
    const el = mapContainer.querySelector(`.pin[data-id="${targetId}"]`);
    if (el) {
      el.classList.remove('pin-highlighted');
      void el.offsetWidth; // reflow para reiniciar animación
      el.classList.add('pin-highlighted');
      setTimeout(() => el.classList.remove('pin-highlighted'), 1500);
    }
    // Abrir el modal del pin destino
    openModal(targetId);
  }, 520);
}

document.getElementById('btn-hide').addEventListener('click', () => {
  if (!selectedId) return;
  const pin = findPin(selectedId);
  if (!pin) return;
  pin.hidden = !pin.hidden;
  savePins(getPins());
  renderPins(mapContainer, openModal);
  const t = getTransform();
  updatePinScales(mapContainer, t.scale);
  const hideBtn = document.getElementById('btn-hide');
  hideBtn.textContent = pin.hidden ? 'Unhide' : 'Hide';
  hideBtn.classList.toggle('active', pin.hidden);
});

document.getElementById('btn-edit').addEventListener('click', () => {
  if (!selectedId || currentMode !== 'dev') return;
  editingId  = selectedId;
  pendingPos = null;
  pinModal.classList.remove('visible');
  openPanel();
});

document.getElementById('btn-del').addEventListener('click', () => {
  if (!selectedId) return;
  removePin(selectedId);
  savePins(getPins());
  renderPins(mapContainer, openModal);
  const t = getTransform();
  updatePinScales(mapContainer, t.scale);
  updatePinCount(pinCountEl);
  applyFilterVisibility();
  renderFilters();
  pinModal.classList.remove('visible');
  selectedId = null;
  showToast('Pin eliminado');
});

pinModal.addEventListener('click', e => {
  if (e.target === pinModal) { pinModal.classList.remove('visible'); selectedId = null; }
});

// ════════════════════════════════════════════════════════════
// FILTROS DE CATEGORÍAS
// ════════════════════════════════════════════════════════════
let hiddenTypes = new Set();

function renderFilters() {
  const container = document.getElementById('filters-categories');
  if (!container) return;
  const pins = getPins();

  container.innerHTML = PIN_GROUPS.map(group => {
    const typesInGroup = Object.entries(PIN_TYPES).filter(([, def]) => def.group === group.key);
    const groupColor   = GROUP_COLORS[group.key];

    const rows = typesInGroup.map(([key, def]) => {
      const count    = pins.filter(p => p.type === key).length;
      const isHidden = hiddenTypes.has(key);
      return `
        <div class="filter-cat-row ${isHidden ? 'hidden-cat' : ''}" data-type="${key}"
             style="--cat-color: ${groupColor}">
          <span class="filter-cat-icon">${def.icon}</span>
          <span class="filter-cat-label">${def.label}</span>
          <span class="filter-cat-count">${count}</span>
        </div>
      `;
    }).join('');

    return `
      <div class="filter-group">
        <div class="filter-group-header" style="--group-color: ${groupColor}">
          <span class="filter-group-dot"></span>
          <span class="filter-group-label">${group.label}</span>
        </div>
        <div class="filter-group-rows">${rows}</div>
      </div>
    `;
  }).join('');

  container.querySelectorAll('.filter-cat-row').forEach(row => {
    row.addEventListener('click', () => {
      const type = row.dataset.type;
      if (hiddenTypes.has(type)) hiddenTypes.delete(type);
      else hiddenTypes.add(type);
      applyFilterVisibility();
      renderFilters();
    });
  });
}

function applyFilterVisibility() {
  mapContainer.querySelectorAll('.pin').forEach(el => {
    const rawId = el.dataset.id;
    const pin = getPins().find(p => String(p.id) === rawId);
    if (!pin) return;
    el.classList.toggle('pin-filter-hidden', hiddenTypes.has(pin.type));
  });
}

document.getElementById('btn-show-all').addEventListener('click', () => {
  hiddenTypes.clear();
  applyFilterVisibility(); renderFilters();
  showToast('Todos los pins visibles');
});
document.getElementById('btn-hide-all').addEventListener('click', () => {
  Object.keys(PIN_TYPES).forEach(k => hiddenTypes.add(k));
  applyFilterVisibility(); renderFilters();
  showToast('Todos los pins ocultos');
});

// ════════════════════════════════════════════════════════════
// ARRANQUE — pantalla de selección de modo
// ════════════════════════════════════════════════════════════
function enterMode(mode) {
  document.getElementById('mode-select-screen').classList.add('hidden');
  document.body.classList.add('mode-ready');
  applyMode(mode);
  if (mode === 'dev') {
    const savedMap = loadMap();
    if (savedMap) initMap(savedMap, workspace, mapContainer, mapImg, uploadPrompt, onMapLoaded);
  }
  // En explore: applyMode ya llama loadExploreMap()
}

document.getElementById('mss-dev').addEventListener('click',     () => enterMode('dev'));
document.getElementById('mss-explore').addEventListener('click', () => enterMode('explore'));

setupPanning(workspace, mapContainer, mapImg);

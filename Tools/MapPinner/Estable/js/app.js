// DOM refs
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

let pendingPos = null;
let activeType = null;
let selectedId = null;

// Toast
function showToast(msg) {
  toastEl.textContent = msg;
  toastEl.classList.add('show');
  setTimeout(() => toastEl.classList.remove('show'), 2200);
}

// Presets UI
loadPresets();
renderPresetsList();

let lastSavedState = null;

function captureState() {
  return {
    map: getMapDataUrl(),
    pins: JSON.stringify(getPins())
  };
}

function hasUnsavedChanges() {
  if (!lastSavedState || !getActivePresetId()) return false;
  const current = captureState();
  return current.map !== lastSavedState.map || current.pins !== lastSavedState.pins;
}

function renderPresetsList() {
  const presets = getPresets();
  const activeId = getActivePresetId();
  
  if (presets.length === 0) {
    presetsList.innerHTML = '<div class="presets-empty">Sin presets guardados.<br>Crea uno nuevo para empezar.</div>';
    return;
  }
  
  presetsList.innerHTML = presets.map((p, idx) => `
    <div class="preset-item ${p.id === activeId ? 'active' : ''}" data-id="${p.id}">
      <div class="preset-item-reorder">
        <button class="btn-reorder btn-reorder-up" data-id="${p.id}" ${idx === 0 ? 'disabled' : ''}>▲</button>
        <button class="btn-reorder btn-reorder-down" data-id="${p.id}" ${idx === presets.length - 1 ? 'disabled' : ''}>▼</button>
      </div>
      <div class="preset-item-name">${p.name}</div>
      <div class="preset-item-meta">${p.pins.length} pin${p.pins.length !== 1 ? 's' : ''}</div>
      <div class="preset-item-controls">
        <button class="preset-item-delete" data-id="${p.id}">×</button>
      </div>
    </div>
  `).join('');
  
  // Click to load preset
  presetsList.querySelectorAll('.preset-item').forEach(item => {
    item.addEventListener('click', e => {
      if (e.target.classList.contains('preset-item-delete') ||
          e.target.classList.contains('btn-reorder')) return;
      
      const targetId = item.dataset.id;
      if (targetId == getActivePresetId()) return;
      
      if (hasUnsavedChanges()) {
        const shouldChange = confirm('Tenés cambios sin guardar en este preset.\n¿Querés cambiar igualmente?');
        
        if (!shouldChange) {
          // No cambiar, quedarse en el preset actual
          return;
        }
        // Si acepta, continúa y cambia sin guardar
      }
      
      loadPresetById(targetId);
    });
  });
  
  // Delete preset
  presetsList.querySelectorAll('.preset-item-delete').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      const presetId = btn.dataset.id;
      const allPresets = getPresets();
      const preset = allPresets.find(p => p.id == presetId);
      if (!preset) return;
      
      if (!confirm(`Eliminar preset "${preset.name}"?`)) return;
      
      const wasActive = presetId == getActivePresetId();
      deletePreset(presetId);
      loadPresets(); // Reload from localStorage
      
      if (wasActive) {
        // Clear screen
        clearPins();
        mapImg.style.display = 'none';
        uploadPrompt.classList.remove('hidden');
        setActivePreset(null);
        lastSavedState = null;
        updatePinCount(pinCountEl);
      }
      
      renderPresetsList();
      showToast('Preset eliminado');
    });
  });
  
  // Reorder up
  presetsList.querySelectorAll('.btn-reorder-up').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      if (movePresetUp(btn.dataset.id)) {
        renderPresetsList();
      }
    });
  });
  
  // Reorder down
  presetsList.querySelectorAll('.btn-reorder-down').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      if (movePresetDown(btn.dataset.id)) {
        renderPresetsList();
      }
    });
  });
}

function loadPresetById(id) {
  const presets = getPresets();
  const preset = presets.find(p => p.id == id);
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

document.getElementById('btn-toggle-presets').addEventListener('click', () => {
  presetsPanel.classList.toggle('collapsed');
});

document.getElementById('btn-show-presets').addEventListener('click', () => {
  presetsPanel.classList.remove('collapsed');
});

document.getElementById('btn-new-preset').addEventListener('click', () => {
  const name = prompt('Nombre del nuevo preset:');
  if (!name || !name.trim()) return;
  
  if (!getMapLoaded()) {
    showToast('Cargá un mapa primero');
    return;
  }
  
  const preset = createPreset(name.trim(), getMapDataUrl(), getPins());
  setActivePreset(preset.id);
  lastSavedState = captureState();
  renderPresetsList();
  showToast(`Preset "${preset.name}" creado`);
});

document.getElementById('btn-save-preset').addEventListener('click', () => {
  const activeId = getActivePresetId();
  
  if (!activeId) {
    showToast('Seleccioná un preset primero o creá uno nuevo');
    return;
  }
  
  if (!getMapLoaded()) {
    showToast('Cargá un mapa primero');
    return;
  }
  
  const preset = getActivePreset();
  const name = prompt('Nombre del preset:', preset.name);
  if (!name || !name.trim()) return;
  
  updatePreset(activeId, name.trim(), getMapDataUrl(), getPins());
  lastSavedState = captureState();
  renderPresetsList();
  showToast(`Preset "${name}" guardado`);
});

// Build type grid from PIN_TYPES
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

// Map init callback
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

document.getElementById('btn-change-map').addEventListener('click', () => fileInput.click());

// Clear
document.getElementById('btn-clear').addEventListener('click', () => {
  if (!confirm('Eliminar todos los pins del mapa?')) return;
  clearPins();
  savePins(getPins());
  renderPins(mapContainer, openModal);
  const t = getTransform();
  updatePinScales(mapContainer, t.scale);
  updatePinCount(pinCountEl);
  renderFilters();
  showToast('Pins eliminados');
});

// Export
document.getElementById('btn-export').addEventListener('click', () => {
  if (!getMapLoaded()) { showToast('Cargá un mapa primero'); return; }
  exportSave(getPins(), getMapDataUrl());
  showToast('Exportado correctamente');
});

// Import
document.getElementById('btn-import').addEventListener('click', () => importInput.click());
importInput.addEventListener('change', e => {
  const file = e.target.files[0];
  if (!file) return;
  importSave(file, data => {
    if (!confirm(`Importar ${data.pins.length} pin(s)? Esto reemplaza el mapa y pins actuales.`)) return;
    saveMap(data.map);
    setPins(data.pins);
    savePins(getPins());
    initMap(data.map, workspace, mapContainer, mapImg, uploadPrompt, () => {
      renderPins(mapContainer, openModal);
      const t = getTransform();
      updatePinScales(mapContainer, t.scale);
      updatePinCount(pinCountEl);
      showToast(`${getPins().length} pin(s) importados`);
    });
  });
  importInput.value = '';
});

// Right-click → open panel directly
workspace.addEventListener('contextmenu', e => {
  e.preventDefault();
  if (!getMapLoaded()) return;
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
  // Reset image slots
  resetImageSlots();
  overlay.classList.add('visible');
  pinPanel.classList.add('visible');
  setTimeout(() => document.getElementById('pin-title').focus(), 80);
}

function closePanel() {
  pinPanel.classList.remove('visible');
  overlay.classList.remove('visible');
  pendingPos = null;
  activeType = null;
}

// ============================================================
// IMÁGENES EN PANEL DE CREACIÓN
// ============================================================
const pendingImages = [null, null]; // base64 strings

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
  // Bloquear slot 1 al inicio
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
      // Desbloquear siguiente slot si existe
      if (i === 0) lockSlot(1, false);
    };
    reader.readAsDataURL(file);
  });
}

function renderSlotPreview(i, dataUrl) {
  const slot = document.getElementById(`pin-img-slot-${i}`);
  slot.querySelector('.pin-img-placeholder').style.display = 'none';
  // Limpiar prev existente
  const old = slot.querySelector('.pin-img-preview');
  if (old) old.remove();
  const oldRem = slot.querySelector('.pin-img-remove');
  if (oldRem) oldRem.remove();
  // Imagen
  const img = document.createElement('img');
  img.className = 'pin-img-preview';
  img.src = dataUrl;
  slot.appendChild(img);
  // Botón quitar
  const rem = document.createElement('button');
  rem.className = 'pin-img-remove';
  rem.textContent = '×';
  rem.addEventListener('click', e => {
    e.stopPropagation();
    e.preventDefault();
    pendingImages[i] = null;
    img.remove();
    rem.remove();
    slot.querySelector('.pin-img-placeholder').style.display = '';
    slot.querySelector('input[type="file"]').value = '';
    // Si se borra la primera, limpiar y bloquear la segunda también
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
  if (!pendingPos) return;
  if (!activeType) { showToast('Seleccioná una categoría'); return; }
  const def = PIN_TYPES[activeType];
  const pin = {
    id:     Date.now() + Math.random(),
    type:   activeType,
    x:      pendingPos.x,
    y:      pendingPos.y,
    title:  document.getElementById('pin-title').value.trim() || def.label,
    desc:   document.getElementById('pin-desc').value.trim(),
    images: pendingImages.filter(Boolean)
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
});

document.getElementById('pin-title').addEventListener('keydown', e => {
  if (e.key === 'Enter') document.getElementById('btn-confirm').click();
});

// ============================================================
// MODAL DE LECTURA
// ============================================================
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
      img.src = src;
      img.alt = '';
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
  document.getElementById('modal-desc').textContent     = pin.desc || '';

  const hideBtn = document.getElementById('btn-hide');
  hideBtn.textContent = pin.hidden ? 'Unhide' : 'Hide';
  hideBtn.classList.toggle('active', pin.hidden);

  pinModal.classList.add('visible');
}

document.getElementById('btn-close-modal').addEventListener('click', () => {
  pinModal.classList.remove('visible'); selectedId = null;
});

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

// ============================================================
// PANEL IZQUIERDO — ACCIONES
// ============================================================
document.getElementById('btn-toggle-actions').addEventListener('click', () => {
  actionsPanel.classList.toggle('collapsed');
});

document.getElementById('btn-show-actions').addEventListener('click', () => {
  actionsPanel.classList.remove('collapsed');
});

// ============================================================
// FILTROS DE CATEGORÍAS
// ============================================================
// Estado: set de tipos ocultos
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
    const typeHidden = hiddenTypes.has(pin.type);
    el.classList.toggle('pin-filter-hidden', typeHidden);
  });
}

document.getElementById('btn-show-all').addEventListener('click', () => {
  hiddenTypes.clear();
  applyFilterVisibility();
  renderFilters();
  showToast('Todos los pins visibles');
});

document.getElementById('btn-hide-all').addEventListener('click', () => {
  Object.keys(PIN_TYPES).forEach(k => hiddenTypes.add(k));
  applyFilterVisibility();
  renderFilters();
  showToast('Todos los pins ocultos');
});

// Panning
setupPanning(workspace, mapContainer, mapImg);

// Auto-load
const savedMap = loadMap();
if (savedMap) initMap(savedMap, workspace, mapContainer, mapImg, uploadPrompt, onMapLoaded);

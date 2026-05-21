const LS_PINS = 'mappinner_pins';
const LS_MAP  = 'mappinner_map';

function savePins(pins) {
  localStorage.setItem(LS_PINS, JSON.stringify(pins));
}

function loadPins() {
  try { return JSON.parse(localStorage.getItem(LS_PINS)) || []; }
  catch { return []; }
}

function saveMap(dataUrl) {
  try {
    localStorage.setItem(LS_MAP, dataUrl);
  } catch {
    showToast('Imagen muy grande para guardar localmente');
  }
}

function loadMap() {
  return localStorage.getItem(LS_MAP);
}

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

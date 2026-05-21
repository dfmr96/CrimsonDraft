let scale = 1, offsetX = 0, offsetY = 0;
let isDragging = false, dragStart = { x: 0, y: 0 };
let mapLoaded = false;
let currentMapDataUrl = null;

function getMapLoaded() { return mapLoaded; }
function getMapDataUrl() { return currentMapDataUrl; }
function getTransform() { return { scale, offsetX, offsetY }; }

function fitImage(workspace, mapImg) {
  const ww = workspace.clientWidth, wh = workspace.clientHeight;
  const iw = mapImg.naturalWidth,   ih = mapImg.naturalHeight;
  scale   = Math.min(ww / iw, wh / ih, 1) * 0.9;
  offsetX = (ww - iw * scale) / 2;
  offsetY = (wh - ih * scale) / 2;
  applyTransform(mapImg.parentElement, mapImg);
}

function applyTransform(mapContainer, mapImg) {
  mapContainer.style.transform = `translate(${offsetX}px,${offsetY}px) scale(${scale})`;
  mapImg.style.width  = mapImg.naturalWidth  + 'px';
  mapImg.style.height = mapImg.naturalHeight + 'px';
  if (typeof updatePinScales === 'function') {
    updatePinScales(mapContainer, scale);
  }
}

function initMap(dataUrl, workspace, mapContainer, mapImg, uploadPrompt, onLoaded) {
  currentMapDataUrl = dataUrl;
  mapImg.src = dataUrl;
  mapImg.style.display = 'block';
  mapImg.onload = () => {
    mapLoaded = true;
    uploadPrompt.classList.add('hidden');
    fitImage(workspace, mapImg);
    if (onLoaded) onLoaded();
  };
}

function loadImageFile(file, workspace, mapContainer, mapImg, uploadPrompt, onLoaded) {
  const reader = new FileReader();
  reader.onload = e => {
    saveMap(e.target.result);
    initMap(e.target.result, workspace, mapContainer, mapImg, uploadPrompt, onLoaded);
  };
  reader.readAsDataURL(file);
}

function setupPanning(workspace, mapContainer, mapImg) {
  workspace.addEventListener('mousedown', e => {
    if (e.button === 0 && mapLoaded) {
      isDragging = true;
      dragStart = { x: e.clientX - offsetX, y: e.clientY - offsetY };
      workspace.style.cursor = 'grabbing';
    }
  });

  window.addEventListener('mousemove', e => {
    if (!isDragging) return;
    offsetX = e.clientX - dragStart.x;
    offsetY = e.clientY - dragStart.y;
    applyTransform(mapContainer, mapImg);
  });

  window.addEventListener('mouseup', () => {
    isDragging = false;
    workspace.style.cursor = 'crosshair';
  });

  workspace.addEventListener('wheel', e => {
    e.preventDefault();
    if (!mapLoaded) return;
    const rect = workspace.getBoundingClientRect();
    const mx = e.clientX - rect.left, my = e.clientY - rect.top;
    const delta = e.deltaY > 0 ? 0.9 : 1.1;
    const ns = Math.max(0.1, Math.min(5, scale * delta));
    offsetX = mx - (mx - offsetX) * (ns / scale);
    offsetY = my - (my - offsetY) * (ns / scale);
    scale = ns;
    applyTransform(mapContainer, mapImg);
  }, { passive: false });
}

function getMapCoords(clientX, clientY, workspace) {
  const rect = workspace.getBoundingClientRect();
  return {
    x: (clientX - rect.left - offsetX) / scale,
    y: (clientY - rect.top  - offsetY) / scale
  };
}

// viewer-steps.js — Steps viewer (read-only)

const ViewerSteps = (() => {

  let steps = [];
  let activeStepId = null;
  let isActive = false;
  let hideOthers = false;
  let mapContainer = null;
  let cwrap = null;
  let panStart = null, panOffStart = null;

  let arrowsSvg = null;
  let arrowsLayer = null;
  const NS = 'http://www.w3.org/2000/svg';
  const RAINBOW = ['#ff4444','#ff8c44','#ffd644','#44ff8c','#44b5ff','#8c44ff','#ff44b5'];

  function init() {
    mapContainer = document.getElementById('map-container');
    cwrap = document.getElementById('cwrap');
    createArrowsOverlay();
    buildPanel();
    initPan();
  }

  function createArrowsOverlay() {
    arrowsSvg = document.createElementNS(NS, 'svg');
    arrowsSvg.classList.add('arrows-overlay');
    arrowsSvg.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none;z-index:4';
    arrowsLayer = document.createElementNS(NS, 'g');
    arrowsLayer.classList.add('arrows-layer');
    arrowsSvg.appendChild(arrowsLayer);
    mapContainer.appendChild(arrowsSvg);
  }

  function renderAllArrows() {
    if (!arrowsLayer) return;
    arrowsLayer.innerHTML = '';
    const state = Editor.getState();

    const step = steps.find(s => s.id === activeStepId);
    const stepArrows = step ? (step.arrows || []) : [];

    stepArrows.forEach(arrow => appendArrowFinal(arrow));

    updateArrowTransform();
    updateArrowNumbers();
  }

  function appendArrowFinal(arrow) {
    const color = arrow.color || '#ff6b35';
    const g = document.createElementNS(NS, 'g');
    g.classList.add('arrow-final', 'arrow-group');
    g.dataset.arrowId = arrow.id;
    g.dataset.arrowNumber = arrow.number;
    g.dataset.arrowText = arrow.text || '';

    const path = document.createElementNS(NS, 'path');
    path.setAttribute('d', buildPolylineD(arrow.points));
    path.setAttribute('vector-effect', 'non-scaling-stroke');
    path.style.cssText = `fill:none;stroke:${color};stroke-width:2.5;stroke-linecap:round;stroke-linejoin:round;pointer-events:none`;
    g.appendChild(path);

    const head = document.createElementNS(NS, 'path');
    head.setAttribute('d', buildArrowheadD(arrow.points));
    head.style.cssText = `fill:${color};pointer-events:none`;
    g.appendChild(head);

    const mid = getMidpoint(arrow.points);
    const text = document.createElementNS(NS, 'text');
    text.classList.add('arrow-number');
    text.setAttribute('x', mid.x);
    text.setAttribute('y', mid.y);
    text.setAttribute('text-anchor', 'middle');
    text.setAttribute('dominant-baseline', 'central');
    text.style.cssText = 'fill:#fff;font-weight:bold;paint-order:stroke;stroke:#000;stroke-width:2.5;stroke-linecap:round;stroke-linejoin:round;pointer-events:none';
    text.textContent = arrow.number;
    g.appendChild(text);

    // Hit area for clicking
    const hit = document.createElementNS(NS, 'path');
    hit.setAttribute('d', buildPolylineD(arrow.points));
    hit.setAttribute('vector-effect', 'non-scaling-stroke');
    hit.style.cssText = 'fill:none;stroke:transparent;stroke-width:12;pointer-events:stroke;cursor:pointer';
    g.insertBefore(hit, path);

    g.addEventListener('click', e => {
      e.stopPropagation();
      openArrowReadModal(g.dataset.arrowId);
    });

    arrowsLayer.appendChild(g);
  }

  function buildPolylineD(points) {
    if (!points || points.length < 2) return '';
    return points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
  }

  function buildArrowheadD(points) {
    if (!points || points.length < 2) return '';
    const last = points[points.length - 1];
    const prev = points[points.length - 2];
    const dx = last.x - prev.x;
    const dy = last.y - prev.y;
    const angle = Math.atan2(dy, dx);
    const len = 20;
    const spread = Math.PI / 6;
    const lx = last.x - len * Math.cos(angle - spread);
    const ly = last.y - len * Math.sin(angle - spread);
    const rx = last.x - len * Math.cos(angle + spread);
    const ry = last.y - len * Math.sin(angle + spread);
    return `M ${last.x} ${last.y} L ${lx} ${ly} L ${rx} ${ry} Z`;
  }

  function getMidpoint(points) {
    let totalLen = 0;
    const segLens = [];
    for (let i = 1; i < points.length; i++) {
      const dx = points[i].x - points[i - 1].x;
      const dy = points[i].y - points[i - 1].y;
      const len = Math.hypot(dx, dy);
      segLens.push(len);
      totalLen += len;
    }
    if (totalLen === 0) return { x: points[0].x, y: points[0].y };
    const half = totalLen / 2;
    let accum = 0;
    for (let i = 0; i < segLens.length; i++) {
      if (accum + segLens[i] >= half) {
        const t = segLens[i] > 0 ? (half - accum) / segLens[i] : 0;
        return {
          x: points[i].x + (points[i + 1].x - points[i].x) * t,
          y: points[i].y + (points[i + 1].y - points[i].y) * t,
        };
      }
      accum += segLens[i];
    }
    return { x: points[points.length - 1].x, y: points[points.length - 1].y };
  }

  function updateArrowTransform() {
    if (!arrowsLayer) return;
    const state = Editor.getState();
    arrowsLayer.setAttribute('transform', `translate(${state.offsetX}, ${state.offsetY}) scale(${state.scale})`);
  }

  function updateArrowNumbers() {
    if (!arrowsLayer) return;
    const state = Editor.getState();
    arrowsLayer.querySelectorAll('.arrow-number').forEach(el => {
      el.setAttribute('font-size', 14 / state.scale);
    });
  }

  // ── Pan ──
  function initPan() {
    if (!cwrap) return;

    cwrap.addEventListener('mousedown', e => {
      if (e.button === 1) e.preventDefault();
    });

    cwrap.addEventListener('mousedown', e => {
      if (!isActive || e.button !== 1) return;
      e.preventDefault();
      const s = Editor.getState();
      panStart = { x: e.clientX, y: e.clientY };
      panOffStart = { x: s.offsetX, y: s.offsetY };
      cwrap.style.cursor = 'grabbing';
    });

    window.addEventListener('mousemove', e => {
      if (!isActive || !panStart) return;
      Editor.panTo(panOffStart.x + (e.clientX - panStart.x),
                   panOffStart.y + (e.clientY - panStart.y));
      if (typeof Pinner !== 'undefined') Pinner.updatePinPositions();
      updateArrowTransform();
      updateArrowNumbers();
    });

    window.addEventListener('mouseup', e => {
      if (e.button !== 1 || !panStart) return;
      panStart = null;
      if (cwrap) cwrap.style.cursor = '';
    });

    cwrap.addEventListener('wheel', () => {
      if (typeof Pinner !== 'undefined') Pinner.updatePinPositions();
      updateArrowTransform();
      updateArrowNumbers();
    }, { passive: true });
  }

  // ── Arrow read modal ──
  let arrowReadPanel = null;

  function openArrowReadModal(id) {
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    const arrow = (step.arrows || []).find(a => String(a.id) === String(id));
    if (!arrow) return;

    closeArrowReadModal();

    const panel = document.createElement('div');
    panel.id = 'vstep-arrow-read';
    panel.style.cssText = 'position:fixed;top:48px;left:0;bottom:0;width:280px;background:var(--panel);border-right:1px solid var(--border);display:flex;flex-direction:column;z-index:200;box-shadow:4px 0 16px rgba(0,0,0,.5)';

    let bodyHtml = '';
    if (arrow.checks && arrow.checks.length) {
      bodyHtml += '<div style="margin-bottom:12px;display:flex;flex-direction:column;gap:2px">';
      arrow.checks.forEach(c => {
        bodyHtml += '<div style="display:flex;align-items:center;gap:6px;padding:4px 6px;border-radius:3px;font-size:11px;font-family:\'IBM Plex Mono\',\'Courier New\',monospace;color:var(--text)">'
          + '<span style="font-size:13px;flex-shrink:0;opacity:' + (c.done ? '1' : '.5') + '">' + (c.done ? '☑' : '○') + '</span>'
          + '<span style="flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-decoration:' + (c.done ? 'line-through' : 'none') + ';opacity:' + (c.done ? '.5' : '1') + '">' + c.text.replace(/</g,'&lt;') + '</span>'
          + '</div>';
      });
      bodyHtml += '</div>';
    }
    if (arrow.checks && arrow.checks.length && arrow.text) {
      bodyHtml += '<div style="height:1px;background:var(--sep);margin:8px 0"></div>';
    }
    if (arrow.text) {
      bodyHtml += '<div style="font-family:\'IBM Plex Mono\',\'Courier New\',monospace;font-size:12px;color:var(--text-dim);line-height:1.75;white-space:pre-wrap">' + arrow.text.replace(/</g,'&lt;') + '</div>';
    }

    panel.innerHTML = `
      <div style="display:flex;align-items:center;justify-content:space-between;padding:12px 16px;border-bottom:1px solid var(--sep);flex-shrink:0">
        <span style="font-family:'IBM Plex Mono','Courier New',monospace;font-size:13px;font-weight:700;color:var(--text)">
          Arrow #${arrow.number}
        </span>
        <button id="vstep-arrow-close" style="background:none;border:none;color:var(--muted);font-size:18px;cursor:pointer;line-height:1;padding:2px 4px">&times;</button>
      </div>
      <div style="flex:1;overflow-y:auto;padding:16px">${bodyHtml}</div>`;

    document.body.appendChild(panel);
    arrowReadPanel = panel;

    document.getElementById('vstep-arrow-close').addEventListener('click', closeArrowReadModal);
  }

  function closeArrowReadModal() {
    if (arrowReadPanel) {
      arrowReadPanel.remove();
      arrowReadPanel = null;
    }
  }

  // ── Panel ──
  function buildPanel() {
    const panel = document.getElementById('pinner-panel');
    if (!panel) return;
    panel.innerHTML = `
      <div class="steps-panel">
        <div class="steps-header">
          <h3>Steps</h3>
        </div>
        <div class="steps-list" id="vsteps-list"></div>
        <div style="padding:8px 12px;border-top:1px solid var(--sep);flex-shrink:0">
          <button class="tbtn ${hideOthers ? 'active' : ''}" id="vstep-hide-others" style="width:100%;font-size:9px;padding:4px 8px">${hideOthers ? 'Show all' : 'Hide others'}</button>
        </div>
      </div>`;

    const list = document.getElementById('vsteps-list');
    if (list) {
      steps.forEach(step => {
        const item = document.createElement('div');
        item.className = 'step-item' + (step.id === activeStepId ? ' active' : '');
        item.textContent = step.name;
        item.dataset.id = step.id;
        item.addEventListener('click', () => selectStep(step.id));
        list.appendChild(item);
      });
    }

    document.getElementById('vstep-hide-others')?.addEventListener('click', () => {
      hideOthers = !hideOthers;
      applyStepVisibility();
      buildPanel();
    });
  }

  function selectStep(id) {
    activeStepId = id;
    closeArrowReadModal();
    applyStepVisibility();
    renderAllArrows();
    buildPanel();
  }

  function applyStepVisibility() {
    document.querySelectorAll('.pin-step-hidden').forEach(el => el.classList.remove('pin-step-hidden'));
    document.querySelectorAll('.vstep-pin-hidden').forEach(el => {
      el.classList.remove('vstep-pin-hidden');
      el.style.display = '';
    });
    if (!activeStepId) return;
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    document.querySelectorAll('.pin').forEach(el => {
      const inStep = step.shownPinIds.includes(el.dataset.id);
      if (!inStep) {
        el.classList.add('pin-step-hidden');
        if (hideOthers) {
          el.classList.add('vstep-pin-hidden');
          el.style.display = 'none';
        }
      }
    });
  }

  // ── Activate / Deactivate ──
  function activate() {
    isActive = true;
    document.getElementById('pinner-panel').classList.remove('hidden');
    if (typeof Pinner !== 'undefined') {
      Pinner.renderPins();
      Pinner.updatePinCount();
    }
    buildPanel();
    if (activeStepId) applyStepVisibility();
    renderAllArrows();
    window.dispatchEvent(new Event('resize'));
  }

  function deactivate() {
    isActive = false;
    hideOthers = false;
    closeArrowReadModal();
    document.getElementById('pinner-panel').classList.add('hidden');
    document.querySelectorAll('.pin-step-hidden').forEach(el => el.classList.remove('pin-step-hidden'));
    document.querySelectorAll('.vstep-pin-hidden').forEach(el => {
      el.classList.remove('vstep-pin-hidden');
      el.style.display = '';
    });
    document.querySelectorAll('#map-container .pin').forEach(el => el.remove());
    if (arrowsLayer) arrowsLayer.innerHTML = '';
  }

  // ── Data ──
  function loadData(data) {
    if (!data) { steps = []; activeStepId = null; return; }
    steps = data.steps || [];
    steps.forEach(s => { if (!s.arrows) s.arrows = []; });
    activeStepId = data.activeStepId || null;
  }

  function getData() {
    return { steps, activeStepId };
  }

  return {
    init, activate, deactivate,
    loadData, getData,
  };

})();

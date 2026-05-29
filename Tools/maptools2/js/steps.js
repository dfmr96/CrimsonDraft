const Steps = (() => {

  let steps = [];
  let activeStepId = null;
  let expandedSteps = new Set();
  let isActive = false;
  let mapContainer = null;
  let cwrap = null;
  let panStart = null, panOffStart = null;

  // Arrow state
  let arrowToolActive = false;
  let drawingArrow = null;
  let arrowMousePos = null;
  let arrowsSvg = null;
  let arrowsLayer = null;
  let editingArrowId = null;
  const NS = 'http://www.w3.org/2000/svg';
  const RAINBOW = ['#ff4444','#ff8c44','#ffd644','#44ff8c','#44b5ff','#8c44ff','#ff44b5'];
  const ARROW_SNAP = 12; // 0.25 * CELL (48)

  function init() {
    mapContainer = document.getElementById('map-container');
    cwrap = document.getElementById('cwrap');
    createArrowsOverlay();
    buildPanel();
    if (cwrap) {
      cwrap.addEventListener('click', handleStepsClick, true);
      cwrap.addEventListener('dblclick', handleStepsDblClick);
    }
    document.addEventListener('keydown', onStepsKeydown);
  }

  // ── SVG Overlay ──
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

    if (drawingArrow) appendArrowPreview();

    updateArrowTransform();
    updateArrowNumbers();
  }

  function appendArrowFinal(arrow) {
    const color = arrow.color || '#ff6b35';
    const g = document.createElementNS(NS, 'g');
    g.classList.add('arrow-final', 'arrow-group');
    g.dataset.arrowId = arrow.id;

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
    text.style.cssText = `fill:#fff;font-weight:bold;paint-order:stroke;stroke:#000;stroke-width:2.5;stroke-linecap:round;stroke-linejoin:round;pointer-events:none`;
    text.textContent = arrow.number;
    g.appendChild(text);

    arrowsLayer.appendChild(g);

    // Add a transparent wider path over the arrow for easier click hit-testing
    const hit = document.createElementNS(NS, 'path');
    hit.setAttribute('d', buildPolylineD(arrow.points));
    hit.setAttribute('vector-effect', 'non-scaling-stroke');
    hit.style.cssText = 'fill:none;stroke:transparent;stroke-width:12;pointer-events:stroke;cursor:pointer';
    g.insertBefore(hit, path);
  }

  function appendArrowPreview() {
    if (!drawingArrow || !drawingArrow.points.length) return;
    const g = document.createElementNS(NS, 'g');
    g.classList.add('arrow-preview');

    if (drawingArrow.points.length >= 2) {
      const path = document.createElementNS(NS, 'path');
      path.setAttribute('d', buildPolylineD(drawingArrow.points));
      path.setAttribute('vector-effect', 'non-scaling-stroke');
      path.classList.add('arrow-preview-path');
      g.appendChild(path);
    }

    if (arrowMousePos) {
      const last = drawingArrow.points[drawingArrow.points.length - 1];
      const ext = document.createElementNS(NS, 'path');
      ext.setAttribute('d', `M ${last.x} ${last.y} L ${arrowMousePos.x} ${arrowMousePos.y}`);
      ext.setAttribute('vector-effect', 'non-scaling-stroke');
      ext.classList.add('arrow-ext');
      g.appendChild(ext);
    }

    arrowsLayer.appendChild(g);
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

  // ── Pan ──
  function onPanDown(e) {
    if (e.button !== 1) return;
    e.preventDefault();
    const s = Editor.getState();
    panStart = { x: e.clientX, y: e.clientY };
    panOffStart = { x: s.offsetX, y: s.offsetY };
    if (cwrap) cwrap.style.cursor = 'grabbing';
  }

  function onPanMove(e) {
    if (!panStart) return;
    Editor.panTo(panOffStart.x + (e.clientX - panStart.x), panOffStart.y + (e.clientY - panStart.y));
    updateArrowTransform();
    updateArrowNumbers();
  }

  function onPanUp(e) {
    if (e.button !== 1 || !panStart) return;
    panStart = null;
    if (cwrap) cwrap.style.cursor = '';
  }

  function onStepsWheel() {
    updateArrowTransform();
    updateArrowNumbers();
  }

  function onStepsContextMenu(e) {
    if (!isActive || !arrowToolActive || !drawingArrow) return;
    e.preventDefault();
    if (drawingArrow.points.length >= 2) {
      finalizeArrow();
    } else {
      drawingArrow = null;
      arrowMousePos = null;
      renderAllArrows();
    }
  }

  // ── Click / Input handlers ──
  function handleStepsClick(e) {
    if (!isActive || !activeStepId) return;
    if (e.button !== 0) return;

    const arrowGroup = e.target.closest('.arrow-group');
    if (arrowGroup) {
      if (!arrowToolActive) {
        openArrowReadModal(arrowGroup.dataset.arrowId);
      }
      return;
    }

    const pinEl = e.target.closest('.pin');
    if (pinEl) {
      if (!arrowToolActive) {
        togglePinInStep(pinEl.dataset.id);
      }
      return;
    }

    if (arrowToolActive) {
      placeArrowPoint(e);
    }
  }

  function handleStepsDblClick(e) {
    if (!isActive || !activeStepId || !arrowToolActive || !drawingArrow) return;
    if (drawingArrow.points.length >= 2) {
      finalizeArrow();
      e.preventDefault();
    }
  }

  function onStepsKeydown(e) {
    if (!isActive || !arrowToolActive) return;
    if (e.key === 'Escape') {
      if (drawingArrow) {
        drawingArrow = null;
        arrowMousePos = null;
        renderAllArrows();
      } else {
        setArrowTool(false);
      }
      e.preventDefault();
    }
    if (e.key === 'Enter') {
      if (drawingArrow && drawingArrow.points.length >= 2) {
        finalizeArrow();
        e.preventDefault();
      }
    }
  }

  function onStepsMouseMove(e) {
    if (!isActive || !arrowToolActive) return;
    const canvas = document.getElementById('canvas');
    const rect = canvas.getBoundingClientRect();
    const state = Editor.getState();
    const snap = v => Math.round(v / ARROW_SNAP) * ARROW_SNAP;
    arrowMousePos = {
      x: snap((e.clientX - rect.left - state.offsetX) / state.scale),
      y: snap((e.clientY - rect.top - state.offsetY) / state.scale),
    };
    if (drawingArrow) {
      renderAllArrows();
    }
  }

  // ── Arrow Drawing ──
  function placeArrowPoint(e) {
    const canvas = document.getElementById('canvas');
    const rect = canvas.getBoundingClientRect();
    const cx = e.clientX - rect.left;
    const cy = e.clientY - rect.top;
    const state = Editor.getState();
    const snap = v => Math.round(v / ARROW_SNAP) * ARROW_SNAP;
    const wx = snap((cx - state.offsetX) / state.scale);
    const wy = snap((cy - state.offsetY) / state.scale);

    if (!drawingArrow) {
      drawingArrow = { points: [{ x: wx, y: wy }] };
    } else {
      drawingArrow.points.push({ x: wx, y: wy });
    }
    renderAllArrows();
  }

  function finalizeArrow() {
    if (!drawingArrow || drawingArrow.points.length < 2) {
      drawingArrow = null;
      arrowMousePos = null;
      renderAllArrows();
      return;
    }
    openArrowCreateModal(drawingArrow.points);
    drawingArrow = null;
    arrowMousePos = null;
  }

  // ── Arrow Create Modal ──
  let pendingChecks = [];

  function openArrowCreateModal(points) {
    closeArrowReadModal();
    pendingChecks = [];
    let modal = document.getElementById('arrow-create-modal');
    if (!modal) {
      modal = document.createElement('div');
      modal.id = 'arrow-create-modal';
      modal.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,.75);display:none;align-items:center;justify-content:center;z-index:300';
      modal.innerHTML = `
        <div class="pin-create-box" style="width:420px">
          <div class="pin-create-header">
            <span class="pin-create-title">New Arrow</span>
            <button class="pin-create-close" id="btn-arrow-create-close">&times;</button>
          </div>
          <div class="pin-create-body">
            <div class="pin-field">
              <label>Number</label>
              <input type="text" id="arrow-number-input" placeholder="e.g. 1, 2, 3..." style="width:100%;background:var(--bg);border:1px solid var(--border);border-radius:4px;padding:8px 11px;font-family:'IBM Plex Mono','Courier New',monospace;font-size:12px;color:var(--text);outline:none;transition:border-color .15s">
            </div>
            <div class="pin-field">
              <label>Checklist</label>
              <div style="display:flex;gap:4px;margin-bottom:6px">
                <input type="text" id="arrow-check-input" placeholder="Add item..." style="flex:1;background:var(--bg);border:1px solid var(--border);border-radius:4px;padding:6px 10px;font-family:'IBM Plex Mono','Courier New',monospace;font-size:11px;color:var(--text);outline:none">
                <button class="tbtn" id="btn-arrow-check-add" style="font-size:10px;padding:4px 8px">Add</button>
              </div>
              <div id="arrow-check-list" style="display:flex;flex-direction:column;gap:2px;max-height:140px;overflow-y:auto;margin-bottom:4px"></div>
            </div>
            <div class="pin-field">
              <label>Description</label>
              <textarea id="arrow-text-input" placeholder="Description..." style="width:100%;background:var(--bg);border:1px solid var(--border);border-radius:4px;padding:8px 11px;font-family:'IBM Plex Mono','Courier New',monospace;font-size:12px;color:var(--text);outline:none;resize:vertical;min-height:60px;transition:border-color .15s"></textarea>
            </div>
          </div>
          <div class="pin-create-footer">
            <button class="btn-cancel-pin" id="btn-arrow-create-cancel">Cancel</button>
            <button class="btn-confirm" id="btn-arrow-create-confirm">Confirm</button>
          </div>
        </div>`;
      document.body.appendChild(modal);

      modal.addEventListener('click', e => {
        if (e.target === modal) closeArrowCreateModal();
      });
      document.getElementById('btn-arrow-create-close').addEventListener('click', closeArrowCreateModal);
      document.getElementById('btn-arrow-create-cancel').addEventListener('click', closeArrowCreateModal);
      document.getElementById('btn-arrow-create-confirm').addEventListener('click', confirmArrow);
      document.getElementById('arrow-number-input').addEventListener('keydown', e => {
        if (e.key === 'Enter') { e.preventDefault(); confirmArrow(); }
        if (e.key === 'Escape') closeArrowCreateModal();
      });
      document.getElementById('btn-arrow-check-add').addEventListener('click', addCheckItem);
      document.getElementById('arrow-check-input').addEventListener('keydown', e => {
        if (e.key === 'Enter') { e.preventDefault(); addCheckItem(); }
      });
    }

    editingArrowId = null;
    document.getElementById('arrow-number-input').value = '';
    document.getElementById('arrow-text-input').value = '';
    document.getElementById('arrow-number-input').focus();
    renderCheckList();
    modal._pendingPoints = points;
    modal.classList.add('visible');
    modal.style.display = 'flex';
  }

  function addCheckItem() {
    const input = document.getElementById('arrow-check-input');
    const text = input.value.trim();
    if (!text) return;
    pendingChecks.push({ text, done: false });
    input.value = '';
    renderCheckList();
    input.focus();
  }

  function renderCheckList() {
    const container = document.getElementById('arrow-check-list');
    if (!container) return;
    container.innerHTML = '';
    pendingChecks.forEach((item, i) => {
      const row = document.createElement('div');
      row.style.cssText = 'display:flex;align-items:center;gap:6px;padding:3px 6px;border-radius:3px;font-size:11px;font-family:\'IBM Plex Mono\',\'Courier New\',monospace;color:var(--text);background:rgba(255,255,255,.03)';
      const icon = document.createElement('span');
      icon.textContent = '○';
      icon.style.cssText = 'font-size:12px;flex-shrink:0;opacity:.6';
      const label = document.createElement('span');
      label.textContent = item.text;
      label.style.cssText = 'flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap';
      const rm = document.createElement('button');
      rm.textContent = '✕';
      rm.style.cssText = 'background:none;border:none;color:var(--muted);cursor:pointer;font-size:10px;padding:0 2px';
      rm.addEventListener('click', () => { pendingChecks.splice(i, 1); renderCheckList(); });
      row.appendChild(icon);
      row.appendChild(label);
      row.appendChild(rm);
      container.appendChild(row);
    });
  }

  function closeArrowCreateModal() {
    const modal = document.getElementById('arrow-create-modal');
    if (modal) {
      modal.classList.remove('visible');
      modal.style.display = 'none';
    }
    editingArrowId = null;
  }

  function confirmArrow() {
    const modal = document.getElementById('arrow-create-modal');
    if (!modal) return;
    const points = modal._pendingPoints;
    const number = document.getElementById('arrow-number-input').value.trim() || (steps.reduce((max, s) => Math.max(max, (s.arrows || []).reduce((m, a) => Math.max(m, parseInt(a.number) || 0), 0)), 0) + 1).toString();
    const text = document.getElementById('arrow-text-input').value.trim();
    const checks = pendingChecks.filter(c => c.text.trim());

    const step = steps.find(s => s.id === activeStepId);
    if (!step) { closeArrowCreateModal(); return; }

    if (editingArrowId) {
      const idx = (step.arrows || []).findIndex(a => a.id === editingArrowId);
      if (idx >= 0) {
        step.arrows[idx].number = number;
        step.arrows[idx].text = text;
        step.arrows[idx].checks = checks;
      }
    } else {
      if (!step.arrows) step.arrows = [];
      const colorIdx = step.arrows.length % RAINBOW.length;
      step.arrows.push({
        id: 'arrow_' + Date.now() + '_' + Math.random().toString(36).slice(2, 6),
        points: points,
        number: number,
        text: text,
        color: RAINBOW[colorIdx],
        checks: checks,
      });
    }

    closeArrowCreateModal();
    renderAllArrows();
    App.markDirty();
  }

  // ── Arrow Read Panel (side panel) ──
  function openArrowReadModal(id) {
    closeArrowCreateModal();
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    const arrow = (step.arrows || []).find(a => a.id === id);
    if (!arrow) return;

    let panel = document.getElementById('arrow-read-panel');
    if (!panel) {
      panel = document.createElement('div');
      panel.id = 'arrow-read-panel';
      panel.innerHTML = `
        <div class="arrow-panel-header">
          <span class="arrow-panel-title" id="arrow-panel-title"></span>
          <button class="arrow-panel-close" id="btn-arrow-panel-close">&times;</button>
        </div>
        <div class="arrow-panel-body">
          <p class="arrow-panel-text" id="arrow-panel-text"></p>
        </div>
        <div class="arrow-panel-footer">
          <button class="tbtn" id="btn-arrow-panel-edit">Edit</button>
          <button class="tbtn danger" id="btn-arrow-panel-delete">Delete</button>
        </div>`;
      document.body.appendChild(panel);

      document.getElementById('btn-arrow-panel-close').addEventListener('click', closeArrowReadModal);
      document.getElementById('btn-arrow-panel-edit').addEventListener('click', () => {
        const aid = panel._currentArrowId;
        closeArrowReadModal();
        editArrow(aid);
      });
      document.getElementById('btn-arrow-panel-delete').addEventListener('click', () => {
        const aid = panel._currentArrowId;
        deleteArrow(aid);
        closeArrowReadModal();
      });
    }

    panel._currentArrowId = id;
    document.getElementById('arrow-panel-title').textContent = 'Arrow #' + arrow.number;

    const body = panel.querySelector('.arrow-panel-body');
    body.innerHTML = '';

    if (arrow.checks && arrow.checks.length) {
      const checkSection = document.createElement('div');
      checkSection.style.cssText = 'margin-bottom:12px;display:flex;flex-direction:column;gap:2px';
      arrow.checks.forEach((c, i) => {
        const row = document.createElement('div');
        row.style.cssText = 'display:flex;align-items:center;gap:6px;padding:4px 6px;border-radius:3px;cursor:pointer;font-size:11px;font-family:\'IBM Plex Mono\',\'Courier New\',monospace;color:var(--text);transition:background .1s';
        row.addEventListener('mouseenter', () => row.style.background = 'rgba(255,255,255,.04)');
        row.addEventListener('mouseleave', () => row.style.background = '');
        const cb = document.createElement('span');
        cb.textContent = c.done ? '☑' : '○';
        cb.style.cssText = 'font-size:13px;flex-shrink:0;opacity:' + (c.done ? '1' : '.5');
        const label = document.createElement('span');
        label.textContent = c.text;
        label.style.cssText = 'flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-decoration:' + (c.done ? 'line-through' : 'none') + ';opacity:' + (c.done ? '.5' : '1');
        row.appendChild(cb);
        row.appendChild(label);
        row.addEventListener('click', () => {
          arrow.checks[i].done = !arrow.checks[i].done;
          cb.textContent = arrow.checks[i].done ? '☑' : '○';
          cb.style.opacity = arrow.checks[i].done ? '1' : '.5';
          label.style.textDecoration = arrow.checks[i].done ? 'line-through' : 'none';
          label.style.opacity = arrow.checks[i].done ? '.5' : '1';
          App.markDirty();
        });
        checkSection.appendChild(row);
      });
      body.appendChild(checkSection);
    }

    if (arrow.checks && arrow.checks.length && arrow.text) {
      const sep = document.createElement('div');
      sep.style.cssText = 'height:1px;background:var(--sep);margin:8px 0';
      body.appendChild(sep);
    }

    if (arrow.text) {
      const textEl = document.createElement('p');
      textEl.className = 'arrow-panel-text';
      textEl.textContent = arrow.text;
      body.appendChild(textEl);
    }

    panel.style.display = 'flex';
  }

  function closeArrowReadModal() {
    const panel = document.getElementById('arrow-read-panel');
    if (panel) panel.style.display = 'none';
  }

  function editArrow(id) {
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    const arrow = (step.arrows || []).find(a => a.id === id);
    if (!arrow) return;

    editingArrowId = id;
    const modal = document.getElementById('arrow-create-modal');
    if (!modal) return;
    document.getElementById('arrow-number-input').value = arrow.number;
    document.getElementById('arrow-text-input').value = arrow.text || '';
    pendingChecks = (arrow.checks || []).map(c => ({ text: c.text, done: c.done }));
    renderCheckList();
    modal._pendingPoints = arrow.points;
    modal.classList.add('visible');
    modal.style.display = 'flex';
    document.getElementById('arrow-number-input').focus();
  }

  function deleteArrow(id) {
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    if (!step.arrows) return;
    step.arrows = step.arrows.filter(a => a.id !== id);
    renderAllArrows();
    App.markDirty();
  }

  // ── Panel ──
  function buildPanel() {
    const panel = document.getElementById('pinner-panel');
    if (!panel) return;
    panel.innerHTML = `
      <div class="steps-panel">
        <div class="steps-header">
          <h3>Steps</h3>
          <button class="tbtn" id="btn-add-step">+ Add Step</button>
        </div>
        <div class="steps-tools">
          <button class="tbtn ${arrowToolActive ? '' : 'active'}" id="btn-steps-select">Select</button>
          <button class="tbtn ${arrowToolActive ? 'active' : ''}" id="btn-steps-arrow">Arrow</button>
        </div>
        <div class="steps-list" id="steps-list"></div>
      </div>`;

    const list = document.getElementById('steps-list');
    if (list) {
      let dragId = null;
      steps.forEach(step => {
        const wrap = document.createElement('div');
        wrap.className = 'step-item-wrap' + (step.id === activeStepId ? ' active' : '');
        wrap.dataset.id = step.id;
        wrap.draggable = true;

        const header = document.createElement('div');
        header.className = 'step-item-header';

        const toggle = document.createElement('span');
        toggle.className = 'step-toggle';
        toggle.textContent = '▸';

        const titleInput = document.createElement('input');
        titleInput.className = 'step-title-input';
        titleInput.value = step.name;
        titleInput.addEventListener('input', () => {
          step.name = titleInput.value;
          App.markDirty();
        });

        const delBtn = document.createElement('button');
        delBtn.className = 'step-del-btn';
        delBtn.textContent = '✕';
        delBtn.title = 'Delete step';
        delBtn.addEventListener('click', e => {
          e.stopPropagation();
          deleteStep(step.id);
        });

        header.appendChild(toggle);
        header.appendChild(titleInput);
        header.appendChild(delBtn);

        toggle.addEventListener('click', e => {
          e.stopPropagation();
          const expanded = wrap.classList.toggle('expanded');
          toggle.textContent = expanded ? '▾' : '▸';
          if (expanded) expandedSteps.add(step.id); else expandedSteps.delete(step.id);
        });

        titleInput.addEventListener('click', e => {
          e.stopPropagation();
          selectStep(step.id, true);
        });

        // Drag and drop
        wrap.addEventListener('dragstart', e => {
          e.stopPropagation();
          dragId = step.id;
          wrap.classList.add('dragging');
          e.dataTransfer.effectAllowed = 'move';
        });
        wrap.addEventListener('dragend', e => {
          e.stopPropagation();
          wrap.classList.remove('dragging');
          list.querySelectorAll('.step-item-wrap').forEach(el => el.classList.remove('drag-over'));
          dragId = null;
        });
        wrap.addEventListener('dragover', e => {
          e.preventDefault();
          e.stopPropagation();
          if (step.id !== dragId) {
            list.querySelectorAll('.step-item-wrap').forEach(el => el.classList.remove('drag-over'));
            wrap.classList.add('drag-over');
          }
        });
        wrap.addEventListener('dragleave', e => {
          e.stopPropagation();
          wrap.classList.remove('drag-over');
        });
        wrap.addEventListener('drop', e => {
          e.preventDefault();
          e.stopPropagation();
          list.querySelectorAll('.step-item-wrap').forEach(el => el.classList.remove('drag-over'));
          if (!dragId || dragId === step.id) return;
          const fromIdx = steps.findIndex(s => s.id === dragId);
          const toIdx = steps.findIndex(s => s.id === step.id);
          if (fromIdx < 0 || toIdx < 0) return;
          const [moved] = steps.splice(fromIdx, 1);
          const insertAt = fromIdx < toIdx ? toIdx : toIdx;
          steps.splice(insertAt, 0, moved);
          buildPanel();
          App.markDirty();
        });

        const body = document.createElement('div');
        body.className = 'step-dropdown';

        const itemsList = document.createElement('div');
        itemsList.className = 'step-items-list';
        (step.items || []).forEach((itemData, i) => {
          const row = document.createElement('div');
          row.className = 'step-item-row';
          const label = document.createElement('span');
          label.textContent = itemData.text;
          const rm = document.createElement('button');
          rm.textContent = '✕';
          rm.className = 'step-item-rm';
          rm.addEventListener('click', e => {
            e.stopPropagation();
            step.items.splice(i, 1);
            buildPanel();
            App.markDirty();
          });
          row.appendChild(label);
          row.appendChild(rm);
          itemsList.appendChild(row);
        });

        const addRow = document.createElement('div');
        addRow.className = 'step-item-add';
        const addInput = document.createElement('input');
        addInput.type = 'text';
        addInput.placeholder = 'Add item...';
        addInput.className = 'step-item-add-input';
        const addBtn = document.createElement('button');
        addBtn.textContent = 'Add';
        addBtn.className = 'step-item-add-btn';
        addBtn.addEventListener('click', e => {
          e.stopPropagation();
          const text = addInput.value.trim();
          if (!text) return;
          if (!step.items) step.items = [];
          step.items.push({ text });
          buildPanel();
          App.markDirty();
        });
        addInput.addEventListener('keydown', e => {
          if (e.key === 'Enter') { e.preventDefault(); addBtn.click(); }
        });
        addRow.appendChild(addInput);
        addRow.appendChild(addBtn);

        body.appendChild(itemsList);
        body.appendChild(addRow);
        wrap.appendChild(header);
        wrap.appendChild(body);
        list.appendChild(wrap);
      });
    }

    document.querySelectorAll('.step-item-wrap').forEach(el => {
      if (expandedSteps.has(el.dataset.id)) {
        el.classList.add('expanded');
        el.querySelector('.step-toggle').textContent = '▾';
      }
    });

    document.getElementById('btn-add-step')?.addEventListener('click', addStep);
    document.getElementById('btn-steps-select')?.addEventListener('click', () => setArrowTool(false));
    document.getElementById('btn-steps-arrow')?.addEventListener('click', () => setArrowTool(true));
  }

  function deleteStep(id) {
    const step = steps.find(s => s.id === id);
    if (!step) return;
    if (!confirm('Delete "' + step.name + '" and all its arrows?')) return;
    const idx = steps.findIndex(s => s.id === id);
    if (idx < 0) return;
    if (steps[idx].id === activeStepId) {
      activeStepId = null;
    }
    steps.splice(idx, 1);
    if (!activeStepId && steps.length) activeStepId = steps[0].id;
    buildPanel();
    applyStepVisibility();
    renderAllArrows();
    App.markDirty();
  }

  function setArrowTool(active) {
    if (arrowToolActive === active) return;
    arrowToolActive = active;
    if (!active) {
      drawingArrow = null;
      arrowMousePos = null;
      renderAllArrows();
    }
    buildPanel();
  }

  function addStep() {
    const step = {
      id: 'step_' + Date.now(),
      name: 'Step ' + (steps.length + 1),
      shownPinIds: [],
      arrows: [],
      items: [],
    };
    steps.push(step);
    selectStep(step.id);
    buildPanel();
    App.markDirty();
  }

  function selectStep(id, skipPanel) {
    activeStepId = id;
    applyStepVisibility();
    renderAllArrows();
    if (skipPanel) {
      document.querySelectorAll('.step-item-wrap').forEach(el => {
        el.classList.toggle('active', el.dataset.id === id);
      });
    } else {
      buildPanel();
    }
  }

  function applyStepVisibility() {
    document.querySelectorAll('.pin-step-hidden').forEach(el => el.classList.remove('pin-step-hidden'));
    if (!activeStepId) return;
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    document.querySelectorAll('.pin').forEach(el => {
      const pinId = el.dataset.id;
      if (!step.shownPinIds.includes(pinId)) {
        el.classList.add('pin-step-hidden');
      }
    });
  }

  function togglePinInStep(pinId) {
    const step = steps.find(s => s.id === activeStepId);
    if (!step) return;
    const idx = step.shownPinIds.indexOf(pinId);
    if (idx >= 0) {
      step.shownPinIds.splice(idx, 1);
    } else {
      step.shownPinIds.push(pinId);
    }
    const el = document.querySelector(`.pin[data-id="${pinId}"]`);
    if (el) el.classList.toggle('pin-step-hidden');
    App.markDirty();
  }

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

    if (cwrap) {
      cwrap.addEventListener('mousedown', onPanDown);
      cwrap.addEventListener('wheel', onStepsWheel);
      window.addEventListener('mousemove', onPanMove);
      window.addEventListener('mouseup', onPanUp);
    }
    document.getElementById('canvas').addEventListener('mousemove', onStepsMouseMove);
    document.getElementById('canvas').addEventListener('contextmenu', onStepsContextMenu);

    // resize canvas to match the new cwrap size after panel opens
    window.dispatchEvent(new Event('resize'));
  }

  function deactivate() {
    isActive = false;
    arrowToolActive = false;
    drawingArrow = null;
    arrowMousePos = null;
    document.getElementById('pinner-panel').classList.add('hidden');

    document.querySelectorAll('.pin-step-hidden').forEach(el => el.classList.remove('pin-step-hidden'));
    document.querySelectorAll('#map-container .pin').forEach(el => el.remove());
    if (arrowsLayer) arrowsLayer.innerHTML = '';
    if (typeof Pinner !== 'undefined' && Pinner.clearPinElements) {
      Pinner.clearPinElements();
    }

    if (cwrap) {
      cwrap.removeEventListener('mousedown', onPanDown);
      cwrap.removeEventListener('wheel', onStepsWheel);
      window.removeEventListener('mousemove', onPanMove);
      window.removeEventListener('mouseup', onPanUp);
    }
    document.getElementById('canvas').removeEventListener('mousemove', onStepsMouseMove);
    document.getElementById('canvas').removeEventListener('contextmenu', onStepsContextMenu);
    closeArrowCreateModal();
    closeArrowReadModal();
    panStart = null;
  }

  function getData() {
    return { steps, activeStepId };
  }

  function loadData(data) {
    if (!data) { steps = []; activeStepId = null; return; }
    steps = data.steps || [];
    // Ensure each step has arrows array
    steps.forEach(s => {
      if (!s.arrows) s.arrows = [];
      if (!s.items) s.items = [];
    });
    activeStepId = data.activeStepId || null;
  }

  return {
    init, activate, deactivate,
    addStep, selectStep, togglePinInStep, applyStepVisibility,
    getData, loadData, renderAllArrows
  };

})();

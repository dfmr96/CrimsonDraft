// viewer-pinner.js — MapPinner en modo solo lectura

const Pinner = (() => {

  // ── PIN_TYPES y colores ──
  const GROUP_COLORS = {
    locations: '#74a8e0',
    loot:      '#c8b560',
    enemies:   '#c05050',
  };
  const PIN_TYPES = {
    save_zone:    { label:'Save Zone',    group:'locations', icon:'save_room'    },
    door:         { label:'Door',         group:'locations', icon:'door'         },
    transit:      { label:'Transit',      group:'locations', icon:'transit'      },
    environment:  { label:'Environment',  group:'locations', icon:'environment'  },
    interactable: { label:'Interactable', group:'locations', icon:'interactable' },
    item:         { label:'Item',         group:'loot',      icon:'item'         },
    key_item:     { label:'Key Item',     group:'loot',      icon:'key_item'     },
    note:         { label:'Note',         group:'loot',      icon:'note'         },
    boss:         { label:'Boss',         group:'enemies',   icon:'boss'         },
    consumed:     { label:'Consumed',     group:'enemies',   icon:'consumed'     },
  };
  Object.values(PIN_TYPES).forEach(d => { d.color = GROUP_COLORS[d.group]; });

  const PIN_GROUPS = [
    { key:'locations', label:'Locations' },
    { key:'loot',      label:'Loot'      },
    { key:'enemies',   label:'Enemies'   },
  ];

  let pins        = [];
  let hiddenTypes = new Set();
  let active      = false;
  let selectedPinId = null;

  let mapContainer, canvas;
  let panStart = null, panOffStart = null;

  // ── Init ──────────────────────────────────────────────────
  function init() {
    mapContainer = document.getElementById('map-container');
    canvas       = document.getElementById('canvas');
    buildFilters();
    initReadModal();
    initPanelToggle();
    initPan();
    initSearch();
  }

  // ── Pan con rueda del mouse y botón central ───────────────
  function initPan() {
    const cwrap = document.getElementById('cwrap');

    cwrap.addEventListener('mousedown', e => {
      if (e.button === 1) e.preventDefault();
    });

    cwrap.addEventListener('mousedown', e => {
      if (!active || e.button !== 1) return;
      e.preventDefault();
      const s = Editor.getState();
      panStart    = { x: e.clientX, y: e.clientY };
      panOffStart = { x: s.offsetX, y: s.offsetY };
      cwrap.style.cursor = 'grabbing';
    });

    window.addEventListener('mousemove', e => {
      if (!active || !panStart) return;
      Editor.panTo(panOffStart.x + (e.clientX - panStart.x),
                   panOffStart.y + (e.clientY - panStart.y));
    });

    window.addEventListener('mouseup', e => {
      if (e.button !== 1 || !panStart) return;
      panStart = null;
      cwrap.style.cursor = '';
      if (active) canvas.style.cursor = 'default';
    });
  }

  // ── Panel derecho colapsable ──────────────────────────────
  function initPanelToggle() {
    const panel   = document.getElementById('pinner-right');
    const showBtn = document.getElementById('btn-show-pr');

    function collapsePanel() {
      panel.style.display = 'none';
      if (showBtn) showBtn.style.display = 'block';
    }
    function expandPanel() {
      panel.style.display = 'flex';
      if (showBtn) showBtn.style.display = 'none';
    }
    document.getElementById('btn-toggle-pr')?.addEventListener('click', collapsePanel);
    showBtn?.addEventListener('click', expandPanel);
  }

  // ── Activate / Deactivate ────────────────────────────────
  function activate() {
    active = true;
    // cursor default — no crosshair porque no se puede colocar pins
    canvas.style.cursor = 'default';
    renderPins();
    updatePinCount();
    applyFilterVisibility();
    buildFilters();
  }

  function deactivate() {
    active = false;
    closeReadModal();
  }

  // ── Modal lectura (solo visualización) ───────────────────
  function openReadModal(id) {
    const pin = pins.find(p => String(p.id) === String(id));
    if (!pin) return;
    selectedPinId = id;

    const def = PIN_TYPES[pin.type] || { label: pin.type, color: '#888', icon: 'item' };

    const modalIcon = document.getElementById('modal-pin-icon');
    modalIcon.innerHTML = `<img src="/icons/${def.icon}.svg" width="28" height="28"
      style="filter:brightness(0) saturate(100%) invert(.82) sepia(.25)">`;
    modalIcon.style.color = def.color;

    document.getElementById('modal-pin-title').textContent = pin.title || def.label;
    document.getElementById('modal-pin-tag').textContent   = def.label;
    document.getElementById('modal-pin-desc').innerHTML    = pin.desc || '';

    // Imágenes
    const imgs = Array.isArray(pin.images) ? pin.images : (pin.image ? [pin.image] : []);
    [0, 1].forEach(i => {
      const el = document.getElementById('modal-pin-img-' + i);
      if (!el) return;
      if (imgs[i]) { el.src = '/images/' + imgs[i]; el.style.display = 'block'; }
      else         { el.src = ''; el.style.display = 'none'; }
    });

    // Requirements
    const reqSection = document.getElementById('modal-requirements');
    const reqList    = document.getElementById('modal-req-list');
    const reqs = (pin.requirements || [])
      .map(rid => pins.find(p => String(p.id) === String(rid)))
      .filter(Boolean);
    if (reqs.length > 0) {
      reqList.innerHTML = '';
      reqs.forEach(rp => {
        const rd   = PIN_TYPES[rp.type] || { icon:'item', color:'#888', label: rp.type };
        const chip = document.createElement('div');
        chip.className = 'modal-rel-chip';
        chip.innerHTML = `<img class="modal-rel-chip-icon" src="/icons/${rd.icon}.svg" width="16" height="16"
            style="opacity:.75;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
          <span class="modal-rel-chip-name">${rp.title || rd.label}</span>
          <span class="modal-rel-chip-arrow">→</span>`;
        chip.addEventListener('click', () => navigateToPin(rp.id));
        reqList.appendChild(chip);
      });
      reqSection.style.display = '';
    } else {
      reqSection.style.display = 'none';
    }

    // Used-in
    const usedSection = document.getElementById('modal-used-in');
    const usedList    = document.getElementById('modal-usedin-list');
    const usedIn = pins.filter(p =>
      (p.requirements || []).some(rid => String(rid) === String(id))
    );
    if (usedIn.length > 0) {
      usedList.innerHTML = '';
      usedIn.forEach(up => {
        const ud   = PIN_TYPES[up.type] || { icon:'item', color:'#888', label: up.type };
        const chip = document.createElement('div');
        chip.className = 'modal-rel-chip';
        chip.innerHTML = `<img class="modal-rel-chip-icon" src="/icons/${ud.icon}.svg" width="16" height="16"
            style="opacity:.75;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
          <span class="modal-rel-chip-name">${up.title || ud.label}</span>
          <span class="modal-rel-chip-arrow">→</span>`;
        chip.addEventListener('click', () => navigateToPin(up.id));
        usedList.appendChild(chip);
      });
      usedSection.style.display = '';
    } else {
      usedSection.style.display = 'none';
    }

    document.getElementById('pin-modal').classList.add('visible');

    // Sincronizar estado del botón hide
    const hideBtn = document.getElementById('btn-modal-hide');
    if (hideBtn) {
      hideBtn.textContent = pin.hidden ? 'Show' : 'Hide';
      hideBtn.classList.toggle('active', !!pin.hidden);
    }
  }

  function closeReadModal() {
    document.getElementById('pin-modal')?.classList.remove('visible');
    selectedPinId = null;
  }

  function initReadModal() {
    document.getElementById('btn-modal-close')?.addEventListener('click', closeReadModal);
    document.getElementById('btn-modal-hide')?.addEventListener('click', () => {
      const pin = pins.find(p => String(p.id) === String(selectedPinId));
      if (!pin) return;
      pin.hidden = !pin.hidden;
      const btn = document.getElementById('btn-modal-hide');
      btn.textContent = pin.hidden ? 'Show' : 'Hide';
      btn.classList.toggle('active', pin.hidden);
      // Fade in/out directo sobre el elemento existente (sin recrear el DOM)
      const el = pinElements.get(String(pin.id));
      if (el) el.classList.toggle('pin-hidden', pin.hidden);
      buildFilters();
    });
    document.getElementById('pin-modal')?.addEventListener('click', e => {
      if (e.target === document.getElementById('pin-modal')) closeReadModal();
    });
  }

  // ── Navegación a pin desde chip de relaciones ─────────────
  function navigateToPin(targetId) {
    const pin = pins.find(p => String(p.id) === String(targetId));
    if (!pin) return;
    closeReadModal();
    const state = Editor.getState();
    const ww = canvas.clientWidth;
    const wh = canvas.clientHeight;
    Editor.panTo(ww / 2 - pin.x * state.scale, wh / 2 - pin.y * state.scale);
    setTimeout(() => {
      highlightPin(targetId);
      openReadModal(targetId);
    }, 320);
  }

  function highlightPin(id) {
    const el = mapContainer.querySelector(`.pin[data-id="${id}"]`);
    if (!el) return;
    el.classList.remove('pin-highlighted');
    void el.offsetWidth;
    el.classList.add('pin-highlighted');
    setTimeout(() => el.classList.remove('pin-highlighted'), 1500);
  }

  // ── Render pins ───────────────────────────────────────────
  function pinSVG(type) {
    const def = PIN_TYPES[type] || PIN_TYPES.item;
    return `<svg width="44" height="60" viewBox="0 0 44 60" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M22 0C9.85 0 0 9.85 0 22c0 16.5 22 38 22 38S44 38.5 44 22C44 9.85 34.15 0 22 0z"
        fill="${def.color}" fill-opacity=".92"/>
      <path d="M22 0C9.85 0 0 9.85 0 22c0 16.5 22 38 22 38S44 38.5 44 22C44 9.85 34.15 0 22 0z"
        fill="none" stroke="rgba(0,0,0,0.35)" stroke-width="1.5"/>
      <image href="/icons/${def.icon}.svg" x="9" y="7" width="26" height="26"
        style="filter:brightness(0) saturate(100%) invert(1)" opacity="0.88"/>
    </svg>`;
  }

  // Map id→elemento DOM para lookup O(1)
  let pinElements = new Map();

  function renderPins() {
    mapContainer.querySelectorAll('.pin').forEach(p => p.remove());
    pinElements.clear();
    const state = Editor.getState();
    const frag = document.createDocumentFragment();
    pins.forEach(pin => {
      const el = document.createElement('div');
      el.className   = 'pin' + (pin.hidden ? ' pin-hidden' : '');
      el.dataset.id  = pin.id;
      el.innerHTML   = pinSVG(pin.type);
      el.style.cssText = 'position:absolute;left:0;top:0;transform-origin:bottom center;will-change:transform';
      const tx = pin.x * state.scale + state.offsetX;
      const ty = pin.y * state.scale + state.offsetY;
      el.style.transform = `translate(calc(${tx}px - 50%), calc(${ty}px - 100%))`;
      const tooltip = document.createElement('div');
      tooltip.className   = 'pin-tooltip';
      tooltip.textContent = pin.title || (PIN_TYPES[pin.type] || {}).label || '';
      el.appendChild(tooltip);
      el.addEventListener('click', e => { e.stopPropagation(); openReadModal(pin.id); });
      pinElements.set(String(pin.id), el);
      frag.appendChild(el);
    });
    mapContainer.appendChild(frag);
  }

  function updatePinPositions() {
    const state = Editor.getState();
    pins.forEach(pin => {
      const el = pinElements.get(String(pin.id));
      if (!el) return;
      const tx = pin.x * state.scale + state.offsetX;
      const ty = pin.y * state.scale + state.offsetY;
      el.style.transform = `translate(calc(${tx}px - 50%), calc(${ty}px - 100%))`;
    });
  }

  function updatePinCount() {
    const el = document.getElementById('pin-count');
    if (el) el.textContent = `${pins.length} pin${pins.length !== 1 ? 's' : ''}`;
  }

  // ── Filtros ───────────────────────────────────────────────
  function buildFilters() {
    const container = document.getElementById('filters-categories');
    if (!container) return;
    container.innerHTML = PIN_GROUPS.map(group => {
      const typesInGroup = Object.entries(PIN_TYPES).filter(([, d]) => d.group === group.key);
      const groupColor   = GROUP_COLORS[group.key];
      const rows = typesInGroup.map(([key, def]) => {
        // En viewer no contamos pins ocultos
        const count    = pins.filter(p => p.type === key && !p.hidden).length;
        const isHidden = hiddenTypes.has(key);
        return `<div class="filter-cat-row ${isHidden ? 'hidden-cat' : ''}" data-type="${key}"
            style="--cat-color:${groupColor}">
          <img class="filter-cat-icon" src="/icons/${def.icon}.svg" width="14" height="14"
            style="opacity:.75;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
          <span class="filter-cat-label">${def.label}</span>
          <span class="filter-cat-count">${count}</span>
        </div>`;
      }).join('');
      return `<div class="filter-group">
        <div class="filter-group-header" style="--group-color:${groupColor}">
          <span class="filter-group-dot"></span>
          <span class="filter-group-label">${group.label}</span>
        </div>
        <div class="filter-group-rows">${rows}</div>
      </div>`;
    }).join('');

    container.querySelectorAll('.filter-cat-row').forEach(row => {
      row.addEventListener('click', () => {
        const type = row.dataset.type;
        if (hiddenTypes.has(type)) hiddenTypes.delete(type);
        else hiddenTypes.add(type);
        applyFilterVisibility();
        buildFilters();
      });
    });
  }

  function applyFilterVisibility() {
    mapContainer.querySelectorAll('.pin').forEach(el => {
      const pin = pins.find(p => String(p.id) === el.dataset.id);
      if (!pin) return;
      el.classList.toggle('pin-filter-hidden', hiddenTypes.has(pin.type));
    });
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('btn-filter-all')?.addEventListener('click', () => {
      hiddenTypes.clear(); applyFilterVisibility(); buildFilters();
    });
    document.getElementById('btn-filter-none')?.addEventListener('click', () => {
      Object.keys(PIN_TYPES).forEach(k => hiddenTypes.add(k));
      applyFilterVisibility(); buildFilters();
    });
  });

  // ── Búsqueda ──────────────────────────────────────────────
  function initSearch() {
    const input   = document.getElementById('pin-search-input');
    const results = document.getElementById('search-results');
    const clear   = document.getElementById('pin-search-clear');
    if (!input) return;

    input.addEventListener('input', () => {
      const q = input.value.trim().toLowerCase();
      clear.style.display = q ? 'flex' : 'none';
      if (!q) { results.style.display = 'none'; results.innerHTML = ''; return; }
      runSearch(q);
    });

    clear.addEventListener('click', () => {
      input.value = '';
      clear.style.display = 'none';
      results.style.display = 'none';
      results.innerHTML = '';
      input.focus();
    });

    // Cerrar resultados al hacer click fuera
    document.addEventListener('click', e => {
      if (!e.target.closest('#pin-search-input') && !e.target.closest('#search-results')) {
        results.style.display = 'none';
      }
    });
    input.addEventListener('focus', () => {
      if (input.value.trim()) results.style.display = 'flex';
    });
  }

  function runSearch(q) {
    const results = document.getElementById('search-results');

    // Buscar en título, etiqueta de tipo y descripción (stripeando HTML)
    const strip = html => html ? html.replace(/<[^>]+>/g,'') : '';
    const matched = pins.filter(pin => {
      if (pin.hidden) return false;
      const def   = PIN_TYPES[pin.type] || {};
      const haystack = [
        pin.title       || '',
        def.label       || '',
        strip(pin.desc) || '',
      ].join(' ').toLowerCase();
      return haystack.includes(q);
    });

    if (matched.length === 0) {
      results.innerHTML = '<div class="search-empty">No results</div>';
      results.style.display = 'flex';
      return;
    }

    results.innerHTML = '';
    matched.slice(0, 12).forEach(pin => {
      const def  = PIN_TYPES[pin.type] || { icon:'item', color:'#888', label: pin.type };
      const item = document.createElement('div');
      item.className = 'search-result-item';

      // Resaltar la coincidencia en el título
      const title = pin.title || def.label;
      const idx   = title.toLowerCase().indexOf(q);
      let titleHtml;
      if (idx >= 0) {
        titleHtml = escapeHtml(title.slice(0, idx))
          + `<mark>${escapeHtml(title.slice(idx, idx + q.length))}</mark>`
          + escapeHtml(title.slice(idx + q.length));
      } else {
        titleHtml = escapeHtml(title);
      }

      item.innerHTML = `
        <img src="/icons/${def.icon}.svg" width="14" height="14"
          style="opacity:.7;filter:brightness(0) saturate(100%) invert(.8) sepia(.2);flex-shrink:0">
        <div class="search-result-text">
          <span class="search-result-name">${titleHtml}</span>
          <span class="search-result-type">${def.label}</span>
        </div>`;

      item.addEventListener('click', () => {
        results.style.display = 'none';
        navigateToPin(pin.id);
      });
      results.appendChild(item);
    });

    if (matched.length > 12) {
      const more = document.createElement('div');
      more.className = 'search-more';
      more.textContent = `+${matched.length - 12} more`;
      results.appendChild(more);
    }

    results.style.display = 'flex';
  }

  function escapeHtml(s) {
    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
  }

  // ── Serialización (load only en viewer) ──────────────────
  function loadData(data) {
    pins = Array.isArray(data) ? data : [];
    hiddenTypes = new Set();
  }

  function getData() { return pins; }

  return {
    init, activate, deactivate,
    renderPins, updatePinPositions, updatePinCount,
    buildFilters, highlightPin,
    getData, loadData,
  };

})();

// pinner.js — lógica del MapPinner

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
  Object.values(PIN_TYPES).forEach(d=>{ d.color=GROUP_COLORS[d.group]; });
  const PIN_GROUPS = [
    {key:'locations',label:'Locations'},
    {key:'loot',label:'Loot'},
    {key:'enemies',label:'Enemies'},
  ];

  let pins = [];
  let activeType = null;
  let selectedPinId = null;
  let editingPinId = null;
  let pendingPos = null;
  let hiddenTypes = new Set();
  let active = false;
  let pendingRequirements = new Set();
  let pendingImageRemoved = [false, false]; // por slot

  let mapContainer, canvas;
  let panStart = null, panOffStart = null;

  // ── Init ──
  function init(){
    mapContainer = document.getElementById('map-container');
    canvas       = document.getElementById('canvas');
    buildFilters();
    initReadModal();
    initCreateModal();
    initPanelToggle();
    initPan();
  }

  function initPan(){
    const cwrap = document.getElementById('cwrap');

    cwrap.addEventListener('mousedown', e=>{
      if(e.button === 1) e.preventDefault();
    });

    cwrap.addEventListener('mousedown', e=>{
      if(!active || e.button !== 1) return;
      e.preventDefault();
      const s = Editor.getState();
      panStart    = { x: e.clientX, y: e.clientY };
      panOffStart = { x: s.offsetX, y: s.offsetY };
      cwrap.style.cursor = 'grabbing';
    });

    window.addEventListener('mousemove', e=>{
      if(!active || !panStart) return;
      const dx = e.clientX - panStart.x;
      const dy = e.clientY - panStart.y;
      Editor.panTo(panOffStart.x + dx, panOffStart.y + dy);
    });

    window.addEventListener('mouseup', e=>{
      if(e.button !== 1 || !panStart) return;
      panStart = null;
      cwrap.style.cursor = '';
      if(active) canvas.style.cursor = 'crosshair';
    });
  }

  // ── Panel derecho colapsable ──
  function initPanelToggle(){
    const panel   = document.getElementById('pinner-right');
    const showBtn = document.getElementById('btn-show-pr');
    function collapsePanel(){
      panel.style.display = 'none';
      if(showBtn) showBtn.style.display = 'block';
      window.dispatchEvent(new Event('resize'));
    }
    function expandPanel(){
      panel.style.display = 'flex';
      if(showBtn) showBtn.style.display = 'none';
      window.dispatchEvent(new Event('resize'));
    }
    document.getElementById('btn-toggle-pr')?.addEventListener('click', collapsePanel);
    showBtn?.addEventListener('click', expandPanel);
  }

  // ── Activate / Deactivate ──
  function activate(){
    active = true;
    canvas.style.cursor = 'crosshair';
    canvas.addEventListener('contextmenu', onCanvasRightClick);
    renderPins();
    updatePinCount();
    applyFilterVisibility();
    buildFilters();
    const _pr = document.getElementById('pinner-right');
    const _sp = document.getElementById('btn-show-pr');
    if(_pr) _pr.style.display = 'flex';
    if(_sp) _sp.style.display = 'none';

    window.dispatchEvent(new Event('resize'));
  }

  function deactivate(){
    active = false;
    canvas.removeEventListener('contextmenu', onCanvasRightClick);
    closeCreateModal();
    closeReadModal();
  }

  function onCanvasRightClick(e){
    e.preventDefault();
    if(!active) return;
    const state = Editor.getState();
    const rect  = canvas.getBoundingClientRect();
    const cx = e.clientX - rect.left;
    const cy = e.clientY - rect.top;
    pendingPos = {
      x: (cx - state.offsetX) / state.scale,
      y: (cy - state.offsetY) / state.scale,
    };
    editingPinId = null;
    openCreateModal();
  }

  // ── Modal creación/edición ──
  function buildCreateTypeGrid(){
    const grid = document.getElementById('create-type-grid');
    if(!grid) return;
    grid.innerHTML = '';
    Object.entries(PIN_TYPES).forEach(([key,def])=>{
      const btn = document.createElement('div');
      btn.className = 'type-btn';
      btn.dataset.type = key;
      btn.style.setProperty('--type-color', def.color);
      btn.innerHTML = `<img class="type-icon-img" src="/icons/${def.icon}.svg" width="20" height="20" style="opacity:.8;filter:brightness(0) saturate(100%) invert(.75) sepia(.3)"><span class="type-label">${def.label}</span>`;
      btn.addEventListener('click',()=>{
        activeType = key;
        grid.querySelectorAll('.type-btn').forEach(b=>b.classList.remove('active'));
        btn.classList.add('active');
      });
      grid.appendChild(btn);
    });
  }

  function openCreateModal(){
    const modal = document.getElementById('pin-create-modal');
    if(!modal) return;
    buildCreateTypeGrid();
    pendingImageRemoved = [false, false];
    [0,1].forEach(i=>{
      const inp = document.getElementById('pin-img-input-'+i);
      if(inp) inp.value='';
    });

    if(editingPinId){
      const pin = pins.find(p=>p.id===editingPinId);
      if(pin){
        document.getElementById('pin-title-input').value = pin.title||'';
        document.getElementById('pin-desc-input').value  = pin.desc||'';
        activeType = pin.type;
        document.getElementById('create-type-grid').querySelectorAll('.type-btn').forEach(b=>{
          b.classList.toggle('active', b.dataset.type===pin.type);
        });
        setUnlocksToggle(!!pin.unlocks);
        pendingRequirements = new Set(pin.requirements||[]);
        // Cargar imagenes en slots
        const imgs = Array.isArray(pin.images) ? pin.images : (pin.image ? [pin.image, null] : [null, null]);
        [0,1].forEach(i=>setSlotPreview(i, imgs[i]));
      }
      document.getElementById('pin-create-title').textContent = 'Editar pin';
    } else {
      document.getElementById('pin-title-input').value = '';
      document.getElementById('pin-desc-input').value  = '';
      activeType = activeType || 'save_zone';
      document.getElementById('create-type-grid').querySelectorAll('.type-btn').forEach(b=>{
        b.classList.toggle('active', b.dataset.type===activeType);
      });
      document.getElementById('pin-create-title').textContent = 'Nuevo pin';
      setUnlocksToggle(false);
      pendingRequirements = new Set();
      [0,1].forEach(i=>setSlotPreview(i, null));
    }
    renderRequirementsList();
    modal.classList.add('visible');
    setTimeout(()=>document.getElementById('pin-title-input').focus(), 100);
  }

  function closeCreateModal(){
    document.getElementById('pin-create-modal')?.classList.remove('visible');
    pendingPos   = null;
    editingPinId = null;
    pendingRequirements = new Set();
  }

  function setSlotPreview(slot, filename){
    const preview = document.getElementById('pin-img-preview-'+slot);
    const remove  = document.getElementById('pin-img-remove-'+slot);
    const ph      = document.getElementById('pin-img-ph-'+slot);
    if(!preview) return;
    if(filename){
      preview.src = '/images/'+filename;
      preview.style.display = 'block';
      if(remove) remove.style.display = 'inline-flex';
      if(ph) ph.style.display = 'none';
    } else {
      preview.src = '';
      preview.style.display = 'none';
      if(remove) remove.style.display = 'none';
      if(ph) ph.style.display = 'flex';
    }
  }

  function initImgSlot(slot){
    const input   = document.getElementById('pin-img-input-'+slot);
    const zone    = document.getElementById('pin-img-zone-'+slot);
    const remove  = document.getElementById('pin-img-remove-'+slot);
    const preview = document.getElementById('pin-img-preview-'+slot);
    const ph      = document.getElementById('pin-img-ph-'+slot);

    if(!input || !zone) return;

    zone.addEventListener('click', ()=> input.click());

    input.addEventListener('change', e=>{
      const file = e.target.files[0];
      if(!file) return;
      const url = URL.createObjectURL(file);
      if(preview){ preview.src=url; preview.style.display='block'; }
      if(remove) remove.style.display='inline-flex';
      if(ph) ph.style.display='none';
      pendingImageRemoved[slot] = false;
    });

    if(remove){
      remove.addEventListener('click', e=>{
        e.stopPropagation();
        if(preview){ preview.src=''; preview.style.display='none'; }
        if(input) input.value='';
        remove.style.display='none';
        if(ph) ph.style.display='flex';
        pendingImageRemoved[slot] = true;
      });
    }

    zone.addEventListener('dragover', e=>{ e.preventDefault(); zone.classList.add('dragover'); });
    zone.addEventListener('dragleave', ()=> zone.classList.remove('dragover'));
    zone.addEventListener('drop', e=>{
      e.preventDefault(); zone.classList.remove('dragover');
      const file = e.dataTransfer.files[0];
      if(!file || !file.type.startsWith('image/')) return;
      const dt = new DataTransfer(); dt.items.add(file);
      input.files = dt.files;
      if(preview){ preview.src=URL.createObjectURL(file); preview.style.display='block'; }
      if(remove) remove.style.display='inline-flex';
      if(ph) ph.style.display='none';
      pendingImageRemoved[slot] = false;
    });
  }

  function initCreateModal(){
    document.getElementById('btn-create-confirm')?.addEventListener('click', confirmPin);
    document.getElementById('btn-create-cancel')?.addEventListener('click', closeCreateModal);
    document.getElementById('btn-create-close')?.addEventListener('click', closeCreateModal);
    document.querySelectorAll('.pin-fmt-btn').forEach(btn=>{
      btn.addEventListener('click', ()=>{
        const ta = document.getElementById('pin-desc-input');
        const start = ta.selectionStart, end = ta.selectionEnd;
        if(start===undefined || end===undefined || start===end) return;
        const tag = btn.dataset.fmt;
        const text = ta.value;
        ta.value = text.slice(0,start) + `<${tag}>` + text.slice(start,end) + `</${tag}>` + text.slice(end);
        ta.focus();
        ta.selectionStart = ta.selectionEnd = end + 2*tag.length + 5;
      });
    });
    document.getElementById('pin-title-input')?.addEventListener('keydown', e=>{
      if(e.key==='Enter'){ e.preventDefault(); confirmPin(); }
      if(e.key==='Escape') closeCreateModal();
    });
    document.getElementById('pin-create-modal')?.addEventListener('click', e=>{
      if(e.target===document.getElementById('pin-create-modal')) closeCreateModal();
    });
    document.getElementById('btn-toggle-unlocks')?.addEventListener('click', ()=>{
      setUnlocksToggle(!getUnlocksToggle());
    });
    initImgSlot(0);
    initImgSlot(1);
  }

  // ── Unlocks / Requirements ──
  function setUnlocksToggle(val){
    const btn = document.getElementById('btn-toggle-unlocks');
    if(!btn) return;
    btn.setAttribute('aria-pressed', val?'true':'false');
    btn.textContent = val ? 'ON' : 'OFF';
  }

  function getUnlocksToggle(){
    return document.getElementById('btn-toggle-unlocks')?.getAttribute('aria-pressed') === 'true';
  }

  function renderRequirementsList(){
    const container = document.getElementById('req-list');
    if(!container) return;
    const candidates = pins.filter(p=>p.unlocks && String(p.id)!==String(editingPinId));
    if(candidates.length===0){
      container.innerHTML = '<div class="req-list-empty">No pins with "Unlocks something" enabled.</div>';
      return;
    }
    container.innerHTML = '';
    candidates.forEach(p=>{
      const def = PIN_TYPES[p.type]||{icon:'?',label:p.type};
      const selected = pendingRequirements.has(String(p.id));
      const item = document.createElement('div');
      item.className = 'req-item' + (selected?' selected':'');
      item.innerHTML = `
        <div class="req-item-check">${selected?'✓':''}</div>
        <img class="req-item-icon" src="/icons/${def.icon}.svg" width="14" height="14" style="opacity:.7;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
        <span class="req-item-name">${p.title||def.label}</span>
        <span class="req-item-type">${def.label}</span>`;
      item.addEventListener('click',()=>{
        const sid = String(p.id);
        if(pendingRequirements.has(sid)) pendingRequirements.delete(sid);
        else pendingRequirements.add(sid);
        renderRequirementsList();
      });
      container.appendChild(item);
    });
  }

  async function confirmPin(){
    const type  = activeType || 'save_zone';
    const def   = PIN_TYPES[type] || PIN_TYPES.save_zone;
    const title = document.getElementById('pin-title-input').value.trim() || def.label;
    const desc  = document.getElementById('pin-desc-input').value.trim();
    const unlocks = getUnlocksToggle();
    const requirements = [...pendingRequirements];

    // Recoger archivos de los 2 slots
    const files = [
      document.getElementById('pin-img-input-0')?.files?.[0] || null,
      document.getElementById('pin-img-input-1')?.files?.[0] || null,
    ];

    async function processSlot(slot, currentImage){
      if(files[slot]){
        if(currentImage) await deleteImage(currentImage);
        return await uploadImage(files[slot], Date.now() + slot);
      } else if(pendingImageRemoved[slot]){
        if(currentImage) await deleteImage(currentImage);
        return null;
      }
      return currentImage || null;
    }

    if(editingPinId){
      const pin = pins.find(p=>p.id===editingPinId);
      if(pin){
        pin.type=type; pin.title=title; pin.desc=desc;
        pin.unlocks=unlocks; pin.requirements=requirements;
        const imgs = Array.isArray(pin.images) ? [...pin.images] : (pin.image ? [pin.image, null] : [null, null]);
        imgs[0] = await processSlot(0, imgs[0]);
        imgs[1] = await processSlot(1, imgs[1]);
        pin.images = imgs;
        delete pin.image; // migrar campo viejo
      }
    } else {
      if(!pendingPos) return;
      const id = Date.now() + Math.random();
      const img0 = files[0] ? await uploadImage(files[0], id) : null;
      const img1 = files[1] ? await uploadImage(files[1], id + 1) : null;
      pins.push({ id, type, x:pendingPos.x, y:pendingPos.y, title, desc, hidden:false, unlocks, requirements, images:[img0, img1] });
    }
    pendingImageRemoved = [false, false];
    closeCreateModal();
    renderPins();
    updatePinCount();
    buildFilters();
    applyFilterVisibility();
    App.markDirty();
  }

  async function uploadImage(file, pinId){
    const ext  = (file.name.split('.').pop()||'jpg').toLowerCase().replace(/[^a-z0-9]/g,'');
    const name = 'pin_' + String(pinId).replace('.','_') + '_' + Date.now() + '.' + ext;
    try {
      const res = await fetch('/api/images/' + name, {
        method:'POST', headers:{'Content-Type': file.type||'application/octet-stream'}, body:file
      });
      const data = await res.json();
      return data.ok ? name : null;
    } catch(e){ console.error('Error subiendo imagen:', e); return null; }
  }

  async function deleteImage(name){
    if(!name) return;
    try { await fetch('/api/images-delete/' + name, {method:'POST'}); } catch(e){}
  }

  // ── Render pins ──
  function pinSVG(type){
    const def = PIN_TYPES[type] || PIN_TYPES.item;
    const iconFile = def.icon;
    // Circulo de fondo con el color del grupo, icono SVG centrado
    return `<svg width="44" height="60" viewBox="0 0 44 60" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M22 0C9.85 0 0 9.85 0 22c0 16.5 22 38 22 38S44 38.5 44 22C44 9.85 34.15 0 22 0z" fill="${def.color}" fill-opacity=".92"/>
      <path d="M22 0C9.85 0 0 9.85 0 22c0 16.5 22 38 22 38S44 38.5 44 22C44 9.85 34.15 0 22 0z" fill="none" stroke="rgba(0,0,0,0.35)" stroke-width="1.5"/>
      <image href="/icons/${iconFile}.svg" x="9" y="7" width="26" height="26"
        style="filter:brightness(0) saturate(100%) invert(1)" opacity="0.88"/>
    </svg>`;
  }

  // Map id→elemento DOM para lookup O(1) en updatePinPositions
  let pinElements = new Map();

  function renderPins(){
    mapContainer.querySelectorAll('.pin').forEach(p=>p.remove());
    pinElements.clear();
    const state = Editor.getState();
    // Fragment para un solo reflow al insertar todos los pins
    const frag = document.createDocumentFragment();
    pins.forEach(pin=>{
      const el = document.createElement('div');
      el.className = 'pin' + (pin.hidden?' pin-hidden':'');
      el.dataset.id = pin.id;
      el.innerHTML  = pinSVG(pin.type);
      // transform en lugar de left/top — no causa reflow, va a GPU
      el.style.cssText = 'position:absolute;left:0;top:0;transform-origin:bottom center;will-change:transform';
      const tx = pin.x * state.scale + state.offsetX;
      const ty = pin.y * state.scale + state.offsetY;
      el.style.transform = `translate(calc(${tx}px - 50%), calc(${ty}px - 100%))`;
      const tooltip = document.createElement('div');
      tooltip.className   = 'pin-tooltip';
      tooltip.textContent = pin.title || (PIN_TYPES[pin.type]||{}).label || '';
      el.appendChild(tooltip);
      el.addEventListener('click', e=>{ e.stopPropagation(); openReadModal(pin.id); });
      pinElements.set(String(pin.id), el);
      frag.appendChild(el);
    });
    mapContainer.appendChild(frag);
  }

  function updatePinPositions(){
    const state = Editor.getState();
    // Lookup O(1) por id, sin querySelectorAll ni find()
    pins.forEach(pin=>{
      const el = pinElements.get(String(pin.id));
      if(!el) return;
      const tx = pin.x * state.scale + state.offsetX;
      const ty = pin.y * state.scale + state.offsetY;
      el.style.transform = `translate(calc(${tx}px - 50%), calc(${ty}px - 100%))`;
    });
  }

  function updatePinCount(){
    const el = document.getElementById('pin-count');
    if(el) el.textContent = `${pins.length} pin${pins.length!==1?'s':''}`;
  }

  // ── Filtros ──
  function buildFilters(){
    const container = document.getElementById('filters-categories');
    if(!container) return;
    container.innerHTML = PIN_GROUPS.map(group=>{
      const typesInGroup = Object.entries(PIN_TYPES).filter(([,d])=>d.group===group.key);
      const groupColor   = GROUP_COLORS[group.key];
      const rows = typesInGroup.map(([key,def])=>{
        const count   = pins.filter(p=>p.type===key).length;
        const isHidden = hiddenTypes.has(key);
        return `<div class="filter-cat-row ${isHidden?'hidden-cat':''}" data-type="${key}" style="--cat-color:${groupColor}">
          <img class="filter-cat-icon" src="/icons/${def.icon}.svg" width="14" height="14" style="opacity:.75;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
          <span class="filter-cat-label">${def.label}</span>
          <span class="filter-cat-count">${count}</span>
        </div>`;
      }).join('');
      return `<div class="filter-group">
        <div class="filter-group-header" style="--group-color:${groupColor}">
          <span class="filter-group-dot"></span>
          <span class="filter-group-label">${group.label}</span>
        </div>
        <div class="filter-group-rows">${rows}</div></div>`;
    }).join('');
    container.querySelectorAll('.filter-cat-row').forEach(row=>{
      row.addEventListener('click',()=>{
        const type=row.dataset.type;
        if(hiddenTypes.has(type)) hiddenTypes.delete(type); else hiddenTypes.add(type);
        applyFilterVisibility(); buildFilters();
      });
    });
  }

  function applyFilterVisibility(){
    mapContainer.querySelectorAll('.pin').forEach(el=>{
      const pin=pins.find(p=>String(p.id)===el.dataset.id);
      if(!pin)return;
      el.classList.toggle('pin-filter-hidden', hiddenTypes.has(pin.type));
    });
  }

  document.addEventListener('DOMContentLoaded',()=>{
    document.getElementById('btn-filter-all')?.addEventListener('click',()=>{ hiddenTypes.clear(); applyFilterVisibility(); buildFilters(); });
    document.getElementById('btn-filter-none')?.addEventListener('click',()=>{ Object.keys(PIN_TYPES).forEach(k=>hiddenTypes.add(k)); applyFilterVisibility(); buildFilters(); });
  });

  // ── Navegación a pin desde modal ──
  function navigateToPin(targetId){
    const pin = pins.find(p=>String(p.id)===String(targetId));
    if(!pin) return;
    closeReadModal();
    const state = Editor.getState();
    const ww = canvas.clientWidth;
    const wh = canvas.clientHeight;
    const newX = ww/2 - pin.x * state.scale;
    const newY = wh/2 - pin.y * state.scale;
    Editor.panTo(newX, newY);
    setTimeout(()=>{
      const el = mapContainer.querySelector(`.pin[data-id="${targetId}"]`);
      if(el){
        el.classList.remove('pin-highlighted');
        void el.offsetWidth;
        el.classList.add('pin-highlighted');
        setTimeout(()=>el.classList.remove('pin-highlighted'), 1500);
      }
      openReadModal(targetId);
    }, 320);
  }

  function highlightPin(id){
    const el = mapContainer.querySelector(`.pin[data-id="${id}"]`);
    if(!el) return;
    el.classList.remove('pin-highlighted');
    void el.offsetWidth;
    el.classList.add('pin-highlighted');
    setTimeout(()=>el.classList.remove('pin-highlighted'), 1500);
  }

  // ── Modal lectura ──
  function openReadModal(id){
    const pin = pins.find(p=>String(p.id)===String(id));
    if(!pin) return;
    selectedPinId = id;
    const def = PIN_TYPES[pin.type]||{label:pin.type,color:'#888',icon:'?'};
    const modalIcon = document.getElementById('modal-pin-icon'); modalIcon.innerHTML = `<img src="/icons/${def.icon}.svg" width="28" height="28" style="filter:brightness(0) saturate(100%) invert(.82) sepia(.25)">`;  modalIcon.style.color = def.color;
    document.getElementById('modal-pin-icon').style.color  = def.color;
    document.getElementById('modal-pin-title').textContent = pin.title||def.label;
    document.getElementById('modal-pin-tag').textContent   = def.label;
    document.getElementById('modal-pin-desc').innerHTML  = pin.desc||'';

    // Imagenes: mostrar las que tenga (hasta 2)
    const imgs = Array.isArray(pin.images) ? pin.images : (pin.image ? [pin.image] : []);
    [0,1].forEach(i=>{
      const el = document.getElementById('modal-pin-img-'+i);
      if(!el) return;
      if(imgs[i]){ el.src='/images/'+imgs[i]; el.style.display='block'; }
      else { el.src=''; el.style.display='none'; }
    });
    document.getElementById('btn-modal-hide').textContent  = pin.hidden?'Show':'Hide';
    document.getElementById('btn-modal-hide').classList.toggle('active', pin.hidden);

    // Requirements
    const reqSection = document.getElementById('modal-requirements');
    const reqList    = document.getElementById('modal-req-list');
    const reqs = (pin.requirements||[]).map(rid=>pins.find(p=>String(p.id)===String(rid))).filter(Boolean);
    if(reqs.length>0){
      reqList.innerHTML = '';
      reqs.forEach(rp=>{
        const rd = PIN_TYPES[rp.type]||{icon:'?',color:'#888',label:rp.type};
        const chip = document.createElement('div');
        chip.className = 'modal-rel-chip';
        chip.innerHTML = `<img class="modal-rel-chip-icon" src="/icons/${rd.icon}.svg" width="16" height="16" style="opacity:.75;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
          <span class="modal-rel-chip-name">${rp.title||rd.label}</span>
          <span class="modal-rel-chip-arrow">→</span>`;
        chip.addEventListener('click',()=>navigateToPin(rp.id));
        reqList.appendChild(chip);
      });
      reqSection.style.display = '';
    } else { reqSection.style.display = 'none'; }

    // Used-in
    const usedSection = document.getElementById('modal-used-in');
    const usedList    = document.getElementById('modal-usedin-list');
    const usedIn = pins.filter(p=>(p.requirements||[]).some(rid=>String(rid)===String(id)));
    if(usedIn.length>0){
      usedList.innerHTML = '';
      usedIn.forEach(up=>{
        const ud = PIN_TYPES[up.type]||{icon:'?',color:'#888',label:up.type};
        const chip = document.createElement('div');
        chip.className = 'modal-rel-chip';
        chip.innerHTML = `<img class="modal-rel-chip-icon" src="/icons/${ud.icon}.svg" width="16" height="16" style="opacity:.75;filter:brightness(0) saturate(100%) invert(.8) sepia(.2)">
          <span class="modal-rel-chip-name">${up.title||ud.label}</span>
          <span class="modal-rel-chip-arrow">→</span>`;
        chip.addEventListener('click',()=>navigateToPin(up.id));
        usedList.appendChild(chip);
      });
      usedSection.style.display = '';
    } else { usedSection.style.display = 'none'; }

    document.getElementById('pin-modal').classList.add('visible');
  }

  function closeReadModal(){
    document.getElementById('pin-modal')?.classList.remove('visible');
    selectedPinId = null;
  }

  function initReadModal(){
    document.getElementById('btn-modal-close')?.addEventListener('click', closeReadModal);
    document.getElementById('btn-modal-edit')?.addEventListener('click',()=>{
      if(!selectedPinId) return;
      editingPinId = selectedPinId;
      pendingPos   = null;
      closeReadModal();
      openCreateModal();
    });
    document.getElementById('btn-modal-delete')?.addEventListener('click',()=>{
      if(!selectedPinId) return;
      pins = pins.filter(p=>String(p.id)!==String(selectedPinId));
      closeReadModal(); renderPins(); updatePinCount(); buildFilters(); App.markDirty();
    });
    document.getElementById('btn-modal-hide')?.addEventListener('click',()=>{
      const pin=pins.find(p=>String(p.id)===String(selectedPinId));
      if(!pin)return;
      pin.hidden=!pin.hidden;
      document.getElementById('btn-modal-hide').textContent=pin.hidden?'Show':'Hide';
      document.getElementById('btn-modal-hide').classList.toggle('active',pin.hidden);
      renderPins(); applyFilterVisibility(); App.markDirty();
    });
    document.getElementById('pin-modal')?.addEventListener('click',e=>{
      if(e.target===document.getElementById('pin-modal')) closeReadModal();
    });
  }

  // ── Serialización ──
  function getData(){ return pins; }
  function loadData(data){ pins = Array.isArray(data)?data:[]; hiddenTypes=new Set(); }
  function clearPinElements(){ pinElements.clear(); }

  return {
    init, activate, deactivate,
    renderPins, updatePinPositions, updatePinCount,
    buildFilters, highlightPin,
    getData, loadData, clearPinElements,
  };

})();
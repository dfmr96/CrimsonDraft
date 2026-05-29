// editor.js — lógica del MapEditor 2D

const Editor = (() => {

  const CELL = MapRender.CELL;

  const PALETTE = {
    wall:  ['#1e1a19','#3a3430','#5a5248','#8a7a6a','#c8c2b4'],
    floor: ['#4d4b42','#533b32','#3a3830','#2a2826','#5e5648','#6a4a3a'],
    door:  ['#28265e','#6f1e1c','#3a6bb5','#c9a020','#3e6e3e'],
    stair: ['#1e1a19','#3a3430','#2a2826','#4a4640'],
    object: ['#4d2627','#7a3a3c','#2d4a3a','#3a5a6a','#5a4a3a'],
  };
  const CUSTOM      = { wall:'#1e1a19', floor:'#4d4b42', door:'#28265e', stair:'#1e1a19', ddoor:'#28265e', object:'#4d2627' };
  const activeColor = { wall:'#1e1a19', floor:'#4d4b42', door:'#28265e', stair:'#1e1a19', ddoor:'#28265e', object:'#4d2627' };

  // State
  let canvas, ctx, cwrap;
  let canvasW=0, canvasH=0;
  let offsetX=0, offsetY=0, scale=1;
  let tool='select';
  let objects=[], selectedId=null;
  let undoStack=[], redoStack=[];
  let refImage=null, refOpacity=0.4, refX=0, refY=0, refScale=1;
  let refDragging=false, refDragStart={x:0,y:0}, refPosStart={x:0,y:0};
  let bgColor='#1e1e1e', gridColor='#2e2e2e';
  let viewMode='normal';
  let linePoints=null, floorStart=null, objectPoints=null;
  let isPanning=false, panStart={x:0,y:0}, panOffStart={x:0,y:0};
  let dragging=false, dragStart=null;
  let movingId=null, movingOrigin=null;
  let mouseWorld={x:0,y:0};
  let doorOrient='h', ddoorOrient='h', ddoorArrow='fwd', stairOrient='h', stairDir='right';
  let doorHasArrow=false;
  let _uid=0;
  let active=false; // true cuando el modo editor está activo

  function uid(){ return 'o'+(++_uid); }
  function snap(v,s){ return Math.round(v/s)*s; }
  function screenToWorld(sx,sy){ return {x:(sx-offsetX)/(scale*CELL), y:(sy-offsetY)/(scale*CELL)}; }

  // ── Init ─────────────────────────────────────────────────
  function init(){
    canvas = document.getElementById('canvas');
    ctx    = canvas.getContext('2d');
    cwrap  = document.getElementById('cwrap');

    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();

    canvas.addEventListener('mousemove', onMove);
    canvas.addEventListener('mousedown', onDown);
    canvas.addEventListener('mouseup',   onUp);
    canvas.addEventListener('dblclick',  onDbl);
    cwrap.addEventListener('wheel',      onWheel, {passive:false});
    canvas.addEventListener('contextmenu', e=>{ e.preventDefault(); onRightClick(); });
    document.addEventListener('keydown', onKeydown);

    buildToolbarListeners();
    buildDropdownListeners();
    initViewModeButtons();
    initRefImageUpload();
    initViewsPanel();
    updateLayers();
    redraw();
  }

  function resizeCanvas(){
    const rect = cwrap.getBoundingClientRect();
    canvasW = Math.floor(rect.width);
    canvasH = Math.floor(rect.height);
    if (canvasW <= 0 || canvasH <= 0) { canvasW = window.innerWidth; canvasH = window.innerHeight; }
    canvas.width  = canvasW;
    canvas.height = canvasH;
    canvas.style.width  = canvasW + 'px';
    canvas.style.height = canvasH + 'px';
    redraw();
  }

  // ── Activate / Deactivate ────────────────────────────────
  function activate(){
    active = true;
    cwrap.className = '';
    applyToolCursor();
    resizeCanvas();
    // Remove pin elements so they don't block canvas in editor mode
    document.querySelectorAll('#map-container .pin').forEach(el => el.remove());
    if (typeof Pinner !== 'undefined' && Pinner.clearPinElements) {
      Pinner.clearPinElements();
    }
    redraw();
  }

  function deactivate(){
    active = false;
    // cancelar operaciones en curso
    if(movingId) cancelMove();
    linePoints = null;
    floorStart = null;
  }

  // ── Draw ─────────────────────────────────────────────────
  function redraw(){
    MapRender.renderToCanvas(canvas, { objects, bgColor, gridColor }, {
      sc: scale, ox: offsetX, oy: offsetY,
      showGrid: viewMode === 'blueprint' || (document.getElementById('grid-vis')?.checked ?? false),
      noShadow: false,
      selectedId, movingId,
      viewMode
    });

    // Imagen de referencia
    if(refImage){
      const c  = ctx;
      const sc = scale;
      const iw = refImage.width  * refScale * sc * CELL / 100;
      const ih = refImage.height * refScale * sc * CELL / 100;
      const sx = refX * sc * CELL + offsetX;
      const sy = refY * sc * CELL + offsetY;
      c.save();
      c.globalAlpha = refOpacity;
      c.drawImage(refImage, sx, sy, iw, ih);
      c.globalAlpha = 1;
      if(tool === 'ref'){
        c.strokeStyle='rgba(124,106,240,.9)'; c.lineWidth=1.5; c.setLineDash([5,3]);
        c.strokeRect(sx,sy,iw,ih); c.setLineDash([]);
        [[0,0],[1,0],[1,1],[0,1]].forEach(([hx,hy])=>{
          c.fillStyle='rgba(124,106,240,.9)';
          c.beginPath(); c.arc(sx+hx*iw, sy+hy*ih, 5, 0, Math.PI*2); c.fill();
        });
      }
      c.restore();
    }

    // Overlays interactivos (solo en modo editor activo)
    if(active){
      const c=ctx, sc=scale, ox=offsetX, oy=offsetY;
      if(tool==='line'  && linePoints && linePoints.length>0) drawLinePreview(c,sc,ox,oy);
      if(tool==='floor' && floorStart) drawFloorPreview(c,sc,ox,oy);
      if(tool==='door')  drawDoorPreview(c,sc,ox,oy);
      if(tool==='ddoor') drawDDoorPreview(c,sc,ox,oy);
      if(tool==='stair') drawStairPreview(c,sc,ox,oy);
      if(tool==='label') drawLabelPreview(c,sc,ox,oy);
      if(tool==='object') drawObjectPreview(c,sc,ox,oy);
      if(movingId) drawMovingHint(c,sc,ox,oy);
    }
  }

  // ── View mode ──────────────────────────────────────────
  function setViewMode(mode){
    viewMode = mode;
    document.querySelectorAll('.view-mode-btn').forEach(b => b.classList.remove('active'));
    document.getElementById('view-mode-'+mode)?.classList.add('active');
    redraw();
  }
  function getViewMode(){ return viewMode; }

  // ── Views (state snapshots) ────────────────────────────
  let views = [];
  let activeViewId = null;

  function initViewsPanel(){
    document.getElementById('btn-new-view')?.addEventListener('click', createViewDialog);
    buildViewsPanel();
  }

  function buildViewsPanel(){
    const list = document.getElementById('views-list');
    if (!list) return;
    list.innerHTML = '';
    views.forEach((v,i) => {
      const item = document.createElement('div');
      item.className = 'view-item' + (v.id === activeViewId ? ' active' : '');
      const icon = document.createElement('img');
      icon.className = 'view-icon';
      icon.src = v.iconSvg || '/icons/tools/door.svg';
      icon.width = 20; icon.height = 20;
      const name = document.createElement('span');
      name.className = 'view-name';
      name.textContent = v.name;
      const del = document.createElement('button');
      del.className = 'view-del-btn';
      del.textContent = '×';
      del.onclick = e => { e.stopPropagation(); deleteView(v.id); };
      item.appendChild(icon);
      item.appendChild(name);
      item.appendChild(del);
      item.onclick = () => activateView(v.id);
      list.appendChild(item);
    });
  }

  function createViewDialog(){
    const name = prompt('View name:');
    if (!name) return;
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.svg';
    input.onchange = function(e){
      const file = e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = function(ev){
        createView(name, ev.target.result);
      };
      reader.readAsDataURL(file);
    };
    input.click();
  }

  function createView(name, iconSvg){
    const snap = JSON.parse(JSON.stringify(objects));
    views.push({ id:'vw'+Date.now(), name, iconSvg, objects: snap, doorColor: activeColor.door, ddoorColor: activeColor.ddoor });
    activeViewId = views[views.length-1].id;
    buildViewsPanel();
    App.markDirty();
  }

  function activateView(id){
    const v = views.find(v => v.id === id);
    if (!v) return;
    activeViewId = id;
    pushUndo();
    objects = JSON.parse(JSON.stringify(v.objects));
    activeColor.door  = v.doorColor;
    activeColor.ddoor = v.ddoorColor;
    selectedId = null; movingId = null;
    // Sync door swatches
    document.querySelectorAll('#door-swatches .swatch').forEach(el => el.classList.toggle('selected', el.style.background===activeColor.door));
    document.querySelectorAll('#door-swatches .swatch-custom').forEach(el => { el.style.background=activeColor.door; el.querySelector('input').value=activeColor.door; });
    document.querySelectorAll('#ddoor-swatches .swatch').forEach(el => el.classList.toggle('selected', el.style.background===activeColor.ddoor));
    document.querySelectorAll('#ddoor-swatches .swatch-custom').forEach(el => { el.style.background=activeColor.ddoor; el.querySelector('input').value=activeColor.ddoor; });
    buildViewsPanel();
    updateLayers();
    redraw();
    App.markDirty();
  }

  function deleteView(id){
    const v = views.find(v => v.id === id);
    if (!v || !confirm('Delete view "'+v.name+'"?')) return;
    views = views.filter(x => x.id !== id);
    if (activeViewId === id) activeViewId = views.length ? views[views.length-1].id : null;
    buildViewsPanel();
    App.markDirty();
  }

  function getActiveViewDoorColors(){
    const v = views.find(v => v.id === activeViewId);
    return v ? { door: v.doorColor, ddoor: v.ddoorColor } : null;
  }

  // ── Tool management ──────────────────────────────────────
  function setTool(t){
    linePoints=null; floorStart=null; objectPoints=null;
    tool = t;
    // Si el checkbox Arrow está marcado al entrar a door, usar ddoor
    if (t === 'door') {
      const ac = document.getElementById('door-arrow-check');
      if (ac && ac.checked) tool = 'ddoor';
    }
    const refSec=document.getElementById('ref-tool-sec'); if(refSec) refSec.style.display=(t==='ref')?'block':'none';
    // Activar boton de toolbar icon
    document.querySelectorAll('.tool-icon-btn').forEach(b=>b.classList.remove('active'));
    document.getElementById('tbtn-'+t)?.classList.add('active');
    // Mostrar panel de opciones correspondiente
    document.querySelectorAll('.tool-opts-panel').forEach(p=>p.style.display='none');
    const panelMap = {line:'opts-wall', floor:'opts-floor', door:'opts-door', ddoor:'opts-door', stair:'opts-stair', label:'opts-label', object:'opts-object'};
    const panelId = panelMap[t];
    if(panelId) document.getElementById(panelId).style.display='flex';
    applyToolCursor();
    const hints={
      select:'Click to select · Drag to move · Del to delete',
      pan:'Drag to pan · Scroll to zoom',
      ref:'Drag to move the image · Scroll to scale · Right-click to exit',
      line:'Click for points · Double-click or Right-click/Esc to finish',
      door:'Click to place door',
      ddoor:'Click to place locked door',
      floor:'Click and drag to draw the room',
      stair:'Click to place stairs',
      label:'Click to place text label',
      object:'Click to trace · Double-click or close at start to finish'
    };
    document.getElementById('hint').textContent = hints[t]||'';
    redraw();
  }

  function applyToolCursor(){
    cwrap.className='';
    if(tool==='pan')    cwrap.classList.add('tool-pan');
    if(tool==='select') cwrap.classList.add('tool-select');
    if(tool==='ref')    cwrap.classList.add('tool-pan');
  }

  function setDoorOrient(o){
    doorOrient=o; ddoorOrient=o;
    document.querySelectorAll('.door-orient-btn').forEach(b=>b.classList.toggle('active', b.dataset.val===o));
    redraw();
  }
  function setDDoorOrient(o){ setDoorOrient(o); }
  function setDoorArrowEnabled(v){
    doorHasArrow = v;
    const row = document.getElementById('door-arrow-row');
    if(row) row.style.display = v ? 'flex' : 'none';
    const dcr = document.getElementById('ddoor-color-row');
    if(dcr) dcr.style.display = v ? 'flex' : 'none';
    const ds = document.getElementById('ddoor-swatches');
    if(ds) ds.style.display = v ? 'flex' : 'none';
    redraw();
  }
  function setDDoorArrow(a){
    ddoorArrow=a;
    document.querySelectorAll('.door-arrow-btn').forEach(b=>b.classList.toggle('active', b.dataset.val===a));
    redraw();
  }
  function setStairOrient(o){
    stairOrient=o;
    document.querySelectorAll('.stair-orient-btn').forEach(b=>b.classList.toggle('active', b.dataset.val===o));
    redraw();
  }
  function setStairDir(d){
    stairDir=d;
    document.querySelectorAll('.stair-dir-btn').forEach(b=>b.classList.toggle('active', b.dataset.val===d));
    redraw();
  }

  // ── Events ───────────────────────────────────────────────
  function canvasPos(e){ const r=canvas.getBoundingClientRect(); return {x:e.clientX-r.left, y:e.clientY-r.top}; }

  function onMove(e){
    if(!active) return;
    const pos = canvasPos(e);
    mouseWorld = screenToWorld(pos.x, pos.y);
    const s = parseFloat(document.getElementById('snap-size').value)||0.5;
    document.getElementById('coords').textContent = `${snap(mouseWorld.x,s).toFixed(2)} m, ${snap(mouseWorld.y,s).toFixed(2)} m`;

    if(isPanning){ offsetX=panOffStart.x+(pos.x-panStart.x); offsetY=panOffStart.y+(pos.y-panStart.y); redraw(); if(typeof Pinner!=='undefined')Pinner.updatePinPositions(); return; }
    if(refDragging&&tool==='ref'){ refX=refPosStart.x+(pos.x-refDragStart.x)/(scale*CELL); refY=refPosStart.y+(pos.y-refDragStart.y)/(scale*CELL); redraw(); return; }
    if(movingId){
      const obj=objects.find(o=>o.id===movingId);
      if(obj) applyMovingPos(obj, snap(mouseWorld.x,s), snap(mouseWorld.y,s));
      updateLayers(); redraw(); return;
    }
    if(dragging&&selectedId){
      const obj=objects.find(o=>o.id===selectedId);
      if(obj){ moveDelta(obj, mouseWorld.x-dragStart.x, mouseWorld.y-dragStart.y, s); dragStart={...mouseWorld}; }
    }
    redraw();
  }

  function onDown(e){
    if(!active) return;
    if(e.button===1){ startPan(e); return; }
    if(e.button===2) return;
    const pos = canvasPos(e);
    mouseWorld = screenToWorld(pos.x, pos.y);
    if(tool==='pan'){ startPan(e); return; }
    if(tool==='ref'){ refDragging=true; refDragStart={x:pos.x,y:pos.y}; refPosStart={x:refX,y:refY}; cwrap.classList.add('panning'); return; }

    const s  = parseFloat(document.getElementById('snap-size').value)||0.5;
    const sx = snap(mouseWorld.x,s), sy=snap(mouseWorld.y,s);

    if(tool==='select'){
      if(movingId){ confirmMove(); return; }
      // If an object is already selected (via hierarchy), use it for dragging directly
      if(selectedId){
        dragging=true; dragStart={...mouseWorld};
        updateLayers(); redraw(); return;
      }
      const hit=hitTest(mouseWorld.x, mouseWorld.y);
      selectedId = hit ? hit.id : null;
      if(hit){ dragging=true; dragStart={...mouseWorld}; }
      updateLayers(); redraw(); return;
    }
    if(tool==='object'){
      const s=0.25;
      const wx=snap(mouseWorld.x,s), wy=snap(mouseWorld.y,s);
      if(!objectPoints){ objectPoints=[{x:wx,y:wy}]; redraw(); return; }
      const first=objectPoints[0];
      const distPx=Math.hypot((wx-first.x)*scale*CELL,(wy-first.y)*scale*CELL);
      if(objectPoints.length>=3 && distPx<12){
        commitObject(); return;
      }
      objectPoints.push({x:wx,y:wy}); redraw(); return;
    }
    if(tool==='line'){
      if(!linePoints) linePoints=[{x:sx,y:sy}];
      else linePoints.push({x:sx,y:sy});
      redraw(); return;
    }
    if(tool==='floor'){ if(!floorStart) floorStart={x:sx,y:sy}; redraw(); return; }
    if(tool==='door'){
      pushUndo();
      const size=parseFloat(document.getElementById('door-size-val').value)||1;
      objects.push({id:uid(),type:'door',x:snap(mouseWorld.x,.25),y:snap(mouseWorld.y,.25),size,orient:doorOrient,color:activeColor.door});
      updateLayers(); redraw(); App.markDirty(); return;
    }
    if(tool==='ddoor'){
      pushUndo();
      const size=parseFloat(document.getElementById('door-size-val').value)||1;
      objects.push({id:uid(),type:'ddoor',x:snap(mouseWorld.x,.25),y:snap(mouseWorld.y,.25),size,orient:ddoorOrient,arrow:ddoorArrow,color:activeColor.ddoor});
      updateLayers(); redraw(); App.markDirty(); return;
    }
    if(tool==='stair'){
      pushUndo();
      const sw=parseFloat(document.getElementById('stair-w-val').value)||2;
      const sh=parseFloat(document.getElementById('stair-h-val').value)||2;
      objects.push({id:uid(),type:'stair',x:sx,y:sy,w:sw,h:sh,color:activeColor.stair,orient:stairOrient});
      updateLayers(); redraw(); App.markDirty(); return;
    }
    if(tool==='label'){
      pushUndo();
      const text=document.getElementById('label-text').value||'Label';
      const size=parseFloat(document.getElementById('label-size').value)||14;
      const color=document.getElementById('label-color').value;
      objects.push({id:uid(),type:'label',x:mouseWorld.x,y:mouseWorld.y,text,size,color});
      updateLayers(); redraw(); App.markDirty(); return;
    }
  }

  function onUp(e){
    if(!active) return;
    if(refDragging){ refDragging=false; cwrap.classList.remove('panning'); redraw(); return; }
    if(dragging){ dragging=false; App.markDirty(); redraw(); return; }
    if(isPanning){ isPanning=false; cwrap.classList.remove('panning'); return; }
    const pos=canvasPos(e);
    mouseWorld=screenToWorld(pos.x,pos.y);
    const s=parseFloat(document.getElementById('snap-size').value)||0.5;
    const sx=snap(mouseWorld.x,s), sy=snap(mouseWorld.y,s);
    if(tool==='floor'&&floorStart){
      const x1=snap(floorStart.x,s), y1=snap(floorStart.y,s);
      if(Math.abs(sx-x1)>0.01||Math.abs(sy-y1)>0.01){
        pushUndo();
        const bc=document.getElementById('floor-border-color').value;
        const hb=document.getElementById('floor-border').checked;
        objects.push({id:uid(),type:'floor',x:Math.min(x1,sx),y:Math.min(y1,sy),w:Math.abs(sx-x1),h:Math.abs(sy-y1),color:activeColor.floor,border:hb,borderColor:bc});
        updateLayers(); App.markDirty();
      }
      floorStart=null; redraw();
    }
  }

  function onDbl(e){
    if(!active) return;
    if(tool==='object'&&objectPoints){ commitObject(); return; }
    if(tool==='line'&&linePoints&&linePoints.length>=2){ commitLineInProgress(); return; }
    if(movingId){ confirmMove(); return; }
    if(tool==='select'&&selectedId){
      const obj=objects.find(o=>o.id===selectedId);
      if(obj){ pushUndo(); movingId=selectedId; movingOrigin=JSON.stringify(obj); document.getElementById('hint').textContent='Move with mouse · Double-click/Enter to confirm · Esc to cancel'; }
      return;
    }
  }

  function onRightClick(){
    if(!active) return;
    if(movingId){ cancelMove(); return; }
    if(tool==='ref'){ setTool('select'); return; }
    commitLineInProgress();
    setTool('select');
  }

  function onKeydown(e){
    if(!active) return;
    const tag=document.activeElement.tagName;
    const typing=tag==='INPUT'||tag==='TEXTAREA'||tag==='SELECT';
    if((e.ctrlKey||e.metaKey)&&e.key==='z'){ e.preventDefault(); undoAction(); }
    if((e.ctrlKey||e.metaKey)&&e.key==='y'){ e.preventDefault(); redoAction(); }
    if(e.key==='Escape'){ if(movingId){cancelMove();return;} if(objectPoints){objectPoints=null;redraw();return;} if(selectedId){selectedId=null; updateLayers(); redraw(); return;} commitLineInProgress(); setTool('select'); return; }
    if(e.key==='Enter'){ if(movingId){confirmMove();return;} }
    if((e.key==='Delete'||e.key==='Backspace')&&!typing&&selectedId&&!movingId) deleteSelected();
    if(!typing&&!e.ctrlKey&&!e.metaKey&&!e.altKey){
      if(e.key==='1') setTool('line');
      if(e.key==='2') setTool('floor');
      if(e.key==='3') setTool('door');
      if(e.key==='4') setTool('stair');
      if(e.key==='5') setTool('label');
      if(e.key==='6') setTool('object');
      if(e.key==='q'||e.key==='Q') setTool('select');
      if(e.key==='w'||e.key==='W') setTool('pan');
    }
  }

  function onWheel(e){
    e.preventDefault();
    const pos=canvasPos(e);
    if(tool==='ref'&&refImage){
      const factor=e.deltaY<0?1.1:1/1.1;
      const mx=(pos.x-offsetX)/(scale*CELL), my=(pos.y-offsetY)/(scale*CELL);
      const oldScale=refScale;
      refScale=Math.min(20,Math.max(0.05,refScale*factor));
      const d=refScale/oldScale;
      refX=mx-(mx-refX)*d; refY=my-(my-refY)*d;
      updateRefScaleDisplay(); redraw(); return;
    }
    zoomAt(pos.x,pos.y,e.deltaY<0?1.1:1/1.1);
  }

  // ── Move / Pan / Zoom ────────────────────────────────────
  function startPan(e){ const pos=canvasPos(e); isPanning=true; panStart={x:pos.x,y:pos.y}; panOffStart={x:offsetX,y:offsetY}; cwrap.classList.add('panning'); }
  function zoomBy(f){ zoomAt(canvasW/2, canvasH/2, f); }
  // rAF throttle para updatePinPositions — evita llamadas redundantes en el mismo frame
  let _pinRafId = null;
  function scheduleUpdatePins(){
    if(typeof Pinner==='undefined') return;
    if(_pinRafId) return; // ya hay un frame pendiente
    _pinRafId = requestAnimationFrame(()=>{
      _pinRafId = null;
      Pinner.updatePinPositions();
    });
  }

  function zoomAt(cx,cy,f){ const wx=(cx-offsetX)/scale, wy=(cy-offsetY)/scale; scale=Math.min(10,Math.max(0.08,scale*f)); offsetX=cx-wx*scale; offsetY=cy-wy*scale; redraw(); scheduleUpdatePins(); }
  function panTo(x,y){ offsetX=x; offsetY=y; redraw(); scheduleUpdatePins(); }
  function resetView(){ offsetX=canvasW/2; offsetY=canvasH/2; scale=1; redraw(); scheduleUpdatePins(); }

  function applyMovingPos(obj,wx,wy){
    if(obj.type==='line'){ const dx=wx-obj.points[0].x,dy=wy-obj.points[0].y; obj.points=obj.points.map(p=>({x:p.x+dx,y:p.y+dy})); }
    else if(obj.type==='door'||obj.type==='ddoor'){ obj.x=snap(wx,.25); obj.y=snap(wy,.25); }
    else { obj.x=wx; obj.y=wy; }
  }
  function moveDelta(obj,dx,dy,s){
    if(obj.type==='line') obj.points=obj.points.map(p=>({x:snap(p.x+dx,s),y:snap(p.y+dy,s)}));
    else if(obj.type==='label'){ obj.x+=dx; obj.y+=dy; }
    else if(obj.type==='door'||obj.type==='ddoor'){ obj.x=snap(obj.x+dx,.25); obj.y=snap(obj.y+dy,.25); }
    else { obj.x=snap(obj.x+dx,s); obj.y=snap(obj.y+dy,s); }
    updateLayers();
  }
  function confirmMove(){ if(!movingId)return; movingId=null; movingOrigin=null; document.getElementById('hint').textContent='Click to select · Drag to move · Del to delete'; updateLayers(); redraw(); App.markDirty(); }
  function cancelMove(){ if(!movingId||!movingOrigin)return; const obj=objects.find(o=>o.id===movingId); if(obj)Object.assign(obj,JSON.parse(movingOrigin)); movingId=null; movingOrigin=null; document.getElementById('hint').textContent='Click to select · Drag to move · Del to delete'; updateLayers(); redraw(); }
  function commitObject(){
    if(!objectPoints || objectPoints.length < 3){ objectPoints=null; redraw(); return; }
    pushUndo();
    const filled=document.getElementById('btn-object-filled')?.getAttribute('aria-pressed')==='true';
    objects.push({ id:uid(), type:'object', points:[...objectPoints], color:activeColor.object, filled });
    objectPoints=null;
    updateLayers(); redraw(); App.markDirty();
  }

  function commitLineInProgress(){ if(tool==='line'&&linePoints&&linePoints.length>=2){ pushUndo(); const lw=parseFloat(document.getElementById('wall-width').value)||4; objects.push({id:uid(),type:'line',points:[...linePoints],color:activeColor.wall,width:lw}); updateLayers(); App.markDirty(); } linePoints=null; floorStart=null; redraw(); }

  // ── Hit test ─────────────────────────────────────────────
  function hitTest(wx,wy){
    for(let i=objects.length-1;i>=0;i--){
      const obj=objects[i];
      if(obj.type==='floor'||obj.type==='stair'){ if(wx>=obj.x&&wx<=obj.x+obj.w&&wy>=obj.y&&wy<=obj.y+obj.h)return obj; }
      if(obj.type==='door'||obj.type==='ddoor'){ const horiz=obj.orient==='h',dw=horiz?obj.size:.5,dh=horiz?.5:obj.size; if(wx>=obj.x&&wx<=obj.x+dw&&wy>=obj.y&&wy<=obj.y+dh)return obj; }
      if(obj.type==='line'){ for(let j=0;j<obj.points.length-1;j++){if(distSeg(wx,wy,obj.points[j],obj.points[j+1])<0.18)return obj;} }
      if(obj.type==='object'){
        if(obj.points&&obj.points.length>=3){
          let inside=false;
          const pts=obj.points;
          for(let j=0,k=pts.length-1;j<pts.length;k=j++){
            const xi=pts[j].x,yi=pts[j].y,xj=pts[k].x,yj=pts[k].y;
            if(((yi>wy)!==(yj>wy))&&(wx<(xj-xi)*(wy-yi)/(yj-yi)+xi))inside=!inside;
          }
          if(inside)return obj;
        }
      }
      if(obj.type==='label'){ if(Math.abs(wx-obj.x)<2&&Math.abs(wy-obj.y)<0.5)return obj; }
    }
    return null;
  }
  function distSeg(px,py,a,b){ const dx=b.x-a.x,dy=b.y-a.y,len2=dx*dx+dy*dy; if(len2===0)return Math.hypot(px-a.x,py-a.y); const t=Math.max(0,Math.min(1,((px-a.x)*dx+(py-a.y)*dy)/len2)); return Math.hypot(px-(a.x+t*dx),py-(a.y+t*dy)); }

  // ── Layers ───────────────────────────────────────────────
  let dragSrcId=null;
  function updateLayers(){
    const list=document.getElementById('layers-list');
    if(!list)return;
    list.innerHTML='';
    const names={line:'Wall',door:'Door',ddoor:'Locked Door',floor:'Floor',stair:'Stairs',label:'Label',object:'Object'};
    for(let i=objects.length-1;i>=0;i--){
      const obj=objects[i];
      const item=document.createElement('div');
      item.className='litem'+(obj.id===selectedId?' sel':'');
      item.draggable=true; item.dataset.id=obj.id;
      const dot=document.createElement('div'); dot.className='ldot'; dot.style.background=obj.color||'#888';
      const lbl=document.createElement('span'); lbl.className='llabel'; lbl.textContent=(names[obj.type]||obj.type)+' #'+(i+1);
      const handle=document.createElement('span'); handle.style.cssText='color:var(--muted);font-size:11px;margin-left:auto;padding:0 3px;cursor:grab;flex-shrink:0'; handle.textContent='⠿';
      const del=document.createElement('button'); del.className='ldelbtn'; del.textContent='×';
      del.onclick=ev=>{ ev.stopPropagation(); pushUndo(); objects.splice(i,1); if(selectedId===obj.id)selectedId=null; updateLayers(); redraw(); App.markDirty(); };
      item.appendChild(dot); item.appendChild(lbl); item.appendChild(handle); item.appendChild(del);
      item.onclick=()=>{ selectedId=obj.id; updateLayers(); redraw(); };
      item.addEventListener('dragstart',e=>{ dragSrcId=obj.id; e.dataTransfer.effectAllowed='move'; requestAnimationFrame(()=>item.classList.add('dragging')); });
      item.addEventListener('dragend',()=>{ dragSrcId=null; list.querySelectorAll('.litem').forEach(el=>el.classList.remove('dragging','drag-over')); });
      item.addEventListener('dragover',e=>{ e.preventDefault(); e.dataTransfer.dropEffect='move'; list.querySelectorAll('.litem').forEach(el=>el.classList.remove('drag-over')); item.classList.add('drag-over'); });
      item.addEventListener('dragleave',e=>{ if(!item.contains(e.relatedTarget))item.classList.remove('drag-over'); });
      item.addEventListener('drop',e=>{ e.preventDefault(); item.classList.remove('drag-over'); if(!dragSrcId||dragSrcId===obj.id)return; const si=objects.findIndex(o=>o.id===dragSrcId),di=objects.findIndex(o=>o.id===obj.id); if(si===-1||di===-1)return; pushUndo(); const [m]=objects.splice(si,1); objects.splice(di,0,m); dragSrcId=null; updateLayers(); redraw(); });
      list.appendChild(item);
    }
    list.ondragover=e=>e.preventDefault();
    list.ondrop=e=>{ e.preventDefault(); if(!dragSrcId)return; const si=objects.findIndex(o=>o.id===dragSrcId); if(si===-1)return; if(e.target===list){ pushUndo(); const [m]=objects.splice(si,1); objects.unshift(m); dragSrcId=null; updateLayers(); redraw(); } };
  }

  // ── Actions ──────────────────────────────────────────────
  function deleteSelected(){ if(!selectedId)return; pushUndo(); objects=objects.filter(o=>o.id!==selectedId); selectedId=null; updateLayers(); redraw(); App.markDirty(); }
  function clearAll(){ if(!confirm('Clear all objects?'))return; pushUndo(); objects=[]; selectedId=null; updateLayers(); redraw(); App.markDirty(); }
  function pushUndo(){ undoStack.push(JSON.stringify(objects)); if(undoStack.length>60)undoStack.shift(); redoStack=[]; }
  function undoAction(){ if(!undoStack.length)return; redoStack.push(JSON.stringify(objects)); objects=JSON.parse(undoStack.pop()); selectedId=null; updateLayers(); redraw(); App.markDirty(); }
  function redoAction(){ if(!redoStack.length)return; undoStack.push(JSON.stringify(objects)); objects=JSON.parse(redoStack.pop()); selectedId=null; updateLayers(); redraw(); App.markDirty(); }

  // ── Ref image ────────────────────────────────────────────
  function activateRefTool(){ if(!refImage){alert('Load a reference image first.');return;} setTool('ref'); }
  function resetRefTransform(){ refX=0; refY=0; refScale=1; updateRefScaleDisplay(); redraw(); }
  function updateRefScaleDisplay(){ const el=document.getElementById('ref-scale-display'); if(el)el.textContent=Math.round(refScale*100)+'%'; }
  function initRefImageUpload(){ document.getElementById('img-upload')?.addEventListener('change',function(e){ const file=e.target.files[0]; if(!file)return; const img=new Image(); img.onload=()=>{refImage=img;redraw();}; img.src=URL.createObjectURL(file); }); }

  function initViewModeButtons(){
    document.querySelectorAll('.view-mode-btn').forEach(btn => {
      btn.addEventListener('click', () => setViewMode(btn.dataset.mode));
    });
    document.getElementById('exp-quick')?.addEventListener('click', quickExport);
    document.getElementById('btn-rotate-90')?.addEventListener('click', rotateAll);
    document.getElementById('btn-snap-grid')?.addEventListener('click', snapAllToGrid);
  }

  function quickExport(){
    const mult = 2;
    const savedW = canvasW, savedH = canvasH;
    const savedScale = scale, savedOX = offsetX, savedOY = offsetY;
    const savedSel = selectedId, savedMov = movingId;
    selectedId = null; movingId = null;
    canvas.width = canvasW * mult;
    canvas.height = canvasH * mult;
    canvasW *= mult; canvasH *= mult;
    scale *= mult;
    offsetX *= mult;
    offsetY *= mult;
    redraw();
    const dataURL = canvas.toDataURL('image/png');
    canvas.width = savedW;
    canvas.height = savedH;
    canvasW = savedW; canvasH = savedH;
    scale = savedScale; offsetX = savedOX; offsetY = savedOY;
    selectedId = savedSel; movingId = savedMov;
    redraw();
    const a = document.createElement('a');
    a.download = 'mapa-2d-'+Date.now()+'.png';
    a.href = dataURL;
    a.click();
  }

  // ── Swatches ─────────────────────────────────────────────
  function buildSwatches(containerId, category){
    const el=document.getElementById(containerId); if(!el)return;
    const colors=PALETTE[category]||[];
    el.innerHTML='';
    colors.forEach(c=>{ const d=document.createElement('div'); d.className='swatch'+(activeColor[category]===c?' selected':''); d.style.background=c; d.title=c; d.onclick=()=>{ activeColor[category]=c; if(category==='door')activeColor.ddoor=c; el.querySelectorAll('.swatch,.swatch-custom').forEach(s=>s.classList.remove('selected')); d.classList.add('selected'); }; el.appendChild(d); });
    const cw=document.createElement('div'); cw.className='swatch-custom'; cw.title='Color personalizado'; cw.innerHTML='<span style="pointer-events:none;color:var(--muted);font-size:11px">+</span>'; const inp=document.createElement('input'); inp.type='color'; inp.value=CUSTOM[category]||'#888'; inp.addEventListener('input',()=>{ activeColor[category]=inp.value; if(category==='door')activeColor.ddoor=inp.value; cw.style.background=inp.value; el.querySelectorAll('.swatch,.swatch-custom').forEach(s=>s.classList.remove('selected')); cw.classList.add('selected'); }); cw.appendChild(inp); cw.addEventListener('click',()=>inp.click()); el.appendChild(cw);
  }
  function buildAllSwatches(){
    buildSwatches('wall-swatches','wall'); buildSwatches('floor-swatches','floor'); buildSwatches('door-swatches','door'); buildSwatches('stair-swatches','stair'); buildSwatches('object-swatches','object');
    // ddoor comparte paleta de door
    const el=document.getElementById('ddoor-swatches'); if(!el)return; el.innerHTML=''; PALETTE.door.forEach(c=>{ const d=document.createElement('div'); d.className='swatch'+(activeColor.ddoor===c?' selected':''); d.style.background=c; d.onclick=()=>{ activeColor.ddoor=c; el.querySelectorAll('.swatch,.swatch-custom').forEach(s=>s.classList.remove('selected')); d.classList.add('selected'); }; el.appendChild(d); }); const cw=document.createElement('div'); cw.className='swatch-custom'; cw.title='Color personalizado'; cw.innerHTML='<span style="pointer-events:none;color:var(--muted);font-size:11px">+</span>'; const inp=document.createElement('input'); inp.type='color'; inp.value=CUSTOM.ddoor; inp.addEventListener('input',()=>{activeColor.ddoor=inp.value;cw.style.background=inp.value;}); cw.appendChild(inp); cw.addEventListener('click',()=>inp.click()); el.appendChild(cw);
  }

  // ── Dropdown ─────────────────────────────────────────────
  function buildToolbarListeners(){
    // Botones de iconos de herramienta
    document.querySelectorAll('.tool-icon-btn').forEach(btn=>{
      btn.addEventListener('click', ()=> setTool(btn.dataset.tool));
    });
    // Orientacion puerta
    document.querySelectorAll('.door-orient-btn').forEach(btn=>{
      btn.addEventListener('click', ()=> setDoorOrient(btn.dataset.val));
    });
    // Arrow checkbox puerta
    document.getElementById('door-arrow-check')?.addEventListener('change', e=>{
      setDoorArrowEnabled(e.target.checked);
      // Si tiene flecha, el tipo es ddoor al colocar
      if(e.target.checked && tool==='door') tool='ddoor';
      else if(!e.target.checked && tool==='ddoor') tool='door';
    });
    // Direccion flecha puerta
    document.querySelectorAll('.door-arrow-btn').forEach(btn=>{
      btn.addEventListener('click', ()=> setDDoorArrow(btn.dataset.val));
    });
    // Orientacion escalera
    document.querySelectorAll('.stair-orient-btn').forEach(btn=>{
      btn.addEventListener('click', ()=> setStairOrient(btn.dataset.val));
    });
    // Direccion escalera
    document.querySelectorAll('.stair-dir-btn').forEach(btn=>{
      btn.addEventListener('click', ()=> setStairDir(btn.dataset.val));
    });
    // Toggle objeto sólido
    document.getElementById('btn-object-filled')?.addEventListener('click',function(){
      const pressed=this.getAttribute('aria-pressed')==='true';
      this.setAttribute('aria-pressed',String(!pressed));
      this.textContent=!pressed?'SOLID':'STRUCTURE';
      redraw();
    });
    // Swatches de color
    buildAllSwatches();
  }

  function buildDropdownListeners(){
    document.getElementById('ddmenu-btn')?.addEventListener('click',toggleDDMenu);
    document.addEventListener('click',e=>{ if(!e.target.closest('.ddmenu-wrap'))closeDDMenu(); });
    ['bg-color','grid-color','floor-border-color','label-color'].forEach(id=>{ document.getElementById(id)?.addEventListener('click',e=>e.stopPropagation()); });
    document.getElementById('bg-wrap')?.addEventListener('click',e=>{e.stopPropagation();document.getElementById('bg-color').click();});
    document.getElementById('grid-wrap')?.addEventListener('click',e=>{e.stopPropagation();document.getElementById('grid-color').click();});
    document.getElementById('fbc-wrap')?.addEventListener('click',e=>{e.stopPropagation();document.getElementById('floor-border-color').click();});
    document.getElementById('lc-wrap')?.addEventListener('click',e=>{e.stopPropagation();document.getElementById('label-color').click();});
    document.getElementById('bg-color')?.addEventListener('input',e=>{bgColor=e.target.value;document.getElementById('bg-preview').style.background=e.target.value;redraw();App.markDirty();});
    document.getElementById('grid-color')?.addEventListener('input',e=>{gridColor=e.target.value;document.getElementById('grid-preview').style.background=e.target.value;redraw();});
    document.getElementById('grid-vis')?.addEventListener('change',()=>redraw());
    document.getElementById('ref-opacity')?.addEventListener('input',function(){refOpacity=this.value/100;document.getElementById('ref-opacity-val').textContent=this.value+'%';redraw();});
  }
  function toggleDDMenu(){ const btn=document.getElementById('ddmenu-btn'),menu=document.getElementById('ddmenu'); const open=menu.classList.toggle('open'); btn.classList.toggle('open',open); }
  function closeDDMenu(){ document.getElementById('ddmenu')?.classList.remove('open'); document.getElementById('ddmenu-btn')?.classList.remove('open'); }

  // ── Export PNG ───────────────────────────────────────────
  function toggleExpManual(){ const manual=document.getElementById('exp-mode').value==='manual'; document.getElementById('exp-manual-row').style.display=manual?'flex':'none'; }
  function doExport(){
    const eScale=parseInt(document.getElementById('exp-scale').value)||2;
    const eGrid=document.getElementById('exp-grid').checked;
    const eRef=document.getElementById('exp-ref').checked;
    const eMode=document.getElementById('exp-mode').value;
    const ePad=parseInt(document.getElementById('exp-pad').value)||40;
    const ov=document.getElementById('export-overlay');
    ov.style.display='flex';
    setTimeout(()=>{
      try{
        const savedScale=scale,savedOX=offsetX,savedOY=offsetY,savedSelId=selectedId,savedMovId=movingId;
        selectedId=null; movingId=null;
        let exportW,exportH;
        if(eMode==='manual'){
          exportW=(parseInt(document.getElementById('exp-w').value)||1920)*eScale;
          exportH=(parseInt(document.getElementById('exp-h').value)||1080)*eScale;
          scale=savedScale*eScale; offsetX=savedOX*eScale; offsetY=savedOY*eScale;
        } else {
          const bounds=MapRender.getContentBounds(objects);
          if(!bounds){alert('No hay objetos.');ov.style.display='none';selectedId=savedSelId;movingId=savedMovId;return;}
          const pad=ePad,cw=(bounds.maxX-bounds.minX)*CELL,ch=(bounds.maxY-bounds.minY)*CELL;
          exportW=Math.ceil((cw+pad*2)*eScale); exportH=Math.ceil((ch+pad*2)*eScale);
          scale=eScale; offsetX=(pad-bounds.minX*CELL)*eScale; offsetY=(pad-bounds.minY*CELL)*eScale;
        }
        const savedW=canvasW,savedH=canvasH;
        canvas.width=exportW; canvas.height=exportH; canvasW=exportW; canvasH=exportH;
        const gridVis=document.getElementById('grid-vis'),gridWas=gridVis.checked; gridVis.checked=eGrid;
        const savedRef=refImage; if(!eRef)refImage=null;
        redraw();
        const dataURL=canvas.toDataURL('image/png');
        refImage=savedRef; gridVis.checked=gridWas;
        scale=savedScale; offsetX=savedOX; offsetY=savedOY; selectedId=savedSelId; movingId=savedMovId;
        canvasW=savedW; canvasH=savedH; canvas.width=canvasW; canvas.height=canvasH; redraw();
        const a=document.createElement('a'); a.download='mapa-2d-'+Date.now()+'.png'; a.href=dataURL; a.click();
      }catch(err){alert('Error al exportar: '+err.message);console.error(err);}
      ov.style.display='none';
    },80);
  }

  // ── Drawns (previews) ────────────────────────────────────
  function drawLinePreview(c,sc,ox,oy){
    const cp=sc*CELL,s=parseFloat(document.getElementById('snap-size').value)||.5;
    const color=activeColor.wall,lw=parseFloat(document.getElementById('wall-width').value)||4;
    const snapX=snap(mouseWorld.x,s),snapY=snap(mouseWorld.y,s);
    c.save();c.strokeStyle=color;c.lineWidth=lw*(sc>1?sc:1);c.lineCap='round';c.globalAlpha=.5;c.setLineDash([6,4]);
    const last=linePoints[linePoints.length-1];c.beginPath();c.moveTo(last.x*cp+ox,last.y*cp+oy);c.lineTo(snapX*cp+ox,snapY*cp+oy);c.stroke();
    c.setLineDash([]);c.globalAlpha=1;c.beginPath();linePoints.forEach((p,i)=>{const px=p.x*cp+ox,py=p.y*cp+oy;i===0?c.moveTo(px,py):c.lineTo(px,py);});c.stroke();
    c.fillStyle=color;c.beginPath();c.arc(snapX*cp+ox,snapY*cp+oy,4,0,Math.PI*2);c.fill();c.restore();
  }
  function drawFloorPreview(c,sc,ox,oy){
    const cp=sc*CELL,s=parseFloat(document.getElementById('snap-size').value)||.5;
    const color=activeColor.floor,bc=document.getElementById('floor-border-color').value,hb=document.getElementById('floor-border').checked;
    const x1=snap(floorStart.x,s),y1=snap(floorStart.y,s),x2=snap(mouseWorld.x,s),y2=snap(mouseWorld.y,s);
    const sx=Math.min(x1,x2)*cp+ox,sy=Math.min(y1,y2)*cp+oy,sw=Math.abs(x2-x1)*cp,sh=Math.abs(y2-y1)*cp;
    c.save();c.globalAlpha=.55;c.fillStyle=color;c.fillRect(sx,sy,sw,sh);if(hb){c.strokeStyle=bc;c.lineWidth=1.5;c.strokeRect(sx,sy,sw,sh);}c.restore();
  }
  function drawDoorPreview(c,sc,ox,oy){
    const cp=sc*CELL;
    const size=parseFloat(document.getElementById('door-size-val').value)||1;
    const horiz=doorOrient==='h';
    const sw=horiz?size*cp:.5*cp, sh=horiz?.5*cp:size*cp;
    const sx=snap(mouseWorld.x,.25)*cp+ox, sy=snap(mouseWorld.y,.25)*cp+oy;
    const isBlocked = tool==='ddoor' || doorHasArrow;
    const arrow = isBlocked ? ddoorArrow : null;
    const col = tool==='ddoor' ? activeColor.ddoor : activeColor.door;
    c.save();c.globalAlpha=.5;MapRender._drawDoorShape(c,sx,sy,sw,sh,col,horiz,isBlocked,arrow,sc);c.restore();
  }
  function drawDDoorPreview(c,sc,ox,oy){ drawDoorPreview(c,sc,ox,oy); }
  function drawStairPreview(c,sc,ox,oy){
    const cp=sc*CELL,s=parseFloat(document.getElementById('snap-size').value)||.5;
    const sw=parseFloat(document.getElementById('stair-w-val').value)||2;
    const sh=parseFloat(document.getElementById('stair-h-val').value)||2;
    const horiz=stairOrient==='h';
    const pw=horiz?sw*cp:sh*cp, ph=horiz?sh*cp:sw*cp;
    const sx=snap(mouseWorld.x,s)*cp+ox,sy=snap(mouseWorld.y,s)*cp+oy;
    c.save();c.globalAlpha=.5;MapRender._drawStairShape(c,sx,sy,pw,ph,activeColor.stair,sc,stairOrient,stairDir);c.restore();
  }
  function drawObjectPreview(c,sc,ox,oy){
    if(!objectPoints || objectPoints.length===0) return;
    const cp=sc*CELL, s=0.25;
    const mx=snap(mouseWorld.x,s), my=snap(mouseWorld.y,s);
    const base=activeColor.object;
    const r=parseInt(base.slice(1,3),16),g=parseInt(base.slice(3,5),16),b=parseInt(base.slice(5,7),16);
    const STROKE=`rgb(${Math.min(255,r+45)},${Math.min(255,g+22)},${Math.min(255,b+22)})`;
    const FILL=`rgba(${r},${g},${b},0.45)`;
    c.save();
    c.beginPath();
    objectPoints.forEach((p,i)=>{
      const px=p.x*cp+ox, py=p.y*cp+oy;
      i===0?c.moveTo(px,py):c.lineTo(px,py);
    });
    c.lineTo(mx*cp+ox, my*cp+oy);
    c.fillStyle=FILL; c.fill();
    c.strokeStyle=STROKE; c.lineWidth=Math.max(1.5,sc*0.8);
    c.setLineDash([5,3]); c.stroke(); c.setLineDash([]);
    c.fillStyle=STROKE;
    objectPoints.forEach(p=>{
      c.beginPath(); c.arc(p.x*cp+ox,p.y*cp+oy,Math.max(2.5,sc*1.3),0,Math.PI*2); c.fill();
    });
    if(objectPoints.length>=3){
      const first=objectPoints[0];
      const distPx=Math.hypot((mx-first.x)*cp,(my-first.y)*cp);
      if(distPx<12){
        c.strokeStyle='rgba(192,64,74,.9)'; c.lineWidth=2;
        c.beginPath(); c.arc(first.x*cp+ox,first.y*cp+oy,8,0,Math.PI*2); c.stroke();
      }
    }
    c.fillStyle='rgba(192,64,74,.8)';
    c.beginPath(); c.arc(mx*cp+ox,my*cp+oy,3,0,Math.PI*2); c.fill();
    c.restore();
  }

  function drawLabelPreview(c,sc,ox,oy){
    const cp=sc*CELL,s=parseFloat(document.getElementById('snap-size').value)||.5;
    const text=document.getElementById('label-text').value||'Label',fs=parseFloat(document.getElementById('label-size').value)*Math.max(.5,sc),color=document.getElementById('label-color').value;
    const sx=snap(mouseWorld.x,s)*cp+ox,sy=snap(mouseWorld.y,s)*cp+oy;
    c.save();c.globalAlpha=.55;c.font=`500 ${fs}px 'Segoe UI',sans-serif`;c.fillStyle=color;c.textAlign='left';c.textBaseline='top';c.fillText(text,sx,sy);c.restore();
  }
  function drawMovingHint(c,sc,ox,oy){
    const s=parseFloat(document.getElementById('snap-size').value)||.5;
    const sx=snap(mouseWorld.x,s)*sc*CELL+ox,sy=snap(mouseWorld.y,s)*sc*CELL+oy;
    c.save();c.fillStyle='rgba(255,200,60,.85)';c.font='bold 11px Segoe UI,sans-serif';c.textAlign='center';c.textBaseline='bottom';c.fillText('Double-click / Enter to confirm · Esc to cancel',sx,sy-8);c.restore();
  }

  // ── Rotation 90° ────────────────────────────────────────
  function getObjPoints(obj) {
    if (obj.type === 'line') return obj.points;
    if (obj.type === 'floor') return [{x:obj.x,y:obj.y},{x:obj.x+obj.w,y:obj.y+obj.h}];
    if (obj.type === 'door' || obj.type === 'ddoor') {
      const h = obj.orient === 'h';
      const sw = h ? obj.size : 0.5, sh = h ? 0.5 : obj.size;
      return [{x:obj.x,y:obj.y},{x:obj.x+sw,y:obj.y+sh}];
    }
    if (obj.type === 'stair') return [{x:obj.x,y:obj.y},{x:obj.x+obj.w,y:obj.y+obj.h}];
    if (obj.type === 'object') return obj.points || [];
    if (obj.type === 'label') return [{x:obj.x,y:obj.y}];
    return [];
  }

  function getObjBounds(obj) {
    let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
    getObjPoints(obj).forEach(p => { minX=Math.min(minX,p.x);minY=Math.min(minY,p.y);maxX=Math.max(maxX,p.x);maxY=Math.max(maxY,p.y); });
    return {minX,minY,maxX,maxY};
  }

  function boxesTouch(a,b,t) {
    return !(a.maxX+t<b.minX||b.maxX+t<a.minX||a.maxY+t<b.minY||b.maxY+t<a.minY);
  }

  function findZones(objs) {
    if (!objs.length) return [];
    const boxes = objs.map(o => getObjBounds(o));
    const parent = objs.map((_,i) => i);
    const find = x => { while (parent[x]!==x) {parent[x]=parent[parent[x]];x=parent[x];} return x; };
    const union = (a,b) => { parent[find(a)]=find(b); };
    for (let i=0;i<objs.length;i++)
      for (let j=i+1;j<objs.length;j++)
        if (boxesTouch(boxes[i],boxes[j],1)) union(i,j);
    const groups = {};
    objs.forEach((o,i) => { const r=find(i); if (!groups[r]) groups[r]=[]; groups[r].push(o); });
    return Object.values(groups);
  }

  function zoneCenter(zone) {
    let sx=0,sy=0,n=0;
    zone.forEach(o => getObjPoints(o).forEach(p => { sx+=p.x; sy+=p.y; n++; }));
    return n ? {x:sx/n,y:sy/n} : {x:0,y:0};
  }

  function rotatePoint(p,cx,cy) {
    const dx=p.x-cx, dy=p.y-cy;
    return {x:cx-dy, y:cy+dx};
  }

  function rotateObject(obj, cx, cy) {
    if (obj.type === 'line' || obj.type === 'object') {
      (obj.points||[]).forEach(p => { const r=rotatePoint(p,cx,cy); p.x=r.x; p.y=r.y; });
    } else if (obj.type === 'floor') {
      const ocx=obj.x+obj.w/2, ocy=obj.y+obj.h/2;
      const nc=rotatePoint({x:ocx,y:ocy},cx,cy);
      const ow=obj.w, oh=obj.h;
      obj.w=oh; obj.h=ow;
      obj.x=nc.x-obj.w/2; obj.y=nc.y-obj.h/2;
    } else if (obj.type === 'door' || obj.type === 'ddoor') {
      const h=obj.orient==='h';
      const sw=h?obj.size:0.5, sh=h?0.5:obj.size;
      const ocx=obj.x+sw/2, ocy=obj.y+sh/2;
      const nc=rotatePoint({x:ocx,y:ocy},cx,cy);
      obj.orient = h?'v':'h';
      const nsw = obj.orient==='h'?obj.size:0.5, nsh = obj.orient==='h'?0.5:obj.size;
      obj.x=nc.x-nsw/2; obj.y=nc.y-nsh/2;
    } else if (obj.type === 'stair') {
      const ocx=obj.x+obj.w/2, ocy=obj.y+obj.h/2;
      const nc=rotatePoint({x:ocx,y:ocy},cx,cy);
      const ow=obj.w, oh=obj.h;
      obj.w=oh; obj.h=ow;
      obj.orient = obj.orient==='h'?'v':'h';
      obj.x=nc.x-obj.w/2; obj.y=nc.y-obj.h/2;
    } else if (obj.type === 'label') {
      const r=rotatePoint(obj,cx,cy);
      obj.x=r.x; obj.y=r.y;
    }
  }

  function rotateAll() {
    // Save undo
    undoStack.push(JSON.stringify(objects));
    redoStack = [];

    // Find zones
    const zones = findZones(objects);
    if (!zones.length) return;

    const centers = zones.map(z => zoneCenter(z));
    // Snap centers to grid so rotated coordinates stay grid-aligned
    const s = parseFloat(document.getElementById('snap-size').value)||0.5;
    centers.forEach(c => { c.x=Math.round(c.x/s)*s; c.y=Math.round(c.y/s)*s; });

    // Rotate editor objects
    zones.forEach((zone, zi) => {
      const cx = centers[zi].x, cy = centers[zi].y;
      zone.forEach(obj => rotateObject(obj, cx, cy));
    });

    // Rotate pins (pin-world coordinates = editor * CELL)
    if (typeof Pinner !== 'undefined') {
      const pins = Pinner.getData() || [];
      pins.forEach(pin => {
        const pex = pin.x / CELL, pey = pin.y / CELL;
        let best = 0, bd = Infinity;
        centers.forEach((c, i) => { const d=Math.hypot(pex-c.x,pey-c.y); if (d<bd) { bd=d; best=i; } });
        const r = rotatePoint({x:pex,y:pey}, centers[best].x, centers[best].y);
        pin.x = r.x * CELL;
        pin.y = r.y * CELL;
      });
      // Only re-render pins if not in editor mode
      const pp = document.getElementById('pinner-panel');
      if (pp && !pp.classList.contains('hidden')) Pinner.renderPins();
    }

    // Rotate arrows (same coordinate system as pins)
    if (typeof Steps !== 'undefined') {
      const sd = Steps.getData();
      (sd.steps||[]).forEach(step => {
        (step.arrows||[]).forEach(arrow => {
          (arrow.points||[]).forEach(p => {
            const pex = p.x / CELL, pey = p.y / CELL;
            let best = 0, bd = Infinity;
            centers.forEach((c, i) => { const d=Math.hypot(pex-c.x,pey-c.y); if (d<bd) { bd=d; best=i; } });
            const r = rotatePoint({x:pex,y:pey}, centers[best].x, centers[best].y);
            p.x = r.x * CELL;
            p.y = r.y * CELL;
          });
        });
      });
      if (document.getElementById('steps-list')) Steps.renderAllArrows();
    }

    selectedId = null; movingId = null;
    updateLayers();
    redraw();
    App.markDirty();
  }

  function snapAllToGrid(){
    pushUndo();
    const s = parseFloat(document.getElementById('snap-size').value)||0.5;
    objects.forEach(obj => {
      if (obj.type === 'floor' || obj.type === 'stair') {
        const x2 = Math.round(obj.x/s)*s, y2 = Math.round(obj.y/s)*s;
        const rw = Math.round(obj.w/s)*s, rh = Math.round(obj.h/s)*s;
        obj.x=x2; obj.y=y2; obj.w=Math.max(rw,s); obj.h=Math.max(rh,s);
      } else if (obj.type === 'door' || obj.type === 'ddoor') {
        obj.x = Math.round(obj.x/s)*s;
        obj.y = Math.round(obj.y/s)*s;
        obj.size = Math.round(obj.size/s)*s;
      } else if (obj.type === 'line' || obj.type === 'object') {
        (obj.points||[]).forEach(p => { p.x=Math.round(p.x/s)*s; p.y=Math.round(p.y/s)*s; });
      } else if (obj.type === 'label') {
        obj.x = Math.round(obj.x/s)*s;
        obj.y = Math.round(obj.y/s)*s;
      }
    });
    selectedId = null; movingId = null;
    updateLayers();
    redraw();
    App.markDirty();
  }

  // ── Serialización ────────────────────────────────────────
  function getData(){
    return { bgColor, gridColor, refX, refY, refScale, refOpacity, objects, viewMode, views, activeViewId };
  }
  function loadData(data){
    if(!data) return;
    objects    = data.objects || [];
    bgColor    = data.bgColor    || '#1e1e1e';
    gridColor  = data.gridColor  || '#2e2e2e';
    refX       = data.refX       ?? 0;
    refY       = data.refY       ?? 0;
    refScale   = data.refScale   ?? 1;
    refOpacity = data.refOpacity ?? 0.4;
    views         = data.views || [];
    activeViewId  = data.activeViewId || null;
    // restaurar _uid
    let maxId=0; objects.forEach(o=>{const n=parseInt(o.id.replace('o',''));if(n>maxId)maxId=n;}); _uid=maxId;
    // sync UI
    const bgEl=document.getElementById('bg-color'); if(bgEl){bgEl.value=bgColor;document.getElementById('bg-preview').style.background=bgColor;}
    const gcEl=document.getElementById('grid-color'); if(gcEl){gcEl.value=gridColor;document.getElementById('grid-preview').style.background=gridColor;}
    const roEl=document.getElementById('ref-opacity'); if(roEl){roEl.value=Math.round(refOpacity*100);document.getElementById('ref-opacity-val').textContent=roEl.value+'%';}
    if (data.viewMode) setViewMode(data.viewMode);
    selectedId=null; movingId=null; undoStack=[]; redoStack=[];
    buildViewsPanel();
    resetView(); updateLayers();
  }

  // API pública
  return {
    init, activate, deactivate, redraw,
    getData, loadData,
    setTool, setViewMode, getViewMode, setDoorOrient, setDDoorOrient, setDDoorArrow, setStairOrient, setStairDir, setDoorArrowEnabled,
    zoomBy, resetView, panTo, clearAll, undoAction, redoAction, deleteSelected,
    doExport, toggleExpManual, rotateAll, activateRefTool, resetRefTransform, updateRefScaleDisplay,
    toggleDDMenu, closeDDMenu,
    createView, activateView, deleteView, getActiveViewDoorColors, buildViewsPanel, snapAllToGrid,
    getCanvas: ()=>canvas,
    getState: ()=>({ scale, offsetX, offsetY, objects, bgColor, gridColor })
  };

})();

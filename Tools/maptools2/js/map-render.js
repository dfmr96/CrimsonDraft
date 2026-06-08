// ============================================================
// map-render.js — engine de render compartido
// Usado por MapEditor (canvas interactivo) y MapPinner (fondo estático).
//
// API pública:
//   MapRender.renderToCanvas(canvas, editorData, opts)
//   MapRender.renderToDataUrl(editorData, opts)  → Promise<dataUrl>
//   MapRender.getContentBounds(objects)           → {minX,minY,maxX,maxY}|null
//
// editorData: { objects, bgColor, gridColor }
// opts: { scale, pad, showGrid, noShadow, selectedId, movingId }
// ============================================================

const MapRender = (() => {

  const CELL = 52;

  // ── Utilidades ────────────────────────────────────────────
  function darken(hex, a) {
    const r = parseInt(hex.slice(1,3),16);
    const g = parseInt(hex.slice(3,5),16);
    const b = parseInt(hex.slice(5,7),16);
    return `rgb(${Math.floor(r*a)},${Math.floor(g*a)},${Math.floor(b*a)})`;
  }

  function getContentBounds(objects) {
    if (!objects || !objects.length) return null;
    let minX=Infinity, minY=Infinity, maxX=-Infinity, maxY=-Infinity;
    for (const obj of objects) {
      if (obj.type === 'line') {
        for (const p of obj.points) {
          minX=Math.min(minX,p.x); minY=Math.min(minY,p.y);
          maxX=Math.max(maxX,p.x); maxY=Math.max(maxY,p.y);
        }
      } else if (obj.type === 'floor' || obj.type === 'stair') {
        minX=Math.min(minX,obj.x); minY=Math.min(minY,obj.y);
        maxX=Math.max(maxX,obj.x+obj.w); maxY=Math.max(maxY,obj.y+obj.h);
      } else if (obj.type === 'door' || obj.type === 'ddoor') {
        const horiz = obj.orient === 'h';
        const dw = horiz ? obj.size : 0.5, dh = horiz ? 0.5 : obj.size;
        minX=Math.min(minX,obj.x); minY=Math.min(minY,obj.y);
        maxX=Math.max(maxX,obj.x+dw); maxY=Math.max(maxY,obj.y+dh);
      } else if (obj.type === 'object') {
        if (obj.points) for (const p of obj.points) {
          minX=Math.min(minX,p.x); minY=Math.min(minY,p.y);
          maxX=Math.max(maxX,p.x); maxY=Math.max(maxY,p.y);
        }
      } else if (obj.type === 'label') {
        minX=Math.min(minX,obj.x); minY=Math.min(minY,obj.y);
        maxX=Math.max(maxX,obj.x+4); maxY=Math.max(maxY,obj.y+1);
      }
    }
    return { minX, minY, maxX, maxY };
  }

  // ── Grid ──────────────────────────────────────────────────
  function gridLines(c, w, h, step, ox, oy) {
    if (step < 2) return;
    let sx = ox % step; if (sx < 0) sx += step;
    let sy = oy % step; if (sy < 0) sy += step;
    c.beginPath();
    for (let x = sx; x <= w; x += step) { c.moveTo(x,0); c.lineTo(x,h); }
    c.stroke();
    c.beginPath();
    for (let y = sy; y <= h; y += step) { c.moveTo(0,y); c.lineTo(w,y); }
    c.stroke();
  }

  function drawGrid(c, w, h, sc, ox, oy, gridColor) {
    const cell = sc * CELL;
    c.save(); c.strokeStyle = gridColor;
    if (cell*0.25 > 4) { c.lineWidth=0.3; c.globalAlpha=0.35; gridLines(c,w,h,cell*0.25,ox,oy); }
    if (cell*0.5  > 4) { c.lineWidth=0.5; c.globalAlpha=0.5;  gridLines(c,w,h,cell*0.5, ox,oy); }
    c.lineWidth=0.8; c.globalAlpha=0.8; gridLines(c,w,h,cell,ox,oy);
    c.restore();
  }

  // ── Formas ────────────────────────────────────────────────
  function drawStairShape(c, sx, sy, sw, sh, strokeColor, sc, orient) {
    const steps = Math.max(3, Math.round(Math.max(sw,sh) / ((sc||1)*CELL*0.28)));
    c.save();
    let grad;
    if (orient === 'h') {
      grad = c.createLinearGradient(sx,sy,sx,sy+sh);
    } else {
      grad = c.createLinearGradient(sx,sy,sx+sw,sy);
    }
    grad.addColorStop(0,   'rgba(0,0,0,0)');
    grad.addColorStop(0.45,'rgba(0,0,0,0.08)');
    grad.addColorStop(1,   'rgba(0,0,0,0.45)');
    c.fillStyle = grad;
    c.fillRect(sx,sy,sw,sh);

    c.strokeStyle = strokeColor;
    c.lineWidth = Math.max(0.8, sc*0.5);
    c.globalAlpha = 0.85;
    if (orient === 'h') {
      const stepH = sh/steps;
      for (let i=1; i<steps; i++) {
        const y = sy+i*stepH;
        c.beginPath(); c.moveTo(sx,y); c.lineTo(sx+sw,y); c.stroke();
      }
    } else {
      const stepW = sw/steps;
      for (let i=1; i<steps; i++) {
        const x = sx+i*stepW;
        c.beginPath(); c.moveTo(x,sy); c.lineTo(x,sy+sh); c.stroke();
      }
    }

    c.globalAlpha = 1;
    c.strokeStyle = strokeColor;
    c.lineWidth = Math.max(1.2, sc*0.7);
    c.strokeRect(sx,sy,sw,sh);

    const mSize = Math.min(sw,sh)*0.28;
    c.lineWidth = Math.max(1, sc*0.6);
    c.globalAlpha = 0.7;
    c.beginPath();
    c.moveTo(sx+2,sy+2);   c.lineTo(sx+mSize,sy+mSize);
    c.moveTo(sx+mSize,sy+2); c.lineTo(sx+2,sy+mSize);
    c.stroke();

    c.strokeStyle = strokeColor;
    c.lineWidth = Math.max(1, sc*0.7);
    c.globalAlpha = 0.6;
    c.beginPath();
    if (orient === 'h') {
      c.moveTo(sx+sw/2, sy+sh*0.78); c.lineTo(sx+sw/2, sy+sh*0.22);
      c.moveTo(sx+sw/2-sw*0.08, sy+sh*0.32); c.lineTo(sx+sw/2, sy+sh*0.22); c.lineTo(sx+sw/2+sw*0.08, sy+sh*0.32);
    } else {
      c.moveTo(sx+sw*0.22, sy+sh/2); c.lineTo(sx+sw*0.78, sy+sh/2);
      c.moveTo(sx+sw*0.68, sy+sh/2-sh*0.08); c.lineTo(sx+sw*0.78, sy+sh/2); c.lineTo(sx+sw*0.68, sy+sh/2+sh*0.08);
    }
    c.stroke();
    c.restore();
  }

  function drawDoorShape(c, sx, sy, sw, sh, color, horiz, hasArrow, arrow, sc) {
    c.fillStyle = color;
    c.fillRect(sx,sy,sw,sh);
    c.fillStyle = 'rgba(255,255,255,.07)';
    const pad = Math.min(sw,sh)*0.14;
    c.fillRect(sx+pad,sy+pad,sw-pad*2,sh-pad*2);
    c.strokeStyle = darken(color,0.5);
    c.lineWidth = 1.4;
    c.strokeRect(sx,sy,sw,sh);
    c.strokeStyle = darken(color,0.65);
    c.lineWidth = 0.8;
    c.beginPath();
    if (horiz) { c.moveTo(sx,sy+sh/2); c.lineTo(sx+sw,sy+sh/2); }
    else        { c.moveTo(sx+sw/2,sy); c.lineTo(sx+sw/2,sy+sh); }
    c.stroke();
    if (hasArrow) {
      c.save();
      c.strokeStyle = 'rgba(255,255,255,.95)';
      c.lineWidth = Math.max(1.2, Math.min(sw,sh)*0.08);
      const cx=sx+sw/2, cy=sy+sh/2;
      let ax=0, ay=0;
      if (horiz) { ay = arrow==='fwd' ? 1 : -1; }
      else        { ax = arrow==='fwd' ? 1 : -1; }
      const aLen = Math.min(sw,sh)*0.38;
      const headLen = aLen*0.42;
      const angle = Math.atan2(ay,ax);
      c.beginPath();
      c.moveTo(cx-ax*aLen, cy-ay*aLen); c.lineTo(cx+ax*aLen, cy+ay*aLen); c.stroke();
      const tipX=cx+ax*aLen, tipY=cy+ay*aLen;
      c.beginPath();
      c.moveTo(tipX,tipY); c.lineTo(tipX-headLen*Math.cos(angle-0.5), tipY-headLen*Math.sin(angle-0.5));
      c.moveTo(tipX,tipY); c.lineTo(tipX-headLen*Math.cos(angle+0.5), tipY-headLen*Math.sin(angle+0.5));
      c.stroke();
      c.restore();
    }
  }

  // ── Object (blueprint polygon) ──────────────────────────
  function drawObjectShape(c, points, cp, ox, oy, sc, sel, moving, color, filled) {
    if (!points || points.length < 2) return;
    const base    = color || '#4d2627';
    const r=parseInt(base.slice(1,3),16),g=parseInt(base.slice(3,5),16),b=parseInt(base.slice(5,7),16);
    const FILL    = `rgba(${r},${g},${b},0.72)`;
    const STROKE  = `rgb(${Math.min(255,r+45)},${Math.min(255,g+22)},${Math.min(255,b+22)})`;
    const LINE_FG = `rgba(${Math.min(255,r+80)},${Math.min(255,g+40)},${Math.min(255,b+40)},0.5)`;

    c.save();
    if (moving) { c.shadowColor='rgba(255,200,60,.9)'; c.shadowBlur=12; }
    else if (sel){ c.shadowColor='rgba(192,64,74,.8)';  c.shadowBlur=10; }

    c.beginPath();
    points.forEach((p,i) => {
      const px=p.x*cp+ox, py=p.y*cp+oy;
      i===0 ? c.moveTo(px,py) : c.lineTo(px,py);
    });
    c.closePath();

    c.fillStyle = FILL;
    c.fill();

    if(!filled){
      // Hatch diagonal blueprint
      c.save();
      c.clip();
      c.strokeStyle = LINE_FG;
      c.lineWidth   = Math.max(0.5, sc * 0.35);
      const spacing = Math.max(6, cp * 0.18);
      let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
      points.forEach(p => {
        const px=p.x*cp+ox, py=p.y*cp+oy;
        if(px<minX)minX=px; if(py<minY)minY=py;
        if(px>maxX)maxX=px; if(py>maxY)maxY=py;
      });
      const diag = Math.hypot(maxX-minX, maxY-minY);
      c.beginPath();
      for (let d = -diag; d < diag*2; d += spacing) {
        c.moveTo(minX+d,        minY);
        c.lineTo(minX+d+diag,   minY+diag);
      }
      c.stroke();
      c.restore();

      // Borde exterior
      c.beginPath();
      points.forEach((p,i) => {
        const px=p.x*cp+ox, py=p.y*cp+oy;
        i===0 ? c.moveTo(px,py) : c.lineTo(px,py);
      });
      c.closePath();
      c.strokeStyle = STROKE;
      c.lineWidth   = Math.max(1.5, sc*0.9);
      c.lineJoin    = 'round';
      c.stroke();

      // Vértices
      c.fillStyle = STROKE;
      points.forEach(p => {
        c.beginPath();
        c.arc(p.x*cp+ox, p.y*cp+oy, Math.max(2, sc*1.2), 0, Math.PI*2);
        c.fill();
      });
    }

    c.restore();
  }

  // ── Batches ───────────────────────────────────────────────
  function drawAllFloors(c, sorted, sc, ox, oy, selectedId, movingId) {
    const cp = sc * CELL;
    const floors = sorted.filter(o => o.type==='floor' && o.id!==selectedId && o.id!==movingId);
    if (!floors.length) return;
    const groups = new Map();
    for (const obj of floors) {
      const key = obj.color;
      if (!groups.has(key)) groups.set(key, { objs:[], borderColor:obj.borderColor, border:obj.border });
      groups.get(key).objs.push(obj);
    }
    for (const [color, {objs, border, borderColor}] of groups) {
      if (border && borderColor) {
        c.save(); c.strokeStyle=borderColor; c.lineWidth=Math.max(1.5,sc*0.8);
        for (const obj of objs) c.strokeRect(obj.x*cp+ox, obj.y*cp+oy, obj.w*cp, obj.h*cp);
        c.restore();
      }
      c.save(); c.fillStyle=color;
      for (const obj of objs) c.fillRect(obj.x*cp+ox, obj.y*cp+oy, obj.w*cp, obj.h*cp);
      c.restore();
      if (border && borderColor) {
        const bw = Math.max(1.5, sc*0.8);
        c.save(); c.strokeStyle=borderColor; c.lineWidth=bw;
        for (const obj of objs)
          c.strokeRect(obj.x*cp+ox+bw/2, obj.y*cp+oy+bw/2, obj.w*cp-bw, obj.h*cp-bw);
        c.restore();
        c.save(); c.fillStyle=color;
        for (const obj of objs)
          c.fillRect(obj.x*cp+ox+bw, obj.y*cp+oy+bw, obj.w*cp-bw*2, obj.h*cp-bw*2);
        c.restore();
      }
    }
  }

  function drawAllWalls(c, sorted, sc, ox, oy, selectedId, movingId, noShadow) {
    const cp = sc * CELL;
    const lines = sorted.filter(o => o.type==='line' && o.id!==selectedId && o.id!==movingId);
    if (!lines.length) return;
    const groups = new Map();
    for (const obj of lines) {
      const key = obj.color+'|'+obj.width;
      if (!groups.has(key)) groups.set(key,[]);
      groups.get(key).push(obj);
    }
    for (const [key, group] of groups) {
      const [color, widthStr] = key.split('|');
      const lw = parseFloat(widthStr) * (sc>1 ? sc : 1);

      // Pass 1: sombra — omitir en render estático (noShadow)
      if (!noShadow) {
        c.save();
        c.strokeStyle = color;
        c.lineWidth = lw; c.lineCap='round'; c.lineJoin='round';
        c.shadowColor='rgba(0,0,0,.75)'; c.shadowBlur=lw*1.8;
        c.shadowOffsetX=1.5; c.shadowOffsetY=1.5;
        for (const obj of group) {
          c.beginPath();
          obj.points.forEach((p,i) => {
            const px=p.x*cp+ox, py=p.y*cp+oy;
            i===0 ? c.moveTo(px,py) : c.lineTo(px,py);
          });
          c.stroke();
        }
        c.restore();
      }

      // Pass 2: línea sólida
      c.save();
      c.strokeStyle=color; c.lineWidth=lw; c.lineCap='round'; c.lineJoin='round';
      for (const obj of group) {
        c.beginPath();
        obj.points.forEach((p,i) => {
          const px=p.x*cp+ox, py=p.y*cp+oy;
          i===0 ? c.moveTo(px,py) : c.lineTo(px,py);
        });
        c.stroke();
      }
      c.restore();
    }
  }

  function drawObj(c, obj, sc, ox, oy, selectedId, movingId) {
    const cp = sc * CELL;
    const sel    = obj.id === selectedId;
    const moving = obj.id === movingId;

    if (obj.type === 'floor') {
      const sx=obj.x*cp+ox, sy=obj.y*cp+oy, sw=obj.w*cp, sh=obj.h*cp;
      c.save();
      if (moving) { c.shadowColor='rgba(255,200,60,.9)'; c.shadowBlur=14; }
      else if (sel) { c.shadowColor='rgba(124,106,240,.7)'; c.shadowBlur=10; }
      c.fillStyle=obj.color; c.fillRect(sx,sy,sw,sh);
      if (obj.border) { c.strokeStyle=obj.borderColor; c.lineWidth=Math.max(1,sc); c.strokeRect(sx,sy,sw,sh); }
      if (moving) { c.strokeStyle='rgba(255,200,60,.9)'; c.lineWidth=1.5; c.setLineDash([4,3]); c.strokeRect(sx,sy,sw,sh); c.setLineDash([]); }
      c.restore();
    }
    if (obj.type === 'stair') {
      const orient = obj.orient || 'h';
      const pw = orient==='h' ? obj.w*cp : obj.h*cp;
      const ph = orient==='h' ? obj.h*cp : obj.w*cp;
      const sx=obj.x*cp+ox, sy=obj.y*cp+oy;
      c.save();
      if (moving) { c.shadowColor='rgba(255,200,60,.9)'; c.shadowBlur=12; }
      else if (sel) { c.shadowColor='rgba(124,106,240,.7)'; c.shadowBlur=8; }
      drawStairShape(c,sx,sy,pw,ph,obj.color,sc,orient);
      if (moving) { c.strokeStyle='rgba(255,200,60,.9)'; c.lineWidth=1.5; c.setLineDash([4,3]); c.strokeRect(sx,sy,pw,ph); c.setLineDash([]); }
      c.restore();
    }
    if (obj.type === 'line') {
      c.save();
      c.strokeStyle=obj.color; c.lineWidth=obj.width*(sc>1?sc:1); c.lineCap='round'; c.lineJoin='round';
      if (moving) { c.shadowColor='rgba(255,200,60,.9)'; c.shadowBlur=10; c.shadowOffsetX=0; c.shadowOffsetY=0; }
      else if (sel) { c.shadowColor='rgba(124,106,240,.9)'; c.shadowBlur=8; c.shadowOffsetX=0; c.shadowOffsetY=0; }
      c.beginPath();
      obj.points.forEach((p,i) => { const px=p.x*cp+ox, py=p.y*cp+oy; i===0?c.moveTo(px,py):c.lineTo(px,py); });
      c.stroke();
      c.restore();
    }
    if (obj.type === 'door' || obj.type === 'ddoor') {
      const horiz = obj.orient === 'h';
      const sx=obj.x*cp+ox, sy=obj.y*cp+oy;
      const sw = horiz ? obj.size*cp : 0.5*cp;
      const sh = horiz ? 0.5*cp : obj.size*cp;
      c.save();
      if (moving) { c.shadowColor='rgba(255,200,60,.9)'; c.shadowBlur=10; }
      else if (sel) { c.shadowColor='rgba(124,106,240,.7)'; c.shadowBlur=7; }
      drawDoorShape(c,sx,sy,sw,sh,obj.color,horiz, obj.type==='ddoor', obj.arrow||null, sc);
      if (moving) { c.strokeStyle='rgba(255,200,60,.9)'; c.lineWidth=1.5; c.setLineDash([4,3]); c.strokeRect(sx,sy,sw,sh); c.setLineDash([]); }
      c.restore();
    }
    if (obj.type === 'object') {
      drawObjectShape(c, obj.points, sc*CELL, ox, oy, sc, sel, moving, obj.color, obj.filled);
    }
    if (obj.type === 'label') {
      const sx=obj.x*cp+ox, sy=obj.y*cp+oy;
      const fs = obj.size * Math.max(0.5,sc);
      c.save();
      c.font=`500 ${fs}px 'Segoe UI',sans-serif`;
      c.fillStyle=obj.color; c.textAlign='left'; c.textBaseline='top';
      c.shadowColor='rgba(0,0,0,.9)'; c.shadowBlur=5;
      if (moving) { c.shadowColor='rgba(255,200,60,.9)'; c.shadowBlur=10; }
      else if (sel) { c.shadowColor='rgba(124,106,240,.9)'; c.shadowBlur=9; }
      c.fillText(obj.text,sx,sy);
      c.restore();
    }
  }

  // ── Render principal ──────────────────────────────────────
  // Dibuja editorData sobre un canvas ya dimensionado.
  // opts.selectedId / opts.movingId: para highlight interactivo (editor)
  // opts.noShadow: true para render estático limpio (pinner, bundle)
  // opts.showGrid: boolean
  // opts.viewMode: 'normal' | 'satellite' | 'blueprint'
  // Los parámetros sc/ox/oy son los del viewport actual del canvas.
  function renderToCanvas(canvas, editorData, opts) {
    const { objects=[], bgColor='#1e1e1e', gridColor='#2e2e2e' } = editorData;
    const {
      sc=1, ox=0, oy=0,
      showGrid=false,
      noShadow=false,
      selectedId=null,
      movingId=null,
      viewMode='normal'
    } = opts || {};

    const c   = canvas.getContext('2d');
    const w   = canvas.width;
    const h   = canvas.height;

    c.setTransform(1,0,0,1,0,0);
    c.clearRect(0,0,w,h);
    c.fillStyle = bgColor;
    c.fillRect(0,0,w,h);

    const gridBefore = viewMode !== 'blueprint';
    if (showGrid && gridBefore) drawGrid(c,w,h,sc,ox,oy,gridColor);

    const sorted = [...objects].sort((a,b) => {
      const order = { floor:0, object:1, stair:2, line:3, door:4, ddoor:4, label:5 };
      return (order[a.type]||0) - (order[b.type]||0);
    });

    drawAllFloors(c, sorted, sc, ox, oy, selectedId, movingId);

    for (const obj of sorted) {
      if (obj.type !== 'line' && obj.type !== 'floor')
        drawObj(c, obj, sc, ox, oy, selectedId, movingId);
    }
    for (const obj of sorted) {
      if (obj.type === 'floor' && (obj.id===selectedId || obj.id===movingId))
        drawObj(c, obj, sc, ox, oy, selectedId, movingId);
    }

    drawAllWalls(c, sorted, sc, ox, oy, selectedId, movingId, noShadow);

    for (const obj of sorted) {
      if (obj.type === 'line' && (obj.id===selectedId || obj.id===movingId))
        drawObj(c, obj, sc, ox, oy, selectedId, movingId);
    }

    if (viewMode === 'blueprint' && showGrid) {
      drawGrid(c,w,h,sc,ox,oy,'#6a8aff');
    }
  }

  // Render auto-fit a dataURL. Devuelve una Promise<string>.
  // opts.scale: multiplicador de resolución (default 2)
  // opts.pad: padding en px antes del scale (default 60)
  // opts.showGrid: boolean (default false)
  // opts.noShadow: boolean (default true — limpio para fondo estático)
  function renderToDataUrl(editorData, opts) {
    return new Promise((resolve, reject) => {
      const { objects=[], bgColor='#1e1e1e', gridColor='#2e2e2e' } = editorData;
      const scale    = (opts && opts.scale)    || 2;
      const pad      = (opts && opts.pad)      || 60;
      const showGrid = (opts && opts.showGrid) || false;
      const noShadow = (opts && opts.noShadow !== undefined) ? opts.noShadow : true;

      const bounds = getContentBounds(objects);
      if (!bounds) { reject(new Error('No hay objetos para renderizar')); return; }

      const contentW = (bounds.maxX - bounds.minX) * CELL;
      const contentH = (bounds.maxY - bounds.minY) * CELL;
      const exportW  = Math.ceil((contentW + pad*2) * scale);
      const exportH  = Math.ceil((contentH + pad*2) * scale);

      const offscreen = document.createElement('canvas');
      offscreen.width  = exportW;
      offscreen.height = exportH;

      const sc = scale;
      const ox = (pad - bounds.minX * CELL) * scale;
      const oy = (pad - bounds.minY * CELL) * scale;

      renderToCanvas(offscreen, editorData, { sc, ox, oy, showGrid, noShadow, selectedId:null, movingId:null });

      resolve(offscreen.toDataURL('image/png'));
    });
  }

  return { CELL, renderToCanvas, renderToDataUrl, getContentBounds,
           _drawDoorShape: drawDoorShape, _drawStairShape: drawStairShape,
           _drawObjectShape: drawObjectShape };

})();

// viewer-app.js — arranque del visor (solo lectura)

const App = (() => {

  function markDirty() {}

  let currentMode = null;

  function toast(msg) {
    const el = document.getElementById('toast');
    if (!el) return;
    el.textContent = msg;
    el.classList.add('show');
    setTimeout(() => el.classList.remove('show'), 2500);
  }

  // ── Carga del main_save ───────────────────────────────────
  async function loadMainSave() {
    try {
      const res  = await fetch('/api/main_save');
      const data = await res.json();
      return (data && data.exists !== false) ? data : null;
    } catch {
      return null;
    }
  }

  function normalizeProjectData(data) {
    if (data && data.objects && !data.editor) {
      return {
        editor: {
          bgColor:    data.bgColor    ?? '#1e1e1e',
          gridColor:  data.gridColor  ?? '#000000',
          refX:       data.refX       ?? 0,
          refY:       data.refY       ?? 0,
          refScale:   data.refScale   ?? 1,
          refOpacity: data.refOpacity ?? 0.5,
          objects:    data.objects    ?? [],
        },
        pinner: data.pinner || [],
        steps:  data.steps  || null,
      };
    }
    return data;
  }

  function setMode(mode) {
    if (mode === currentMode) return;
    const prev = currentMode;
    currentMode = mode;

    if (prev === 'pins') Pinner.deactivate();
    if (prev === 'steps') ViewerSteps.deactivate();

    document.querySelectorAll('.btn-mode').forEach(b => b.classList.remove('active'));

    const pinnerRight = document.getElementById('pinner-right');
    const showBtn     = document.getElementById('btn-show-pr');

    if (mode === 'pins') {
      pinnerRight.style.display = 'flex';
      if (showBtn) showBtn.style.display = 'none';
      document.getElementById('btn-viewer-pins').classList.add('active');
      Pinner.activate();
    } else {
      pinnerRight.style.display = 'none';
      if (showBtn) showBtn.style.display = 'none';
      document.getElementById('btn-viewer-steps').classList.add('active');
      ViewerSteps.activate();
    }
  }

  // ── Viewer Views dropdown ────────────────────────────────
  function buildViewerViews() {
    const data = Editor.getData();
    const views = data.views || [];
    const activeViewId = data.activeViewId;
    const dd = document.getElementById('viewer-views-dropdown');
    if (!dd) return;
    dd.innerHTML = '';
    if (views.length === 0) {
      dd.innerHTML = '<div class="vv-empty">No views saved</div>';
      return;
    }
    views.forEach(v => {
      const item = document.createElement('div');
      item.className = 'vv-item' + (v.id === activeViewId ? ' active' : '');
      const icon = document.createElement('img');
      icon.src = v.iconSvg || '/icons/tools/door.svg';
      icon.width = 16; icon.height = 16;
      const name = document.createElement('span');
      name.textContent = v.name;
      item.appendChild(icon);
      item.appendChild(name);
      item.onclick = () => {
        Editor.activateView(v.id);
        // Refresh active state in dropdown
        dd.querySelectorAll('.vv-item').forEach(el => el.classList.remove('active'));
        item.classList.add('active');
        // Update views count in the button
        updateViewsBtn();
        dd.classList.add('hidden');
      };
      dd.appendChild(item);
    });
  }

  function updateViewsBtn() {
    const data = Editor.getData();
    const views = data.views || [];
    const btn = document.getElementById('viewer-views-btn');
    if (btn) btn.textContent = `Views (${views.length}) ▾`;
  }

  function initViewerViews() {
    buildViewerViews();
    updateViewsBtn();
    const btn = document.getElementById('viewer-views-btn');
    const dd  = document.getElementById('viewer-views-dropdown');
    if (btn && dd) {
      btn.addEventListener('click', e => {
        e.stopPropagation();
        dd.classList.toggle('hidden');
        buildViewerViews(); // refresh in case views changed
        updateViewsBtn();
      });
      document.addEventListener('click', () => dd.classList.add('hidden'));
      dd.addEventListener('click', e => e.stopPropagation());
    }
  }

  // ── Init ──────────────────────────────────────────────────
  async function init() {
    Editor.init();
    Pinner.init();
    ViewerSteps.init();

    const raw  = await loadMainSave();
    const data = raw ? normalizeProjectData(raw) : null;

    const loading = document.getElementById('viewer-loading');
    const error   = document.getElementById('viewer-error');
    const shell   = document.getElementById('app-shell');

    if (!data) {
      loading.classList.add('hidden');
      error.classList.remove('hidden');
      return;
    }

    Editor.loadData(data.editor || null);
    Pinner.loadData(data.pinner || []);
    ViewerSteps.loadData(data.steps || null);

    loading.classList.add('hidden');
    shell.style.display = 'flex';

    // Default to pins mode
    setMode('pins');

    const nameEl = document.getElementById('viewer-project-name');
    if (nameEl && data.name) nameEl.textContent = data.name;

    document.getElementById('btn-viewer-pins')?.addEventListener('click', () => setMode('pins'));
    document.getElementById('btn-viewer-steps')?.addEventListener('click', () => setMode('steps'));

    document.querySelectorAll('#app-shell .view-mode-btn').forEach(btn => {
      btn.addEventListener('click', () => Editor.setViewMode(btn.dataset.mode));
    });

    // Init viewer views after data is loaded
    initViewerViews();
  }

  document.addEventListener('DOMContentLoaded', init);

  return { markDirty, toast };

})();

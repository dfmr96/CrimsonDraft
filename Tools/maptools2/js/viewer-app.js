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
  }

  document.addEventListener('DOMContentLoaded', init);

  return { markDirty, toast };

})();

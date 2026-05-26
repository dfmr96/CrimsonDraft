// app.js — arranque, modo switching, save/load

const App = (() => {

  let currentMode = 'editor';
  let isDirty     = false;
  let projectLoaded = false;

  // ── Init ─────────────────────────────────────────────────
  async function init(){
    Editor.init();
    Pinner.init();
    Steps.init();
    showStartup();
  }

  // ── Startup ──────────────────────────────────────────────
  function showStartup(){
    document.getElementById('startup').style.display = 'flex';
    document.getElementById('app-shell').style.display = 'none';
  }

  function hideStartup(){
    document.getElementById('startup').style.display = 'none';
    document.getElementById('app-shell').style.display = 'flex';
  }

  // ── Entrar como editor — carga main_save automático ──────
  async function enterEditor(){
    const data = await loadMainSave();
    if(data){
      loadProjectData(data);
      hideStartup();
      projectLoaded = true;
      setMode('editor');
      toast('main_save loaded');
    } else {
      // Si no hay main_save, pedir un archivo
      document.getElementById('json-load-input').click();
    }
  }

  // ── Carga de main_save desde el servidor ─────────────────
  async function loadMainSave(){
    try {
      const res  = await fetch('/api/main_save');
      const data = await res.json();
      if(!data || data.exists === false) return null;
      if(!data.editor && !data.pinner && !data.objects) return null;
      return data;
    } catch {
      return null;
    }
  }

  function handleLoadProject(e){
    const file = e.target.files[0];
    if(!file) return;
    const reader = new FileReader();
    reader.onload = function(ev){
      try {
        const data = JSON.parse(ev.target.result);
        if(!data.editor && !data.pinner && !data.objects) {
          toast('The file does not contain a valid project');
          return;
        }
        loadProjectData(data);
        hideStartup();
        projectLoaded = true;
        setMode('editor');
        isDirty = false;
        updateSaveDot();
        toast('Project loaded: ' + file.name);
      } catch(err) {
        toast('Error reading file');
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  }

  function loadProjectData(data){
    if(data && data.objects && !data.editor){
      data = {
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
    Editor.loadData(data.editor || null);
    Pinner.loadData(data.pinner || []);
    Steps.loadData(data.steps || null);
    isDirty = false;
    updateSaveDot();
  }

  // ── Mode switching ────────────────────────────────────────
  function setMode(mode){
    const prevMode = currentMode;
    currentMode = mode;

    const layersPanel    = document.getElementById('layers-panel');
    const pinnerPanel    = document.getElementById('pinner-panel');
    const pinnerRight    = document.getElementById('pinner-right');
    const pinCreatePanel = document.getElementById('pin-create-panel');
    const showBtn        = document.getElementById('btn-show-pr');
    const editorToolbar  = document.getElementById('editor-toolbar');

    // Deactivate previous mode
    if(prevMode === 'pinner') Pinner.deactivate();
    if(prevMode === 'steps')  Steps.deactivate();
    if(prevMode === 'editor') Editor.deactivate();

    // Reset buttons
    document.querySelectorAll('.btn-mode').forEach(b=>b.classList.remove('active'));

    if(mode === 'editor'){
      if(editorToolbar) editorToolbar.classList.remove('hidden');
      layersPanel.classList.remove('hidden');
      pinnerPanel.classList.add('hidden');
      pinnerRight.style.display = 'none';
      if(showBtn) showBtn.style.display = 'none';
      pinCreatePanel?.classList.add('hidden');
      document.getElementById('coords').style.display = '';

      document.getElementById('btn-mode-editor').classList.add('active');
      document.getElementById('hint').textContent = 'Click to select · Drag to move · Del to delete';

      Editor.activate();

    } else if(mode === 'pinner'){
      if(editorToolbar) editorToolbar.classList.add('hidden');
      layersPanel.classList.add('hidden');
      pinnerPanel.classList.add('hidden');
      pinnerRight.style.display = 'flex';
      if(showBtn) showBtn.style.display = 'none';
      document.getElementById('coords').style.display = 'none';

      document.getElementById('btn-mode-pinner').classList.add('active');
      document.getElementById('hint').textContent = 'Right-click on the map to place a pin';

      Pinner.activate();

    } else if(mode === 'steps'){
      if(editorToolbar) editorToolbar.classList.add('hidden');
      layersPanel.classList.add('hidden');
      pinnerRight.style.display = 'none';
      if(showBtn) showBtn.style.display = 'none';
      pinCreatePanel?.classList.add('hidden');
      document.getElementById('coords').style.display = 'none';

      document.getElementById('btn-mode-steps').classList.add('active');
      document.getElementById('hint').textContent = 'Click a pin to toggle visibility for the active step';

      Steps.activate();
    }
  }

  // ── Save / Export ─────────────────────────────────────────
  function markDirty(){
    if(!isDirty){ isDirty = true; updateSaveDot(); }
  }

  function exportProject(){
    const data = {
      editor: Editor.getData(),
      pinner: Pinner.getData(),
      steps: Steps.getData(),
    };
    const json = JSON.stringify(data, null, 2);
    const blob = new Blob([json], {type: 'application/json'});
    const a = document.createElement('a');
    a.download = 'project-' + Date.now() + '.json';
    a.href = URL.createObjectURL(blob);
    a.click();
    URL.revokeObjectURL(a.href);
    isDirty = false;
    updateSaveDot('saved');
    setTimeout(()=>updateSaveDot(), 1500);
    toast('Project exported');
  }

  function updateSaveDot(state){
    const dot = document.getElementById('save-dot');
    if(!dot) return;
    dot.className = 'save-dot';
    if(state==='saved') dot.classList.add('saved');
    else if(state==='error') dot.classList.add('dirty');
    else if(isDirty) dot.classList.add('dirty');
  }

  // ── Toast ─────────────────────────────────────────────────
  function toast(msg){
    const el = document.getElementById('toast');
    if(!el) return;
    el.textContent = msg;
    el.classList.add('show');
    setTimeout(()=>el.classList.remove('show'), 2500);
  }

  // ── DOM listeners ─────────────────────────────────────────
  document.addEventListener('DOMContentLoaded', ()=>{
    init();

    document.getElementById('btn-open-project')?.addEventListener('click', enterEditor);
    document.getElementById('btn-mode-editor')?.addEventListener('click', ()=>setMode('editor'));
    document.getElementById('btn-mode-pinner')?.addEventListener('click', ()=>setMode('pinner'));
    document.getElementById('btn-mode-steps')?.addEventListener('click', ()=>setMode('steps'));
    document.getElementById('btn-save-now')?.addEventListener('click', exportProject);
    document.getElementById('json-load-input')?.addEventListener('change', handleLoadProject);

    document.addEventListener('keydown', e=>{
      if((e.ctrlKey||e.metaKey)&&e.key==='s'){ e.preventDefault(); exportProject(); }
    });
  });

  return { markDirty, toast, setMode, exportProject };

})();

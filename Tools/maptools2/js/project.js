// project.js — comunicación con el servidor para leer y escribir project.json

const Project = (() => {

  // Carga el proyecto desde disco.
  // Devuelve el objeto JSON o null si no existe todavía.
  async function load() {
    try {
      const res  = await fetch('/api/project');
      const data = await res.json();
      if (data.exists === false) return null;
      return data;
    } catch {
      return null;
    }
  }

  // Guarda el proyecto en disco.
  // data: { editor: {...}, pinner: { pins: [...] } }
  async function save(data) {
    try {
      const res = await fetch('/api/project', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(data)
      });
      const result = await res.json();
      return result.ok === true;
    } catch {
      return false;
    }
  }

  // Borra el proyecto (resetea a vacío en disco)
  async function reset() {
    return save({ exists: false });
  }

  return { load, save, reset };

})();

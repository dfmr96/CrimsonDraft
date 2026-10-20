# Comparación de versiones de PlayStation

> Funciones comparadas con `psx_funcmatch.py`: se busca cada función de la Beta 2 (con símbolos) en las otras versiones, enmascarando los inmediatos y las direcciones de salto. "N/48" = instrucciones que coinciden.

| Versión | `Debug_menu` | `Padd2_menu` | Otros restos |
|---|---|---|---|
| **Beta 2** | Presente (Select en el mando 1) | Presente | `MAIN.SYM`, `MAIN.CPE`, cabeceras |
| **Prototipo de noviembre de 1996** | — | Coincidencia parcial (25/48) | — |
| **Comercial USA** (`SLUS_007.48`, Dual Shock) | **Eliminado** (7/48: ruido) | **Código muerto** (45/48 en `0x80013BBC`, sin llamadas) | Rutas `d:/bio2/room/...` en texto; llamada a `SetGraphDebug` |

## Conclusiones

- La versión comercial conserva `Padd2_menu` compilado pero desconectado: no hay forma de activarlo sin parchear.
- El menú de Select se quitó antes de publicar.
- En PC tampoco hay menú de depuración → [restos en PC](restos-debug-pc.md).

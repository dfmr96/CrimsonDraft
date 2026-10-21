# Cámara al entrar en una sala

## Regla

**La decide la puerta.** Cada `door_aot_se` guarda:

| Campo | Contenido |
|---|---|
| `NextStage` | Etapa de destino (**base 0**: 0 = etapa 1) |
| `NextRoom` | Sala de destino |
| **`NextCamera`** | **Cámara inicial** de la sala de destino |
| `NextFloor` | Planta |
| Llegada x / y / z, dirección | Dónde y cómo aparece el jugador |

## Verificación

Cruce de las **552 puertas** del escenario de Leon: en **539 (97,6%)** la `NextCamera` es la cámara cuya área (primer registro RVD) contiene el punto de llegada. Las 13 restantes son casos especiales (cámara 11 en algunas transiciones, posiblemente escenas como ascensores) o cuadriláteros que la prueba geométrica no resolvió.

## Al cargar la sala (`0x4C4600`)

1. Se toma la cámara que trae la puerta (pasa por `0x9944EA` → `0x999118`).
2. Se guarda el área de esa cámara (`0x992C30`).
3. Desde el primer frame funciona el cambio por zonas normal.

No se busca la cámara por posición: es un **dato puesto a mano** en cada puerta, coherente con las áreas.

# Referencia GD — Dispersión y Recoil

> Para diseñar los stats de una nueva arma, define los valores de cada variable en las tres capas. El doc técnico completo es [[Sistema de Dispersión y Apuntado]].

---

## Las tres capas

Cada disparo pasa por tres capas en orden. Cada capa desplaza el punto de impacto desde donde lo dejó la anterior.

---

### Capa 1 — Dispersión por estado del operador

**Cuándo actúa:** solo en el **primer disparo** de cada ráfaga.

**Qué hace:** desplaza el punto de intención del jugador dentro de un círculo. Cuanto más daño ha recibido el operador, más grande es ese círculo.

**Variables:**

| Variable | Descripción | Efecto de subir el valor |
|---|---|---|
| `dispersion_base` | Radio del círculo a HP 100% (px) | El arma es menos precisa desde el primer disparo |
| `HP_FACTOR` | Multiplicador máximo del radio a HP 0% | A más valor, más castiga el daño recibido. **Fijo en 2.0** |

**Ejemplo:** con `dispersion_base = 12` y `HP_FACTOR = 2.0`, el radio va de 12 px (HP lleno) a 24 px (HP vacío).

---

### Capa 2 — Imperfección mecánica del arma

**Cuándo actúa:** en **todos los disparos**, siempre.

**Qué hace:** añade un desplazamiento aleatorio uniforme en ambos ejes. No acumula entre disparos — es ruido independiente en cada uno.

**Variable:**

| Variable | Descripción | Efecto de subir el valor |
|---|---|---|
| `weapon_deviation` | Margen de ruido mecánico (±px) | El arma tiembla más en cada disparo, incluso a HP 100% |

**Ejemplo:** con `weapon_deviation = 2`, cada disparo se desplaza hasta ±2 px en X y hasta ±2 px en Y de forma independiente.

---

### Capa 3 — Patrón de recoil

**Cuándo actúa:** desde el **segundo disparo** en adelante. El primer disparo tiene recoil (0, 0).

**Qué hace:** aplica un desplazamiento acumulado disparo a disparo siguiendo una tabla predefinida. El patrón define la "forma" del recoil del arma. Al llegar al último paso de la tabla, ese valor se repite indefinidamente.

El componente horizontal `dx` se **invierte automáticamente para operadores zurdos**.

**Variables:**

| Variable | Descripción | Efecto de subir el valor |
|---|---|---|
| `recoil_pattern[]` | Tabla de desplazamientos `(dx, dy)` por disparo | Define la forma y magnitud del recoil (ver abajo) |
| `pattern_spread` | Aleatoriedad sobre cada paso del patrón (±px) | El patrón se vuelve menos predecible; dificulta la compensación |

**Cómo leer la tabla `recoil_pattern`:**

- `dy` negativo → el impacto sube en pantalla
- `dy` positivo → el impacto baja en pantalla
- `dx` positivo → deriva a la derecha (para diestros)
- `dx` negativo → deriva a la izquierda (para diestros)
- Disparo 1 siempre es `(0, 0)` — sin recoil en el primer disparo

---

## Tabla comparativa de armas

| Variable | P229 | MP5 | Mk18 | Benelli M4 |
|---|---|---|---|---|
| `dispersion_base` | 12 px | 14 px | 6 px | 40 px |
| `weapon_deviation` | ±2 px | ±2 px | ±1 px | ±3 px |
| `pattern_spread` | ±2 px | ±3 px | ±2 px | ±4 px |
| Pasos de patrón | 13 | 29 | 29 | 7 |
| Forma del recoil | "7" — sube y deriva derecha | "I" — subida vertical, leve deriva | "J invertida" — curva agresiva izquierda | "V invertida" — patada brutal, cae derecha |
| Compensación del jugador | Abajo-izquierda | Casi solo abajo | Abajo-derecha | Fuerte abajo desde inicio |
| Perfil general | Pistola de control medio | Subfusil predecible | Rifle de asalto técnico | Escopeta de alto riesgo |

---

## Guía para crear una nueva arma

1. **Decidir el perfil general:** ¿cuánto castiga el daño recibido? → `dispersion_base`
2. **Decidir la "estabilidad base":** ¿cómo se comporta incluso sin disparar mucho? → `weapon_deviation`
3. **Diseñar la forma del recoil:** dibujar mentalmente la curva de disparos consecutivos → tabla `recoil_pattern`
   - Pocas filas = se estabiliza rápido o es de disparo lento (escopeta, sniper)
   - Muchas filas = el arma escala el recoil disparo a disparo (SMG, AR)
4. **Decidir si el recoil es compensable:** a menor `pattern_spread`, más fácil de dominar por el jugador

---

## Variables que NO son modificables por arma

| Variable | Valor fijo | Por qué |
|---|---|---|
| `HP_FACTOR` | 2.0 | Escala global del castigo por daño — cambiarlo afecta todas las armas |
| Distribución dentro del círculo | Uniforme por área | Garantiza que el centro no sea favorecido injustamente |
| Inversión dx para zurdos | Automática | Comportamiento del sistema, no del arma |
| Repetición del último paso al agotar el patrón | Automática | El patrón siempre termina en un estado definido |

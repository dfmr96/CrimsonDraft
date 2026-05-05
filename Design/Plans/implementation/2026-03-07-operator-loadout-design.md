# Operator Loadout Persistence — Design

**Goal:** Arma equipada y balas actuales viven en el item (no en el operador), los operadores se definen como assets, y Navigation y Combat leen el mismo estado sin duplicación.

**Date:** 2026-03-07

---

## Problema actual

- `OperatorRuntime` trackea `Ammo`/`MaxAmmo` por operador — no por arma. Si un operador cambia de arma, pierde el conteo previo.
- `DefaultOperatorRosterSeedProvider` crea `OperatorData` en blanco en runtime (sin nombres reales).
- `InventoryDebugSeeder` agrega items de debug via MonoBehaviour — no es gameplay real.
- `InventoryItem` no tiene cantidad de balas ni cantidad de cajas.

---

## Modelo de datos aprobado

### ScriptableObjects (data assets)

```
ItemData (base)           → itemId, itemType, displayName
  └─ WeaponData           → caliber, magazineCapacity
  └─ AmmoBoxData          → caliber, defaultQuantity
  └─ ConsumableData       → (vacío por ahora)

OperatorData              → operatorId, displayName, sprite
```

`OperatorData` agrega `displayName` (nombre para UI, separado de `operatorId` que es clave interna).

`ItemData` se subclasea: `WeaponData` tiene `caliber` y `magazineCapacity`; `AmmoBoxData` tiene `caliber` y `defaultQuantity`. `magazineCapacity` en `WeaponData` (no en `ItemData` base) porque es una propiedad del tipo de arma, irrelevante para ammo boxes y consumables.

### Runtime items

```
InventoryItem (base)              → Data: ItemData, EquippedBySlot: int
  └─ WeaponItem : IWeaponSlot     → CurrentAmmo (init = magazineCapacity)
  └─ AmmoBoxItem                  → Quantity (init = defaultQuantity o override del loadout)
  └─ ConsumableItem               → (vacío)
```

### Interfaz en CrimsonDraft.Operators (evita dependencia circular)

```
IWeaponSlot
  string Caliber      { get; }
  int    CurrentAmmo  { get; }
  int    MaxAmmo      { get; }
  void   SetAmmo(int value)
```

`WeaponItem` (en `CrimsonDraft.Inventory`) implementa `IWeaponSlot`.
`OperatorRuntime` (en `CrimsonDraft.Operators`) referencia `IWeaponSlot?` — sin dependencia circular.

---

## OperatorRuntime

**Eliminar:** `Ammo`, `MaxAmmo`, `Reload()`, `ConsumeAmmo(int)`

**Agregar:**
```
IWeaponSlot? EquippedWeapon  { get; private set; }
void SetEquippedWeapon(IWeaponSlot? weapon)
```

Lecturas de ammo en combat: `roster[slot].EquippedWeapon?.CurrentAmmo ?? 0`

---

## Starting Loadout (reemplaza DefaultOperatorRosterSeedProvider + InventoryDebugSeeder)

```
StartingLoadout : ScriptableObject
  OperatorData?[4]     operatorSlots      → qué operador ocupa cada slot (null = vacío)
  StartingItemEntry[]  items              → inventario compartido inicial

StartingItemEntry (serializable struct)
  ItemData   item
  int        quantity    → para AmmoBox: balas iniciales en la caja
                           para Weapon: ignorado (siempre inicia con magazineCapacity)
```

**Reemplazos:**

| Eliminado | Reemplazado por |
|---|---|
| `DefaultOperatorRosterSeedProvider` | `StartingLoadoutRosterSeedProvider` (lee `operatorSlots`) |
| `InventoryDebugSeeder` | `InventoryBootstrap : IInitializable` (lee `items`) |

`OperatorRosterSeed` elimina `DefaultAmmo` (ya no existe ammo por operador).

`NavigationScope` recibe `StartingLoadout` como `[SerializeField]` en el componente del scope.

---

## InventoryService: factory y operaciones

### AddItem(ItemData data, int quantity = 0)

```
WeaponData   → new WeaponItem(data)      CurrentAmmo = magazineCapacity
AmmoBoxData  → new AmmoBoxItem(data)     Quantity = quantity
default      → new ConsumableItem(data)
```

### EquipWeapon(int itemIndex, int operatorSlot)

```
1. Desequipar arma anterior del slot (si existe): item.EquippedBySlot = -1, roster[slot].SetEquippedWeapon(null)
2. item.EquippedBySlot = operatorSlot
3. roster[operatorSlot].SetEquippedWeapon(item as IWeaponSlot)
```

### UnequipWeapon(int itemIndex)

```
1. slot = item.EquippedBySlot
2. item.EquippedBySlot = -1
3. roster[slot].SetEquippedWeapon(null)
```

### CanReload(int ammoBoxIndex, int operatorSlot)

```
- item es AmmoBoxItem
- caliber del box coincide con caliber del arma equipada
- operador vivo
- weapon.CurrentAmmo < weapon.MaxAmmo
```

### ReloadOperator(int ammoBoxIndex, int operatorSlot)

```
rounds = weapon.MaxAmmo - weapon.CurrentAmmo
weapon.SetAmmo(weapon.MaxAmmo)
box.Quantity -= rounds
if box.Quantity <= 0: remover box del inventario
```

Una caja de 99 balas puede recargar múltiples veces hasta agotarse.

---

## Combat: cambios de lectura/escritura de ammo

| Antes | Después |
|---|---|
| `roster[slot].Ammo` | `roster[slot].EquippedWeapon?.CurrentAmmo ?? 0` |
| `roster[slot].MaxAmmo` | `roster[slot].EquippedWeapon?.MaxAmmo ?? 0` |
| `roster[slot].ConsumeAmmo(shots)` | `roster[slot].EquippedWeapon?.SetAmmo(current - shots)` |
| `roster[slot].Reload()` | no aplica — recarga viene de InventoryService |

Sin cambios en lógica de daño (`ApplyDamage`, `HpRatio`, ECG).

---

## Dependencias entre assemblies

```
CrimsonDraft.Operators   → define IWeaponSlot, OperatorRuntime, OperatorData
CrimsonDraft.Inventory   → referencia Operators; WeaponItem implementa IWeaponSlot
CrimsonDraft.Navigation  → referencia ambos; InventoryBootstrap, StartingLoadout
CrimsonDraft.Combat      → referencia Operators; lee EquippedWeapon del roster
```

Sin cambios en el grafo de dependencias — solo se agrega `IWeaponSlot` al assembly de Operators.

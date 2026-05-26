# Beeper — MorseDecoder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar `MorseDecoder`, una clase C# pura que traduce pulsaciones cortas/largas a letras Morse letra por letra y acumula una palabra.

**Architecture:** Un `Dictionary<string, char>` estático mapea secuencias de símbolos (`.` y `-`) a letras. El estado es un `StringBuilder` para la letra en progreso y una `List<char>` para la palabra confirmada. El timing de pulsación vive fuera de esta clase — el llamador decide si llamar `InputDot()` o `InputDash()`.

**Tech Stack:** C# puro · NUnit · Unity Test Runner (EditMode)

**Spec:** `Design/Plans/superpowers/specs/2026-05-25-beeper-morse-design.md`

---

## Archivos

| Acción | Ruta |
|--------|------|
| Crear  | `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs` |
| Crear  | `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs` |

No se requieren cambios en `.asmdef` — `CrimsonDraft.Tests.EditMode` ya referencia `CrimsonDraft.Navigation`.

---

## Task 1: Scaffold + InputDot / InputDash → CurrentSequence

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs`

- [ ] **Step 1: Escribir los tests que fallan**

Crear `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class MorseDecoderTests
    {
        private MorseDecoder _decoder = null!;

        [SetUp]
        public void SetUp() => _decoder = new MorseDecoder();

        [Test]
        public void InputDot_AppendsDotToCurrentSequence()
        {
            _decoder.InputDot();
            Assert.AreEqual(".", _decoder.CurrentSequence);
        }

        [Test]
        public void InputDash_AppendsDashToCurrentSequence()
        {
            _decoder.InputDash();
            Assert.AreEqual("-", _decoder.CurrentSequence);
        }

        [Test]
        public void MultipleInputs_BuildSequenceInOrder()
        {
            _decoder.InputDot();
            _decoder.InputDash();
            _decoder.InputDot();
            Assert.AreEqual(".-.", _decoder.CurrentSequence);
        }
    }
}
```

- [ ] **Step 2: Correr los tests — deben fallar (tipo no existe)**

En Unity: Window → General → Test Runner → EditMode → filtrar `MorseDecoderTests` → Run.
Resultado esperado: error de compilación o 3 failures "type not found".

- [ ] **Step 3: Crear la clase `MorseDecoder` con InputDot/InputDash**

Crear `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Text;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class MorseDecoder
    {
        private readonly StringBuilder _currentSequence = new();
        private readonly List<char>    _word            = new();

        public string              CurrentSequence => _currentSequence.ToString();
        public IReadOnlyList<char> Word            => _word;

        public void InputDot()  => _currentSequence.Append('.');
        public void InputDash() => _currentSequence.Append('-');

        public void Confirm()  { }
        public void Backspace() { }
        public void Reset()    { }
        public string GetWord() => "";
    }
}
```

- [ ] **Step 4: Correr los tests — deben pasar**

Resultado esperado: `InputDot_AppendsDotToCurrentSequence` PASS, `InputDash_AppendsDashToCurrentSequence` PASS, `MultipleInputs_BuildSequenceInOrder` PASS.

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs"
git commit -m "feat(beeper): MorseDecoder scaffold with InputDot/InputDash"
```

---

## Task 2: Confirm — happy path

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs`

- [ ] **Step 1: Agregar los tests que fallan**

Añadir al final de `MorseDecoderTests` (dentro de la clase):

```csharp
[Test]
public void Confirm_DotDash_AddsLetterA()
{
    _decoder.InputDot();
    _decoder.InputDash();
    _decoder.Confirm();
    Assert.AreEqual(1, _decoder.Word.Count);
    Assert.AreEqual('A', _decoder.Word[0]);
}

[Test]
public void Confirm_ClearsCurrentSequence()
{
    _decoder.InputDot();
    _decoder.InputDash();
    _decoder.Confirm();
    Assert.AreEqual("", _decoder.CurrentSequence);
}

[Test]
public void Confirm_SingleDash_AddsLetterT()
{
    _decoder.InputDash();
    _decoder.Confirm();
    Assert.AreEqual('T', _decoder.Word[0]);
}

[Test]
public void Confirm_ThreeDots_AddsLetterS()
{
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.Confirm();
    Assert.AreEqual('S', _decoder.Word[0]);
}
```

- [ ] **Step 2: Correr — deben fallar**

Resultado esperado: los 4 tests nuevos FAIL (Confirm() es no-op).

- [ ] **Step 3: Implementar `Confirm()` con el diccionario completo**

Reemplazar la clase `MorseDecoder` con:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Text;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class MorseDecoder
    {
        private static readonly Dictionary<string, char> s_table = new()
        {
            ["-"]    = 'T', ["."]    = 'E',
            ["--"]   = 'M', ["-."]   = 'N', [".-"]   = 'A', [".."]   = 'I',
            ["---"]  = 'O', ["--."]  = 'G', ["-.-"]  = 'K', ["-.."]  = 'D',
            [".--"]  = 'W', [".-."]  = 'R', ["..-"]  = 'U', ["..."]  = 'S',
            ["--.-"] = 'Q', ["--.."] = 'Z', ["-.--"] = 'Y', ["-.-."] = 'C',
            ["-..-"] = 'X', ["-..."] = 'B', [".---"] = 'J', [".--."]=  'P',
            [".-.."] = 'L', ["..-."] = 'F', ["...-"] = 'V', ["...."] = 'H',
        };

        private readonly StringBuilder _currentSequence = new();
        private readonly List<char>    _word            = new();

        public string              CurrentSequence => _currentSequence.ToString();
        public IReadOnlyList<char> Word            => _word;

        public void InputDot()  => _currentSequence.Append('.');
        public void InputDash() => _currentSequence.Append('-');

        public void Confirm()
        {
            var seq = _currentSequence.ToString();
            if (seq.Length == 0) return;
            if (!s_table.TryGetValue(seq, out var letter)) return;
            _word.Add(letter);
            _currentSequence.Clear();
        }

        public void Backspace() { }
        public void Reset()     { }
        public string GetWord() => "";
    }
}
```

- [ ] **Step 4: Correr todos los tests — deben pasar**

Resultado esperado: los 7 tests de Task 1 y Task 2 PASS.

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs"
git commit -m "feat(beeper): Confirm() con diccionario Morse de 26 letras"
```

---

## Task 3: Confirm — edge cases + Word + GetWord

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs`

- [ ] **Step 1: Agregar los tests que fallan**

Añadir a `MorseDecoderTests`:

```csharp
[Test]
public void Confirm_EmptySequence_DoesNothing()
{
    _decoder.Confirm();
    Assert.AreEqual(0, _decoder.Word.Count);
    Assert.AreEqual("", _decoder.CurrentSequence);
}

[Test]
public void Confirm_InvalidSequence_DoesNothing()
{
    // "....." no existe en el árbol
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.Confirm();
    Assert.AreEqual(0, _decoder.Word.Count);
    Assert.AreEqual(".....", _decoder.CurrentSequence);
}

[Test]
public void Confirm_MultipleLetters_BuildsWord()
{
    _decoder.InputDot();   // E = "."
    _decoder.Confirm();
    _decoder.InputDash();
    _decoder.InputDot();   // N = "-."
    _decoder.Confirm();
    Assert.AreEqual(2, _decoder.Word.Count);
    Assert.AreEqual('E', _decoder.Word[0]);
    Assert.AreEqual('N', _decoder.Word[1]);
}

[Test]
public void GetWord_ReturnsConfirmedLettersAsString()
{
    _decoder.InputDot();   // E
    _decoder.Confirm();
    _decoder.InputDash();
    _decoder.InputDot();   // N
    _decoder.Confirm();
    Assert.AreEqual("EN", _decoder.GetWord());
}
```

- [ ] **Step 2: Correr — deben fallar**

`Confirm_EmptySequence_DoesNothing` y `Confirm_InvalidSequence_DoesNothing` ya pasan (Confirm() tiene las guards). `Confirm_MultipleLetters_BuildsWord` pasa. `GetWord_ReturnsConfirmedLettersAsString` FAIL porque `GetWord()` devuelve `""`.

- [ ] **Step 3: Implementar `GetWord()`**

Reemplazar el método `GetWord()` en `MorseDecoder.cs`:

```csharp
public string GetWord() => new string(_word.ToArray());
```

- [ ] **Step 4: Correr todos los tests — deben pasar**

Resultado esperado: todos los tests hasta ahora PASS.

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs"
git commit -m "feat(beeper): GetWord() y cobertura de edge cases en Confirm"
```

---

## Task 4: Backspace

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs`

- [ ] **Step 1: Agregar los tests que fallan**

Añadir a `MorseDecoderTests`:

```csharp
[Test]
public void Backspace_WithCurrentSequenceNonEmpty_ClearsCurrentSequence()
{
    _decoder.InputDot();
    _decoder.InputDot();
    _decoder.Backspace();
    Assert.AreEqual("", _decoder.CurrentSequence);
    Assert.AreEqual(0, _decoder.Word.Count);
}

[Test]
public void Backspace_WithCurrentSequenceEmpty_RemovesLastWordLetter()
{
    _decoder.InputDot();   // E
    _decoder.Confirm();
    _decoder.InputDash();  // T
    _decoder.Confirm();
    _decoder.Backspace();
    Assert.AreEqual("E", _decoder.GetWord());
}

[Test]
public void Backspace_AllEmpty_DoesNothing()
{
    Assert.DoesNotThrow(() => _decoder.Backspace());
    Assert.AreEqual("", _decoder.CurrentSequence);
    Assert.AreEqual("", _decoder.GetWord());
}
```

- [ ] **Step 2: Correr — deben fallar**

Los 3 tests FAIL (Backspace() es no-op).

- [ ] **Step 3: Implementar `Backspace()`**

Reemplazar el método `Backspace()` en `MorseDecoder.cs`:

```csharp
public void Backspace()
{
    if (_currentSequence.Length > 0)
    {
        _currentSequence.Clear();
        return;
    }
    if (_word.Count > 0)
        _word.RemoveAt(_word.Count - 1);
}
```

- [ ] **Step 4: Correr todos los tests — deben pasar**

Resultado esperado: todos PASS.

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs"
git commit -m "feat(beeper): Backspace() — cancela secuencia o borra última letra"
```

---

## Task 5: Reset

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs`

- [ ] **Step 1: Agregar el test que falla**

Añadir a `MorseDecoderTests`:

```csharp
[Test]
public void Reset_ClearsCurrentSequenceAndWord()
{
    _decoder.InputDot();
    _decoder.InputDash();
    _decoder.Confirm();    // palabra = "A"
    _decoder.InputDot();   // secuencia en progreso = "."
    _decoder.Reset();
    Assert.AreEqual("", _decoder.CurrentSequence);
    Assert.AreEqual("", _decoder.GetWord());
}
```

- [ ] **Step 2: Correr — debe fallar**

`Reset_ClearsCurrentSequenceAndWord` FAIL (Reset() es no-op).

- [ ] **Step 3: Implementar `Reset()`**

Reemplazar el método `Reset()` en `MorseDecoder.cs`:

```csharp
public void Reset()
{
    _currentSequence.Clear();
    _word.Clear();
}
```

- [ ] **Step 4: Correr todos los tests — deben pasar**

Resultado esperado: todos PASS (≥ 15 tests en `MorseDecoderTests`).

- [ ] **Step 5: Commit**

```
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/MorseDecoder.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/MorseDecoderTests.cs"
git commit -m "feat(beeper): Reset() completa la implementación de MorseDecoder"
```

---

## Estado final de `MorseDecoder.cs`

Para referencia — al terminar el Task 5, el archivo completo debe quedar así:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Text;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class MorseDecoder
    {
        private static readonly Dictionary<string, char> s_table = new()
        {
            ["-"]    = 'T', ["."]    = 'E',
            ["--"]   = 'M', ["-."]   = 'N', [".-"]   = 'A', [".."]   = 'I',
            ["---"]  = 'O', ["--."]  = 'G', ["-.-"]  = 'K', ["-.."]  = 'D',
            [".--"]  = 'W', [".-."]  = 'R', ["..-"]  = 'U', ["..."]  = 'S',
            ["--.-"] = 'Q', ["--.."] = 'Z', ["-.--"] = 'Y', ["-.-."] = 'C',
            ["-..-"] = 'X', ["-..."] = 'B', [".---"] = 'J', [".--."]=  'P',
            [".-.."] = 'L', ["..-."] = 'F', ["...-"] = 'V', ["...."] = 'H',
        };

        private readonly StringBuilder _currentSequence = new();
        private readonly List<char>    _word            = new();

        public string              CurrentSequence => _currentSequence.ToString();
        public IReadOnlyList<char> Word            => _word;

        public void InputDot()  => _currentSequence.Append('.');
        public void InputDash() => _currentSequence.Append('-');

        public void Confirm()
        {
            var seq = _currentSequence.ToString();
            if (seq.Length == 0) return;
            if (!s_table.TryGetValue(seq, out var letter)) return;
            _word.Add(letter);
            _currentSequence.Clear();
        }

        public void Backspace()
        {
            if (_currentSequence.Length > 0)
            {
                _currentSequence.Clear();
                return;
            }
            if (_word.Count > 0)
                _word.RemoveAt(_word.Count - 1);
        }

        public void Reset()
        {
            _currentSequence.Clear();
            _word.Clear();
        }

        public string GetWord() => new string(_word.ToArray());
    }
}
```

# 🚀 GUÍA: Cómo usar SofaScore en vivo para predicciones en tiempo real

## 📋 Resumen de lo que se agregó

Se han creado 3 componentes principales para conectar **SofaScore API** en vivo a Oloráculo:

| Archivo | Ubicación | Propósito |
|---------|-----------|----------|
| **SofascoreModels.cs** | `Oloraculo.Web/Models/` | Modelos de datos para eventos en vivo |
| **LiveMatchService.cs** | `Oloraculo.Web/Services/` | Lógica de conexión y cálculos de probabilidad |
| **LiveMatchMonitor.razor** | `Oloraculo.Web/Components/Pages/` | UI Blazor para visualizar datos en vivo |
| **appsettings.json** | `Oloraculo.Web/` | Configuración actualizada |

---

## 🔧 PASO 1: Registrar el servicio en Program.cs

Abre `Oloraculo.Web/Program.cs` y agrega estas líneas **después de las otras inyecciones de dependencia**:

```csharp
// Agregar después de builder.Services.Add* existentes
builder.Services.AddScoped<ILiveMatchService, LiveMatchService>();

// Agregar HttpClient para SofaScore
builder.Services.AddHttpClient<ILiveMatchService, LiveMatchService>(client =>
{
    client.BaseAddress = new Uri("https://api.sofascore.com/api/v1");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

---

## 🏃 PASO 2: Correr la aplicación

```bash
cd Oloraculo.Web
dotnet restore
dotnet run
```

La aplicación iniciará en `https://localhost:7xxx` (Blazor Server)

---

## 🎯 PASO 3: Ver probabilidades EN VIVO de un partido

### Opción A: URL directa (Ejemplo: Canadá vs Qatar)

1. Necesitas el **ID de SofaScore** del partido
2. Navega a: `https://localhost:7xxx/live/{MATCH_ID}`

**Ejemplo real para Canadá vs Qatar (matchId: 10634876):**
```
https://localhost:7xxx/live/10634876
```

### Opción B: Encontrar el ID del partido

Visita https://www.sofascore.com/ y busca el partido:
- Click en el partido
- El ID está en la URL: `sofascore.com/event/{MATCH_ID}`

---

## 📊 ¿Qué ves en la pantalla EN VIVO?

Una vez en `/live/10634876`, verás:

### 1️⃣ **Marcador en tiempo real**
```
🔴 EN VIVO | 34'
  Canadá 1 - 0 Qatar
```

### 2️⃣ **Probabilidad de Gol (ACTUALIZADA)**
- **Próximos 5 min**: 22.5%
- **Próximos 15 min**: 48.3%
- **Hasta el final**: 71.2%

### 3️⃣ **Análisis Detallado** (por qué cambió)
```
📊 Análisis en vivo (34') - CAN vs QAT

Factores:
• xG promedio: 0.45
• Tiros al arco: 3
• Córners: 2
• Jugadas peligrosas: 8

⚡ Influencia de factores:
📈 ShotRhythm: +0.08
📈 xG: +0.11
📈 EventDensity: +0.04
📉 Possession: -0.02
```

### 4️⃣ **Estadísticas del partido**
```
          CAN  | QAT
Posesión   58% | 42%
Tiros       5  |  2
Tiros al arco 3 | 1
Córners      2  | 0
xG         0.6 | 0.2
```

---

## 🔄 Auto-actualización

✅ **Automático cada 30 segundos** - No necesitas hacer nada
- Los datos se actualizan en tiempo real
- La probabilidad se recalcula con cada evento
- Las estadísticas se refrescan automáticamente

**O actualiza manualmente:** Click en botón "🔄 Actualizar ahora"

---

## 💡 EJEMPLOS: Preguntas que puedes hacer

### "¿Cuál es la probabilidad de otro gol en el partido Canadá vs Qatar AHORA MISMO?"

**En la pantalla verás:**
```
⚡ Probabilidad de Gol Actualizada

Próximos 5 min      22.5% ▓▓▓░░░
Próximos 15 min     48.3% ▓▓▓▓▓░░
Hasta el final      71.2% ▓▓▓▓▓▓░
```

**Respuesta:** Si estamos en el minuto 34' y ves 48.3% en los próximos 15 min, significa hay **casi 50% de chance** de que entre otro gol antes del 49'.

### ¿Qué factores influyen?

Mira la sección **"Análisis Detallado"** que muestra:

| Factor | Influencia | Significa |
|--------|-----------|-----------|
| xG (Expected Goals) | +0.11 | Muchas ocasiones de gol |
| ShotRhythm | +0.08 | Hay tiros frecuentes |
| Córners | +0.05 | Oportunidades claras |
| Possession | -0.02 | Posesión baja = menos chances |
| Minute | +0.03 | Final del partido = más abierto |

---

## 🔌 ¿Cómo funcionan los cálculos?

**Fórmula simplificada:**

```
Prob Final = Prob Base 
           + (xG × 0.25)           // Expected Goals
           + (Tiros recientes × 0.05)
           + (Córners × 0.03)
           + (Posesión > 55% ? +0.05 : -0.02)
           + (Minuto > 70 ? +0.03 : 0)
           + (Eventos densos ? +0.04 : 0)
```

**Ejemplo:**
```
Prob Base: 0.30 (30% - predicción pre-partido)
+ xG:      0.11
+ Tiros:   0.08
+ Córners: 0.03
+ Pos:    -0.02
+ Minuto:  0.03
= TOTAL:   0.53 (53% probabilidad)
```

---

## 🚨 TROUBLESHOOTING

### ❌ "Error: API no responde"
**Solución:**
```bash
# Verificar que SofaScore está disponible
curl https://api.sofascore.com/api/v1/event/10634876

# Si falla, espera 30s y reintentar (rate limiting)
```

### ❌ "Match ID no encontrado"
**Solución:**
1. Verifica el ID en https://sofascore.com
2. Debe ser numérico, ej: `10634876`
3. El partido debe estar EN VIVO o completado

### ❌ "Probabilidad no se actualiza"
**Solución:**
1. Abre DevTools (F12)
2. Mira la consola para errores
3. Click "🔄 Actualizar ahora"
4. Espera 30s para auto-refresh

---

## 📱 Integración con UI existente

Para agregar un link a los partidos EN VIVO desde tu pantalla `/matches`:

**En Matches.razor, agrega:**
```razor
@if (match.Status == "inProgress")
{
    <a href="/live/@match.SofascoreId" class="btn btn-danger">
        🔴 VER EN VIVO
    </a>
}
```

---

## 🎓 Siguientes pasos

1. **Mapear IDs:** Conecta los partidos de tu DB con los IDs de SofaScore
2. **Historial:** Guarda las predicciones en vivo para evaluar accuracy
3. **WebSocket:** Cambia de polling (30s) a WebSocket para latencia <1s
4. **Alertas:** Notifica cuando xG sube 0.5+ puntos

---

## 📞 Resumen rápido

| Acción | Cómo |
|--------|-----|
| Ver partido en vivo | Ir a `/live/MATCH_ID` |
| Match ID | Buscar en https://sofascore.com |
| Actualizar datos | Automático cada 30s o botón manual |
| Ver probabilidades | En la tarjeta "⚡ Probabilidad de Gol" |
| Entender cambios | Leer "📊 Análisis Detallado" |

---

¡Listo! 🎉 Ya tienes predicciones EN VIVO con SofaScore. Cualquier duda, revisa los logs en la consola del navegador (F12 → Console).

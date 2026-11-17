# WinPla — Windows Window Placement Manager

Coloca las ventanas de las aplicaciones que elijas en el monitor y el estado que quieras
(maximizada, normal o posición personalizada) cada vez que inicias sesión en Windows.

## Requisitos

- Windows 10/11.
- **PowerShell 7** (`pwsh`), que ya trae .NET y WPF. No hace falta nada más.

## Instalación

Pega esto en PowerShell (Windows PowerShell 5.1 o PowerShell 7):

```powershell
irm https://raw.githubusercontent.com/propstgonz/winpla-manager/main/install.ps1 | iex
```

Descarga la última versión en `%APPDATA%\WinPla\app`, crea el acceso directo **WinPla** en el menú Inicio y abre
el programa. Volver a ejecutarlo actualiza la versión conservando tu configuración.

Después, ábrelo desde **menú Inicio → WinPla** (tarda un par de segundos: compila el código al arrancar)
y marca **Iniciar con Windows**.

### Desinstalar

Desmarca *Iniciar con Windows*, borra la carpeta `%APPDATA%\WinPla` y el acceso directo **WinPla** del menú Inicio.

### Desde una copia local (desarrollo)

Clona o descarga el repositorio y abre **`WinPla.cmd`** con doble clic. Si mueves la carpeta, ábrelo una vez
desde la nueva ubicación y la entrada de inicio se corrige sola.

### ¿Por qué un `.cmd` y no un `.exe`?

Smart App Control de Windows bloquea los ejecutables sin firmar compilados localmente. `WinPla.ps1`
ejecuta exactamente el mismo código C# compilándolo en memoria, y eso sí está permitido.
En un PC sin Smart App Control (o si firmas el binario) puedes usar el ejecutable que genera
`dotnet publish -c Release -o dist` (la carpeta `dist\` no se versiona), que requiere el *.NET Desktop Runtime 10*.

## Uso

- **Añadir aplicación**: hay tres formas de elegirla.
  - *Examinar…*: seleccionas el `.exe`.
  - *…o elige una ventana abierta*: la aplicación debe estar abierta. Es la forma más fiable.
  - *…o elige una aplicación del arranque de Windows*: lista las aplicaciones que tienen activado el
    inicio automático (las mismas que salen activadas en el Administrador de tareas). No incluye las
    apps de la Microsoft Store ni las entradas que arrancan mediante `cmd` o un `.bat`.

  En algunos programas la ventana pertenece a un ejecutable distinto del que arranca
  (p. ej. Steam → `steamwebhelper.exe`). Si una ventana no se coloca, abre el programa y elígelo
  en la lista de ventanas abiertas.
- Elige el **monitor** y el **modo**:
  - *Maximizada*.
  - *Normal*: se mueve al monitor conservando su tamaño.
  - *Personalizada*: posición y tamaño exactos. *Usar la posición actual de la ventana* los rellena
    a partir de dónde está la ventana ahora.
- Cada regla se puede editar, eliminar o desactivar con la casilla *Activada*. Los cambios se guardan al momento.
- **Aplicar reglas ahora** coloca las ventanas ya abiertas y muestra el resultado de cada una.
- **Colocar las ventanas automáticamente al iniciar sesión** activa o desactiva el trabajo del arranque
  sin quitar la entrada de inicio.

## Cómo funciona al iniciar sesión

Windows lanza `WinPla.ps1 --startup` sin ventana. Durante 5 minutos comprueba una vez por segundo si
han aparecido ventanas de las aplicaciones configuradas, las coloca y después **el proceso termina**.
No queda nada residente ni icono en la bandeja.

- Se coloca cada ventana nueva de la aplicación, no solo la primera (así se ignoran las pantallas de carga).
- Cada ventana se vigila hasta que lleva 3 segundos seguidos en su sitio (muchas aplicaciones restauran
  su última posición justo después de mostrarse) o hasta 15 intentos; después se deja en paz.
- Las ventanas minimizadas no se restauran: quedan preparadas para aparecer en su sitio al abrirlas.

## Limitaciones conocidas

- Si una aplicación arranca oculta en la bandeja y abres su ventana pasados los 5 minutos, no se coloca.
  El tiempo se cambia en `WatchSeconds` de `config.json`; también puedes pulsar *Aplicar reglas ahora*.
- Las aplicaciones que se ejecutan **como administrador** no se pueden mover (Windows lo impide a un
  programa sin privilegios). WinPla lo indica en el resultado.
- Si el monitor de una regla está **desconectado**, la ventana no se toca y la regla aparece marcada.
  Los monitores se identifican por dispositivo, no por número, así que una renumeración no afecta;
  conectar el mismo monitor a otro puerto puede requerir reasignarlo.
- Las apps de la Microsoft Store que se alojan en `ApplicationFrameHost.exe` no se distinguen entre sí.
- Al pasar una ventana entre monitores con distinto escalado, la propia aplicación ajusta su tamaño.

## Ficheros de datos

| Fichero | Contenido |
|---|---|
| `%APPDATA%\WinPla\app\` | El programa instalado. Se reemplaza al actualizar. |
| Menú Inicio → `WinPla.lnk` | Acceso directo que abre la interfaz sin consola. |
| `%APPDATA%\WinPla\config.json` | Reglas y opciones. Legible y editable a mano. Si se corrompe se aparta como `.bak`. |
| `%APPDATA%\WinPla\log.txt` | Qué se colocó y qué falló. Primer sitio donde mirar si algo no funciona. |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `WinPla` | Entrada de inicio automático. |

## Documentación técnica

Un solo proyecto C# / WPF (.NET 10), sin dependencias externas, unas 900 líneas.

| Ruta | Responsabilidad |
|---|---|
| `Contracts.cs` | Modelos (`Rule`, `Config`, `MonitorInfo`, `WindowInfo`, `PlaceResult`) y contrato entre módulos. |
| `App.cs` | Punto de entrada y modos: interfaz (sin argumentos), `--startup`, `--selftest`. |
| `Core/` | Win32: `Monitors` (EnumDisplayMonitors + EnumDisplayDevices), `AppWindows` (EnumWindows y filtrado), `Placer` (cálculo puro del destino + SetWindowPlacement), `Engine` (pasada única y vigilancia). |
| `Storage/` | `ConfigStore` (JSON con escritura atómica), `Autostart` (clave Run), `Log`. |
| `UI/` | `MainWindow` y `RuleDialog`. El XAML se carga en tiempo de ejecución (`Ui.Load`) y los eventos se enlazan en código, para que el mismo código funcione compilado y bajo `WinPla.ps1`. |
| `WinPla.ps1` / `WinPla.cmd` | Lanzador: compila los `.cs` en memoria con `Add-Type` y llama a `App.Main`. |

Decisiones:

- Las reglas casan por **nombre de ejecutable** del proceso dueño de la ventana, nunca por ruta
  (las rutas cambian con las actualizaciones).
- El proceso es **PerMonitorV2**: todas las coordenadas son píxeles físicos.
- Vigilancia por **sondeo acotado** (1 s) en lugar de hooks: más simple y también recoge las ventanas
  que ya existían. Si el consumo importase, el paso siguiente es `SetWinEventHook(EVENT_OBJECT_SHOW)`.

Comprobaciones:

- `pwsh -NoProfile -STA -File WinPla.ps1 --selftest` → código de salida 0 y "Selftest OK" en el log
  (lógica pura de cálculo de posiciones).
- `dotnet build` → comprobación de tipos del conjunto.

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ControlLab.ClienteWindows
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // CONFIGURACIÓN PARA MEGANLABAUTH
        // ============================================================
        private const string API_BASE = "http://localhost:5000/api/Auth";
        private const string LOGIN_ENDPOINT = "/login";
        private const string LOGOUT_ENDPOINT = "/logout";
        private const string BLOQUEO_ENDPOINT = "/bloqueo-inactividad";
        private const string DESBLOQUEO_ENDPOINT = "/desbloqueo";
        private const string SESION_ACTIVA_ENDPOINT = "/sesion-activa/";
        private const string ACTUALIZAR_EQUIPO_ENDPOINT = "/actualizar-equipo";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // ============================================================
        // BLOQUEO SOLO DE LA TECLA WINDOWS
        // ============================================================
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_MENU = 0x12; // ALT
        private const int VK_F4 = 0x73;

        private IntPtr _hookTeclado = IntPtr.Zero;
        private LowLevelKeyboardProc? _procTeclado;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId
        );

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam
        );

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void ActivarBloqueoTeclaWindows()
        {
            if (_hookTeclado != IntPtr.Zero)
                return;

            _procTeclado = HookTeclado;

            using Process procesoActual = Process.GetCurrentProcess();
            using ProcessModule? moduloActual = procesoActual.MainModule;

            if (moduloActual == null)
                return;

            _hookTeclado = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _procTeclado,
                GetModuleHandle(moduloActual.ModuleName),
                0
            );
        }

        private void DesactivarBloqueoTeclaWindows()
        {
            if (_hookTeclado != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookTeclado);
                _hookTeclado = IntPtr.Zero;
            }
        }

        private IntPtr HookTeclado(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int tecla = Marshal.ReadInt32(lParam);

                // Bloquea únicamente la tecla Windows izquierda y derecha.
                // Alt + F4 y el resto del teclado siguen funcionando.
                if (tecla == VK_LWIN || tecla == VK_RWIN)
                {
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_hookTeclado, nCode, wParam, lParam);
        }

        private static bool AltF4Presionado()
        {
            bool altPresionado = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            bool f4Presionado = (GetAsyncKeyState(VK_F4) & 0x8000) != 0;

            return altPresionado && f4Presionado;
        }

        // ============================================================
        // TIMERS
        // ============================================================
        private readonly DispatcherTimer relojTimer = new DispatcherTimer();
        private readonly DispatcherTimer sesionTimer = new DispatcherTimer();
        private readonly DispatcherTimer inactividadTimer = new DispatcherTimer();
        private readonly DispatcherTimer redTimer = new DispatcherTimer();

        private readonly TimeSpan limiteInactividad = TimeSpan.FromMinutes(5);

        // ============================================================
        // ESTADO DE SESIÓN
        // ============================================================
        private DateTime? horaInicioSesion;
        private string cedulaActual = "";
        private string nombrePc = "";
        private string ipPc = "";
        private string macPc = "";
        private string adaptadorRedActual = "";
        private string ultimaFirmaRedSincronizada = "";
        private bool sincronizandoRedBackend = false;
        private int sessionIdActual = 0;
        private string ubicacionActual = "Laboratorio 3A";
        private string nombreCompletoActual = "";
        private string carreraActual = "";
        private string tipoUsoActual = "";

        private bool sesionActiva = false;
        private bool pantallaBloqueada = false;
        private bool permitirCerrar = false;
        private bool cerrandoPorEvento = false;

        // ============================================================
        // CONTADOR FLOTANTE SUPERIOR IZQUIERDO
        // ============================================================
        private Window? ventanaContador;
        private TextBlock? TxtMiniTiempo;
        private TextBlock? TxtMiniTiempoCompacto;
        private TextBlock? TxtMiniDetalle;
        private TextBlock? TxtMiniUsuario;
        private bool contadorMinimizado = true; // true = burbuja compacta, false = tarjeta completa

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public MainWindow()
        {
            InitializeComponent();

            ActivarBloqueoTeclaWindows();

            ConfigurarVentanaBloqueo();
            CargarDatosEquipo();
            ConfigurarReloj();
            ConfigurarTimers();

            TxtCedula.TextChanged += TxtCedula_TextChanged;
            DataObject.AddPastingHandler(TxtCedula, TxtCedula_Pasting);

            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            // Eventos reales de Windows: se disparan cuando cambia la red, IP, Wi-Fi o Ethernet.
            // Esto ayuda a que no dependa solo del timer.
            NetworkChange.NetworkAddressChanged += NetworkChange_Detectada;
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailability_Detectada;

            ActualizarPlaceholder();
            TxtCedula.Focus();

            _ = VerificarSesionActivaAlInicio();
        }

        // ============================================================
        // CONFIGURACIÓN INICIAL
        // ============================================================
        private void ConfigurarVentanaBloqueo()
        {
            Topmost = true;
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
        }

        private void CargarDatosEquipo()
        {
            ActualizarDatosEquipoEnTiempoReal(forzarActualizacion: true);
        }

        private void ConfigurarReloj()
        {
            relojTimer.Interval = TimeSpan.FromSeconds(1);
            relojTimer.Tick += RelojTimer_Tick;
            relojTimer.Start();
            ActualizarHoraFecha();
        }

        private void ConfigurarTimers()
        {
            sesionTimer.Interval = TimeSpan.FromSeconds(1);
            sesionTimer.Tick += SesionTimer_Tick;

            inactividadTimer.Interval = TimeSpan.FromSeconds(5);
            inactividadTimer.Tick += InactividadTimer_Tick;

            // Revisa en tiempo real si cambió la red activa.
            // También se complementa con NetworkChange para reaccionar más rápido.
            redTimer.Interval = TimeSpan.FromSeconds(2);
            redTimer.Tick += RedTimer_Tick;
            redTimer.Start();
        }

        // ============================================================
        // RELOJ
        // ============================================================
        private void RelojTimer_Tick(object? sender, EventArgs e) => ActualizarHoraFecha();

        private void ActualizarHoraFecha()
        {
            DateTime ahora = DateTime.Now;
            TxtHora.Text = ahora.ToString("hh:mm tt", new CultureInfo("es-EC"))
                               .Replace("AM", "a. m.")
                               .Replace("PM", "p. m.");
            TxtFecha.Text = ahora.ToString("dd/MM/yyyy");
        }

        // ============================================================
        // RED EN TIEMPO REAL
        // ============================================================
        private void RedTimer_Tick(object? sender, EventArgs e)
        {
            ActualizarDatosEquipoEnTiempoReal();
        }

        private void NetworkChange_Detectada(object? sender, EventArgs e)
        {
            _ = RevisarRedDespuesDeCambioAsync();
        }

        private void NetworkAvailability_Detectada(object? sender, NetworkAvailabilityEventArgs e)
        {
            _ = RevisarRedDespuesDeCambioAsync();
        }

        private async Task RevisarRedDespuesDeCambioAsync()
        {
            // Windows tarda unos segundos en entregar la nueva IP después de cambiar Wi-Fi/Ethernet.
            // Por eso se revisa 2 veces: una rápida y otra después de que DHCP actualice la IP.
            await Task.Delay(1200);
            await Dispatcher.InvokeAsync(() => ActualizarDatosEquipoEnTiempoReal(forzarActualizacion: true));

            await Task.Delay(2500);
            await Dispatcher.InvokeAsync(() => ActualizarDatosEquipoEnTiempoReal(forzarActualizacion: true));
        }

        private void ActualizarDatosEquipoEnTiempoReal(bool forzarActualizacion = false)
        {
            string nuevoNombrePc = Environment.MachineName;
            var datosRed = ObtenerDatosRedPrincipal();

            bool cambioDetectado = forzarActualizacion ||
                                   !string.Equals(nombrePc, nuevoNombrePc, StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(ipPc, datosRed.Ip, StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(macPc, datosRed.Mac, StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(adaptadorRedActual, datosRed.Adaptador, StringComparison.OrdinalIgnoreCase);

            if (!cambioDetectado) return;

            nombrePc = nuevoNombrePc;
            ipPc = datosRed.Ip;
            macPc = datosRed.Mac;
            adaptadorRedActual = datosRed.Adaptador;

            // Actualiza la información visible en la pantalla de bloqueo/login.
            TxtPc.Text = nombrePc;
            TxtUbicacion.Text = ubicacionActual;
            TxtDatosPc.Text = $"IP: {ipPc}   |   MAC: {macPc}";

            // Si ya existe una sesión, también actualiza el estado y la burbuja.
            if (sesionActiva)
            {
                string tipoUso = string.IsNullOrWhiteSpace(tipoUsoActual) ? "PRESTAMO" : tipoUsoActual;
                TxtEstadoSesion.Text = pantallaBloqueada
                    ? $"Estado: PANTALLA BLOQUEADA POR INACTIVIDAD  —  {nombrePc}  —  {ubicacionActual}  —  IP: {ipPc}"
                    : $"Estado: sesión activa en {nombrePc}  —  {ubicacionActual}  —  Tipo: {tipoUso}  —  IP: {ipPc}  —  MAC: {macPc}";

                ActualizarContadorMini();
            }

            RegistrarEventoLocal("RED", $"Red actualizada: PC={nombrePc}, IP={ipPc}, MAC={macPc}, Adaptador={adaptadorRedActual}");

            // Envía la información capturada al backend para guardarla en PostgreSQL.
            _ = SincronizarDatosEquipoConBackendAsync(forzarActualizacion);
        }

        private async Task SincronizarDatosEquipoConBackendAsync(bool forzar = false)
        {
            if (sincronizandoRedBackend) return;

            string firmaActual = $"{sessionIdActual}|{nombrePc}|{ipPc}|{macPc}|{adaptadorRedActual}|{ubicacionActual}";
            if (!forzar && string.Equals(ultimaFirmaRedSincronizada, firmaActual, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            sincronizandoRedBackend = true;

            try
            {
                var request = new
                {
                    sesionId = sessionIdActual,
                    nombrePc = nombrePc,
                    ip = ipPc,
                    mac = macPc,
                    adaptador = adaptadorRedActual,
                    ubicacion = ubicacionActual,
                    sistemaOperativo = Environment.OSVersion.VersionString
                };

                var response = await _httpClient.PostAsJsonAsync($"{API_BASE}{ACTUALIZAR_EQUIPO_ENDPOINT}", request);
                if (!response.IsSuccessStatusCode)
                {
                    RegistrarEventoLocal("RED_API_ERROR", $"Backend respondió: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResp = JsonSerializer.Deserialize<AuthResponse>(json, options);

                if (authResp != null && authResp.Autorizado)
                {
                    ultimaFirmaRedSincronizada = firmaActual;
                    RegistrarEventoLocal("RED_API_OK", $"Equipo sincronizado en BD. IdComputadora={authResp.IdComputadora}");
                }
                else
                {
                    RegistrarEventoLocal("RED_API_ERROR", authResp?.Mensaje ?? "No se pudo sincronizar equipo en backend.");
                }
            }
            catch (Exception ex)
            {
                // No se muestra MessageBox para no interrumpir al usuario.
                // El cliente sigue funcionando y deja el error en el log local.
                RegistrarEventoLocal("RED_API_ERROR", ex.Message);
            }
            finally
            {
                sincronizandoRedBackend = false;
            }
        }

        // ============================================================
        // VERIFICAR SESIÓN ACTIVA AL INICIO
        // ============================================================
        private async Task VerificarSesionActivaAlInicio()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{API_BASE}{SESION_ACTIVA_ENDPOINT}{nombrePc}");
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                JsonElement data = JsonSerializer.Deserialize<JsonElement>(json, options);

                bool tieneActiva = data.GetProperty("tieneSesionActiva").GetBoolean();
                if (!tieneActiva) return;

                var sesion = data.GetProperty("sesionActiva");
                sessionIdActual = sesion.GetProperty("idSesion").GetInt32();

                if (sesion.TryGetProperty("cedula", out var cedulaProp))
                    cedulaActual = cedulaProp.GetString() ?? "";

                horaInicioSesion = sesion.GetProperty("horaInicio").GetDateTime();
                nombreCompletoActual = sesion.GetProperty("usuario").GetString() ?? "";
                ubicacionActual = sesion.GetProperty("ubicacion").GetString() ?? ubicacionActual;

                if (sesion.TryGetProperty("ip", out var ipProp))
                    ipPc = ipProp.GetString() ?? ipPc;

                if (sesion.TryGetProperty("mac", out var macProp))
                    macPc = macProp.GetString() ?? macPc;

                // Importante: aunque el backend devuelva la IP antigua, aquí volvemos a leer
                // la red actual de Windows para que la pantalla no se quede con datos viejos.
                ActualizarDatosEquipoEnTiempoReal(forzarActualizacion: true);

                sesionActiva = true;

                PanelLogin.Visibility = Visibility.Collapsed;
                PanelSesion.Visibility = Visibility.Visible;

                TxtUsuarioSesion.Text = $"{nombreCompletoActual}  |  Cédula: {cedulaActual}  |  {carreraActual}";
                TxtInicioSesion.Text = $"Hora inicio: {horaInicioSesion:dd/MM/yyyy HH:mm:ss}";
                TxtEstadoSesion.Text = $"Estado: sesión activa en {nombrePc}  —  {ubicacionActual}";
                TxtTiempoSesion.Text = "Tiempo de uso: 00:00:00";
                TxtFinInactividad.Text = "Inactividad: 00:00 / 05:00";
                TxtUbicacion.Text = ubicacionActual;

                sesionTimer.Start();
                inactividadTimer.Start();

                MostrarContadorFlotanteAutoBurbuja();
                _ = SincronizarDatosEquipoConBackendAsync(forzar: true);
            }
            catch { /* Si falla, mostrar login */ }
        }

        // ============================================================
        // LOGIN Y DESBLOQUEO (integrado)
        // ============================================================
        private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string cedula = TxtCedula.Text.Trim();

            // ---- NUEVA LÓGICA: Si la pantalla está bloqueada, desbloquear ----
            if (pantallaBloqueada && sesionActiva)
            {
                bool desbloqueado = await DesbloquearYRestaurarSesion(cedula);
                if (desbloqueado)
                {
                    MostrarMensajeCorrecto("Sesión desbloqueada correctamente.");
                    return;
                }
                else
                {
                    MostrarMensajeError("No se pudo desbloquear la sesión. Verifique su cédula.");
                    return;
                }
            }

            // ---- Login normal ----
            if (!CedulaEsValida(cedula))
            {
                MostrarMensajeError("Ingrese una cédula válida de 10 dígitos.");
                TxtCedula.Focus();
                return;
            }

            BtnIngresar.IsEnabled = false;
            MostrarMensajeInfo("Verificando cédula...");

            bool exito = await IniciarSesionApiAsync(cedula);

            BtnIngresar.IsEnabled = true;

            if (!exito)
            {
                MostrarMensajeError("No se pudo iniciar sesión. Verifique el backend.");
            }
        }

        private async Task<bool> IniciarSesionApiAsync(string cedula)
        {
            try
            {
                // Antes de enviar login, leer la IP/MAC actuales de Windows.
                ActualizarDatosEquipoEnTiempoReal(forzarActualizacion: true);

                var request = new
                {
                    cedula,
                    nombrePc = nombrePc,
                    ip = ipPc,
                    mac = macPc,
                    adaptador = adaptadorRedActual,
                    ubicacion = ubicacionActual,
                    sistemaOperativo = Environment.OSVersion.VersionString
                };

                var response = await _httpClient.PostAsJsonAsync($"{API_BASE}{LOGIN_ENDPOINT}", request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MostrarMensajeError($"Error en el servidor: {response.StatusCode}");
                    return false;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResp = JsonSerializer.Deserialize<AuthResponse>(json, options);

                if (authResp == null)
                {
                    MostrarMensajeError("No se pudo interpretar la respuesta del servidor.");
                    return false;
                }

                if (!authResp.Autorizado)
                {
                    MostrarMensajeError(authResp.Mensaje ?? "Error desconocido");
                    return false;
                }

                IniciarSesionUI(
                    cedula,
                    authResp.SesionId ?? 0,
                    authResp.NombreCompleto ?? cedula,
                    ubicacionActual,
                    authResp.TipoUso ?? "PRESTAMO",
                    authResp.Carrera ?? ""
                );

                return true;
            }
            catch (HttpRequestException httpEx)
            {
                MostrarMensajeError($"No se pudo conectar al servidor: {httpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                MostrarMensajeError($"Error inesperado: {ex.Message}");
                return false;
            }
        }

        private void IniciarSesionUI(string cedula, int idSesion, string nombreCompleto,
                                     string ubicacion, string tipoUso, string carrera)
        {
            cedulaActual = cedula;
            horaInicioSesion = DateTime.Now;
            sessionIdActual = idSesion;
            sesionActiva = true;
            ubicacionActual = ubicacion;
            nombreCompletoActual = nombreCompleto;
            carreraActual = carrera;
            tipoUsoActual = tipoUso;
            pantallaBloqueada = false;

            PanelLogin.Visibility = Visibility.Collapsed;
            PanelSesion.Visibility = Visibility.Visible;

            TxtUsuarioSesion.Text = $"{nombreCompleto}  |  Cédula: {cedula}  |  {carrera}";
            TxtInicioSesion.Text = $"Hora inicio: {horaInicioSesion:dd/MM/yyyy HH:mm:ss}";
            TxtEstadoSesion.Text = $"Estado: sesión activa en {nombrePc}  —  {ubicacion}  —  Tipo: {tipoUso}";
            TxtTiempoSesion.Text = "Tiempo de uso: 00:00:00";
            TxtFinInactividad.Text = "Inactividad: 00:00 / 05:00";
            TxtUbicacion.Text = ubicacion;

            sesionTimer.Start();
            inactividadTimer.Start();

            MostrarContadorFlotanteAutoBurbuja();
            RegistrarEventoLocal("INICIO", $"Sesión iniciada (ID: {idSesion})");

            // Al iniciar sesión se vuelve a sincronizar, ahora con el ID de sesión real.
            _ = SincronizarDatosEquipoConBackendAsync(forzar: true);
        }

        // ============================================================
        // NUEVO: DESBLOQUEAR Y RESTAURAR SESIÓN
        // ============================================================
        private async Task<bool> DesbloquearYRestaurarSesion(string cedula)
        {
            try
            {
                // Verificar que la cédula coincida con la sesión activa
                if (cedula != cedulaActual)
                {
                    MostrarMensajeError($"La cédula no coincide con la sesión activa. Use: {cedulaActual}");
                    return false;
                }

                // Llamar al endpoint de desbloqueo
                var request = new { sesionId = sessionIdActual };
                var response = await _httpClient.PostAsJsonAsync($"{API_BASE}{DESBLOQUEO_ENDPOINT}", request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MostrarMensajeError("Error al desbloquear la computadora.");
                    return false;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResp = JsonSerializer.Deserialize<AuthResponse>(json, options);

                if (authResp == null || !authResp.Autorizado)
                {
                    MostrarMensajeError(authResp?.Mensaje ?? "No se pudo desbloquear.");
                    return false;
                }

                // Restaurar UI
                pantallaBloqueada = false;
                TxtEstadoSesion.Text = $"Estado: sesión activa en {nombrePc}  —  {ubicacionActual}";

                // Mostrar panel de sesión
                PanelLogin.Visibility = Visibility.Collapsed;
                PanelSesion.Visibility = Visibility.Visible;

                MostrarContadorFlotanteAutoBurbuja();

                // Reiniciar timers
                sesionTimer.Start();
                inactividadTimer.Start();

                return true;
            }
            catch (Exception ex)
            {
                MostrarMensajeError($"Error al desbloquear: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // USO DE LA COMPUTADORA
        // ============================================================
        private void BtnUsarComputadora_Click(object sender, RoutedEventArgs e) => MostrarContadorFlotante();

        private void OcultarPantallaParaUso()
        {
            MostrarContadorFlotante();
        }

        // ============================================================
        // CIERRE DE SESIÓN (LOGOUT)
        // ============================================================
        private async void BtnCerrarSesionManual_Click(object sender, RoutedEventArgs e)
        {
            await CerrarSesionAsync("LOGOUT", true);
        }

        private async Task CerrarSesionAsync(string motivo, bool mostrarMensaje)
        {
            if (cerrandoPorEvento) return;
            cerrandoPorEvento = true;

            try
            {
                if (!sesionActiva || horaInicioSesion == null) return;

                sesionTimer.Stop();
                inactividadTimer.Stop();
                OcultarContadorFlotante();

                DateTime horaFin = DateTime.Now;
                TimeSpan duracion = horaFin - horaInicioSesion.Value;
                int duracionMinutos = 0;

                try
                {
                    var request = new { sesionId = sessionIdActual, motivoCierre = motivo };
                    var response = await _httpClient.PostAsJsonAsync($"{API_BASE}{LOGOUT_ENDPOINT}", request);
                    var json = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var authResp = JsonSerializer.Deserialize<AuthResponse>(json, options);
                        if (authResp != null && authResp.Autorizado)
                            duracionMinutos = authResp.DuracionMinutos ?? 0;
                    }
                    else
                    {
                        RegistrarSesionLocal(horaInicioSesion.Value, horaFin, duracion, motivo);
                    }
                }
                catch
                {
                    RegistrarSesionLocal(horaInicioSesion.Value, horaFin, duracion, motivo);
                }

                sesionActiva = false;
                pantallaBloqueada = false;
                horaInicioSesion = null;
                sessionIdActual = 0;

                MostrarPantallaLogin($"Sesión cerrada por {motivo}.");

                if (mostrarMensaje)
                {
                    string mensaje = $"Sesión cerrada correctamente\n\n" +
                                     $"Motivo: {motivo}\n" +
                                     $"Cédula: {cedulaActual}\n" +
                                     $"PC: {nombrePc}\n" +
                                     $"IP: {ipPc}\n" +
                                     $"MAC: {macPc}\n" +
                                     $"Duración: {FormatearTiempo(duracion)}" +
                                     (duracionMinutos > 0 ? $"\nMinutos registrados: {duracionMinutos}" : "");

                    await MostrarMensajeAutoCerrarAsync(
                        "Control de Uso de Computadoras",
                        mensaje,
                        3
                    );
                }

                cedulaActual = "";
            }
            finally
            {
                cerrandoPorEvento = false;
            }
        }

        // ============================================================
        // BLOQUEO / DESBLOQUEO POR INACTIVIDAD
        // ============================================================
        private async Task<bool> BloquearInactividadAsync()
        {
            if (!sesionActiva || sessionIdActual == 0) return false;

            try
            {
                var request = new
                {
                    sesionId = sessionIdActual,
                    minutosInactividad = (int)limiteInactividad.TotalMinutes,
                    cerrarSesion = false
                };

                var response = await _httpClient.PostAsJsonAsync($"{API_BASE}{BLOQUEO_ENDPOINT}", request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) return false;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResp = JsonSerializer.Deserialize<AuthResponse>(json, options);

                return authResp != null && authResp.Autorizado;
            }
            catch { return false; }
        }

        private async Task<bool> DesbloquearAsync()
        {
            if (!sesionActiva || sessionIdActual == 0) return false;

            try
            {
                var request = new { sesionId = sessionIdActual };
                var response = await _httpClient.PostAsJsonAsync($"{API_BASE}{DESBLOQUEO_ENDPOINT}", request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) return false;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResp = JsonSerializer.Deserialize<AuthResponse>(json, options);

                return authResp != null && authResp.Autorizado;
            }
            catch { return false; }
        }

        // ============================================================
        // TIMERS DE SESIÓN E INACTIVIDAD
        // ============================================================
        private void SesionTimer_Tick(object? sender, EventArgs e)
        {
            if (!sesionActiva || horaInicioSesion == null) return;

            TimeSpan duracion = DateTime.Now - horaInicioSesion.Value;
            TimeSpan inactivo = ObtenerTiempoInactivo();

            TxtTiempoSesion.Text = $"Tiempo de uso: {FormatearTiempo(duracion)}";
            TxtFinInactividad.Text = $"Inactividad: {FormatearMinutosSegundos(inactivo)} / {FormatearMinutosSegundos(limiteInactividad)}";

            ActualizarContadorMini(duracion);
        }

        private async void InactividadTimer_Tick(object? sender, EventArgs e)
        {
            if (!sesionActiva || pantallaBloqueada) return;

            TimeSpan tiempoInactivo = ObtenerTiempoInactivo();

            if (tiempoInactivo >= limiteInactividad)
            {
                bool bloqueado = await BloquearInactividadAsync();
                if (bloqueado)
                {
                    pantallaBloqueada = true;
                    TxtEstadoSesion.Text = "Estado: PANTALLA BLOQUEADA POR INACTIVIDAD";

                    // Mostrar panel de login para que el usuario pueda desbloquear
                    OcultarContadorFlotante();
                    PanelSesion.Visibility = Visibility.Collapsed;
                    PanelLogin.Visibility = Visibility.Visible;
                    TxtCedula.Text = "";
                    TxtCedula.Focus();
                    MostrarMensajeInfo("Pantalla bloqueada por inactividad. Ingrese su cédula para desbloquear.");

                    if (!IsVisible)
                        MostrarVentanaBloqueo();

                    // Detener timers mientras está bloqueado
                    sesionTimer.Stop();
                    inactividadTimer.Stop();
                }
                else
                {
                    await CerrarSesionAsync("INACTIVIDAD", true);
                }
            }
        }

        // ============================================================
        // EVENTOS DEL SISTEMA
        // ============================================================
        private async void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (sesionActiva && horaInicioSesion != null)
                await CerrarSesionAsync("APAGADO", false);
        }

        private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
                if (sesionActiva && horaInicioSesion != null)
                    await CerrarSesionAsync("SUSPENSION", false);
        }

        private async void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (sesionActiva && horaInicioSesion != null)
            {
                string motivo = e.Reason switch
                {
                    SessionSwitchReason.SessionLogoff => "CIERRE_SESION_WINDOWS",
                    SessionSwitchReason.SessionLock => "BLOQUEO_PANTALLA_WINDOWS",
                    SessionSwitchReason.SessionLogon => "CAMBIO_USUARIO",
                    _ => "CAMBIO_SESION"
                };
                await CerrarSesionAsync(motivo, false);
            }
        }

        // ============================================================
        // UI HELPERS
        // ============================================================
        private void MostrarPantallaLogin(string mensaje)
        {
            OcultarContadorFlotante();
            TxtCedula.Text = "";
            PanelSesion.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
            MostrarMensajeCorrecto(mensaje);
            MostrarVentanaBloqueo();
            TxtCedula.Focus();
        }

        private void MostrarVentanaBloqueo()
        {
            OcultarContadorFlotante();

            Show();
            Topmost = true;
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Activate();
        }

        private void MostrarContadorFlotante()
        {
            if (!sesionActiva) return;

            pantallaBloqueada = false;

            PanelLogin.Visibility = Visibility.Collapsed;
            PanelSesion.Visibility = Visibility.Collapsed;

            Topmost = false;
            Hide();

            if (ventanaContador == null)
            {
                ventanaContador = CrearVentanaContador();
            }

            AplicarFormaContadorFlotante();
            ActualizarContadorMini();

            ventanaContador.Left = 14;
            ventanaContador.Top = 14;
            ventanaContador.Show();
            ventanaContador.Topmost = true;
            ventanaContador.Activate();
        }

        private void MostrarContadorFlotanteAutoBurbuja()
        {
            if (!sesionActiva) return;

            // Primero muestra la tarjeta completa como en la imagen.
            contadorMinimizado = false;
            MostrarContadorFlotante();

            // Luego, después de 3 segundos, se convierte automáticamente en burbuja.
            _ = MinimizarContadorDespuesDeAsync(3);
        }

        private async Task MinimizarContadorDespuesDeAsync(int segundos)
        {
            await Task.Delay(TimeSpan.FromSeconds(segundos));

            if (!sesionActiva || pantallaBloqueada || ventanaContador == null)
                return;

            MinimizarContadorFlotante();
        }

        private void AplicarFormaContadorFlotante()
        {
            if (ventanaContador == null) return;

            if (contadorMinimizado)
            {
                ventanaContador.Width = 86;
                ventanaContador.Height = 86;
                ventanaContador.Content = CrearBurbujaContador();
            }
            else
            {
                ventanaContador.Width = 405;
                ventanaContador.Height = 92;
                ventanaContador.Content = CrearTarjetaContadorCompleto();
            }
        }

        private Window CrearVentanaContador()
        {
            var win = new Window
            {
                Width = 405,
                Height = 92,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false
            };

            win.Closing += (s, e) =>
            {
                if (!permitirCerrar && sesionActiva)
                {
                    e.Cancel = true;
                    win.Hide();
                }
            };

            return win;
        }

        private Border CrearTarjetaContadorCompleto()
        {
            TxtMiniTiempo = new TextBlock
            {
                Text = "00:00:00",
                FontSize = 25,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5B59")),
                VerticalAlignment = VerticalAlignment.Center
            };

            TxtMiniUsuario = new TextBlock
            {
                Text = "Sesión activa",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5B59")),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            TxtMiniDetalle = new TextBlock
            {
                Text = "Equipo • Ubicación",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6F7F7D")),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var indicador = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(21),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF4F1")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8DEDA")),
                BorderThickness = new Thickness(1.2),
                Cursor = Cursors.Hand,
                ToolTip = "Convertir en burbuja",
                Child = new TextBlock
                {
                    Text = "⏱",
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            indicador.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                MinimizarContadorFlotante();
            };

            var textos = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 8, 0)
            };

            textos.Children.Add(TxtMiniUsuario);
            textos.Children.Add(TxtMiniTiempo);
            textos.Children.Add(TxtMiniDetalle);

            var btnCerrar = new Button
            {
                Content = "Cerrar",
                Width = 68,
                Height = 34,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B0000")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            btnCerrar.Click += async (_, _) => await CerrarSesionAsync("LOGOUT", true);

            var grid = new Grid();

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(indicador, 0);
            Grid.SetColumn(textos, 1);
            Grid.SetColumn(btnCerrar, 2);

            grid.Children.Add(indicador);
            grid.Children.Add(textos);
            grid.Children.Add(btnCerrar);

            var tarjeta = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FBFA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDD4CF")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(13, 10, 13, 10),
                Child = grid,
                ToolTip = "Arrastra para mover. Doble clic para convertir en burbuja.",
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 0,
                    Opacity = 0.26
                }
            };

            tarjeta.MouseLeftButtonDown += (_, e) =>
            {
                try
                {
                    if (e.ClickCount >= 2)
                    {
                        MinimizarContadorFlotante();
                    }
                    else
                    {
                        ventanaContador?.DragMove();
                    }
                }
                catch
                {
                }
            };

            return tarjeta;
        }

        private Border CrearBurbujaContador()
        {
            TxtMiniTiempoCompacto = new TextBlock
            {
                Text = "00:00:00",
                FontSize = 11,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5B59")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };

            var icono = new TextBlock
            {
                Text = "⏱",
                FontSize = 29,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };

            var estado = new Border
            {
                Width = 13,
                Height = 13,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E7D4D")),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0)
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(icono);
            stack.Children.Add(TxtMiniTiempoCompacto);

            var grid = new Grid();
            grid.Children.Add(stack);
            grid.Children.Add(estado);

            var tarjeta = new Border
            {
                Width = 76,
                Height = 76,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FBFA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDD4CF")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(38),
                Child = grid,
                Cursor = Cursors.Hand,
                ToolTip = "Burbuja de sesión activa. Doble clic para abrir el contador.",
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 0.30
                }
            };

            var menu = new ContextMenu();

            var abrir = new MenuItem { Header = "Abrir contador" };
            abrir.Click += (_, _) => RestaurarContadorFlotante();

            var cerrar = new MenuItem { Header = "Cerrar sesión" };
            cerrar.Click += async (_, _) => await CerrarSesionAsync("LOGOUT", true);

            menu.Items.Add(abrir);
            menu.Items.Add(new Separator());
            menu.Items.Add(cerrar);
            tarjeta.ContextMenu = menu;

            tarjeta.MouseLeftButtonDown += (_, e) =>
            {
                try
                {
                    if (e.ClickCount >= 2)
                    {
                        RestaurarContadorFlotante();
                    }
                    else
                    {
                        ventanaContador?.DragMove();
                    }
                }
                catch
                {
                }
            };

            return tarjeta;
        }

        private void MinimizarContadorFlotante()
        {
            if (ventanaContador == null) return;

            contadorMinimizado = true;
            TxtMiniTiempo = null;
            TxtMiniUsuario = null;
            TxtMiniDetalle = null;

            ventanaContador.Width = 86;
            ventanaContador.Height = 86;
            ventanaContador.Content = CrearBurbujaContador();

            ActualizarContadorMini();
        }

        private void RestaurarContadorFlotante()
        {
            if (ventanaContador == null) return;

            contadorMinimizado = false;
            TxtMiniTiempoCompacto = null;

            ventanaContador.Width = 405;
            ventanaContador.Height = 92;
            ventanaContador.Content = CrearTarjetaContadorCompleto();

            ActualizarContadorMini();
        }

        private void ActualizarContadorMini(TimeSpan? duracionActual = null)
        {
            TimeSpan duracion = duracionActual ??
                                (horaInicioSesion.HasValue ? DateTime.Now - horaInicioSesion.Value : TimeSpan.Zero);

            string tiempoFormateado = FormatearTiempo(duracion);

            if (TxtMiniTiempo != null)
                TxtMiniTiempo.Text = tiempoFormateado;

            if (TxtMiniTiempoCompacto != null)
                TxtMiniTiempoCompacto.Text = tiempoFormateado;

            if (TxtMiniUsuario != null)
            {
                TxtMiniUsuario.Text = string.IsNullOrWhiteSpace(nombreCompletoActual)
                    ? "Sesión activa"
                    : nombreCompletoActual;
            }

            if (TxtMiniDetalle != null)
                TxtMiniDetalle.Text = $"{nombrePc} • {ubicacionActual} • IP {ipPc}";
        }

        private void OcultarContadorFlotante()
        {
            if (ventanaContador != null && ventanaContador.IsVisible)
            {
                ventanaContador.Hide();
            }
        }

        private async Task MostrarMensajeAutoCerrarAsync(string titulo, string mensaje, int segundos)
        {
            var txtTitulo = new TextBlock
            {
                Text = titulo,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5B59")),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var txtMensaje = new TextBlock
            {
                Text = mensaje,
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#263D3A")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            };

            var txtCuenta = new TextBlock
            {
                Text = $"Este mensaje se cerrará en {segundos} segundos...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6F7F7D")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(22)
            };

            panel.Children.Add(txtTitulo);
            panel.Children.Add(txtMensaje);
            panel.Children.Add(txtCuenta);

            var contenedor = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDD4CF")),
                BorderThickness = new Thickness(1.5),
                Child = panel,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 0.28
                }
            };

            var ventana = new Window
            {
                Title = titulo,
                Width = 390,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = contenedor
            };

            ventana.Show();

            for (int i = segundos; i >= 1; i--)
            {
                txtCuenta.Text = $"Este mensaje se cerrará en {i} segundo{(i == 1 ? "" : "s")}...";
                await Task.Delay(1000);
            }

            if (ventana.IsVisible)
            {
                ventana.Close();
            }
        }

        private void MostrarMensajeError(string msg)
        {
            TxtMensaje.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B00020"));
            TxtMensaje.Text = msg;
        }

        private void MostrarMensajeCorrecto(string msg)
        {
            TxtMensaje.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E7D4D"));
            TxtMensaje.Text = msg;
        }

        private void MostrarMensajeInfo(string msg)
        {
            TxtMensaje.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));
            TxtMensaje.Text = msg;
        }

        private bool CedulaEsValida(string cedula)
        {
            return !string.IsNullOrWhiteSpace(cedula) &&
                   cedula.Length == 10 &&
                   cedula.All(char.IsDigit);
        }

        // ============================================================
        // MANEJO DE TEXTBOX CÉDULA
        // ============================================================
        private void TxtCedula_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void TxtCedula_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActualizarPlaceholder();
            TxtMensaje.Text = "";
        }

        private void TxtCedula_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }
            string texto = e.DataObject.GetData(typeof(string)) as string ?? "";
            if (!texto.All(char.IsDigit)) { e.CancelCommand(); return; }
            if (TxtCedula.Text.Length + texto.Length > 10) e.CancelCommand();
        }

        private void ActualizarPlaceholder()
        {
            TxtPlaceholder.Visibility = string.IsNullOrWhiteSpace(TxtCedula.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ============================================================
        // RED
        // ============================================================
        private (string Ip, string Mac, string Adaptador) ObtenerDatosRedPrincipal()
        {
            try
            {
                // Primero se obtiene la IP que Windows usa realmente para salir a la red.
                // Este método evita quedarse con IPs viejas o adaptadores virtuales.
                string? ipRutaActiva = ObtenerIpPorRutaActiva();

                var candidatos = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(EsAdaptadorConectado)
                    .Select(adaptador =>
                    {
                        var propiedades = adaptador.GetIPProperties();

                        var ipv4 = propiedades.UnicastAddresses
                            .Where(a =>
                                a.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !IPAddress.IsLoopback(a.Address) &&
                                !a.Address.ToString().StartsWith("169.254"))
                            .Select(a => a.Address.ToString())
                            .ToList();

                        bool contieneRutaActiva = !string.IsNullOrWhiteSpace(ipRutaActiva) &&
                                                   ipv4.Any(ip => string.Equals(ip, ipRutaActiva, StringComparison.OrdinalIgnoreCase));

                        bool tieneGateway = propiedades.GatewayAddresses.Any(g =>
                            g.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(g.Address) &&
                            g.Address.ToString() != "0.0.0.0"
                        );

                        bool esWifiOEthernet = adaptador.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                               adaptador.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                               adaptador.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet;

                        return new
                        {
                            Adaptador = adaptador,
                            Ipv4 = ipv4,
                            ContieneRutaActiva = contieneRutaActiva,
                            TieneGateway = tieneGateway,
                            EsWifiOEthernet = esWifiOEthernet,
                            EsVirtual = EsAdaptadorVirtual(adaptador)
                        };
                    })
                    .Where(x => x.Ipv4.Count > 0)
                    .ToList();

                // Orden correcto:
                // 1) Adaptador que Windows está usando realmente para salir.
                // 2) Adaptador físico, no virtual.
                // 3) Adaptador con puerta de enlace.
                // 4) Wi-Fi/Ethernet.
                var seleccionado = candidatos
                    .OrderByDescending(x => x.ContieneRutaActiva)
                    .ThenBy(x => x.EsVirtual)
                    .ThenByDescending(x => x.TieneGateway)
                    .ThenByDescending(x => x.EsWifiOEthernet)
                    .ThenByDescending(x => x.Adaptador.Speed)
                    .FirstOrDefault();

                if (seleccionado != null)
                {
                    string ipSeleccionada = seleccionado.ContieneRutaActiva && !string.IsNullOrWhiteSpace(ipRutaActiva)
                        ? ipRutaActiva
                        : seleccionado.Ipv4.First();

                    return (
                        ipSeleccionada,
                        FormatearMac(seleccionado.Adaptador.GetPhysicalAddress().ToString()),
                        seleccionado.Adaptador.Name
                    );
                }

                return ("IP_NO_DETECTADA", "MAC_NO_DETECTADA", "SIN_ADAPTADOR");
            }
            catch
            {
                return ("IP_NO_DETECTADA", "MAC_NO_DETECTADA", "SIN_ADAPTADOR");
            }
        }

        private string? ObtenerIpPorRutaActiva()
        {
            // No envía datos reales; solo obliga a Windows a decir qué IP local usaría
            // para salir por la ruta principal actual.
            string[] destinos = { "8.8.8.8", "1.1.1.1", "208.67.222.222" };

            foreach (string destino in destinos)
            {
                try
                {
                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Connect(destino, 65530);

                    if (socket.LocalEndPoint is IPEndPoint endPoint &&
                        endPoint.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(endPoint.Address) &&
                        !endPoint.Address.ToString().StartsWith("169.254"))
                    {
                        return endPoint.Address.ToString();
                    }
                }
                catch
                {
                    // Intentar con el siguiente destino.
                }
            }

            return null;
        }

        private bool EsAdaptadorConectado(NetworkInterface adaptador)
        {
            return adaptador.OperationalStatus == OperationalStatus.Up &&
                   adaptador.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                   adaptador.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                   adaptador.GetPhysicalAddress().GetAddressBytes().Length > 0;
        }

        private bool EsAdaptadorVirtual(NetworkInterface adaptador)
        {
            string texto = $"{adaptador.Name} {adaptador.Description}".ToLowerInvariant();

            string[] palabrasVirtuales =
            {
                "virtual", "virtualbox", "vmware", "hyper-v", "vethernet", "docker",
                "wsl", "npcap", "loopback", "bluetooth", "tap", "vpn", "pseudo"
            };

            return palabrasVirtuales.Any(p => texto.Contains(p));
        }

        private string FormatearMac(string macRaw)
        {
            if (string.IsNullOrWhiteSpace(macRaw))
                return "MAC_NO_DETECTADA";

            macRaw = macRaw.Replace(":", "").Replace("-", "").Trim().ToUpperInvariant();

            if (macRaw.Length < 12)
                return "MAC_NO_DETECTADA";

            return string.Join("-",
                Enumerable.Range(0, macRaw.Length / 2)
                    .Select(i => macRaw.Substring(i * 2, 2)));
        }

        // ============================================================
        // INACTIVIDAD
        // ============================================================
        private TimeSpan ObtenerTiempoInactivo()
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
            uint tiempoInactivo = (uint)Environment.TickCount - info.dwTime;
            return TimeSpan.FromMilliseconds(tiempoInactivo);
        }

        // ============================================================
        // FORMATEO
        // ============================================================
        private static string FormatearTiempo(TimeSpan t) =>
            $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

        private static string FormatearMinutosSegundos(TimeSpan t) =>
            $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

        // ============================================================
        // REGISTRO LOCAL
        // ============================================================
        private void RegistrarSesionLocal(DateTime inicio, DateTime fin, TimeSpan duracion, string motivo)
        {
            try
            {
                string carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ControlLab");
                Directory.CreateDirectory(carpeta);

                string archivo = Path.Combine(carpeta, "sesiones_locales.csv");
                bool archivoNuevo = !File.Exists(archivo);

                using var w = new StreamWriter(archivo, true, Encoding.UTF8);
                if (archivoNuevo)
                    w.WriteLine("session_id,cedula,nombre_pc,ip,mac,ubicacion,hora_inicio,hora_fin,duracion,motivo");

                w.WriteLine($"{sessionIdActual},{cedulaActual},{nombrePc},{ipPc},{macPc},{ubicacionActual}," +
                            $"{inicio:yyyy-MM-dd HH:mm:ss},{fin:yyyy-MM-dd HH:mm:ss}," +
                            $"{FormatearTiempo(duracion)},{motivo}");
            }
            catch { /* no interrumpir el flujo */ }
        }

        private void RegistrarEventoLocal(string tipo, string detalle)
        {
            try
            {
                string carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ControlLab");
                Directory.CreateDirectory(carpeta);

                string archivo = Path.Combine(carpeta, "eventos_locales.csv");
                bool archivoNuevo = !File.Exists(archivo);

                using var w = new StreamWriter(archivo, true, Encoding.UTF8);
                if (archivoNuevo)
                    w.WriteLine("fecha,tipo,detalle,cedula,nombre_pc,ip,mac");

                w.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tipo},{detalle}," +
                            $"{cedulaActual},{nombrePc},{ipPc},{macPc}");
            }
            catch { /* no interrumpir el flujo */ }
        }

        // ============================================================
        // CIERRE DE VENTANA
        // ============================================================
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Permite cerrar únicamente con Alt + F4.
            if (AltF4Presionado())
            {
                permitirCerrar = true;
            }

            if (permitirCerrar)
            {
                DesactivarBloqueoTeclaWindows();
                return;
            }

            e.Cancel = true;
            MostrarVentanaBloqueo();
            MostrarMensajeError("La aplicación de control solo puede cerrarse con Alt + F4.");
        }

        // ============================================================
        // ATAJO DE DESARROLLO
        // ============================================================
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.SystemKey == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                permitirCerrar = true;
                DesactivarBloqueoTeclaWindows();
                Application.Current.Shutdown();
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Q)
            {
                permitirCerrar = true;
                if (sesionActiva)
                    await CerrarSesionAsync("CIERRE_DESARROLLO", false);
                SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
                SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
                SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
                NetworkChange.NetworkAddressChanged -= NetworkChange_Detectada;
                NetworkChange.NetworkAvailabilityChanged -= NetworkAvailability_Detectada;
                DesactivarBloqueoTeclaWindows();
                Application.Current.Shutdown();
            }

            // Desbloquear al presionar cualquier tecla si la pantalla está bloqueada
            if (pantallaBloqueada)
            {
                await DesbloquearAsync();
                pantallaBloqueada = false;
                TxtEstadoSesion.Text = $"Estado: sesión activa en {nombrePc}  —  {ubicacionActual}";
                MostrarMensajeCorrecto("Pantalla desbloqueada.");
            }
        }

        // ============================================================
        // DTOs
        // ============================================================
        public class AuthResponse
        {
            public bool Autorizado { get; set; }
            public string Mensaje { get; set; } = "";
            public string? NombreCompleto { get; set; }
            public int? SesionId { get; set; }
            public int? IdComputadora { get; set; }
            public string? Carrera { get; set; }
            public string? TipoUso { get; set; }
            public int? DuracionMinutos { get; set; }
        }

        // ============================================================
        // P/INVOKE
        // ============================================================
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    }
}
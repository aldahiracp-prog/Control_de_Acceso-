using Microsoft.Win32;
using System;
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

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // ============================================================
        // TIMERS
        // ============================================================
        private readonly DispatcherTimer relojTimer = new DispatcherTimer();
        private readonly DispatcherTimer sesionTimer = new DispatcherTimer();
        private readonly DispatcherTimer inactividadTimer = new DispatcherTimer();

        private readonly TimeSpan limiteInactividad = TimeSpan.FromMinutes(5);

        // ============================================================
        // ESTADO DE SESIÓN
        // ============================================================
        private DateTime? horaInicioSesion;
        private string cedulaActual = "";
        private string nombrePc = "";
        private string ipPc = "";
        private string macPc = "";
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
        // CONSTRUCTOR
        // ============================================================
        public MainWindow()
        {
            InitializeComponent();

            ConfigurarVentanaBloqueo();
            CargarDatosEquipo();
            ConfigurarReloj();
            ConfigurarTimers();

            TxtCedula.TextChanged += TxtCedula_TextChanged;
            DataObject.AddPastingHandler(TxtCedula, TxtCedula_Pasting);

            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

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
            nombrePc = Environment.MachineName;
            var datosRed = ObtenerDatosRedPrincipal();
            ipPc = datosRed.Ip;
            macPc = datosRed.Mac;

            TxtPc.Text = nombrePc;
            TxtUbicacion.Text = ubicacionActual;
            TxtDatosPc.Text = $"IP: {ipPc}   |   MAC: {macPc}";
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
                var data = JsonSerializer.Deserialize<dynamic>(json, options)!;

                bool tieneActiva = data.GetProperty("tieneSesionActiva").GetBoolean();
                if (!tieneActiva) return;

                var sesion = data.GetProperty("sesionActiva");
                sessionIdActual = sesion.GetProperty("idSesion").GetInt32();
                cedulaActual = sesion.GetProperty("usuario").GetString() ?? "";
                horaInicioSesion = sesion.GetProperty("horaInicio").GetDateTime();
                nombreCompletoActual = sesion.GetProperty("usuario").GetString() ?? "";
                ubicacionActual = sesion.GetProperty("ubicacion").GetString() ?? ubicacionActual;

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

                MostrarVentanaBloqueo();
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
                var request = new
                {
                    cedula,
                    nombrePc = nombrePc,
                    ip = ipPc,
                    mac = macPc
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

            MostrarVentanaBloqueo();
            RegistrarEventoLocal("INICIO", $"Sesión iniciada (ID: {idSesion})");
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

                MostrarVentanaBloqueo();

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
        private void BtnUsarComputadora_Click(object sender, RoutedEventArgs e) => OcultarPantallaParaUso();

        private void OcultarPantallaParaUso()
        {
            if (!sesionActiva) return;
            Topmost = false;
            Hide();
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

                    MessageBox.Show(mensaje, "Control de Uso de Computadoras",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
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
            TxtCedula.Text = "";
            PanelSesion.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
            MostrarMensajeCorrecto(mensaje);
            MostrarVentanaBloqueo();
            TxtCedula.Focus();
        }

        private void MostrarVentanaBloqueo()
        {
            Show();
            Topmost = true;
            WindowState = WindowState.Maximized;
            Activate();
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
        private (string Ip, string Mac) ObtenerDatosRedPrincipal()
        {
            try
            {
                var adaptadores = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        n.GetPhysicalAddress().GetAddressBytes().Length > 0
                    );

                foreach (var adaptador in adaptadores)
                {
                    var ip = adaptador.GetIPProperties()
                        .UnicastAddresses
                        .FirstOrDefault(a =>
                            a.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(a.Address) &&
                            !a.Address.ToString().StartsWith("169.254")
                        );

                    if (ip != null)
                    {
                        string macRaw = adaptador.GetPhysicalAddress().ToString();
                        string macFmt = string.Join("-",
                            Enumerable.Range(0, macRaw.Length / 2)
                                      .Select(i => macRaw.Substring(i * 2, 2)));
                        return (ip.Address.ToString(), macFmt);
                    }
                }
                return ("IP_NO_DETECTADA", "MAC_NO_DETECTADA");
            }
            catch
            {
                return ("IP_NO_DETECTADA", "MAC_NO_DETECTADA");
            }
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
            if (!permitirCerrar)
            {
                e.Cancel = true;
                MostrarVentanaBloqueo();
                MostrarMensajeError("La aplicación de control no puede cerrarse desde aquí.");
            }
        }

        // ============================================================
        // ATAJO DE DESARROLLO
        // ============================================================
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Q)
            {
                permitirCerrar = true;
                if (sesionActiva)
                    await CerrarSesionAsync("CIERRE_DESARROLLO", false);
                SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
                SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
                SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
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
using System.Collections.Generic;
using System.Globalization;

namespace DiscordActivityMockV2
{
    public static class Localization
    {
        public enum Language
        {
            English,
            Spanish,
            Portuguese
        }

        public static Language CurrentLanguage { get; private set; } = Language.English;

        private static Dictionary<Language, string> T(string english, string spanish, string portuguese) => new()
        {
            [Language.English] = english,
            [Language.Spanish] = spanish,
            [Language.Portuguese] = portuguese
        };

        private static readonly Dictionary<string, Dictionary<Language, string>> _strings = new()
        {
            // Window and common buttons
            ["WindowTitle"] = T("Discord Activity Mock", "Discord Activity Mock", "Discord Activity Mock"),
            ["Minimize"] = T("Minimize", "Minimizar", "Minimizar"),
            ["Maximize"] = T("Maximize", "Maximizar", "Maximizar"),
            ["Restore"] = T("Restore", "Restaurar", "Restaurar"),
            ["Close"] = T("Close", "Cerrar", "Fechar"),
            ["DarkTheme"] = T("Dark theme", "Tema oscuro", "Tema escuro"),
            ["OK"] = T("OK", "Aceptar", "OK"),
            ["Cancel"] = T("Cancel", "Cancelar", "Cancelar"),
            ["Error"] = T("Error", "Error", "Erro"),

            // Step 1: Get WordPad
            ["Step1Title"] = T("Get WordPad", "Obtener WordPad", "Obter WordPad"),
            ["CopyFromSystem"] = T("Copy from System", "Copiar del Sistema", "Copiar do Sistema"),
            ["BundledBackup"] = T("Use bundled x86", "Usar respaldo x86", "Usar backup x86"),
            ["Status"] = T("Status", "Estado", "Estado"),
            ["SelectOptionAbove"] = T("Select an option above", "Selecciona una opción arriba", "Selecione uma opção acima"),
            ["PreparingWordPad"] = T("Preparing WordPad...", "Preparando WordPad...", "Preparando o WordPad..."),
            ["WordPadReady"] = T("WordPad ready!", "¡WordPad listo!", "WordPad pronto!"),
            ["WordPadSetupFailed"] = T("WordPad setup failed", "No se pudo preparar WordPad", "Falha ao preparar o WordPad"),
            ["WordPadUnavailable"] = T("WordPad unavailable", "WordPad no disponible", "WordPad indisponível"),
            ["BundledWordPadUnavailable"] = T("Bundled WordPad unavailable", "WordPad incluido no disponible", "WordPad incluído indisponível"),
            ["WordPadNotFound"] = T(
                "WordPad was removed from this Windows installation. Use the bundled x86 copy instead.",
                "WordPad fue eliminado de esta instalación de Windows. Usa la copia x86 incluida.",
                "O WordPad foi removido desta instalação do Windows. Use a cópia x86 incluída."),
            ["BundledFileMissing"] = T(
                "A required bundled x86 WordPad file is missing.",
                "Falta un archivo necesario del WordPad x86 incluido.",
                "Está faltando um arquivo necessário do WordPad x86 incluído."),
            ["WordPadOutsideSession"] = T(
                "The WordPad files are outside the private session folder.",
                "Los archivos de WordPad están fuera de la carpeta privada de la sesión.",
                "Os arquivos do WordPad estão fora da pasta privada da sessão."),
            ["WorkingCopyFailed"] = T(
                "Could not create an isolated working copy of WordPad.",
                "No se pudo crear una copia de trabajo aislada de WordPad.",
                "Não foi possível criar uma cópia de trabalho isolada do WordPad."),
            ["ActivityRunningTitle"] = T("Activity running", "Actividad en ejecución", "Atividade em execução"),
            ["StopBeforeChangingSource"] = T(
                "Stop the current activity before changing the WordPad source.",
                "Detén la actividad actual antes de cambiar el origen de WordPad.",
                "Pare a atividade atual antes de alterar a origem do WordPad."),
            ["WordPadExeMissing"] = T(
                "The WordPad executable was not found in the source files.",
                "No se encontró el ejecutable de WordPad en los archivos de origen.",
                "O executável do WordPad não foi encontrado nos arquivos de origem."),
            ["PeNotWindowsApp"] = T(
                "The WordPad executable is not a valid Windows application.",
                "El ejecutable de WordPad no es una aplicación de Windows válida.",
                "O executável do WordPad não é um aplicativo válido do Windows."),
            ["PeInvalidHeader"] = T(
                "The WordPad executable has an invalid PE header.",
                "El ejecutable de WordPad tiene un encabezado PE no válido.",
                "O executável do WordPad tem um cabeçalho PE inválido."),
            ["PeInvalidSignature"] = T(
                "The WordPad executable has an invalid PE signature.",
                "El ejecutable de WordPad tiene una firma PE no válida.",
                "O executável do WordPad tem uma assinatura PE inválida."),
            ["PeUnsupportedMachine"] = T(
                "This WordPad copy must be an x86 or x64 executable.",
                "Esta copia de WordPad debe ser un ejecutable x86 o x64.",
                "Esta cópia do WordPad deve ser um executável x86 ou x64."),
            ["PeRequires64Bit"] = T(
                "This WordPad copy requires 64-bit Windows.",
                "Esta copia de WordPad requiere Windows de 64 bits.",
                "Esta cópia do WordPad requer Windows de 64 bits."),

            // Step 2: Select Activity
            ["Step2Title"] = T("Select Activity", "Seleccionar Actividad", "Selecionar Atividade"),
            ["Search"] = T("Search...", "Buscar...", "Pesquisar..."),
            ["CustomActivity"] = T("Custom Activity", "Actividad personalizada", "Atividade personalizada"),
            ["SelectActivity"] = T("Select activity", "Seleccionar actividad", "Selecionar atividade"),
            ["ReloadActivityList"] = T("Reload activity list", "Recargar lista de actividades", "Recarregar lista de atividades"),
            ["FolderHeader"] = T("Folder", "Carpeta", "Pasta"),
            ["ExeHeader"] = T("Exe name", "Nombre del exe", "Nome do exe"),
            ["Clear"] = T("Clear", "Limpiar", "Limpar"),
            ["OfflineCatalogLoaded"] = T("Offline activity list loaded", "Lista de actividades sin conexión cargada", "Lista de atividades offline carregada"),
            ["CatalogUnavailable"] = T("Activity list unavailable: {0}", "Lista de actividades no disponible: {0}", "Lista de atividades indisponível: {0}"),
            ["OfflineModeTitle"] = T("Offline mode", "Modo sin conexión", "Modo offline"),
            ["OfflineModeMessage"] = T(
                "The online list could not be loaded. The bundled activity list was used instead.",
                "No se pudo cargar la lista en línea. Se usó la lista de actividades incluida.",
                "Não foi possível carregar a lista online. A lista de atividades incluída foi usada."),
            ["CatalogErrorTitle"] = T("Activity list error", "Error en la lista de actividades", "Erro na lista de atividades"),
            ["CatalogMissing"] = T(
                "Could not download the activity list and no bundled list is available.",
                "No se pudo descargar la lista de actividades y no hay una lista incluida.",
                "Não foi possível baixar a lista de atividades e não há uma lista incluída."),
            ["CatalogTooLarge"] = T(
                "The activity list is larger than expected.",
                "La lista de actividades es más grande de lo esperado.",
                "A lista de atividades é maior do que o esperado."),
            ["CatalogEmpty"] = T("The activity list is empty.", "La lista de actividades está vacía.", "A lista de atividades está vazia."),
            ["CatalogNoValidEntries"] = T(
                "The activity list does not contain any valid entries.",
                "La lista de actividades no contiene entradas válidas.",
                "A lista de atividades não contém entradas válidas."),
            ["FolderMustBeRelative"] = T(
                "The folder must be a relative path.",
                "La carpeta debe ser una ruta relativa.",
                "A pasta deve ser um caminho relativo."),
            ["FolderInvalidSegment"] = T(
                "The folder name contains an invalid path segment.",
                "El nombre de la carpeta contiene un segmento de ruta no válido.",
                "O nome da pasta contém um segmento de caminho inválido."),
            ["FolderOutsideWorkingDirectory"] = T(
                "The folder must stay inside the private working folder.",
                "La carpeta debe quedar dentro de la carpeta de trabajo privada.",
                "A pasta deve ficar dentro da pasta de trabalho privada."),
            ["ExeNameInvalid"] = T(
                "The executable name is invalid.",
                "El nombre del ejecutable no es válido.",
                "O nome do executável é inválido."),
            ["ExeNameReserved"] = T(
                "The executable name is reserved by Windows.",
                "El nombre del ejecutable está reservado por Windows.",
                "O nome do executável é reservado pelo Windows."),

            // Steam manifest activities
            ["SteamManifestRequiredTitle"] = T("Steam manifest required", "Se requiere un manifiesto de Steam", "Manifesto da Steam necessário"),
            ["SteamManifestRequiredMessage"] = T(
                "This activity needs appmanifest_{0}.acf. The mock executable and manifest will be created in a Steam library and kept there after the activity stops so Steam and Discord can detect them. Existing files will not be overwritten. If progress does not start, restart Steam and Discord. Continue?",
                "Esta actividad necesita appmanifest_{0}.acf. El ejecutable simulado y el manifiesto se crearán en una biblioteca de Steam y se conservarán ahí después de detener la actividad, para que Steam y Discord puedan detectarlos. No se sobrescribirán archivos existentes. Si el progreso no empieza, reinicia Steam y Discord. ¿Continuar?",
                "Esta atividade precisa do appmanifest_{0}.acf. O executável simulado e o manifesto serão criados em uma biblioteca da Steam e mantidos lá depois que a atividade parar, para que a Steam e o Discord possam detectá-los. Arquivos existentes não serão substituídos. Se o progresso não começar, reinicie a Steam e o Discord. Continuar?"),
            ["CreateAndStart"] = T("Create and start", "Crear e iniciar", "Criar e iniciar"),
            ["SteamManifestReady"] = T("Steam manifest ready", "Manifiesto de Steam listo", "Manifesto da Steam pronto"),
            ["SteamExeMismatch"] = T(
                "The executable name of a Steam manifest activity must match its catalog entry.",
                "El nombre del ejecutable de una actividad con manifiesto de Steam debe coincidir con su entrada del catálogo.",
                "O nome do executável de uma atividade com manifesto da Steam deve corresponder à sua entrada no catálogo."),
            ["SteamAppIdInvalid"] = T(
                "The Steam manifest AppID must be a positive number.",
                "El AppID del manifiesto de Steam debe ser un número positivo.",
                "O AppID do manifesto da Steam deve ser um número positivo."),
            ["SteamNameInvalid"] = T(
                "The Steam manifest name is invalid.",
                "El nombre del manifiesto de Steam no es válido.",
                "O nome do manifesto da Steam é inválido."),
            ["SteamStateFlagsInvalid"] = T(
                "The Steam manifest StateFlags value must be a number.",
                "El valor StateFlags del manifiesto de Steam debe ser un número.",
                "O valor StateFlags do manifesto da Steam deve ser um número."),
            ["SteamInstallDirInvalid"] = T(
                "The Steam manifest installDir must be a single valid folder name.",
                "El installDir del manifiesto de Steam debe ser un único nombre de carpeta válido.",
                "O installDir do manifesto da Steam deve ser um único nome de pasta válido."),
            ["SteamFolderMismatch"] = T(
                "For a Steam manifest entry, the folder must match steamManifest.installDir.",
                "En una entrada con manifiesto de Steam, la carpeta debe coincidir con steamManifest.installDir.",
                "Em uma entrada com manifesto da Steam, a pasta deve corresponder a steamManifest.installDir."),
            ["SteamSourceMissing"] = T(
                "The temporary activity executable is not available to copy.",
                "El ejecutable temporal de la actividad no está disponible para copiarlo.",
                "O executável temporário da atividade não está disponível para cópia."),
            ["SteamLibraryNotFound"] = T(
                "Could not find a Steam library. Start Steam once and check its installation path.",
                "No se encontró ninguna biblioteca de Steam. Abre Steam una vez y revisa su ruta de instalación.",
                "Nenhuma biblioteca da Steam foi encontrada. Abra a Steam uma vez e verifique o caminho de instalação."),
            ["SteamManifestInSeveralLibraries"] = T(
                "More than one Steam library already contains {0}; no files were changed.",
                "Más de una biblioteca de Steam ya contiene {0}; no se cambió ningún archivo.",
                "Mais de uma biblioteca da Steam já contém {0}; nenhum arquivo foi alterado."),
            ["SteamManifestDifferent"] = T(
                "Steam already has a different {0}; it was left untouched.",
                "Steam ya tiene un {0} distinto; no se modificó.",
                "A Steam já tem um {0} diferente; ele não foi modificado."),
            ["SteamManifestIsDirectory"] = T(
                "A folder already exists where Steam expects {0}; it was left untouched.",
                "Ya existe una carpeta donde Steam espera {0}; no se modificó.",
                "Já existe uma pasta onde a Steam espera {0}; ela não foi modificada."),
            ["SteamExistingFolderMismatch"] = T(
                "The existing '{0}' folder does not match the activity mock. Existing files were left untouched.",
                "La carpeta existente '{0}' no coincide con la actividad simulada. No se modificaron los archivos existentes.",
                "A pasta existente '{0}' não corresponde à atividade simulada. Os arquivos existentes não foram modificados."),
            ["SteamFileMismatch"] = T(
                "The existing Steam file '{0}' does not match the temporary mock executable and was left untouched.",
                "El archivo de Steam existente '{0}' no coincide con el ejecutable simulado temporal y no se modificó.",
                "O arquivo existente da Steam '{0}' não corresponde ao executável simulado temporário e não foi modificado."),
            ["SteamAccessDenied"] = T(
                "Windows denied writing the mock executable or Steam manifest to every available library. Choose a writable Steam library or grant the app permission.",
                "Windows impidió escribir el ejecutable simulado o el manifiesto de Steam en todas las bibliotecas disponibles. Elige una biblioteca de Steam con permiso de escritura o da permiso a la app.",
                "O Windows impediu a gravação do executável simulado ou do manifesto da Steam em todas as bibliotecas disponíveis. Escolha uma biblioteca da Steam gravável ou conceda permissão ao app."),
            ["SteamNoFreeLibrary"] = T(
                "No Steam library is available for '{0}'. A folder with that name already exists in each candidate library, so nothing was overwritten.",
                "No hay ninguna biblioteca de Steam disponible para '{0}'. Ya existe una carpeta con ese nombre en cada biblioteca candidata, así que no se sobrescribió nada.",
                "Nenhuma biblioteca da Steam está disponível para '{0}'. Já existe uma pasta com esse nome em cada biblioteca candidata, então nada foi substituído."),

            // Step 3: Run Activity
            ["Step3Title"] = T("Run Activity", "Ejecutar Actividad", "Executar Atividade"),
            ["Start"] = T("Start", "Iniciar", "Iniciar"),
            ["Stop"] = T("Stop", "Detener", "Parar"),
            ["Running"] = T("Running", "Ejecutando", "Executando"),
            ["Inactive"] = T("Inactive", "Inactivo", "Inativo"),
            ["HideSettings"] = T("Hide settings", "Opciones de ocultar", "Opções de ocultar"),
            ["HideWordPad"] = T("Hide WordPad", "Ocultar WordPad", "Ocultar WordPad"),
            ["EnableHide"] = T("Enable hide", "Activar ocultar", "Ativar ocultar"),
            ["Delay"] = T("Delay", "Retraso", "Atraso"),
            ["SecondsUnit"] = T("sec", "seg", "seg"),
            ["HideDelayHelp"] = T(
                "Discord may take time to detect the activity. If it's not recognized, increase this value.",
                "Discord puede tardar en detectar la actividad. Si no la reconoce, aumenta este valor.",
                "O Discord pode demorar para detectar a atividade. Se ela não for reconhecida, aumente este valor."),
            ["HideStatusOn"] = T("Hide: {0}s", "Ocultar: {0}s", "Ocultar: {0}s"),
            ["HideStatusOff"] = T("Hide: Off", "Ocultar: No", "Ocultar: Não"),
            ["TimerSettings"] = T("Timer settings", "Opciones del temporizador", "Opções do temporizador"),
            ["AutoStop"] = T("Auto-stop timer", "Detención automática", "Parada automática"),
            ["EnableAutoStop"] = T("Enable auto-stop", "Activar detención automática", "Ativar parada automática"),
            ["StopAfter"] = T("Stop after", "Detener tras", "Parar após"),
            ["MinutesUnit"] = T("min", "min", "min"),
            ["AutoStopMax"] = T("Max: 1440 min (24 hours)", "Máx.: 1440 min (24 horas)", "Máx.: 1440 min (24 horas)"),
            ["TimerStatusOn"] = T("Timer: {0}m", "Temporizador: {0}m", "Temporizador: {0}m"),
            ["TimerStatusOff"] = T("Timer: Off", "Temporizador: No", "Temporizador: Não"),
            ["StopProcessFailed"] = T(
                "Windows could not stop the activity process. Try again.",
                "Windows no pudo detener el proceso de la actividad. Inténtalo de nuevo.",
                "O Windows não conseguiu parar o processo da atividade. Tente novamente."),
            ["PartialStopFailed"] = T(
                "Windows could not stop the partially started process.",
                "Windows no pudo detener el proceso iniciado parcialmente.",
                "O Windows não conseguiu parar o processo iniciado parcialmente."),
            ["SelectWordPadFirst"] = T("Select WordPad source first", "Selecciona primero el origen de WordPad", "Selecione primeiro a origem do WordPad"),
            ["WordPadRequiredMessage"] = T(
                "Prepare a valid WordPad copy before starting an activity.",
                "Prepara una copia válida de WordPad antes de iniciar una actividad.",
                "Prepare uma cópia válida do WordPad antes de iniciar uma atividade."),
            ["InvalidActivityTitle"] = T("Invalid activity", "Actividad no válida", "Atividade inválida"),
            ["SetupErrorTitle"] = T("Setup error", "Error de preparación", "Erro de preparação"),
            ["RestoreWorkingCopyFailed"] = T(
                "Could not restore the isolated WordPad copy. Reload the WordPad source and try again.",
                "No se pudo restaurar la copia aislada de WordPad. Vuelve a cargar el origen de WordPad e inténtalo de nuevo.",
                "Não foi possível restaurar a cópia isolada do WordPad. Recarregue a origem do WordPad e tente novamente."),
            ["WorkingFolderOutsideSession"] = T(
                "The current working folder is outside the private session folder.",
                "La carpeta de trabajo actual está fuera de la carpeta privada de la sesión.",
                "A pasta de trabalho atual está fora da pasta privada da sessão."),
            ["TargetExeExists"] = T(
                "The target executable already exists in the isolated working folder.",
                "El ejecutable de destino ya existe en la carpeta de trabajo aislada.",
                "O executável de destino já existe na pasta de trabalho isolada."),
            ["TargetFolderExists"] = T(
                "The requested activity folder already exists in the private session folder.",
                "La carpeta de actividad solicitada ya existe en la carpeta privada de la sesión.",
                "A pasta de atividade solicitada já existe na pasta privada da sessão."),
            ["FolderInsideWorkingFolder"] = T(
                "The activity folder cannot be inside the temporary WordPad folder.",
                "La carpeta de la actividad no puede estar dentro de la carpeta temporal de WordPad.",
                "A pasta da atividade não pode ficar dentro da pasta temporária do WordPad."),
            ["MuiExists"] = T(
                "The localized resource file of the target executable already exists.",
                "El archivo de recursos localizados del ejecutable de destino ya existe.",
                "O arquivo de recursos localizados do executável de destino já existe."),
            ["ProcessStartFailed"] = T(
                "Windows did not start the activity process.",
                "Windows no inició el proceso de la actividad.",
                "O Windows não iniciou o processo da atividade."),
            ["StartFailedTitle"] = T("Could not start activity", "No se pudo iniciar la actividad", "Não foi possível iniciar a atividade"),

            // Start warning dialog
            ["BeforeStarting"] = T("Before Starting", "Antes de Empezar", "Antes de Começar"),
            ["WarningMessage1"] = T(
                "A dialog will appear saying \"Could not create a new document\". This is expected - DO NOT close it, as this will stop the activity.",
                "Aparecerá un diálogo diciendo \"No se pudo crear un nuevo documento\". Esto es esperado - NO lo cierres, ya que esto detendrá la actividad.",
                "Uma caixa de diálogo aparecerá dizendo \"Não foi possível criar um novo documento\". Isso é esperado - NÃO feche, pois isso interromperá a atividade."),
            ["WarningMessage2"] = T(
                "If this dialog is annoying, enable 'Hide WordPad' to automatically hide it after a delay.",
                "Si este diálogo es molesto, habilita 'Ocultar WordPad' para ocultarlo automáticamente después de un retraso.",
                "Se essa caixa de diálogo for incômoda, ative 'Ocultar WordPad' para ocultá-la automaticamente após um atraso."),
            ["WarningMessage3"] = T(
                "Note: Discord may not detect the activity if it's hidden too quickly. If this happens, increase the hide delay or disable hiding.",
                "Nota: Discord puede no detectar la actividad si se oculta muy rápido. Si esto sucede, aumenta el retraso de ocultación o desactívalo.",
                "Nota: O Discord pode não detectar a atividade se for ocultada muito rapidamente. Se isso acontecer, aumente o atraso para ocultar ou desative a ocultação."),
            ["DontShowAgain"] = T("Don't show this again", "No mostrar de nuevo", "Não mostrar novamente"),

            // Footer and About
            ["GitHub"] = T("GitHub", "GitHub", "GitHub"),
            ["About"] = T("About", "Acerca de", "Sobre"),
            ["MadeWith"] = T("Made with", "Hecho con", "Feito com"),
            ["By"] = T("by", "por", "por"),
            ["AboutTitle"] = T("About Discord Activity Mock", "Acerca de Discord Activity Mock", "Sobre Discord Activity Mock"),
            ["AboutContent"] = T(
                "Discord Activity Mock\n\n" +
                "This tool allows you to mock Discord activities by renaming WordPad to appear as different games.\n\n" +
                "DISCLAIMER:\n" +
                "This application is made for joking around with friends about games.\n" +
                "I am not responsible for any use outside of this purpose.\n\n" +
                "Created by iroaK\n" +
                "Licensed under MIT License",
                "Discord Activity Mock\n\n" +
                "Esta herramienta te permite simular actividades de Discord renombrando WordPad para que aparezca como diferentes juegos.\n\n" +
                "AVISO:\n" +
                "Esta aplicación está hecha para bromear con amigos sobre juegos.\n" +
                "No me hago responsable de su uso fuera de este propósito.\n\n" +
                "Creado por iroaK\n" +
                "Licenciado bajo Licencia MIT",
                "Discord Activity Mock\n\n" +
                "Esta ferramenta permite simular atividades do Discord renomeando o WordPad para aparecer como diferentes jogos.\n\n" +
                "AVISO:\n" +
                "Este aplicativo foi feito para brincar com amigos sobre jogos.\n" +
                "Não me responsabilizo pelo uso fora deste propósito.\n\n" +
                "Criado por iroaK\n" +
                "Licenciado sob Licença MIT"),
            ["VersionLabel"] = T("Version: {0}", "Versión: {0}", "Versão: {0}"),

            // Updates
            ["Updates"] = T("Updates", "Actualizaciones", "Atualizações"),
            ["CheckForUpdates"] = T("Check for updates", "Buscar actualizaciones", "Buscar atualizações"),
            ["NoUpdatesAvailable"] = T("You have the latest version", "Tienes la última versión", "Você tem a versão mais recente"),
            ["UpdateAvailable"] = T("Update Available", "Actualización Disponible", "Atualização Disponível"),
            ["UpdateMessage"] = T(
                "A new version ({0}) is available. Would you like to download it?",
                "Una nueva versión ({0}) está disponible. ¿Deseas descargarla?",
                "Uma nova versão ({0}) está disponível. Gostaria de baixá-la?"),
            ["UpdateNow"] = T("Update now", "Actualizar ahora", "Atualizar agora"),
            ["ViewOnGitHub"] = T("View on GitHub", "Ver en GitHub", "Ver no GitHub"),
            ["Later"] = T("Later", "Más tarde", "Mais tarde"),
            ["DownloadingUpdate"] = T("Downloading update...", "Descargando actualización...", "Baixando atualização..."),
            ["UpdateDownloadProgress"] = T("Downloaded {0}%", "Descargado {0}%", "Baixado {0}%"),
            ["UpdateDownloadFailed"] = T(
                "The update could not be downloaded. Check your connection and try again.",
                "No se pudo descargar la actualización. Revisa tu conexión e inténtalo de nuevo.",
                "Não foi possível baixar a atualização. Verifique sua conexão e tente novamente."),
            ["UpdateIntegrityFailed"] = T(
                "The update could not be verified against the SHA-256 checksum published on GitHub, so it was not installed. Download it from the release page instead.",
                "No se pudo verificar la actualización con el checksum SHA-256 publicado en GitHub, así que no se instaló. Descárgala desde la página de la release.",
                "Não foi possível verificar a atualização com o checksum SHA-256 publicado no GitHub, por isso ela não foi instalada. Baixe-a pela página da release."),
            ["UpdateApplyFailed"] = T(
                "The update could not be applied. Open the release on GitHub to install it manually.",
                "No se pudo aplicar la actualización. Abre la release en GitHub para instalarla manualmente.",
                "Não foi possível aplicar a atualização. Abra a release no GitHub para instalá-la manualmente."),
            ["UpdateCheckFailed"] = T(
                "Could not check for updates. Check your internet connection and try again.",
                "No se pudieron buscar actualizaciones. Revisa tu conexión e inténtalo de nuevo.",
                "Não foi possível verificar atualizações. Verifique sua conexão e tente novamente."),
            ["StopActivityBeforeUpdate"] = T(
                "Stop the current activity before updating.",
                "Detén la actividad actual antes de actualizar.",
                "Pare a atividade atual antes de atualizar.")
        };

        /// <summary>All keys, for tests.</summary>
        internal static IEnumerable<string> Keys => _strings.Keys;

        /// <summary>The text for one language without falling back to English, for tests.</summary>
        internal static bool TryGetExact(string key, Language language, out string text)
        {
            if (_strings.TryGetValue(key, out var translations) && translations.TryGetValue(language, out string? value))
            {
                text = value;
                return true;
            }

            text = "";
            return false;
        }

        public static void DetectLanguage()
        {
            var culture = CultureInfo.CurrentUICulture;
            var language = culture.TwoLetterISOLanguageName.ToLowerInvariant();

            CurrentLanguage = language switch
            {
                "es" => Language.Spanish,
                "pt" => Language.Portuguese,
                _ => Language.English
            };
        }

        public static string Get(string key)
        {
            if (_strings.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(CurrentLanguage, out var text))
                {
                    return text;
                }
                // Fallback to English
                if (translations.TryGetValue(Language.English, out var englishText))
                {
                    return englishText;
                }
            }
            return key; // Return key if not found
        }

        public static string Get(string key, params object[] args)
        {
            var template = Get(key);
            return string.Format(template, args);
        }
    }
}

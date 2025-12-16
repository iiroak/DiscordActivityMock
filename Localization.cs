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

        private static readonly Dictionary<string, Dictionary<Language, string>> _strings = new()
        {
            // Window Title
            ["WindowTitle"] = new()
            {
                [Language.English] = "Discord Activity Mock",
                [Language.Spanish] = "Discord Activity Mock",
                [Language.Portuguese] = "Discord Activity Mock"
            },

            // Step 1: Get WordPad
            ["Step1Title"] = new()
            {
                [Language.English] = "Get WordPad",
                [Language.Spanish] = "Obtener WordPad",
                [Language.Portuguese] = "Obter WordPad"
            },
            ["CopyFromSystem"] = new()
            {
                [Language.English] = "Copy from System",
                [Language.Spanish] = "Copiar del Sistema",
                [Language.Portuguese] = "Copiar do Sistema"
            },
            ["DownloadBackup"] = new()
            {
                [Language.English] = "Download Backup",
                [Language.Spanish] = "Descargar Respaldo",
                [Language.Portuguese] = "Baixar Backup"
            },
            ["Status"] = new()
            {
                [Language.English] = "Status",
                [Language.Spanish] = "Estado",
                [Language.Portuguese] = "Estado"
            },
            ["SelectOptionAbove"] = new()
            {
                [Language.English] = "Select an option above",
                [Language.Spanish] = "Selecciona una opción arriba",
                [Language.Portuguese] = "Selecione uma opção acima"
            },

            // Step 2: Select Activity
            ["Step2Title"] = new()
            {
                [Language.English] = "Select Activity",
                [Language.Spanish] = "Seleccionar Actividad",
                [Language.Portuguese] = "Selecionar Atividade"
            },
            ["Search"] = new()
            {
                [Language.English] = "Search...",
                [Language.Spanish] = "Buscar...",
                [Language.Portuguese] = "Pesquisar..."
            },
            ["ActivitiesLoaded"] = new()
            {
                [Language.English] = "{0} activities loaded",
                [Language.Spanish] = "{0} actividades cargadas",
                [Language.Portuguese] = "{0} atividades carregadas"
            },

            // Step 3: Control
            ["Step3Title"] = new()
            {
                [Language.English] = "Run Activity",
                [Language.Spanish] = "Ejecutar Actividad",
                [Language.Portuguese] = "Executar Atividade"
            },
            ["StartActivity"] = new()
            {
                [Language.English] = "Start Activity",
                [Language.Spanish] = "Iniciar Actividad",
                [Language.Portuguese] = "Iniciar Atividade"
            },
            ["StopActivity"] = new()
            {
                [Language.English] = "Stop Activity",
                [Language.Spanish] = "Detener Actividad",
                [Language.Portuguese] = "Parar Atividade"
            },
            ["Start"] = new()
            {
                [Language.English] = "Start",
                [Language.Spanish] = "Iniciar",
                [Language.Portuguese] = "Iniciar"
            },
            ["Stop"] = new()
            {
                [Language.English] = "Stop",
                [Language.Spanish] = "Detener",
                [Language.Portuguese] = "Parar"
            },
            ["Ready"] = new()
            {
                [Language.English] = "Ready",
                [Language.Spanish] = "Listo",
                [Language.Portuguese] = "Pronto"
            },
            ["Running"] = new()
            {
                [Language.English] = "Running",
                [Language.Spanish] = "Ejecutando",
                [Language.Portuguese] = "Executando"
            },
            ["Stopped"] = new()
            {
                [Language.English] = "Stopped",
                [Language.Spanish] = "Detenido",
                [Language.Portuguese] = "Parado"
            },

            // Settings
            ["Settings"] = new()
            {
                [Language.English] = "Settings",
                [Language.Spanish] = "Configuración",
                [Language.Portuguese] = "Configurações"
            },
            ["HideWordPad"] = new()
            {
                [Language.English] = "Hide WordPad",
                [Language.Spanish] = "Ocultar WordPad",
                [Language.Portuguese] = "Ocultar WordPad"
            },
            ["AutoStop"] = new()
            {
                [Language.English] = "Auto Stop",
                [Language.Spanish] = "Auto Detener",
                [Language.Portuguese] = "Parar Automático"
            },

            // Footer
            ["GitHub"] = new()
            {
                [Language.English] = "GitHub",
                [Language.Spanish] = "GitHub",
                [Language.Portuguese] = "GitHub"
            },
            ["Discord"] = new()
            {
                [Language.English] = "Discord",
                [Language.Spanish] = "Discord",
                [Language.Portuguese] = "Discord"
            },
            ["About"] = new()
            {
                [Language.English] = "About",
                [Language.Spanish] = "Acerca de",
                [Language.Portuguese] = "Sobre"
            },
            ["MadeWith"] = new()
            {
                [Language.English] = "Made with",
                [Language.Spanish] = "Hecho con",
                [Language.Portuguese] = "Feito com"
            },
            ["By"] = new()
            {
                [Language.English] = "by",
                [Language.Spanish] = "por",
                [Language.Portuguese] = "por"
            },

            // About Dialog
            ["AboutTitle"] = new()
            {
                [Language.English] = "About Discord Activity Mock",
                [Language.Spanish] = "Acerca de Discord Activity Mock",
                [Language.Portuguese] = "Sobre Discord Activity Mock"
            },
            ["AboutContent"] = new()
            {
                [Language.English] = "Discord Activity Mock\n\n" +
                                     "This tool allows you to mock Discord activities by renaming WordPad to appear as different games.\n\n" +
                                     "DISCLAIMER:\n" +
                                     "This application is made for joking around with friends about games.\n" +
                                     "I am not responsible for any use outside of this purpose.\n\n" +
                                     "Created by iroaK\n" +
                                     "Licensed under MIT License",
                [Language.Spanish] = "Discord Activity Mock\n\n" +
                                     "Esta herramienta te permite simular actividades de Discord renombrando WordPad para que aparezca como diferentes juegos.\n\n" +
                                     "AVISO:\n" +
                                     "Esta aplicación está hecha para bromear con amigos sobre juegos.\n" +
                                     "No me hago responsable de su uso fuera de este propósito.\n\n" +
                                     "Creado por iroaK\n" +
                                     "Licenciado bajo Licencia MIT",
                [Language.Portuguese] = "Discord Activity Mock\n\n" +
                                        "Esta ferramenta permite simular atividades do Discord renomeando o WordPad para aparecer como diferentes jogos.\n\n" +
                                        "AVISO:\n" +
                                        "Este aplicativo foi feito para brincar com amigos sobre jogos.\n" +
                                        "Não me responsabilizo pelo uso fora deste propósito.\n\n" +
                                        "Criado por iroaK\n" +
                                        "Licenciado sob Licença MIT"
            },
            ["OK"] = new()
            {
                [Language.English] = "OK",
                [Language.Spanish] = "OK",
                [Language.Portuguese] = "OK"
            },

            // Warning Dialog
            ["BeforeStarting"] = new()
            {
                [Language.English] = "Before Starting",
                [Language.Spanish] = "Antes de Empezar",
                [Language.Portuguese] = "Antes de Começar"
            },
            ["WarningMessage1"] = new()
            {
                [Language.English] = "A dialog will appear saying \"Could not create a new document\". This is expected - DO NOT close it, as this will stop the activity.",
                [Language.Spanish] = "Aparecerá un diálogo diciendo \"No se pudo crear un nuevo documento\". Esto es esperado - NO lo cierres, ya que esto detendrá la actividad.",
                [Language.Portuguese] = "Uma caixa de diálogo aparecerá dizendo \"Não foi possível criar um novo documento\". Isso é esperado - NÃO feche, pois isso interromperá a atividade."
            },
            ["WarningMessage2"] = new()
            {
                [Language.English] = "If this dialog is annoying, enable 'Hide WordPad' to automatically hide it after a delay.",
                [Language.Spanish] = "Si este diálogo es molesto, habilita 'Ocultar WordPad' para ocultarlo automáticamente después de un retraso.",
                [Language.Portuguese] = "Se essa caixa de diálogo for incômoda, ative 'Ocultar WordPad' para ocultá-la automaticamente após um atraso."
            },
            ["WarningMessage3"] = new()
            {
                [Language.English] = "Note: Discord may not detect the activity if it's hidden too quickly. If this happens, increase the hide delay or disable hiding.",
                [Language.Spanish] = "Nota: Discord puede no detectar la actividad si se oculta muy rápido. Si esto sucede, aumenta el retraso de ocultación o desactívalo.",
                [Language.Portuguese] = "Nota: O Discord pode não detectar a atividade se for ocultada muito rapidamente. Se isso acontecer, aumente o atraso para ocultar ou desative a ocultação."
            },
            ["DontShowAgain"] = new()
            {
                [Language.English] = "Don't show this again",
                [Language.Spanish] = "No mostrar de nuevo",
                [Language.Portuguese] = "Não mostrar novamente"
            },
            ["Cancel"] = new()
            {
                [Language.English] = "Cancel",
                [Language.Spanish] = "Cancelar",
                [Language.Portuguese] = "Cancelar"
            },

            // Status Messages
            ["WordPadReady"] = new()
            {
                [Language.English] = "WordPad ready!",
                [Language.Spanish] = "¡WordPad listo!",
                [Language.Portuguese] = "WordPad pronto!"
            },
            ["WordPadNotFound"] = new()
            {
                [Language.English] = "WordPad not found in system",
                [Language.Spanish] = "WordPad no encontrado en el sistema",
                [Language.Portuguese] = "WordPad não encontrado no sistema"
            },
            ["Downloading"] = new()
            {
                [Language.English] = "Downloading...",
                [Language.Spanish] = "Descargando...",
                [Language.Portuguese] = "Baixando..."
            },
            ["DownloadComplete"] = new()
            {
                [Language.English] = "Download complete!",
                [Language.Spanish] = "¡Descarga completa!",
                [Language.Portuguese] = "Download completo!"
            },
            ["DownloadFailed"] = new()
            {
                [Language.English] = "Download failed",
                [Language.Spanish] = "Descarga fallida",
                [Language.Portuguese] = "Falha no download"
            },
            ["SelectWordPadFirst"] = new()
            {
                [Language.English] = "Select WordPad source first",
                [Language.Spanish] = "Selecciona la fuente de WordPad primero",
                [Language.Portuguese] = "Selecione a fonte do WordPad primeiro"
            },
            ["SelectActivityFirst"] = new()
            {
                [Language.English] = "Select an activity first",
                [Language.Spanish] = "Selecciona una actividad primero",
                [Language.Portuguese] = "Selecione uma atividade primeiro"
            },
            ["ActivityStarted"] = new()
            {
                [Language.English] = "Activity started!",
                [Language.Spanish] = "¡Actividad iniciada!",
                [Language.Portuguese] = "Atividade iniciada!"
            },
            ["ActivityStopped"] = new()
            {
                [Language.English] = "Activity stopped",
                [Language.Spanish] = "Actividad detenida",
                [Language.Portuguese] = "Atividade parada"
            },
            ["Error"] = new()
            {
                [Language.English] = "Error",
                [Language.Spanish] = "Error",
                [Language.Portuguese] = "Erro"
            },

            // Update strings
            ["UpdateAvailable"] = new()
            {
                [Language.English] = "Update Available",
                [Language.Spanish] = "Actualización Disponible",
                [Language.Portuguese] = "Atualização Disponível"
            },
            ["UpdateMessage"] = new()
            {
                [Language.English] = "A new version ({0}) is available. Would you like to download it?",
                [Language.Spanish] = "Una nueva versión ({0}) está disponible. ¿Deseas descargarla?",
                [Language.Portuguese] = "Uma nova versão ({0}) está disponível. Gostaria de baixá-la?"
            },
            ["Download"] = new()
            {
                [Language.English] = "Download",
                [Language.Spanish] = "Descargar",
                [Language.Portuguese] = "Baixar"
            },
            ["Later"] = new()
            {
                [Language.English] = "Later",
                [Language.Spanish] = "Más tarde",
                [Language.Portuguese] = "Mais tarde"
            },
            ["CheckingForUpdates"] = new()
            {
                [Language.English] = "Checking for updates...",
                [Language.Spanish] = "Buscando actualizaciones...",
                [Language.Portuguese] = "Verificando atualizações..."
            },
            ["NoUpdatesAvailable"] = new()
            {
                [Language.English] = "You have the latest version",
                [Language.Spanish] = "Tienes la última versión",
                [Language.Portuguese] = "Você tem a versão mais recente"
            }
        };

        public static void DetectLanguage()
        {
            var culture = CultureInfo.CurrentUICulture;
            var language = culture.TwoLetterISOLanguageName.ToLower();

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

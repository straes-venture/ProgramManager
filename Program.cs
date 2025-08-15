// ==============================================================================================
// Program.cs  (Windows Forms, .NET 6/7/8)
// ==============================================================================================
// PURPOSE:
//   - Application entry point. Isolated from UI files for clarity.
// ==============================================================================================

using System;
using System.Windows.Forms;

namespace ProgramManager
{
    // ==============================================================================================
    // [BEGIN] APPLICATION ENTRY POINT
    // ----------------------------------------------------------------------------------------------
    internal static class Program
    {
        public static int minNotesWidth { get; private set; }

        [STAThread]
        static void Main()
        {
            // [BEGIN] WinForms bootstrap
#if NET6_0_OR_GREATER
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#endif
            Application.Run(new MainForm(minNotesWidth));
            // [END] WinForms bootstrap
        }
    }
    // [END] APPLICATION ENTRY POINT
    // ==============================================================================================
}

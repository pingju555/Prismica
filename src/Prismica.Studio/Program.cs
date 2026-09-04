using System;
using System.Windows;

namespace Prismica.Studio;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application();
        var window = new StudioWindow();
        app.Run(window);
    }
}

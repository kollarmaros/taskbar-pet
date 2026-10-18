namespace TaskbarPet;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Only one pet per user session.
        using var mutex = new Mutex(true, @"Local\TaskbarPet.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
            return;

        StartupManager.RepairPath();
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}

using POS.Data;
using POS.Services;
using POS.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using POS;

public partial class App : Application
{
    public static ServiceProvider ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        // Aquí puedes abrir tu ventana principal, pasándole los servicios si es necesario
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 1. Registra el DbContext
        // AddDbContext es la forma recomendada porque maneja el ciclo de vida del contexto.
        services.AddDbContext<AppDbContext>();

        // 2. Registra los servicios
        // AddScoped significa que se creará una nueva instancia por cada "scope" (ej. por cada ventana o petición)
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IComboService, ComboService>();

        // 3. Registra tu ventana principal
        services.AddTransient<MainWindow>();
    }
}

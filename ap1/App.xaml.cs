using POS.Data;
using POS.Services;
using POS.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace POS
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            var nfcReader = ServiceProvider.GetRequiredService<INFCReaderService>();
            nfcReader.Connect();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var nfcReader = ServiceProvider.GetService<INFCReaderService>();
            nfcReader?.Dispose();

            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>();

            services.AddScoped<ICategoriaService, CategoriaService>();
            services.AddScoped<IProductoService, ProductoService>();
            services.AddScoped<IComboService, ComboService>();
            services.AddScoped<ITiempoService, TiempoService>();
            services.AddScoped<IPrecioTiempoService, PrecioTiempoService>();
            services.AddScoped<IVentaService, VentaService>();

            services.AddSingleton<INFCReaderService, NFCReaderService>();

            services.AddTransient<MainWindow>();
        }
    }
}

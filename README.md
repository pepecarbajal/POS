
### Prerrequisitos

Asegúrate de tener instalado:
* Visual Studio 2022 (o superior) con la carga de trabajo **"Desarrollo de escritorio de .NET"**.
* El SDK de .NET correspondiente a la versión del proyecto.

### Pasos de Instalación

1.  **Clona el Repositorio**
    Abre una terminal y ejecuta el siguiente comando:
    ```bash
    git clone https://github.com/pepecarbajal/POS.git
    ```

2.  **Abre el Proyecto**
    Abre el archivo de la solución (`.sln`) con Visual Studio.

3.  **Restaura los Paquetes NuGet 📦**
    Este paso descarga todas las dependencias necesarias que no están en el repositorio.
    * Haz clic derecho sobre la **Solución** en el Explorador de Soluciones.
    * Selecciona **"Restaurar paquetes NuGet"**.

4.  **Crea la Base de Datos 💿**
    La base de datos se creará a partir de las migraciones incluidas en el proyecto.
    * Ve a `Herramientas > Administrador de paquetes NuGet > Consola del Administrador de paquetes`.
    * Ejecuta el siguiente comando:
    ```powershell
    Update-Database
    ```

5.  **Ejecuta la Aplicación ▶️**
    ¡Todo está listo! Presiona **F5** o el botón "Iniciar" para compilar y ejecutar el proyecto. La aplicación se iniciará y la base de datos ya estará creada.

---

## 🛠️ Construido Con

* [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - El framework de UI.
* [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) - El ORM para la base de datos.
* [SQLite](https://www.sqlite.org/) - El motor de la base de datos.
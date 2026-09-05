namespace LibretaCobros.Services;

/// <summary>Constantes de conexión y versión visible. La versión sale del &lt;Version&gt; del .csproj.</summary>
public static class AppInfo
{
    public static string Version { get; } = ComputeVersion();

    private static string ComputeVersion()
    {
        var v = typeof(AppInfo).Assembly.GetName().Version;
        return v == null ? "dev" : $"{v.Major}.{v.Minor}";
    }

    public const string SupabaseUrl = "https://dsnsqrqoxddtvqxaevtx.supabase.co";

    /// <summary>
    /// Clave pública (publishable). Viaja en el WASM a propósito: por sí sola no da acceso a nada,
    /// solo a lo que permitan las políticas RLS. Los datos quedan detrás del login.
    /// </summary>
    public const string SupabaseAnonKey = "sb_publishable_XaOLEWhYuofmj2CxX8PXlg_HFf7xTat";
}

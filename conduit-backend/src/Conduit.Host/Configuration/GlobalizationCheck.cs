using System.Text;

namespace Conduit.Host.Configuration;

public static class GlobalizationCheck
{
    public static void EnsureNormalizationAvailable()
    {
        if ("\u00E9".Normalize(NormalizationForm.FormKD).Length != 2)
        {
            throw new ConfigurationException(
                "Unicode normalization is unavailable. Install ICU and disable invariant globalization, "
                + "set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false");
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Loopy.MaelstromTest;

public static class EdnParser
{
    /// <summary>
    /// Convert and parse EDN as JSON - dirty and very lacking
    /// </summary>
    public static bool TryParse(string edn, out JsonNode? result)
    {
        // concatenate lines
        edn = edn.ReplaceLineEndings(string.Empty);

        // replace keyword keys with strings (this cannot handle keywords as values)
        edn = Regex.Replace(edn, @"(?<=[{,] *):([a-z0-9_\-?]+)", @"""$1"":");

        // replace numeric keys with strings
        edn = Regex.Replace(edn, @"(?<=[{,] *)([0-9]+)", @"""$1"":");

        // replace array space delimiter with commas
        edn = Regex.Replace(edn, @"(?<=\[[^\]]+)( )(?=[^[]+\])", ", ");

        try
        {
            result = JsonNode.Parse(edn);
            return true;
        }
        catch (JsonException)
        {
            result = null;
            return false;
        }
    }
}

using System.Collections.Generic;
using System.Globalization;

namespace SmartStorage;

internal static class L10n
{
    private static readonly Dictionary<string, Dictionary<string, string>> Strings =
        new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "Employees: Allowed",
                ["StorageBlocked"] = "Employees: Blocked",
                ["StorageHint"] = "Click to let employees take items from this storage.",
                ["StorageAccessLabel"] = "Employees can take from this storage",
            },
            ["es"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "Empleados: Permitido",
                ["StorageBlocked"] = "Empleados: Bloqueado",
                ["StorageHint"] = "Haz clic para permitir que los empleados tomen objetos de este almacén.",
                ["StorageAccessLabel"] = "Los empleados pueden tomar de este almacén",
            },
            ["fr"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "Employés : autorisés",
                ["StorageBlocked"] = "Employés : bloqués",
                ["StorageHint"] = "Cliquez pour autoriser les employés à prendre des objets dans ce stockage.",
                ["StorageAccessLabel"] = "Les employés peuvent prendre dans ce stockage",
            },
            ["de"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "Mitarbeiter: erlaubt",
                ["StorageBlocked"] = "Mitarbeiter: blockiert",
                ["StorageHint"] = "Klicken, damit Mitarbeiter Gegenstände aus diesem Lager entnehmen dürfen.",
                ["StorageAccessLabel"] = "Mitarbeiter dürfen aus diesem Lager entnehmen",
            },
            ["pt"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "Funcionários: permitido",
                ["StorageBlocked"] = "Funcionários: bloqueado",
                ["StorageHint"] = "Clique para permitir que os funcionários retirem itens deste armazenamento.",
                ["StorageAccessLabel"] = "Os funcionários podem retirar deste armazenamento",
            },
            ["ru"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "Сотрудники: разрешено",
                ["StorageBlocked"] = "Сотрудники: запрещено",
                ["StorageHint"] = "Нажмите, чтобы разрешить сотрудникам брать предметы из этого хранилища.",
                ["StorageAccessLabel"] = "Сотрудники могут брать предметы из этого хранилища",
            },
            ["zh"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "员工：允许",
                ["StorageBlocked"] = "员工：禁止",
                ["StorageHint"] = "点击允许员工从此存储中取用物品。",
                ["StorageAccessLabel"] = "员工可以从此存储中取用物品",
            },
            ["ja"] = new Dictionary<string, string>
            {
                ["StorageAllowed"] = "従業員：許可",
                ["StorageBlocked"] = "従業員：禁止",
                ["StorageHint"] = "クリックして、従業員がこの収納からアイテムを取ることを許可します。",
                ["StorageAccessLabel"] = "従業員はこの収納からアイテムを取ることができます",
            },
        };

    private static string _language = "en";

    internal static void Init()
    {
        string wanted = Config.Language == null ? "auto" : (Config.Language.Value ?? "auto").Trim().ToLowerInvariant();
        if (wanted == "auto" || wanted.Length == 0)
        {
            string os = CultureInfo.CurrentUICulture == null
                ? "en"
                : (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName ?? "en").ToLowerInvariant();
            wanted = os;
        }

        if (wanted.Length > 2) wanted = wanted.Substring(0, 2);
        _language = Strings.ContainsKey(wanted) ? wanted : "en";
        Log.Msg("[The Employees Know] UI language: " + _language);
    }

    internal static string Get(string key)
    {
        if (Strings.TryGetValue(_language, out Dictionary<string, string> table) && table.TryGetValue(key, out string value))
            return value;
        return Strings["en"].TryGetValue(key, out string fallback) ? fallback : key;
    }
}

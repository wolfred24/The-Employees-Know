using System;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartStorage;

internal static class StorageToggleUI
{
    private static GameObject _row;
    private static Toggle _toggle;
    private static TextMeshProUGUI _label;
    private static StorageRef _current;
    private static bool _updating;

    internal static bool NativeOk => _row != null;
    internal static StorageRef Current => _current;

    internal static void Show(StorageMenu menu, StorageEntity entity)
    {
        if (menu == null || entity == null) return;
        if (!StorageIndex.TryMapStorageEntity(entity, out StorageRef storage))
        {
            _current = null;
            if (_row != null) _row.SetActive(false);
            return;
        }

        _current = storage;
        if (!EnsureRow(menu))
        {
            if (_row != null) _row.SetActive(false);
            return;
        }

        if (_row != null) _row.SetActive(true);

        bool allowed = StoragePermissions.IsAllowed(storage.Guid);
        try
        {
            _updating = true;
            _toggle.isOn = allowed;
        }
        catch { }
        finally { _updating = false; }
    }

    private static bool EnsureRow(StorageMenu menu)
    {
        try
        {
            if (_row != null && _row.transform != null && _row.transform.parent != null)
                return true;
        }
        catch { }

        Transform parent = menu.Container != null
            ? menu.Container
            : (menu.CloseButtonContainer != null ? menu.CloseButtonContainer.parent : null);
        if (parent == null) return false;

        try
        {
            var row = new GameObject("SmartStorage_ToggleRow");
            row.transform.SetParent(parent, false);
            row.SetActive(true);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(24f, -100f);
            rowRect.sizeDelta = new Vector2(360f, 32f);

            if (parent.GetComponent<LayoutGroup>() != null)
            {
                var layout = row.AddComponent<LayoutElement>();
                layout.preferredHeight = 32f;
                layout.flexibleWidth = 1f;
                rowRect.anchoredPosition = Vector2.zero;
            }

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(row.transform, false);
            var backgroundRect = backgroundObject.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(4f, 0f);
            backgroundRect.sizeDelta = new Vector2(24f, 24f);

            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            background.raycastTarget = true;

            var checkObject = new GameObject("Checkmark");
            checkObject.transform.SetParent(backgroundObject.transform, false);
            var checkRect = checkObject.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0f);
            checkRect.anchorMax = new Vector2(1f, 1f);
            checkRect.offsetMin = new Vector2(4f, 4f);
            checkRect.offsetMax = new Vector2(-4f, -4f);

            var checkmark = checkObject.AddComponent<Image>();
            checkmark.color = Color.white;
            checkmark.raycastTarget = false;
            try
            {
                Sprite sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Checkmark.psd");
                if (sprite != null) checkmark.sprite = sprite;
            }
            catch { }

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(row.transform, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(36f, 0f);
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            try
            {
                TextMeshProUGUI source = menu.TitleLabel != null ? menu.TitleLabel : menu.SubtitleLabel;
                if (source != null)
                {
                    label.font = source.font;
                    label.fontSharedMaterial = source.fontSharedMaterial;
                }
            }
            catch { }
            label.text = L10n.Get("StorageAccessLabel");
            label.fontSize = 18f;
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.raycastTarget = true;

            var toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.transition = Selectable.Transition.None;
            toggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)(OnValueChanged));

            _row = row;
            _toggle = toggle;
            _label = label;
            row.transform.SetAsLastSibling();
            return true;
        }
        catch (Exception e)
        {
            Log.Warning("[The Employees Know] Could not create the storage checkbox: " + e.Message);
            return false;
        }
    }

    private static void OnValueChanged(bool allowed)
    {
        if (_updating) return;
        if (_current == null || string.IsNullOrWhiteSpace(_current.Guid)) return;

        try
        {
            StoragePermissions.SetAllowed(_current.Guid, allowed);
            Log.Msg("[The Employees Know] " + (_current.Name ?? "storage") + ": " + (allowed ? "employees allowed" : "employees blocked") + ".");
        }
        catch (Exception e)
        {
            Log.Warning("[The Employees Know] Could not update the storage permission: " + e.Message);
        }
    }
}

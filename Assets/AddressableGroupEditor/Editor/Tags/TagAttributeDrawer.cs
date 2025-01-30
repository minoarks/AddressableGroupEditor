using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace ARK.EditorTools.CustomAttribute
{

    public class AddressableLabelsTagStringDrawer : OdinAttributeDrawer<AddressableLabelsTagAttribute, string>
    {

        private GUIContent m_buttonContent = new GUIContent();

        protected override void Initialize() => UpdateButtonContent();

        private void UpdateButtonContent()
        {
            m_buttonContent.text = ValueEntry.SmartValue ?? "未設定";
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect(label != null);

            if(label == null)
                rect = EditorGUI.IndentedRect(rect);
            else
                rect = EditorGUI.PrefixLabel(rect, label);

            if(!EditorGUI.DropdownButton(rect, m_buttonContent, FocusType.Passive))
                return;

            if (string.IsNullOrEmpty(ValueEntry.SmartValue))
            {
                Debug.LogWarning("ValueEntry.SmartValue 為空，無法顯示選擇器。");
                return;
            }

            var settings     = AddressableAssetSettingsDefaultObject.Settings;
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var labelTable   = settings.GetType().GetProperty("labelTable", bindingFlags).GetValue(settings);
            var labelNames   = (List<string>)labelTable.GetType().GetProperty("labelNames", bindingFlags).GetValue(labelTable);
            var labels       = labelNames.ToArray();
            labels = labels.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            var selector     = new AddressableLabelsTagSelector(labels);
            selector.SetSelection(ValueEntry.SmartValue);
            selector.ShowInPopup(rect.position);

            selector.SelectionChanged += x =>
            {
                ValueEntry.Property.Tree.DelayAction(() =>
                {
                    ValueEntry.SmartValue = x.FirstOrDefault();

                    UpdateButtonContent();
                });
            };
        }

    }

    public abstract class AddressableLabelsTagStringListBaseDrawer<T> : OdinAttributeDrawer<AddressableLabelsTagAttribute, T> where T : IList<string>
    {

        private GUIContent m_buttonContent = new GUIContent();

        protected override void Initialize() => UpdateButtonContent();

        private void UpdateButtonContent()
        {
            m_buttonContent.text = m_buttonContent.tooltip = string.Join(", ", ValueEntry.SmartValue);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect(label != null);

            if(label == null)
                rect = EditorGUI.IndentedRect(rect);
            else
                rect = EditorGUI.PrefixLabel(rect, label);

            if(!EditorGUI.DropdownButton(rect, m_buttonContent, FocusType.Passive))
                return;

            // if (string.IsNullOrEmpty(ValueEntry.SmartValue.ToString()))
            // {
            //     Debug.LogWarning("ValueEntry.SmartValue 為空，無法顯示選擇器。");
            //     return;
            // }

            var settings     = AddressableAssetSettingsDefaultObject.Settings;
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var labelTable   = settings.GetType().GetProperty("labelTable", bindingFlags).GetValue(settings);
            var labelNames   = (List<string>)labelTable.GetType().GetProperty("labelNames", bindingFlags).GetValue(labelTable);
            var labels       = labelNames.ToArray();
            labels = labels.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            var selector     = new AddressableLabelsTagSelector(labels);

            rect.y += rect.height;

            selector.SetSelection(ValueEntry.SmartValue);
            selector.ShowInPopup(rect.position);

            selector.SelectionChanged += x =>
            {
                ValueEntry.Property.Tree.DelayAction(() =>
                {
                    UpdateValue(x);

                    UpdateButtonContent();
                });
            };
        }

        protected abstract void UpdateValue(IEnumerable<string> x);

    }

    [DrawerPriority(1)]
    [DontApplyToListElements]
    public class AddressableLabelsTagStringArrayDrawer : AddressableLabelsTagStringListBaseDrawer<string[]>
    {

        protected override void UpdateValue(IEnumerable<string> x) => ValueEntry.SmartValue = x.ToArray();

    }

    [DrawerPriority(1)]
    [DontApplyToListElements]
    public class AddressableLabelsTagStringListDrawer : AddressableLabelsTagStringListBaseDrawer<List<string>>
    {

        protected override void UpdateValue(IEnumerable<string> x) => ValueEntry.SmartValue = x.ToList();

    }

    public class AddressableLabelsTagSelector : GenericSelector<string>
    {

        private FieldInfo m_requestCheckboxUpdate;

        public AddressableLabelsTagSelector(string[] tags) : base(tags)
        {
            CheckboxToggle = true;

            m_requestCheckboxUpdate = typeof(GenericSelector<string>).GetField("requestCheckboxUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        protected override void DrawSelectionTree()
        {
            base.DrawSelectionTree();

            EditorGUILayout.BeginHorizontal();

            if(GUILayout.Button("None"))
            {
                SetSelection(new List<string>());

                m_requestCheckboxUpdate.SetValue(this, true);
                TriggerSelectionChanged();
            }

            if(GUILayout.Button("All"))
            {
                var settings     = AddressableAssetSettingsDefaultObject.Settings;
                var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var labelTable   = settings.GetType().GetProperty("labelTable", bindingFlags).GetValue(settings);
                var labelNames   = (List<string>)labelTable.GetType().GetProperty("labelNames", bindingFlags).GetValue(labelTable);
                var labels       = labelNames.ToArray();
                labels = labels.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                SetSelection(labels);

                m_requestCheckboxUpdate.SetValue(this, true);
                TriggerSelectionChanged();
            }

            EditorGUILayout.EndHorizontal();
        }

    }
}